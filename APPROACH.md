# Approach

How each assignment task was tackled. Only **Task 1** is finished.

---

## Task 1: Defect fix and server-side filtering

### 1.1 DEFECT-117

1. **Reproduce** — Changed Status to *In progress* with DevTools open; no network call until search or page changed. Matched the report.
2. **Locate** — `useTicketList` owned both `filters` and the fetch effect. The effect dependency array listed only `search`, `page`, and `pageSize`.
3. **Root cause** — Non-search filter updates never triggered `load`, so the UI showed stale data until something in the dependency list changed.
4. **Fix** — Split “display search” from “committed filters”: debounce writes search into `filters`; use `useAsyncData` so any committed `filters` change reloads. Avoid remount/`key` tricks.
5. **Regression tests** — Vitest + fake timers: status/priority/etc. trigger an immediate second `getTickets` call; search debounces; status survives search clear (third symptom in the report).

### 1.2 Server-side filtering

1. **Read the contract** — `TicketQuery` already bound every query param; `TicketQueries.GetPagedAsync` counted and paged the full set.
2. **Order of operations** — Filter first, `CountAsync` on the filtered query, then sort/skip/take. Wrong order breaks `totalCount` and the pager (FL-5).
3. **Implementation** — Private `ApplyFilters`: one optional `Where` per bound filter, ANDed. Search on title, reference, customer name; trim via `Normalized()`.
4. **Tests** — Seed SQLite with known tickets; test exact priority, AND combination, customer-name search, unassigned-only, and paging over a filtered set. Command log confirms SQL is generated (not client-side evaluation).
5. **Task 2 hook** — Left filters as isolated `if` blocks so `slaStatus` is a property + one block, not a rewrite.

---

## Task 2: Ticket escalation and assignment

Not started.

---

## Task 3: Login and authentication

Not started.

---

## Task 4: Dockerise the API

Not started.
