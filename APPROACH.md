# Architectural & Engineering Approach

This document outlines the thought process, architectural decisions, and problem-solving methodology applied throughout the Northwind Support Desk assessment. Each decision records what was chosen, what was rejected, and why. See [`README.md`](README.md) for setup, endpoints, and the full decision table.

---

## Guiding Principles

- **Dependencies point inward.** Domain depends on nothing; Application depends on Domain; Infrastructure implements the interfaces they declare; Presentation wires everything together and is the only layer that knows about HTTP and JWT. Architecture tests enforce this (Domain has no framework dependency, Application does not reference ASP.NET, the EF model must match the migrations).
- **Rules are pure.** Priority, SLA window and owner selection take values, "now" and their configured numbers, and return a decision with a reason. They can be tested without a database.
- **Derive, do not store.** SLA status and an agent's open-ticket count are computed every time, so they never go stale.
- **One place for each cross-cutting rule.** Sign-in is one fallback policy, errors go through one middleware, a `401` is handled in one web client, and list filters live in one `ApplyFilters`.
- **Reads and writes differ.** Reads go through query services that project to DTOs without tracking. Writes go through domain entities, and a use case saves once (the ticket and its history row together).
- **Settings are configuration.** The Docker image holds code, never settings or secrets.

---

## Task 1: Defect Fix & Server-Side Filtering

### 1.1 Defect Analysis & Resolution (DEFECT-117)
- **Thought Process:**
  The ticket list did not re-query the backend when a filter dropdown (Status, Priority, Category, Agent) changed. The effect that triggered the fetch depended only on `search`, `page` and `pageSize`. Changing a filter updated state but never ran `load`, so the UI showed stale data.
- **Decision:**
  Fix the root cause rather than work around it. Search is debounced (300 ms) into the committed `filters`, and `useAsyncData` reloads whenever the committed `filters` change. Dropdowns commit immediately, so only typing is delayed. Remounting the component or changing a `key` was rejected because it hides the bug instead of fixing the dependency.
- **Verification:**
  - Automated: `useTicketList.test.ts` (fake timers) covers filter-change requests, the search debounce, and a DEFECT-117 regression.
  - Manual: with DevTools open, changing Status to *In progress* immediately sends `GET /api/tickets?...status=InProgress`.

### 1.2 Server-Side Filtering Architecture
- **Thought Process:**
  Filtering has to happen in SQL Server before the count and paging. Filtering in C# afterwards would return a wrong `totalCount` and a broken pager.
- **Decision:**
  `TicketQueries.ApplyFilters` ANDs one optional `Where` per bound filter, then runs `CountAsync` on the filtered query, then sorts and pages. That order keeps `totalCount` and the pager correct.
- **Verification:**
  Tests run against in-memory SQLite with an EF command interceptor, so the logged SQL proves the filter runs in the database and not on the client. `TicketQueriesFilterTests` covers priority, AND combination, customer-name search, and paging over a filtered set.
- **Extensibility:**
  Task 2's `slaStatus` was added as one property and one `if` block in `ApplyFilters`.

---

## Task 2: Ticket Escalation, Triage & Assignment

### 2.1 Automated Triage on Create
- **Thought Process:**
  Create and escalate both need priority, due date and owner decisions. Putting the rules in handlers would give two copies that could drift apart and could not be tested without a database. A rules engine or MediatR was rejected as more machinery than the problem needs.
- **Decision:**
  Small pure units in the domain, assembled by `TicketTriager` so create and escalate share one copy:
  - `PriorityRule`: uses the requested priority, Medium if none is given, and always Critical when the category's flag says so.
  - `SlaPolicy`: picks the window by priority and customer tier, and computes the due date.
  - `AgentSelector`: considers active agents, a specialist when the category needs one, agents strictly below their limit, then the fewest open tickets, with the lowest id winning a tie.
- **SLA Configuration:**
  Base windows are Critical 4 h, High 8 h, Medium 24 h, Low 72 h. The Premium multiplier is 0.5 with a 1 h floor. "At risk" starts at 25% of the window remaining. These live in the `Sla` configuration section. Invalid or missing values stop startup, because default numbers in code would let a wrong config go unnoticed.
