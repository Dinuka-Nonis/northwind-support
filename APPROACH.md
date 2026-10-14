# Approach

How each assignment task was tackled. **Tasks 1, 2 and 3** are finished.

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

### 3.1 How I read the task

The app was open to anyone, and the only thing that needs a real identity is the escalation history,
which until now took a typed name. So I treated three things as the core and kept everything else
small:

1. **A password must be hard to steal and a failure must say nothing.** The hash is slow and salted,
   and every way of failing returns the same answer.
2. **The server decides who is allowed in, once.** If each controller has to remember to check, one
   will eventually forget.
3. **The signed-in agent is a fact from the token, never from the request.** Anything the client can
   type, the client can fake.

### 3.2 Where I put things, and why

- **Domain** only gains the `PasswordHash` on `Agent`, with one method that rejects a blank hash.
- **Application** owns the login rules (`LoginCommandHandler`) and two small seams: `IPasswordHasher`
  and `ITokenIssuer`. They are real seams: the hash algorithm and the token format can change without
  touching the rules, and the rules are tested with the real hasher and a mocked issuer.
- **Infrastructure** has the BCrypt hasher and the narrow, read-only lookup for sign-in.
- **Presentation** is the only layer that knows about JWT: the options, the issuer, the bearer setup,
  the fallback policy and the claim readers. Application never sees a token library.
- **Web** keeps the session in one small module with no React in it, the API client reads it, and a
  provider turns it into React state. A `401` is handled in one place, the client, not in each screen.

### 3.3 The decisions I thought hardest about

- **A column, not a Users table.** Each login is one agent and there are no roles. A second table
  would only add a join. It would be the right move if other kinds of user appeared.
- **A fallback policy, not `[Authorize]` everywhere.** Closed by default means a new controller cannot
  be left open by mistake. The cost is that Swagger and login need to be called out, and they are.
- **The token lives in memory.** A saved token can be read by any script on the page. Keeping it in
  memory means a refresh signs you out, which I accepted and wrote down. The better answer for
  production is an `httpOnly` cookie or a backend-for-frontend.
- **60 minutes and no refresh token.** Long enough for a working session, short enough to limit a
  leaked token. Refresh tokens are out of scope.
- **One generic failure, with the real reason in the log.** The caller learns nothing about which
  emails exist, and the team can still see what happened. An unknown email is not logged, because
  people paste passwords into that box.
- **A late `401` must not sign out a fresh session.** The client only clears the session when the token
  the failed call carried is still the current one.
- **`escalatedBy` was removed from the request, not just ignored.** The type no longer has the field,
  so it cannot be misused later. The price is a known limit: a name over 100 characters would fail.

### 3.4 How I checked it

- Unit tests for the rules: the hasher, each way login can fail, and the token's claims and exact
  expiry using the fixed clock.
- An enforcement test on a small host that calls the real `AddJwtAuthentication`. It has an endpoint
  with no `[Authorize]`, so a `401` there can only come from the fallback policy. No token, an expired
  token, a tampered one and one signed with another key are all refused.
- Web tests for the behaviour that matters: the token is attached, a `401` clears the session, a
  signed-out visit goes to login and comes back, and invalid input never reaches the API.
- By hand against a running API: a ticket call with and without the token, the same `401` for a wrong
  email and a wrong password, inactive Yuki refused, only hashes in the table, and the redirect to login
  and back in the browser.

---

## Task 4: Dockerise the API

Not started.
