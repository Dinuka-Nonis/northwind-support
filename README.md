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

Tasks 1 and 2 are complete. Tasks 3–4 are not started. Details below and in
[`APPROACH.md`](APPROACH.md).

**Fork:** https://github.com/Dinuka-Nonis/northwind-support (branch `Task-2`)

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

Not implemented (Task 3). The API and web app run **without authentication** on a local dev setup.
Until then, the escalation form takes the "Escalated by" name as typed text.

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

#### Known limitations

- Tasks **3–4** not done (login, Docker API image).
- No login, roles, or API auth yet.
- Search uses `Contains` (substring match); case follows SQL Server collation on real DB.
- No dedicated indexes added for filter columns (40 seed tickets only).
- **At risk after an escalation** can appear earlier than 25% of the new window (decision 5).
- **At risk and Within SLA filters** count whole seconds and need SQL Server (`DATEDIFF`). Right on the
  threshold the SQL and the C# status can differ by up to one second.
- **Two tickets created at the same moment** can both pick the last free slot of an agent, so an agent
  can briefly go one or two over their limit. Fixing it needs a lock or a conditional update, which was
  judged out of scope.
- **"Escalated by"** is typed in until Task 3.
- **The ticket page does not live-update.** If someone else escalates a ticket while it is open, the
  form still shows until you submit and the API answers `409`; the message is shown.
- **The `Sla` configuration** is only in the Development settings until Task 4.
- **Not built:** the optional SLA summary report (2.5), because it is only for after Tasks 1-3.

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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `slaStatus`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. Every filter is applied in the database, before the count and paging (Task 1; `slaStatus` added in Task 2). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket. Priority, due date and owner are decided automatically; the response includes a `triage` object with the reasons (Task 2). |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
| `POST /api/tickets/{id}/escalate` | Raise the priority one level, with a `reason` and `escalatedBy`. `200` with the ticket, the history row and the reasons; `400` invalid; `404` unknown; `409` Critical, Resolved or Closed (Task 2). |
| `GET /api/tickets/{id}/escalations` | The ticket's escalation history, newest first (Task 2). |
| `GET /api/customers` · `/api/agents` · `/api/categories` | Reference data |

Errors are `ProblemDetails`: `400` validation, `404` not found, `409` conflict or business rule.

**Seed data:** 5 categories, 6 customers (3 Premium), 6 agents (one inactive, one at their limit)
and 40 tickets across every status, priority and SLA state.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| API cannot reach the database | `npm run db:up`, wait ~20 s, check `docker compose ps` |
| Port 1433 already in use | Change the port in `docker-compose.yml` and `.env` |
| Browser warns about the certificate | `dotnet dev-certs https --trust`, or use the web app, which proxies |
| Web app shows "Unable to load tickets" | The API is not running, or `VITE_API_PROXY_TARGET` points at the wrong port |
| SQL Server slow on Apple Silicon | Enable Rosetta in Docker Desktop → Settings → General |
