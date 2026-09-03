# Tasks: Durable storage for customers and subscriptions

**Input**: Design documents from `/specs/001-durable-storage/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/api-contract.md](./contracts/api-contract.md), [quickstart.md](./quickstart.md)

**Tests**: No test tasks. The specification does not request them, the solution has no test project, and the constitution records that absence as a known gap. Verification is the baseline diff plus the manual checks in [quickstart.md](./quickstart.md).

**Organization**: Grouped by user story. Note the execution order below — it is not priority order, and that is deliberate.

---

## ⚠️ Status: 13 of 24 done, blocked on an unreachable package feed

**Done (13)**: T001–T005, T007–T011, T013, T019, T020. All code is written and
`dotnet build subscription-manager.sln` reports **0 errors, 0 compiler warnings**.

**Blocked (11)**: T006, T012, T014–T018, T021–T024.

**The blocker**: `api.nuget.org` became unreachable partway through implementation —
`github.com` answers 200, so general connectivity is fine; NuGet specifically is not.
`Microsoft.EntityFrameworkCore.Design` is absent from the local package cache and the
`dotnet ef` tool is not installed, so the initial migration (T006) cannot be generated.
Without a migration there is no schema, so nothing can be run and nothing verified.

Runtime packages *are* cached, which is why the build passes: `Microsoft.EntityFrameworkCore`,
`.Relational`, `Npgsql` and the provider all resolve offline. Applying migrations at runtime
needs only `.Relational`; only *generating* them needs `Design`.

`Microsoft.EntityFrameworkCore.Design` is currently commented out in the `.csproj` with a
`TODO(T006)`, because an unresolvable package reference breaks the build — and the build is the
only verification available while the feed is down.

**Deliberately not done**: switching to `EnsureCreated` to sidestep the missing tooling. The
plan rejected it with reasoning (no upgrade path), and letting a transient network failure
rewrite a design decision is the wrong trade. Resume at T006 when the feed returns.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: US1, US2, US3 — maps to the user stories in spec.md
- Exact file paths are given in every task

## Path Conventions

Web app layout. Backend is a single project at `backend/src/SubscriptionManager.Api/`. The frontend at `frontend/` is **not touched by any task** — if a task seems to need it, the contract moved and something upstream is wrong.

---

## Execution order is not priority order

Stories run **US1 → US3 → US2**, not P1 → P2 → P3.

US2 ("existing clients keep working") is not a feature slice — it is a whole-system verification of the finished result, and its central check is a clean baseline diff. That diff cannot be clean until ordering (US3) is in place, because the ordering change **is** the one declared difference. Verifying the contract before ordering exists would produce a diff nobody can interpret: two changes overlapping, one intended, one not.

So US3, the lowest-priority story, is implemented before the higher-priority one is verified. US2 remains P2 in value; it simply cannot be checked earlier.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dependencies and infrastructure, before any code changes

- [X] T001 Add `Microsoft.EntityFrameworkCore` 8.0.x, `Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.x and `Microsoft.EntityFrameworkCore.Design` 8.0.x (build-time only) to `backend/src/SubscriptionManager.Api/SubscriptionManager.Api.csproj`, then confirm `dotnet build subscription-manager.sln` still reports 0 errors. If restore stalls for minutes it is the machine's unreachable private Azure DevOps feed — restore against nuget.org only (see quickstart prerequisites)
- [X] T002 Add a `db` service to `docker-compose.yml` using `postgres:16-alpine`, with a named volume for `/var/lib/postgresql/data`, a `pg_isready` healthcheck, and `depends_on: db: condition: service_healthy` on the `backend` service
- [X] T003 [P] Add the connection string to `backend/src/SubscriptionManager.Api/appsettings.json` with a development-only default, overridable by environment variable, and set that variable for the `backend` service in `docker-compose.yml`. No credential that is not a local development default may be committed

