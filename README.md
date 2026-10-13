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

Task 1 is complete. Tasks 2–4 are not started. Details below and in [`APPROACH.md`](APPROACH.md).

**Fork:** https://github.com/Dinuka-Nonis/northwind-support (branch `Task-1`)

#### What was implemented (Task 1)

- **DEFECT-117:** Ticket list requests fire when any filter changes (not only search/page).
- **Server-side filtering:** `GET /api/tickets` applies all query filters in SQL Server before
  `totalCount`, paging, and sorting. Regression tests on the web hook and API query layer.

#### Design decisions

| Topic | Chosen | Rejected | Why |
| --- | --- | --- | --- |
| When to fetch (web) | `useAsyncData` with a `load` callback keyed on the full `filters` object; search debounced into `filters` separately | Keeping a manual `useEffect` with a partial dependency list; remount keys / forced reload | Partial deps caused the bug. Remount/reload hides stale state instead of fixing it. |
| Search UX | 300 ms debounce on committed `filters.search`; dropdowns commit immediately | Debouncing every filter | Spec requires immediate requests for non-search filters. |
| Where filters run (API) | `ApplyFilters` on `IQueryable`, then `CountAsync`, then sort/page | Filtering in memory after `ToListAsync` | FL-5: count and pager must match the filtered set in the database. |
| Search normalization | Trim and blank→null in `TicketQuery.Normalized()` | Only trim in SQL | One representation of “no search”; avoids accidental match-nothing filters. |
| Filter tests (API) | In-memory SQLite + EF command interceptor | Pure mocked `DbSet` | Confirms EF translates filters to SQL, not just LINQ-to-objects. |

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
- **Next filter (e.g. Task 2 `slaStatus`):** Add property on `TicketQuery` and one `if` block in
  `ApplyFilters`.

#### Development login

Not implemented (Task 3). The API and web app run **without authentication** on a local dev setup.

#### Testing

Use [Quick start](#quick-start) and [Tests](#tests) below. Task 1–specific checks:

```bash
npm test
```

- Web: `useTicketList.test.ts` — filter change requests, search debounce, DEFECT-117 regression.
- API: `TicketQueriesFilterTests` — priority, combined filters, customer-name search, paging counts.

**Manual smoke:** With API + web running, filter by status/priority and confirm the list and page
count shrink; search for a customer name (e.g. `Contoso`).

#### AI usage

- **Tool:** Claude for exploring the repo, drafting tests, and reviewing EF query shape.
- **Workflow:** Reproduce DEFECT-117 in the UI → read `useTicketList` → fix → add Vitest regression →
  implement `ApplyFilters` → add SQLite integration tests → run `npm test`.
- **Verified manually:** All generated code was run through tests and a quick UI pass

#### Known limitations

- Tasks **2–4** not done (escalation/SLA, login, Docker API image).
- No login, roles, or API auth yet.
- Search uses `Contains` (substring match); case follows SQL Server collation on real DB.
- No dedicated indexes added for filter columns (40 seed tickets only).

#### Time spent (rough)

| Area | Hours |
| --- | --- |
| DEFECT-117 investigation + fix + web tests | ~1.5 |
| Server-side filtering + API tests | ~1.5 |
| README / APPROACH | ~0.5 |
| **Task 1 total** | **~3.5** |

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
| `GET /api/tickets` | Paged list with `search`, `status`, `priority`, `categoryId`, `customerId`, `assignedAgentId`, `unassignedOnly`, `sortBy`, `sortDirection`. Filters applied server-side (Task 1). |
| `GET /api/tickets/{id}` | One ticket |
| `POST /api/tickets` | Create a ticket |
| `PATCH /api/tickets/{id}/status` · `/assignment` | Change status / assignee |
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