- **Edge Case:**
  When nobody is eligible, the ticket is created unassigned. This is a normal result, not an error. `POST /api/tickets` returns `201` with a `triage` object listing the reason for each decision.

### 2.2 Escalation & SLA Status Derivation
- **Escalation Rules:**
  - `POST /api/tickets/{id}/escalate` raises priority one level.
  - The due date is recomputed from the moment of escalation.
  - The current agent is kept if still active and qualified, otherwise one is reselected. No capacity check is applied, because the ticket already counts towards their load and a check would remove it just because it became more important.
  - The ticket and a `TicketEscalations` row are saved together. The row is immutable and records before/after priority, due date and agent, plus reason, actor and time.
  - The reason must be 5 to 500 characters.
  - Critical, Resolved and Closed tickets return `409` with the real reason, and nothing is saved.
  - `GET /api/tickets/{id}/escalations` returns the history, newest first, served by the index `IX_TicketEscalations_TicketId_EscalatedAtUtc`.
- **Dynamic SLA Status (derived, never stored):**
  `SlaEvaluator` returns Within SLA, At risk, Breached, Met or No SLA from the dates. A stored status goes stale as time passes and needs a job to refresh it, so it was rejected. Boundaries: exactly 25% left and due exactly now are both At risk, and resolved on the due date is Met.
- **SQL Server Translation:**
  The `slaStatus` filter must run in the database before count and paging, so it uses `EF.Functions.DateDiffSecond` with the same rule as `SlaEvaluator`. One "now" per request is shared by the filter and the row labels, so the filter and the badge never disagree.
- **Spec Ambiguities Resolved:**
  - "Open" means not Resolved or Closed.
  - Priority defaults to Medium, and forced Critical comes from the category flag.
  - A tie in agent selection goes to the lowest id.
  - "At the limit" means full.
  - The at-risk window starts at creation, even after an escalation.
  - The 1 h floor applies to Premium only, and Premium is never longer than Standard (this cap was my addition).
  - A refusal names its real reason.

### 2.3 Frontend User Experience
- **SLA Indicator (`SlaIndicator`):**
  Every state has its own words and symbol (● ▲ ✕ ✓ –), so meaning does not depend on colour alone.
- **Escalation Panel & History:**
  Shows the escalation history, handles the `409` path with the real reason, and shows the triage result after create. The ticket list also gains an SLA status filter.

---

## Task 3: Agent Authentication & Route Protection

### 3.1 Credential Storage & Hashing
- **Aggregate Boundary:**
  A nullable `PasswordHash` column (`nvarchar(256)`, migration `AddAgentPasswordHash`) was added directly to `Agent`. Every login is one agent and there are no roles, so a separate `Users` table would only add a join. The column is nullable so the migration is safe for existing rows, and 256 leaves room if the algorithm changes.
- **Hashing:**
  BCrypt (enhanced mode, work factor 12) behind `IPasswordHasher`. It is maintained, deliberately slow and salted, and enhanced mode removes the 72-byte password limit. PBKDF2 and a home-made hash were rejected.
- **Anti-Enumeration:**
  `POST /api/auth/login` returns the identical `401` for a wrong email, a wrong password, an inactive agent, and an agent with no hash. The real reason goes to the log only, so nothing reveals which emails exist.

### 3.2 Token Architecture & Centralized Authorization
- **Token Design:**
  HS256 JWT with `sub`, `name`, `email` and `jti`, valid for 60 minutes (`Jwt:LifetimeMinutes`). The API validates signature, issuer, audience and expiry with no clock skew. The key comes from `Jwt__Key`, must be at least 32 bytes, and a missing or short key stops the API at startup. No refresh token is issued, which keeps the API stateless and out of scope for this brief.
- **Secure by Default:**
  A fallback authorization policy requires an authenticated user for every endpoint that has no authorization of its own. Only `/api/auth/login` and `/health` are opened, each deliberately. `[Authorize]` on every controller was rejected because a forgotten attribute would leave a new controller open. Swagger is middleware, so the policy never sees it.
- **Actor Integrity:**
  The escalate request body contains only `reason`. The actor's name is read from the token, so a caller cannot record an escalation under someone else's name.

