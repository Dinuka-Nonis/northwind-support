# Northwind Support Desk

A support ticket management system: raise tickets, search and filter them, assign them to agents,
and move them through their lifecycle.

**.NET 10 Web API** + **React 19 / TypeScript** + **SQL Server**, in one monorepo.

---

## The assignment

You are joining a team that already has this app working. Complete the tasks **in order**.
Full requirements are in **[`ASSIGNMENT_CANDIDATE.md`](ASSIGNMENT_CANDIDATE.md)**.

| # | Task | Done looks like | Effort |
| --- | --- | --- | --- |
| 1 | **Fix a defect, add server-side filtering** | Root cause fixed with a regression test; every list filter applied in the database with a correct count | ~2.5 h |
| 2 | **Ticket escalation & assignment** | Automatic triage on create, escalation with history, SLA status, UI for all of it | ~7 h |
| 3 | **Login and authentication** | Agents sign in; API rejects unauthenticated calls; login UI | ~4 h |
| 4 | **Dockerise the API** *(optional)* | `docker compose up` gives a migrated, seeded API with no baked-in secret | ~1.5 h |

### Guidance

- **Time: 2 days.** Honestly marking a task "not finished" beats rushing all of them.
- **Fork** this repo to your own GitHub account and work there. **Do not push to the original.**
- **AI tools are allowed.** You must understand and be able to defend every line you submit.
- **Commit as you go** and keep the real history. Commit the Task 1 defect fix and the filtering
  separately from each other and from the feature work. A single squashed commit is not accepted.
- **Where the spec is ambiguous**, make a decision, write it down in the README, and move on.
- **Out of scope:** registration, password reset, roles, OAuth/SSO, refresh tokens, MFA,
  notifications, CI, caching, visual redesign. Mention them under *Known limitations* instead.

### Submission

Tasks 1, 2 and 3 are complete. Task 4 is not started. Details below and in
[`APPROACH.md`](APPROACH.md).

**Fork:** https://github.com/Dinuka-Nonis/northwind-support (branch `Task-3`)

#### What was implemented (Task 1)

- **DEFECT-117:** Ticket list requests fire when any filter changes (not only search/page).
- **Server-side filtering:** `GET /api/tickets` applies all query filters in SQL Server before
  `totalCount`, paging, and sorting. Regression tests on the web hook and API query layer.

#### What was implemented (Task 2)

- **Triage on create:** a new ticket gets its priority, due date and owner automatically, each with a
  plain-language reason. `POST /api/tickets` returns a `triage` object with those reasons.
- **Escalation with history:** `POST /api/tickets/{id}/escalate` raises the priority one level and
  stores a `TicketEscalations` row. `GET /api/tickets/{id}/escalations` returns the history.
- **SLA status:** Within SLA, At risk, Breached, Met or No SLA, derived from the dates every time and
  filterable with `slaStatus`.
- **UI:** a reusable `SlaIndicator`, an SLA status filter, an escalation panel with history, and the
  triage result shown after a ticket is created.