**Checkpoint**: dependencies resolve, the stack has somewhere to store data

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The persistence layer itself. Nothing in any user story can work until this is complete.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create `backend/src/SubscriptionManager.Api/Data/AppDbContext.cs` exposing **only** `DbSet<Customer>` — no `DbSet<Subscription>`, per Principle I. Configure per [data-model.md](./data-model.md): `Price` as **unconstrained `numeric`** (adding precision or scale is a contract break — see research.md), all `DateTime` columns as `timestamp with time zone`, `Guid` as `uuid`, the subscription collection as a navigation with cascade delete on the foreign key, and indexes on `Customers(CreatedAt, Id)` and `Subscriptions(CustomerId, CreatedAt, Id)`. **Do NOT add a unique index on `Customers.Email`, and do NOT add check constraints for price or dates.** Both read as obvious hardening and both move client errors from 400/409 to 500, breaking FR-015 and baseline case 9. Uniqueness and validation stay in the services (Principle III)
- [X] T005 In `backend/src/SubscriptionManager.Api/Extensions/ServiceCollectionExtensions.cs` register `AddDbContext<AppDbContext>` with the Npgsql provider and change both repository registrations from `AddSingleton` to `AddScoped`. A singleton capturing a scoped `DbContext` surfaces as concurrency corruption rather than a startup error
- [ ] T006 Generate the initial migration into `backend/src/SubscriptionManager.Api/Data/Migrations/` and commit it. Read the generated SQL and confirm the price column is `numeric` with no precision or scale, timestamps are `timestamptz`, and the foreign key cascades
- [X] T007 In `backend/src/SubscriptionManager.Api/Program.cs` apply pending migrations at startup, preceded by a **bounded** wait for the database to accept connections — retry for up to **30 seconds at roughly one-second intervals**, then fail visibly with a message naming the database as the cause (FR-005). A service still starting is a normal condition, not a fault; exiting on the first refused connection breaks FR-023
- [X] T008 Create `backend/src/SubscriptionManager.Api/Data/DatabaseSeeder.cs` that writes the three demonstration customers and five subscriptions **only when the customer count is zero** (FR-004), reproducing the exact values and `CreatedAt` timestamps currently in `InMemoryCustomerRepository.SeedData()`. Do **not** use EF Core `HasData` — model seeding reconciles on every migration and would resurrect a deleted demonstration customer. Call the seeder after migrations in `Program.cs`

**Checkpoint**: the application starts against a real database with seeded data; repositories are not yet backed by it

---

## Phase 3: User Story 1 — Work is not lost when the application restarts (Priority: P1) 🎯 MVP

**Goal**: Customers and subscriptions survive a full stop and start, including updates, status changes and deletions.

**Independent Test**: create a customer and a subscription, restart the API, read them back unchanged. This story alone delivers the entire point of the feature.

- [X] T009 [US1] Create `backend/src/SubscriptionManager.Api/Repositories/EfCustomerRepository.cs` implementing `ICustomerRepository` with its signature unchanged. Reads load subscriptions via `Include`; writes mutate the tracked customer graph and save once, so the aggregate is one unit of change (FR-002)
- [X] T010 [US1] Create `backend/src/SubscriptionManager.Api/Repositories/EfSubscriptionRepository.cs` implementing `ISubscriptionRepository` with **every method still carrying `customerId`** (Principle I). Operate through the parent customer's loaded collection — never a direct subscription query. Deleting a customer must remove its subscriptions by cascade (FR-003)
- [X] T011 [US1] Delete `backend/src/SubscriptionManager.Api/Repositories/InMemoryCustomerRepository.cs` and `InMemorySubscriptionRepository.cs`. Two live implementations of one interface leave the authoritative one decided by registration order, and no requirement calls for an in-memory mode
- [ ] T012 [US1] Verify every restart scenario in [quickstart.md](./quickstart.md) step 4 — create, update, status change, delete, **deleting a seeded customer** (must not reappear), and an idle restart. The seeded-delete row is the one most likely to fail: it breaks if seeding reconciles instead of checking for an empty database

**Checkpoint**: US1 is independently testable and complete — the feature's core value is delivered