### 3.3 Frontend Authentication
- **Token Storage:**
  The token lives in a module variable in memory. `localStorage`, `sessionStorage` and cookies were rejected so that nothing is saved where a script can read it later. The trade-off is that a refresh means signing in again.
- **HTTP Client:**
  The web client attaches `Authorization: Bearer <token>` to requests. Any `401` that carried the current token clears the session and sends the user back to login.
- **Protected Routing:**
  A route guard protects the app, remembers the wanted page (inside the app only) and returns the user there after login. The header shows the agent's name and a Log out button.
- **Verification:**
  Tests cover the hasher, each way login can fail, token claims and exact expiry, and enforcement on a small host using the real `AddJwtAuthentication` (no token, expired, tampered and wrong-key tokens all return `401`). Web tests cover the token being attached, `401` clearing the session, and the guard redirect.

---

## Task 4: Dockerisation & Operational Hardening

### 4.1 Multi-Stage Container Architecture
- **Build vs. Runtime Separation:**
  - Build stage: `mcr.microsoft.com/dotnet/sdk:10.0`
  - Runtime stage: `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Build Cache Optimization:**
  `global.json`, `Directory.Build.props`, `Directory.Packages.props` and the four project files are copied first and `dotnet restore` runs, then the source is copied and published. A code-only change reuses the restore layer.
- **Runtime Image Choice:**
  Debian `aspnet:10.0` was chosen over Alpine and chiseled. The brief checks `docker compose exec api whoami`, which needs a shell, and a chiseled image has none. Alpine adds musl and no ICU.

### 4.2 Security & Configuration Hardening
- **Least Privilege:** The container runs as the non-root `app` user on plain HTTP port 8080 (published as `5080:8080`).
- **Nothing Baked In:** No secrets or settings are in the image. `appsettings.Development.json` is excluded from every publish. Reading the code before writing the Dockerfile showed that `Sla` existed only in the Development file, so it was moved to `appsettings.json` so the image still starts.
- **Secrets in Compose:** `Jwt__Key=replace-me` in `.env.example` would crash the container, because the key is too short on purpose. It is commented out, and Compose supplies a fake, published default that must never leave a local machine.
- **Environment:** Compose runs as `Development` by default, because the brief says to migrate and seed "as it does today in Development". A new `RUN_MIGRATIONS` switch was rejected. The cost is that Swagger is on in Compose.

### 4.3 Production Readiness & Health Checks
- **Health Endpoint:**
  `GET /health` is anonymous, checks the database, and returns `200 Healthy` or `503 Unhealthy` with no data in the body.
- **Orchestration:**
  `docker compose up` starts SQL Server first and the API once the database is healthy, then gives a migrated, seeded API.
- **Production Migration Strategy:**
  In a real deployment, set `ASPNETCORE_ENVIRONMENT=Production` (migrate, seed and Swagger off) and run migrations as a separate step with an EF Core migration bundle. The app migrating itself was rejected because:
  - Replicas would race to migrate the same database.
  - The app's login should not have schema rights.
  - A failed migration should fail the deploy, not crash-loop the app.
  - A script can be reviewed and rolled back, which startup code cannot.

  The bundle is documented, not built or wired into Compose.
- **Verification:**
  Task 4 has no automated test, as the brief says. Five checks were run by hand: clean run (`/health` returns `200`, login works, 40 seeded tickets with a token and `401` without), layer caching (restore step `CACHED`), `whoami` prints `app`, no `appsettings.Development.json` in `/app`, and the web app signing in against the container API.

---

## Known Trade-offs

- **SLA:** At risk can appear early after an escalation because the window starts at creation. The At risk and Within SLA filters need SQL Server (SQLite has no `DATEDIFF`), so they were verified by hand against SQL Server.
- **Assignment:** Two tickets created at the same moment can both take an agent's last slot.
- **Auth:** No lockout, rate limiting, refresh tokens, revocation or password reset. A deactivated agent keeps access for up to 60 minutes. Production would use an `httpOnly` cookie instead of an in-memory token.
- **Docker:** The container runs plain HTTP as Development with a fake published JWT key. Production migrations are documented, not built.