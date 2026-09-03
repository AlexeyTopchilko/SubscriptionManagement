# Implementation Plan: Durable storage for customers and subscriptions

**Branch**: `alexeytopchilko/ale-9-persist-data-in-a-real-database-instead-of-in-memory` | **Date**: 2026-08-26 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-durable-storage/spec.md`

## Summary

Replace the in-memory singleton repositories with PostgreSQL, so customers and their
subscriptions survive a restart, while keeping the wire contract byte-identical apart from the
one ordering change the spec declares.

The technical approach is EF Core 8 with the Npgsql provider, two tables joined by a cascading
foreign key, migrations applied at startup, seeding only into an empty database, and repositories
moved from `Singleton` to `Scoped`. The monetary column is unconstrained `numeric`.

PostgreSQL was not the obvious choice going in — SQLite was, on infrastructure cost. It was
eliminated by measurement: EF Core stores a `decimal` in SQLite as text and hands it back with a
different scale, turning `"price": 4900` into `"price": 4900.0`. That is a silent break of 15 of
the 29 recorded baseline cases, with no build failure and no error at runtime. Full measurements
in [research.md](./research.md).

That same measurement exposed a contradiction between two of the spec's own requirements, and
FR-024 was amended before this plan was finalised rather than worked around in code.

## Technical Context

**Language/Version**: C# 12 on .NET 8 (`net8.0`). Built with SDK 10.0.303; the target framework
is unchanged by this feature.

**Primary Dependencies**: new — `Microsoft.EntityFrameworkCore` 8.0.x,
`Npgsql.EntityFrameworkCore.PostgreSQL` 8.0.x, `Microsoft.EntityFrameworkCore.Design` (build-time,
for migrations). Existing — `Swashbuckle.AspNetCore` 6.5.0, `Microsoft.AspNetCore.OpenApi` 8.0.0.

**Storage**: PostgreSQL 16 (`postgres:16-alpine`) as a `docker-compose` service, with a named
volume. Monetary amounts in unconstrained `numeric`; timestamps in `timestamp with time zone`;
the status enum as `integer` in storage, unchanged as a string on the wire.

**Testing**: no test project exists. Verification is the manual smoke check plus a clean diff of
the regenerated `docs/api-baseline.md` (29 cases) — see [quickstart.md](./quickstart.md).

**Target Platform**: Linux containers via `docker-compose`; Windows for local development.

**Project Type**: web service (ASP.NET Core API) plus a separate SPA. Only the API changes.

**Performance Goals**: none beyond current behaviour. At the assumed scale of tens to low
thousands of customers, reads that feel immediate today must continue to.

**Constraints**: the wire contract may not change except for the declared ordering swap. No new
endpoint, no new DTO field, no change to the frontend. Storage credentials never enter the
repository.

**Scale/Scope**: single application instance. Two tables. Roughly 11 endpoints already exist and
none are added.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Assessed against constitution **v1.1.0**.

| Principle | Gate | Verdict |
|---|---|---|
| **I. Customer Is the Aggregate Root** | No access path may bypass the aggregate; every repository operation carries a `customerId`; the aggregate is written as one consistency unit | **PASS** — a separate `Subscriptions` table is explicitly permitted by v1.1.0. `ISubscriptionRepository` keeps `customerId` in every signature; no `DbSet<Subscription>` is exposed; writes save the tracked customer graph once. Cascade delete enforces FR-003 in the schema rather than trusting application code |
| **II. Expected Failures Are Returned, Not Thrown** | Services return `ServiceResult`; storage faults are unexpected and belong to the middleware | **PASS** — no service signature changes. A storage fault is genuinely unexpected and correctly reaches `GlobalExceptionMiddleware` as a 500 |
| **III. Strict Layering** | Controllers → Services → Repositories, no shortcuts; repositories do not validate | **PASS** — the `DbContext` is confined to the repository layer. No controller or service references EF Core. Validation stays in services |
| **IV. Status Is a State Machine** | Transition table unchanged, including the same-status no-op; `PUT` does not touch status | **PASS** — `SubscriptionService` is untouched. Status persists as `integer` and serialises as a string exactly as now. Baseline cases 18–25 verify it |
| **V. Stable API Contract** | camelCase, enums as strings, DTOs unchanged, frontend calls relative paths | **PASS** — `Program.cs` JSON configuration untouched; DTOs untouched; the frontend is not modified. This is the principle the provider choice was made to protect |
| **VI. Branch, Verify, Then Pull Request** | Verify, record on the ticket, then open the PR | **PASS** — planned; see quickstart for what "verify" means concretely here |

**Additional Constraints check.** The constitution states that repositories are registered
`Singleton` so in-memory state survives requests, and that all data is lost on restart. This
feature makes both statements false. They are descriptive notes in the Additional Constraints
section, not principles, so correcting them is part of this change rather than an amendment —
`CLAUDE.md` and `docs/CODEBASE_MAP.md` need the same correction.

**Development Workflow check.** The section requires that a wrong implementation be fixed by
correcting the spec and re-running the step, never by patching generated code. Already exercised:
FR-024 was amended during Phase 0 and this plan re-derived from the corrected spec.

**Result: no violations. Complexity Tracking section omitted as unnecessary.**

### Re-check after Phase 1 design

Re-run against the completed design artifacts. Still **no violations**, and the design surfaced
two places where a plausible "improvement" would have broken a principle:

- **A unique index on `Customers.Email` would have violated Principle V.** It looks like the
  correct database-level hardening. It would move the duplicate-email failure from the service's
  409 to a `DbUpdateException` reaching the middleware as a 500 — a client's ordinary mistake
  reported as a server fault. The index is deliberately omitted and email uniqueness stays in
  the service. Recorded in [data-model.md](./data-model.md).
- **Check constraints for price and dates would have violated Principle III and V** for the same
  reason: validation duplicated into storage reports violations as 500s, not the 400s the
  baseline pins, and creates a second place the rules can disagree. Columns stay permissive;
  services enforce.

One new hazard was created and is documented rather than left implicit: storing `Status` as an
integer makes the enum's declaration order a storage contract. Reordering `SubscriptionStatus`
would silently reinterpret every existing row. The wire contract is insulated because JSON
serialises by name, so this is a storage-layer trap only — see
[data-model.md](./data-model.md).

## Project Structure

### Documentation (this feature)

```text
specs/001-durable-storage/
├── plan.md              # This file
├── spec.md              # Phase -1, amended during Phase 0 (FR-024)
├── research.md          # Phase 0 output — measurements behind every decision
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output — how to verify
├── contracts/
│   └── api-contract.md  # Phase 1 output — the invariant surface
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 — created by /speckit-tasks, not here
```

### Source Code (repository root)

```text
backend/src/SubscriptionManager.Api/
├── Data/                                  # NEW — the only place EF Core appears
│   ├── AppDbContext.cs                    # DbSet<Customer>, model configuration
│   ├── DatabaseSeeder.cs                  # seeds only when the customer count is zero
│   └── Migrations/                        # NEW — initial migration, committed
├── Repositories/
│   ├── ICustomerRepository.cs             # unchanged signature
│   ├── ISubscriptionRepository.cs         # unchanged signature, customerId retained
│   ├── EfCustomerRepository.cs            # NEW — replaces InMemoryCustomerRepository
│   └── EfSubscriptionRepository.cs        # NEW — replaces the in-memory facade
├── Extensions/
│   └── ServiceCollectionExtensions.cs     # AddDbContext; Singleton -> Scoped
├── Program.cs                             # migrate + seed on startup, bounded readiness wait
├── Models/                                # UNCHANGED
├── DTOs/                                  # UNCHANGED
├── Services/                              # UNCHANGED
└── Controllers/                           # UNCHANGED