---

## Phase 4: User Story 3 — Collections come back in a predictable order (Priority: P3)

**Goal**: Deterministic collection order that does not shuffle between reads or restarts.

**Independent Test**: read a customer's subscriptions repeatedly, across a restart, and confirm the order is identical every time and matches the declared rule.

**Why here**: implemented before US2 is verified, because the ordering change is the single declared baseline difference. Verifying the contract first would mix an intended change with any unintended one in the same diff.

- [X] T013 [US3] Apply `ORDER BY CreatedAt, Id` in `backend/src/SubscriptionManager.Api/Repositories/EfCustomerRepository.cs` (the customer list) and `backend/src/SubscriptionManager.Api/Repositories/EfSubscriptionRepository.cs` (subscriptions within a customer), pushed to the database rather than sorted in memory. `Id` is the stable tiebreaker FR-021 requires for records sharing a creation instant — plausible for seeded rows written in one batch
- [ ] T014 [US3] Verify determinism per [quickstart.md](./quickstart.md): read collections repeatedly with a restart in between and confirm identical order every time (SC-004). Confirm the one demonstration customer whose two subscriptions have creation times opposite to their insertion order now returns them oldest-first — this is the declared change, and it is expected

**Checkpoint**: ordering is deterministic and the declared difference is the only one introduced

---

## Phase 5: User Story 2 — Existing clients keep working, unmodified (Priority: P2)

**Goal**: The wire contract is byte-identical apart from the declared ordering swap, and the frontend works untouched.

**Independent Test**: exercise every endpoint and compare each status code and response body against `docs/api-baseline.md`.

- [ ] T015 [US2] Run `./tools/capture-api-baseline.ps1 -OutFile after.md` against the migrated API and diff it against `docs/api-baseline.md`. **Differences are permitted in exactly four cases — 1, 2, 4 and 5 — and all four must be the same swap.** Case 5 is the counter-intuitive one: it asks for a single subscription by id, but the capture script picks `subscriptions[0]`, so after reordering it returns the other subscription, with a different plan, price, status and notes. Treat every other difference as a regression to fix, not to explain — the allow-list is closed
- [ ] T016 [US2] Check timestamps **by hand** per [quickstart.md](./quickstart.md) step 5. The baseline normalises them to `<timestamp>`, so it cannot see a timezone shift or lost sub-second precision — and moving to a timezone-aware column is exactly what causes both. Confirm a trailing `Z` and sub-second digits, matching the shape recorded before the change
- [ ] T017 [US2] Post a subscription priced `49.99` and confirm it returns `49.99` — not `49.990`, not `50` — and that a seeded whole amount still returns `4900`, not `4900.0` (FR-024). Every existing price is a whole number, so without this the fractional path ships untested. This is the specific defect that eliminated SQLite
- [ ] T018 [US2] Drive the SPA end to end — list customers, open one, add a subscription, change its status, delete it — with **zero** changes to `frontend/` (FR-017). Also confirm `GET /api/subscriptions` still returns 404 and no repository method takes a bare subscription id (Principle I)

