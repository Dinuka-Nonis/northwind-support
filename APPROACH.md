# Approach

How each assignment task was tackled. **Tasks 1 and 2** are finished.

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

### 2.1 How I read the task

The rules are short: a priority, a due date, an owner, one level up on escalation. The task says the
hard part is where they go, so I started from that. Three things shaped everything else:

1. **Create and escalate need the same decisions.** Both work out a due date and an owner. If that
   lived in the handlers, I would have two copies that could drift.
2. **Time is an input.** Every rule that depends on "now" takes it as a value, so the tests can use a
   fixed clock and check exact boundaries.
3. **The numbers belong to configuration, and the category behaviour to the category flags.** Nothing
   in code names a category or hides a default.

### 2.2 Where I put things, and why

- **Domain (`Triage/`)** holds the rules as plain, pure units: `PriorityRule`, `SlaPolicy`,
  `AgentSelector`. They take values and return a decision plus a reason, so each one is tested with
  no database and no mocks.
- **Domain (`Ticket`)** owns the escalation guards and the history row. A ticket that cannot be
  escalated is refused by the ticket itself, whoever calls it.
- **Application (`TicketTriager`)** only assembles those rules for a use case. It holds no rule of its
  own, which is what lets create and escalate share it without sharing a handler.
- **Handlers** load, call the rules, save and map. Nothing else.
- **Infrastructure** keeps reads in one place. The SLA filter is one more `if` in `ApplyFilters`.
- **Web** keeps the rules (limits, "can this be escalated, and if not why") in one small file with no
  React in it, so they are tested directly.

### 2.3 The decisions I thought hardest about

- **Derived SLA status, not stored.** A stored status is wrong as soon as time passes. Deriving it
  costs a bit of date arithmetic in the filter, but it can never be stale.
- **Keeping the current agent on escalation without a capacity check.** The ticket already counts
  towards that agent's load. Checking capacity would take a ticket away from someone because it
  became more important, which is the opposite of what a lead would want.
- **The at-risk window starts at creation, even after an escalation.** The alternative is a stored
  window start. I kept the model smaller and wrote the trade-off down instead.
- **A refusal names its real reason.** A resolved Critical ticket says "resolved", not "Critical",
  and the screen and the API use the same order.
- **Reasons come with every decision.** Priority, window and owner each return a sentence, so nobody
  has to guess why a ticket went where it did.

### 2.4 How I checked it

- Unit tests for each rule at its boundaries, using the fixed clock and the ticket builder.
- The SQL filter runs against in-memory SQLite for the statuses SQLite can express, with a test that
  compares the filter with `SlaEvaluator` so the two cannot drift. At risk and Within SLA need
  `DATEDIFF`, so I checked those by hand against SQL Server and looked at the generated SQL.
- Web tests for the behaviour that matters: a short reason never reaches the API, a 409 is shown and
  the ticket is left alone, every SLA state looks different, and a new filter requests immediately.
- Architecture tests that were already in the repo kept the layers honest.

---

## Task 3: Login and authentication

Not started.

---

## Task 4: Dockerise the API

Not started.
