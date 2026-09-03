# Phase 0 Research: Durable storage for customers and subscriptions

**Feature**: [spec.md](./spec.md) · **Date**: 2026-08-26

Every claim below was measured against a running database, not recalled. The probe was a
throwaway console project using EF Core 8.0.10 with both the SQLite and Npgsql providers,
plus a disposable `postgres:16-alpine` container. Where a measurement turned out to be
invalid, that is stated rather than quietly dropped.

---

## Decision 1 — Storage provider: PostgreSQL

**Decision**: PostgreSQL 16, as a `docker-compose` service, with the monetary column declared
as unconstrained `numeric`.

**Rationale**: SQLite was the cheaper option on infrastructure and was the front-runner going
in. It was eliminated by measurement, not by preference — it silently breaks the wire contract
this feature is built to preserve.

### The measurement that decided it

EF Core maps `decimal` to a SQLite `TEXT` column. No warning is emitted at model build or at
schema creation. Values survive exactly *in decimal terms* — but their **scale does not**:

| Written | Stored (TEXT) | Read back | Scale | JSON emitted |
|---|---|---|---|---|
| `4900m` | `"4900.0"` | `4900.0m` | 1 | **`4900.0`** |
| `19900m` | `"19900.0"` | `19900.0m` | 1 | **`19900.0`** |
| `49.99m` | `"49.99"` | `49.99m` | 2 | `49.99` |

`docs/api-baseline.md` pins `"price": 4900`. SQLite returns `4900.0`. That is a contract break
on 15 of the 29 recorded cases, it fails no build, and the application starts and answers 200
throughout. It is exactly the failure mode the baseline was built to catch — and it caught it
before a line of feature code existed.

The mechanism matters: `4900m == 4900.0m` is `true` in C#, so no equality check would have
noticed. Only the serialised form differs, because `System.Text.Json` renders a decimal's
scale:

```
decimal 4900     scale=0  ->  JSON 4900
decimal 4900.0   scale=1  ->  JSON 4900.0
decimal 4900.00  scale=2  ->  JSON 4900.00
```

### PostgreSQL, measured on a live instance

Unconstrained `numeric`, all seven probe values:

| Check | Result |
|---|---|
| All 7 values round-trip byte-identical in JSON | **YES** |
| `WHERE Price > 100` | **4** — correct |
| `ORDER BY Price` numerically correct | **YES** |
| `ORDER BY CreatedAt` correct (FR-020 depends on it) | **YES** |

Raw stored values, read straight out of the table, confirm the storage keeps the submitted
form including a meaningful trailing zero:

```
 Id |    Price
----+-------------
  1 |        4900
  6 |       0.005
  7 | 12345678.90
```

### Alternatives considered

**SQLite with EF Core defaults** — rejected. Breaks `price` on the wire, silently. Also
`ORDER BY` on a decimal throws `NotSupportedException` outright:
*"SQLite does not support expressions of type 'decimal' in ORDER BY clauses."*

**SQLite plus a value converter** storing the decimal's invariant string form and parsing it
back — genuinely fixes the contract. Measured: all 7 values round-trip byte-identical. But it
converts the column to plain text, so the database can no longer reason about the number:

```
WHERE Price > 100  ->  5   (correct answer is 4)
ORDER BY Price     ->  0.005, 0.01, 1234.56, 12345678.90, 19900, 49.99, 4900
```

Both wrong, both silent, both permanent. Nothing in the API filters or sorts by price today —
verified across all 11 endpoints — so it would ship working. The first person to add
"subscriptions over 100" gets wrong results with no error and no clue why. Rejected: trading a
contract break for a correctness landmine is not a fix, it is a relocation.

**SQLite with prices as integer minor units** — correct arithmetic, no infrastructure. Rejected
on risk: it needs conversion at every boundary and must reconstruct the original scale on the
way out, so `4900` does not become `4900.00`. That is the same problem as the converter, with
more code to get wrong and no test suite to catch it.

### Costs accepted with PostgreSQL

- A `db` service in `docker-compose`, a volume, and a connection string in configuration.
- FR-005's bounded readiness wait becomes real work rather than trivially satisfied.
- This is the "heavy dependency" the topic attachment's Step 4 advised against. The deviation
  was already accepted on ALE-7 for the feature as a whole; this measurement is the concrete
  justification for it, and is worth reporting in ALE-11.

### Invalid measurement, recorded rather than hidden