docker-compose.yml                         # NEW db service, volume, depends_on healthcheck
docs/api-baseline.md                       # regenerated; only the declared swap may differ
CLAUDE.md                                  # correct the Singleton / data-loss notes
docs/CODEBASE_MAP.md                       # same correction
frontend/                                  # UNTOUCHED
```

**Structure Decision**: the existing single-project backend layout is kept. One new folder,
`Data/`, holds everything EF Core touches, which is what keeps Principle III intact — the context
never leaks past the repository layer. The old in-memory repositories are removed rather than
left alongside: two live implementations of the same interface would leave which one is
authoritative up to registration order, and there is no requirement for an in-memory mode.

## Key implementation decisions

Full reasoning and measurements in [research.md](./research.md). Summarised:

1. **PostgreSQL, `numeric` unconstrained.** SQLite silently rewrites `4900` as `4900.0` on the
   wire. A value converter fixes that but makes `WHERE price > 100` and `ORDER BY price` return
   wrong answers, silently and permanently. Neither is acceptable for a feature whose entire
   verification is "nothing changed".
2. **Two tables, cascading foreign key, access path enforced in the interface.** The schema is
   free per Principle I as amended; the access path is not, and is enforced where it is checkable.
3. **Migrations, not `EnsureCreated`.** `EnsureCreated` works today and has no upgrade path.
   Retrofitting migrations later costs more than creating one now.
4. **Seed only when the customer count is zero.** Not EF `HasData`, which reconciles on every
   migration and would resurrect a deleted demonstration customer, violating FR-004 and US1
   scenario 3.
5. **`Scoped` repositories.** `Singleton` existed solely to keep in-memory state alive. A
   singleton capturing a scoped `DbContext` is a defect that appears as concurrency corruption
   rather than a startup error.
6. **`ORDER BY CreatedAt, Id` in the repository**, pushed to the database. `Id` is the stable
   tiebreaker FR-021 requires.

## Risks and how each is caught

| Risk | Caught by |
|---|---|
| `price` scale changes on the wire | Baseline diff. This is the risk that eliminated SQLite; it is verified, not hoped |
| `status` becomes a number instead of a string | Baseline diff, cases 13–25 |
| `notes` / `endDate` become absent instead of `null` | Baseline diff |
| Collection order changes beyond the declared swap | Baseline diff on cases 1, 2, 4 |
| Timestamps shift or lose precision on a timezone-aware column | Baseline normalises timestamps, so this needs an explicit manual check — noted in quickstart |
| Duplicate email starts returning 500 from a unique constraint instead of 409 | Baseline case 9. The uniqueness check stays in the service; no unique index is added, precisely so the failure path does not move |
| API starts before the database is ready | FR-005 bounded wait; verified by `docker-compose up` from cold |
| Adding EF packages stalls on the private NuGet feed | Observed during research: the machine's `nuget.config` includes an unreachable Azure DevOps feed that made a restore take 2.8 minutes before failing. Restore against nuget.org only if it recurs |