- The rules, configuration, database table and SLA filter are described in
  [Task 2: Ticket escalation and assignment](#task-2-ticket-escalation-and-assignment).

#### What was implemented (Task 3)

- **Credentials:** a nullable `PasswordHash` column on `Agents` (migration `AddAgentPasswordHash`),
  filled with a BCrypt hash. Every seeded agent gets the development password at seed time.
- **Sign in:** `POST /api/auth/login` returns a JWT, its expiry and the agent. A wrong email, a wrong
  password and an inactive agent give the identical `401`. `GET /api/auth/me` returns the signed-in
  agent or `401`.
- **Every other endpoint needs a token**, enforced once by a fallback authorization policy. Login and
  Swagger stay open.
- **`escalatedBy` comes from the token.** The escalate request body now holds only the `reason`.
- **UI:** a `/login` page, the token kept in memory and attached by `client.ts`, protected routes that
  send visitors to login and bring them back, the agent's name with a Logout button, and any `401`
  clearing the session.
- How it works and what was traded off is in [Authentication](#authentication-task-3).

#### Design decisions

**Task 1**

| Topic | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| When to fetch (web) | `useAsyncData` with a `load` callback keyed on the full `filters` object; search debounced into `filters` separately | Keeping a manual `useEffect` with a partial dependency list; remount keys / forced reload | Partial deps caused the bug. Remount/reload hides stale state instead of fixing it. |
| Search UX | 300 ms debounce on committed `filters.search`; dropdowns commit immediately | Debouncing every filter | Spec requires immediate requests for non-search filters. |
| Where filters run (API) | `ApplyFilters` on `IQueryable`, then `CountAsync`, then sort/page | Filtering in memory after `ToListAsync` | FL-5: count and pager must match the filtered set in the database. |
| Search normalization | Trim and blank→null in `TicketQuery.Normalized()` | Only trim in SQL | One representation of “no search”; avoids accidental match-nothing filters. |
| Filter tests (API) | In-memory SQLite + EF command interceptor | Pure mocked `DbSet` | Confirms EF translates filters to SQL, not just LINQ-to-objects. |

**Task 2** (the 11 spec ambiguities I had to decide are listed in the
[Task 2 section](#decisions-where-the-spec-was-ambiguous))

| Topic | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| Where the rules live | Small pure units in the domain (`PriorityRule`, `SlaPolicy`, `AgentSelector`, `Ticket.Escalate`), assembled by `TicketTriager`, which create and escalate share | Rules in handlers or controllers; a rules engine, a strategy per rule, or MediatR | Create and escalate would each need a copy that could drift, and the rules could not be tested without the application layer. Five small rules do not need more machinery; no new packages were added. |
| SLA status | Derived from the dates every time (`SlaEvaluator`) | A stored status column | A stored status goes stale as time passes and needs a job to refresh it. |
| SLA status filter | One more SQL condition in `ApplyFilters` | Filtering in C# | Breaks the count and the paging. |
| SLA numbers | The `Sla` configuration section; a missing or invalid value stops the API at startup | Default numbers in code | A wrong or missing config would go unnoticed. |
| Current agent on escalation | Kept if active and qualified, with no capacity check | Checking capacity before keeping them | The ticket already counts towards their load. A capacity check would take a ticket away from someone because it became more important. |
| Loading agent candidates | `GetAssignmentCandidatesAsync`: one statement, open count as a subquery and specializations joined in | One query per agent to count open tickets | One round trip instead of one per agent. |

**Task 3**

| Topic | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| Where credentials live | A nullable `PasswordHash` column on `Agent` | A separate `Users` table | Every login is exactly one agent and there are no other kinds of user or roles. A second table is a join and a second thing to keep in step. Nullable, so the migration is safe for existing rows; an agent with no hash cannot sign in. |
| Password hashing | BCrypt (BCrypt.Net-Next), enhanced mode, work factor 12 | Identity `PasswordHasher`, PBKDF2, a home-made hash | A maintained, deliberately slow, salted hash. Enhanced mode removes BCrypt's 72-byte limit. Behind `IPasswordHasher`, so it can change without touching the login rules. |
| Token | HMAC-SHA256 JWT with `sub`, `name`, `email`, signed with a key from configuration | Server-side sessions; opaque tokens | The API stays stateless and the token alone identifies the agent, which is what `escalatedBy` needs. The cost is revocation (see limitations). |
| Token lifetime | 60 minutes, set by `Jwt:LifetimeMinutes`, no refresh token | A long lifetime; refresh tokens | An hour covers a working session and limits a leaked token. Refresh tokens are out of scope. |
| Enforcing sign-in | A fallback policy that requires an authenticated user | `[Authorize]` on each controller | A new controller is closed by default. Opening an endpoint is the deliberate act (`[AllowAnonymous]`). |
| Failed sign-in | One `InvalidCredentialsException`, one generic message; the real reason goes to the log | Telling the caller which part was wrong | Nothing reveals which emails exist. |
| Token in the browser | A module variable in `auth/session.ts`, attached by `client.ts` | `localStorage`, `sessionStorage`, a cookie | Nothing is saved where a script can read it later. A refresh means signing in again (see [Authentication](#authentication-task-3)). |
| Test host for enforcement | A small host that calls the real `AddJwtAuthentication` | A full `WebApplicationFactory` over the real app | No database and no seeding needed, and what is tested is the same setup the API uses. |
| Swagger Authorize button | Added | Leaving Swagger without a way to send a token | Not in the spec. With every endpoint closed, Swagger is hard to use without it. It is one small file and can be dropped. |

#### DEFECT-117 — root cause and verification

**Root cause:** The ticket list `useEffect` only depended on `filters.search`, `filters.page`, and
`filters.pageSize`. Changing status (or other filters) updated state but did not run `load`, so no
request went out until search or paging changed.

**Fix:** Load when committed `filters` change (`useCallback` + `useAsyncData`), with search still
debounced before it enters `filters`.

**Verify manually:** Open the web app → change Status to *In progress* → Network tab shows an
immediate `GET /api/tickets?...status=InProgress`. Type in search → one request after ~300 ms idle,
not one per keystroke.

**Automated:** `apps/web/src/features/tickets/hooks/useTicketList.test.ts` (DEFECT-117).

#### Server-side filtering

- **Where:** `TicketQueries.ApplyFilters` in
  `apps/api/src/SupportDesk.Infrastructure/Queries/TicketQueries.cs` — filter → count → sort → page.
- **SQL check:** Unit tests log generated SQL via an EF interceptor; assertions use counts and IDs
  from seeded SQLite data (`TicketQueriesFilterTests`).
- **Next filter:** Task 2 added `slaStatus` exactly this way: one property on `TicketQuery` and one
  `if` block in `ApplyFilters` (see [SLA status filter](#sla-status-filter)).

#### Development login

Every seeded agent signs in with the same password, which is a **local development fixture, not a
secret**. It is published here on purpose, it only exists because the seeder runs in Development, and
the database holds its hash, never the text.

| Setting | Value |
| --- | --- |
| Password (all agents) | `Local_Dev_Agent1` |
| Emails | `alex.turner@`, `ben.osei@`, `priya.nair@`, `sara.lindqvist@`, `marcus.doyle@`, `yuki.tanaka@` (all `northwind-support.example`) |
| Inactive agent | `yuki.tanaka@` is refused with the same `401` as a wrong password |

The seeder gives the password to any agent that has none, so a database seeded before Task 3 is
repaired on the next start. An agent who already has a hash is left alone.

**Try it in Swagger:** open `/swagger`, call `POST /api/auth/login`, copy the `token`, press
**Authorize**, paste it, then call any endpoint.

**Or from a terminal:**

```bash
curl -s -X POST http://localhost:5043/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"alex.turner@northwind-support.example","password":"Local_Dev_Agent1"}'
```

#### Testing

Use [Quick start](#quick-start) and [Tests](#tests) below. Task-specific checks:

```bash
npm test
```

**Task 1**

- Web: `useTicketList.test.ts` — filter change requests, search debounce, DEFECT-117 regression.
- API: `TicketQueriesFilterTests` — priority, combined filters, customer-name search, paging counts.

**Manual smoke (Task 1):** With API + web running, filter by status/priority and confirm the list and
page count shrink; search for a customer name (e.g. `Contoso`).

**Task 2** — both suites run without a database.

| Area | What is covered |
| --- | --- |
| SLA policy | all four priorities for both tiers, the 1 h floor (with a custom policy, since the default numbers never reach it), Premium never longer than Standard, a config change changes the result, a missing priority throws |
| Priority rule | requested, default Medium, forced Critical whatever was requested |
| Agent selection | fewest open, specialists only, at-limit excluded, inactive excluded, tie goes to the lowest id, nobody eligible, keep versus reassign on escalation |
| Escalation | one level at a time, Critical rejected, Resolved and Closed rejected, history row before/after values, due date counted from now, owner kept or changed, nothing saved on a 409 |
| SLA status | every boundary: 25% left is At risk, one minute more is Within SLA, due exactly now is At risk, 1 ms past is Breached, resolved on the due date is Met, 1 ms late is Breached, no due date is No SLA |
| Queries | the SLA filter (see [SLA status filter](#sla-status-filter)), the escalation history order, agent candidates in one query |
| Web | the SLA indicator renders every state distinguishably; the SLA filter requests immediately; a short reason never calls the API; a 409 is shown and the ticket is unchanged; the history states; the triage summary |

The architecture tests already in the repo also run, and fail if the EF model and the migrations drift
apart.

**Manual smoke (Task 2):** With API + web running, create a ticket and read the triage summary
(priority, due date, owner, with reasons). Open a ticket and escalate it with a reason of at least
5 characters; the history lists the new row. Try escalating a Critical, Resolved or Closed ticket and
confirm the `409` message is shown and the ticket is unchanged. Use the SLA status filter and confirm
the list and the page count change. The *At risk* and *Within SLA* filters need SQL Server (see
[SLA status filter](#sla-status-filter)).

**Task 3** — all of it runs without a database.

| Area | What is covered |
| --- | --- |
| Password hashing | the right password verifies, the wrong one fails, it is case sensitive, the hash is not the plain text, two hashes of one password differ |
| Login | success, a wrong password gives no token, an unknown email, an inactive agent and an agent with no hash all fail, and a wrong password and an unknown email give the same message; blank and over-long input is rejected |
| Token | the claims, the expiry exactly 60 minutes after the fixed clock, a changed lifetime, a missing or short key stops startup |
| Enforcement | on a test host that uses the real setup: no token gives `401`, a valid token gives `200`, and an expired, tampered, wrong-key or malformed token gives `401`; an endpoint marked anonymous gives `200` |
| Escalation | who escalated is the name passed in, taken from the token, and the request has only a reason |
| Web | the token is attached, a `401` clears the session, a login `401` does not; a signed-out visit goes to login, a signed-in one renders; invalid input never calls the API; rejected credentials show an error; the escalation form no longer asks who is escalating |

**Manual smoke (Task 3):** Log in through Swagger or `curl`, then call a ticket endpoint with and
without the token (`401`, then `200`). A wrong email and a wrong password return the same `401`.
`yuki.tanaka@` gets `401`. `SELECT Email, PasswordHash FROM Agents` shows hashes only. In the web app,
open `/tickets/5` signed out, get sent to login, then land back on it. Escalate a ticket and check the
history names the signed-in agent. Log out, refresh, and confirm you must sign in again.

#### AI usage

- **Tool:** Claude for exploring the repo, drafting tests, and reviewing EF query shape.
- **Workflow:** Reproduce DEFECT-117 in the UI → read `useTicketList` → fix → add Vitest regression →
  implement `ApplyFilters` → add SQLite integration tests → run `npm test`.
- **Verified manually:** All generated code was run through tests and a quick UI pass
- **Task 2 tool:** Claude for drafting the rule units and their boundary tests, reviewing the EF
  migration and the SQL shape of the SLA filter, and drafting the web components.
- **Task 2 workflow:** Rules first, each with its tests (SLA policy → SLA status → priority and agent
  selection → escalation on the ticket) → table and migration → triage on create → escalate endpoint
  and history → SLA filter in SQL → web (indicator, filter, escalation panel, triage result) → docs.
- **Task 2 verified manually:** Every rule decision (the 11 listed above) was read and checked
  against the spec. The *At risk* and *Within SLA* filters were run by hand against SQL Server, since
  SQLite cannot run `DATEDIFF`. The escalation panel and triage result got a UI pass, including the
  `409` path.
- **Task 3 tool:** Claude for drafting the hasher, the login rules, the token issuer, the enforcement
  tests, the web session code and the tests for each.
- **Task 3 workflow:** credentials and hashing first, each with tests (column and migration → BCrypt
  hasher → seeded dev passwords) → login use case → token and endpoints → login page and session
  handling → central enforcement → `escalatedBy` from the token → docs.
- **Task 3 verified manually:** the checks listed under *Manual smoke (Task 3)*: a ticket endpoint with
  and without a token, the same `401` for a wrong email and a wrong password, inactive Yuki refused,
  hashes only in the table, and the redirect to login and back in the browser.

#### Known limitations

- Task **4** not done (Docker API image).
- Search uses `Contains` (substring match); case follows SQL Server collation on real DB.
- No dedicated indexes added for filter columns (40 seed tickets only).
- **At risk after an escalation** can appear earlier than 25% of the new window (decision 5).
- **At risk and Within SLA filters** count whole seconds and need SQL Server (`DATEDIFF`). Right on the
  threshold the SQL and the C# status can differ by up to one second.
- **Two tickets created at the same moment** can both pick the last free slot of an agent, so an agent
  can briefly go one or two over their limit. Fixing it needs a lock or a conditional update, which was
  judged out of scope.
- **The ticket page does not live-update.** If someone else escalates a ticket while it is open, the
  form still shows until you submit and the API answers `409`; the message is shown.
- **The `Sla` configuration** is only in the Development settings until Task 4.
- **Not built:** the optional SLA summary report (2.5).

**Task 3**

- **Login timing can differ** between an unknown email and a wrong password, because only a real
  agent gets a BCrypt check. The responses are identical; the time is not.
- **No lockout or rate limiting, no refresh tokens and no token revocation.** The spec leaves these out.
- **A deactivated agent keeps access until their token expires** (up to 60 minutes). New logins are refused.
- **A refresh or a new tab means signing in again**, because the token is only in memory.
- **A very long name can fail an escalation.** `Agent.FullName` allows 200 characters and the history's
  `EscalatedBy` allows 100, so a name over 100 characters would fail on escalate. No seeded agent is close.
- **The only way to set a password is the development seeder.** There is no registration or password
  reset, because the spec leaves them out. Outside Development a new agent cannot sign in.
- **`/health` (Task 4)** will need `[AllowAnonymous]`, and Compose will need to pass `Jwt__Key`.

#### Time spent (rough)

| Area | Hours |
| --- | --- |
| DEFECT-117 investigation + fix + web tests | ~1.5 |
| Server-side filtering + API tests | ~1.5 |
| README / APPROACH | ~0.5 |
| **Task 1 total** | **~3.5** |
| Task 2 API: rules, escalation, migration, SLA filter + tests | ~4 |
| Task 2 web: SLA indicator, filter, escalation panel, triage result + tests | ~3 |
| Task 2 README / APPROACH | ~0.5 |
| **Task 2 total** | **~7.5** |
| Task 3 API: credentials, hashing, login, token, enforcement, `escalatedBy` + tests | ~2.5 |
| Task 3 web: login page, session, routes, header + tests | ~1.5 |
| Task 3 README / APPROACH | ~0.5 |
| **Task 3 total** | **~4.5** |

---

## Quick start

**Prerequisites:** .NET SDK 10 (`global.json`), Node.js 22 (`.nvmrc`; 20.19+ works), Docker.

```bash
cp .env.example .env          # optional, every value has a working default
npm install                   # installs the web app
npm run db:up                 # SQL Server 2022 in Docker on localhost:1433
npm run api                   # terminal 1: migrates, seeds, then serves the API
npm run web                   # terminal 2: React dev server
```

| What | Where |
| --- | --- |
| Web app | http://localhost:5173 |
| API | https://localhost:7043 (also http://localhost:5043) |
| Swagger | https://localhost:7043/swagger |

In Development the API applies migrations and seeds demo data on startup. **Never commit real
credentials.**

**Signing in:** the web app opens on a login page. Use `alex.turner@northwind-support.example` with the
development password `Local_Dev_Agent1` (all six agents share it; see [Development login](#development-login)).
`npm run api` needs no signing key setup: Development uses a clearly fake one. Anywhere else the API
needs `Jwt__Key` (see `.env.example`) and refuses to start without it.

**Visual Studio 2026:** run `npm run db:up`, open `SupportDesk.sln`, set
**SupportDesk.Presentation** as the startup project, and press F5 on the **https** profile.

## Tests

```bash
npm test                      # dotnet test, then the web tests (Vitest)
```

Both pass on a clean checkout and neither needs a database.

## Commands

| Command | What it does |
| --- | --- |
| `npm run db:up` / `db:down` / `db:reset` | Start / stop / wipe SQL Server |
| `npm run api` / `npm run web` | Run the API / the React app |
| `npm run build` | Build both |
| `npm run lint` / `typecheck` / `format` | Web lint and types / `dotnet format` |
| `npm run ef -- migrations add <Name>` | Add an EF Core migration |

## API at a glance

| Endpoint | Description |
| --- | --- |
| `POST /api/auth/login` | Sign in with an email and password. Returns a token, its expiry and the agent. A wrong email, a wrong password and an inactive agent give the same `401`. The only endpoint open without a token (Task 3). |
| `GET /api/auth/me` | The signed-in agent, or `401` (Task 3). |
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `slaStatus`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. Every filter is applied in the database, before the count and paging (Task 1; `slaStatus` added in Task 2). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket. Priority, due date and owner are decided automatically; the response includes a `triage` object with the reasons (Task 2). |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Raise the priority one level, with a `reason`. Who escalated is the signed-in agent, read from the token (Task 3). `200` with the ticket, the history row and the reasons; `400` invalid; `404` unknown; `409` Critical, Resolved or Closed (Task 2). |
| `GET /api/tickets/{id}/escalations` | The ticket's escalation history, newest first (Task 2). |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Every endpoint except login needs `Authorization: Bearer <token>`; without a valid one the answer is
`401`. Errors are `ProblemDetails`: `400` validation, `401` bad credentials on login, `404` not found,
`409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| The API will not start and mentions `Jwt` | `Jwt:Key` is missing or shorter than 32 bytes. Development has one in `appsettings.Development.json`; elsewhere set `Jwt__Key` (see `.env.example`). |
| The web app keeps returning to the login page | The token expired after 60 minutes, or the page was refreshed (the token is only in memory). Sign in again. |
| Every login says "Invalid email or password." | Use a seeded email and `Local_Dev_Agent1`. `yuki.tanaka@` is inactive and is always refused. |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