**Checkpoint**: the contract is verified unchanged; the feature is functionally complete

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T019 [P] Correct the now-false statements in `CLAUDE.md` and `docs/CODEBASE_MAP.md`: data is no longer lost on process restart, and repositories are no longer Singleton. Document the new `docker-compose` database service and the connection-string configuration. Both files describe current behaviour, so a change that makes them wrong is incomplete until they are fixed
- [X] T020 [P] Add a comment to `SubscriptionStatus` in `backend/src/SubscriptionManager.Api/Models/Subscription.cs` warning that the enum persists as an integer, so member order is now a storage contract — reordering or inserting a member anywhere but the end silently reinterprets every stored row. A hazard this feature creates; the wire is insulated only because JSON serialises by name
- [ ] T021 Regenerate and commit `docs/api-baseline.md` so it reflects the new intended behaviour. The committed diff on that file must contain **only** the declared swap
- [ ] T022 Final gate: `docker-compose down -v` then `docker-compose up --build` from a genuinely empty volume, confirming a working stack with no manual step (FR-023, FR-005). Then `dotnet build subscription-manager.sln` and `npm run build` in `frontend/`, both clean
- [ ] T023 Verify the storage-failure paths, which nothing else exercises. Stop the `db` container while the API runs and confirm a request returns **500** with a generic message disclosing nothing about the storage layer, and specifically **not** a 400 (FR-006). Then start the API with `db` down and confirm it fails visibly, naming the database, **after** the 30-second wait rather than on the first refused connection (FR-005, SC-006). Added during analysis: both requirements had zero coverage
- [ ] T024 Complete the sequence Principle VI mandates, in order: record the verification results on ALE-9 with concrete evidence, run the `DoD` gate, and **only then** open the pull request. Added during analysis — this sequence was already violated once today on ALE-12, and leaving it out of the executable list is how that repeats

---

## Dependencies

```text
Phase 1 (Setup: T001-T003)
        │
        ▼
Phase 2 (Foundational: T004-T008)          ← blocks everything
        │
        ▼
Phase 3 (US1: T009-T012)  🎯 MVP           ← delivers the feature's value
        │
        ▼
Phase 4 (US3: T013-T014)                   ← must precede US2 verification
        │
        ▼
Phase 5 (US2: T015-T018)                   ← verifies the whole result
        │
        ▼
Phase 6 (Polish: T019-T022)
```

Within Phase 2, T004 precedes T005 and T006; T006 precedes T007; T007 precedes T008. Within Phase 3, T009 precedes T010 (the subscription repository works through the customer aggregate); T011 follows both; T012 verifies. T013 modifies the repositories created in T009 and T010.

## Parallel opportunities

Genuinely limited — this is a layered change through one code path, so most of it is a chain rather than a fan-out. Honest list:

- **T003** is independent of T001 and T002 (different files, no shared state)
- **T019 and T020** are independent of each other and of T021 (documentation and a comment, versus a regenerated artifact)

Everything else is sequential. Marking more tasks `[P]` would be decoration.

## Implementation strategy

**MVP is Phase 1 + Phase 2 + Phase 3.** At that point data survives a restart, which is the whole reason the feature exists. Stopping there would leave the ordering non-deterministic and the contract unverified, so it is not shippable — but it is the point at which the value is demonstrable.

**Then Phase 4**, a small addition that finalises the one intended contract change.

**Then Phase 5**, which is verification rather than construction. If it finds a difference beyond the declared swap, the rule from the constitution's Development Workflow section applies: **correct the specification and re-run the affected step — do not patch the generated code.** Every such re-run is worth recording on ALE-9; it is the most useful evidence the follow-up in ALE-11 can carry.

**Then Phase 6**, without which the change is incomplete: documentation that contradicts the code is a defect, not a leftover.

## Task count and a deviation worth naming

**24 tasks** across 6 phases: 3 setup, 5 foundational, 4 in US1, 2 in US3, 4 in US2, 6 polish.

T023 and T024 were added by `speckit-analyze`, which found four requirements with zero task
coverage. Two are now covered — FR-006 and SC-006, by T023 — and the constitution's mandated
workflow sequence became executable rather than implied, by T024. FR-022 was closed the other
way: softened in the spec to state the guarantee is derived and accepted untested, rather than
carrying a task whose check could never fail. FR-016 was left uncovered as low-risk, since it is
satisfied by leaving `Services/` untouched, which the plan already states.

The topic attachment suggests 8–12 tasks for this step. This list is roughly double, for the same reason the feature itself exceeded the attachment's sizing advice: database persistence is a larger change than the `/health` endpoint the attachment had in mind. That deviation was accepted deliberately on ALE-7 and is to be reported in ALE-11.

The list was compressed once already — verification steps fully specified in [quickstart.md](./quickstart.md) are referenced rather than re-enumerated here, which is why tasks like T012 and T016 are single entries rather than six apiece.