The probe also appeared to show that `numeric(18,2)` round-trips all seven values identically —
which cannot be true, since `0.005` does not fit in two decimal places. Inspecting the live
schema showed `numeric` with no precision or scale: EF Core caches the built model per context
type, so the second configuration silently reused the first. The declared-scale variant was
therefore never tested. It is not needed — unconstrained `numeric` is both verified and the
correct choice — but the result is not evidence and is not treated as such.

---

## Decision 2 — Aggregate mapping: separate table, access path enforced in the interface

**Decision**: `Customers` and `Subscriptions` as two tables, joined by a foreign key from
subscription to customer, with cascade delete. The subscription collection is configured as a
navigation owned by the customer aggregate and is always loaded through it.

**Rationale**: Constitution v1.1.0 explicitly frees the physical layout and names a separate
table with a foreign key as the expected relational mapping. What it binds is the *access path*,
and that is enforced where it belongs — in the repository interface, not in the schema.

`ISubscriptionRepository` keeps `customerId` in every signature. Reads load the customer with
its subscriptions included and select from that graph; writes mutate the tracked customer
aggregate and save once. There is no `DbSet<Subscription>` exposed outside the context and no
operation that takes a bare subscription id.

Cascade delete satisfies FR-003 at the storage level rather than relying on application code
remembering to clean up — a subscription cannot outlive its customer even if a future code path
forgets.

**Alternatives considered**: EF Core `OwnsMany`. It expresses ownership more strongly and would
also produce a separate table. Rejected as unnecessary: an owned type cannot be queried
independently, which sounds ideal, but it also complicates the existing repository shape for no
gain the interface constraint does not already provide.

---

## Decision 3 — Schema creation: EF Core migrations

**Decision**: EF Core migrations, applied at startup, with an initial migration committed.

**Rationale**: `EnsureCreated` is tempting given there is no deployed data to preserve, and it
would be defensible today. It is rejected because it has no upgrade path: the first schema
change after this feature requires either dropping the database or retrofitting migrations onto
an existing schema. The cost of starting correctly is one command now; the cost of retrofitting
is unbounded and lands on whoever changes the schema next.

Migrations also make the schema reviewable. The initial migration is a diffable artifact showing
exactly what `numeric`, what nullability and what cascade behaviour was chosen — which matters
more than usual here, because the column type is the thing that broke SQLite.

**Seeding** (FR-004) is separate from schema creation and runs after migrations: count the
customers, and write the three demonstration records only if the count is zero. Not
`HasData` — EF's model seeding reconciles on every migration, which would resurrect a deleted
demonstration customer and violate FR-004 and US1 scenario 3.

---

## Decision 4 — Service lifetimes: Scoped

**Decision**: repositories become `Scoped`. Services stay `Scoped`. `AddDbContext` registers the
context as `Scoped`, which is its default.

**Rationale**: the current `Singleton` registration exists for exactly one reason, stated in the
code and in `CLAUDE.md` — to keep in-memory state alive between requests. External storage
removes that reason entirely. Keeping `Singleton` would be worse than pointless: a singleton
capturing a scoped context is a lifetime violation that surfaces as concurrency corruption
under load rather than as a startup error.

`ServiceCollectionExtensions` changes accordingly, and the `CLAUDE.md` note claiming
repositories are Singleton becomes false and must be corrected in the same change.

---

## Decision 5 — Ordering: in the repository query

**Decision**: `ORDER BY CreatedAt, Id` — applied in the repository, pushed to the database, for
both customers and the subscriptions within a customer.

**Rationale**: FR-019 through FR-021 require determinism with a stable tiebreaker. `CreatedAt`
gives the meaningful order; `Id` breaks ties when two records share a creation instant, which is
plausible for seeded records written in one batch. Ordering in the repository keeps it in one
place and lets the database do it. Verified above that `ORDER BY CreatedAt` behaves correctly on
this provider.

The declared consequence stands: one demonstration customer's two subscriptions swap position,
because their insertion order does not match their creation times. That is the single permitted
baseline difference and it is pre-declared in the spec.

---

## Requirement corrected as a result of this research

**FR-024 was wrong and has been amended.** It required "an explicitly declared precision and
scale". A fixed scale pads every amount to that many decimal places, so a scale of 2 renders
`4900` as `4900.00` — the exact wire change FR-009 forbids. The two requirements contradicted
each other, and the contradiction was only visible once the serialised form was measured.

The amended requirement asks for what was actually wanted: exactness, no floating point, and
preservation of the submitted form. Unconstrained `numeric` satisfies all three.

Per the constitution's Development Workflow section, the spec was corrected and this phase
re-derived from it, rather than the discrepancy being worked around in code.
