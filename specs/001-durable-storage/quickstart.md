# Phase 1 Quickstart: how to verify this feature

**Feature**: [spec.md](./spec.md) · **Plan**: [plan.md](./plan.md) · **Date**: 2026-08-26

There is no test project, so this document *is* the test plan. Every step below has a stated
expected outcome; anything else is a failure.

Run these in order. Step 3 is the one that would have caught the SQLite defect, and step 5 covers
the gap the automated guard cannot see.

---

## Prerequisites

Verified present on this machine: dotnet SDK 10.0.303 (builds `net8.0`), node 24.13.1,
npm 11.8.0, Docker 29.2.1 with a live daemon, PowerShell 7. Ports 5000, 5173 and the database
port must be free.

> If `dotnet restore` stalls for minutes, it is the machine's `nuget.config` reaching for an
> unreachable private Azure DevOps feed. Observed during Phase 0 research: 2.8 minutes, then
> failure. Restore against nuget.org only.

---

## 1. It builds

```powershell
dotnet build subscription-manager.sln
cd frontend; npm run build; cd ..
```

**Expect**: 0 errors from both. The only acceptable warnings from the backend are the pre-existing
`NU1900` notices about that private feed.

## 2. Cold start from nothing brings up a working stack — FR-023, FR-005

```powershell
docker-compose down -v      # -v matters: drop the volume so this is genuinely a first run
docker-compose up --build
```

**Expect**: one command, no manual step in between. The API waits for the database rather than
crashing on the startup race, then applies migrations and seeds. The frontend answers on port 80.

**Expect** three seeded customers with 2, 1 and 2 subscriptions respectively.

**This is the FR-005 check.** If the API exits immediately on the first attempt because the
database was still starting, the bounded wait is missing or too short.

## 3. The baseline diff — the primary verification

Restart the API so the store is at its seeded state, then:

```powershell
./tools/capture-api-baseline.ps1 -OutFile after.md
git diff --no-index docs/api-baseline.md after.md
```

**Expect exactly one difference**: one demonstration customer's two subscriptions swapping
position, in cases 1, 2 and 4. That is the declared ordering change from FR-020.

**Any other difference is a regression** and must be fixed, not explained. The allow-list has one
entry and it is closed.

Then commit the regenerated baseline so it reflects the new intended behaviour:

```powershell
./tools/capture-api-baseline.ps1        # rewrites docs/api-baseline.md
```

**Expect** the committed diff on that file to contain only the swap.

> This step is not a formality. Phase 0 measured that SQLite would have failed it on 15 of 29
> cases, silently, with the application starting and every request returning 200.

## 4. Data actually survives a restart — FR-001, User Story 1

```powershell
# create
$c = curl -s -X POST http://localhost:5000/api/customers `
  -H "Content-Type: application/json" `
  -d '{"name":"Restart Probe","email":"restart@probe.test"}' | ConvertFrom-Json
curl -s -X POST "http://localhost:5000/api/customers/$($c.id)/subscriptions" `
  -H "Content-Type: application/json" `
  -d '{"plan":"Pro","price":4900,"billingCycle":"monthly","startDate":"2027-01-01T00:00:00Z"}'
```

Restart the API only — leave the database running.

**Expect** `Restart Probe` still present, still with its subscription, every field unchanged.

Then, covering the acceptance scenarios the create case does not:

| Do this, then restart | Expect |
|---|---|
| Change the subscription's status to `Active` | Status reads `Active`, not `Future` |
| Update the customer's name | The new name, not the original |
| Delete the customer | Still absent — deletions persist too |
| Delete a **seeded** customer | Still absent. It must **not** be reseeded — FR-004 |
| Nothing at all | Collections identical, in identical order — SC-004 |

The seeded-delete row is the one most likely to be wrong: it fails if seeding reconciles instead
of checking for an empty database.

## 5. Timestamps by hand — the gap the baseline cannot see

The baseline normalises timestamps to `<timestamp>`, so it is blind to timezone shifts and lost
precision. Moving to a timezone-aware column is exactly what causes both.

```powershell
curl -s http://localhost:5000/api/customers | ConvertFrom-Json |
  Select-Object -First 1 -ExpandProperty createdAt
```

**Expect** a UTC instant with a trailing `Z` and sub-second digits, matching the shape recorded
before the change — for example `2026-02-26T11:26:33.341441Z`. A value that has shifted by a
timezone offset, lost its `Z`, or been truncated to whole seconds is a regression the automated
check will not report.

## 6. Money keeps its form — FR-024

```powershell
# a fractional price, which no existing data exercises
curl -s -X POST "http://localhost:5000/api/customers/$($c.id)/subscriptions" `
  -H "Content-Type: application/json" `
  -d '{"plan":"Odd","price":49.99,"billingCycle":"monthly","startDate":"2027-01-01T00:00:00Z"}'
```

**Expect** `"price": 49.99` — not `49.990`, not `50`. And confirm a seeded whole amount still
reads `"price": 4900`, not `4900.0`.

Every price in the existing data is a whole number, so without this step the fractional path
ships untested.

## 7. The frontend works, unmodified — FR-017

Browse to the SPA and exercise: list customers, open one, add a subscription, change its status,
delete it.

**Expect** every flow to work with **zero** changes to `frontend/`. If the SPA needed editing,
the contract moved and step 3 should have caught it.

## 8. Aggregate boundary — Principle I

```powershell
curl -s -o /dev/null -w "%{http_code}`n" http://localhost:5000/api/subscriptions
```

**Expect** 404. A top-level subscriptions route must not exist.

Also review `ISubscriptionRepository`: every method must still carry `customerId`. A signature
taking a bare subscription id is a constitutional violation regardless of whether it works.

---

## Done when

- [ ] Both builds clean
- [ ] `docker-compose up` from a dropped volume yields a working stack, no manual step
- [ ] Baseline diff shows the declared swap and nothing else; regenerated file committed
- [ ] All six restart scenarios in step 4 behave as tabled
- [ ] Timestamps keep timezone and precision (checked by hand)
- [ ] `49.99` round-trips as `49.99`; `4900` stays `4900`
- [ ] Frontend works with zero changes
- [ ] No top-level subscriptions route; repository signatures still carry `customerId`
- [ ] `CLAUDE.md` and `docs/CODEBASE_MAP.md` no longer claim data is lost on restart or that
      repositories are Singleton

Record the results on ALE-9 **before** opening the pull request, then run the DoD gate — the
order `CLAUDE.md` now prescribes.
