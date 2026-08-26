# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Codebase Overview

Small full-stack subscription manager: ASP.NET Core 8 Web API backend with in-memory storage, paired with a React 18 + Vite SPA. The backend is organized as Controllers → Services → Repositories with `Customer` as the aggregate root — `Subscription` lives inside `Customer.Subscriptions` and is always accessed under `/api/customers/{customerId}/subscriptions/...`.

**Stack**: .NET 8, ASP.NET Core, Swashbuckle/Swagger; React 18, Vite, React Router 6 (no UI library, inline styles).
**Structure**: `backend/src/SubscriptionManager.Api/` (single project) + `frontend/src/` (pages, hooks, components, single `api/index.js` client) + `docker-compose.yml` for full-stack run.

For detailed architecture, route table, state machine, data flow diagrams, and a navigation guide, see [docs/CODEBASE_MAP.md](docs/CODEBASE_MAP.md).

## Workflow — quality gates (read this first)

Two gates bracket every piece of work: **DoR before it starts, DoD before it goes to review.** Both skills live in `.claude/skills/`. They are gates, not optional analysis — skipping either is a process violation, not a shortcut.

### Before starting — Definition of Ready

**Run the `DoR` skill on the ticket before anything else. No branch, no code, no `/speckit.*` command until the verdict is READY.**

- **Scope:** every feature, fix, or task tracked by a Linear ticket. The trivial repo-meta changes that may go straight to `main` (see the next section) do not need a DoR — there is no ticket to refine. If work is substantial enough to deserve a ticket, it is substantial enough to gate.
- Give the skill the ticket's full content *and* the relevant repo context. It can only judge what it is handed; a DoR run on a one-line summary is theatre.
- **Verdict NOT READY → fix the ticket, then re-run.** Do not start work "while the ticket gets sorted out". The entire value of the gate is that ambiguity is cheapest to resolve before any artifact exists.
- Record the verdict where the work can be traced to it — a comment on the Linear ticket, or the description itself if that is what got fixed.

Why this is binding rather than advisory: a DoR check on ALE-9 found that Principle I of the project constitution forbade relational persistence outright — it required subscriptions to be "stored inside a list", which no relational database can do. The feature would have been non-compliant before a line was written. Caught at refinement it cost a single amendment (ALE-12). Caught one step later, after `/speckit.specify`, the spec would have inherited the contradiction — and the SDD rule below forbids fixing that in code, so the whole cycle would have restarted.

### Before review — Definition of Done

**Run the `DoD` skill before opening the pull request** — after verification and after the Linear comment (steps 1–2 of the post-fix workflow below). **No PR, and no move to In Review, until it has run.**

- **Verdict NOT DONE → either close the gap, or state explicitly on the ticket which criterion is unmet and why that is acceptable.** An unexplained NOT DONE that gets a PR anyway means the gate ran for nothing.
- Report the verdict as it came out. A DoD result quietly adjusted toward the desired answer is worse than no check at all, because it launders an unverified change as a verified one.

### Two DoD criteria this project cannot satisfy

The base DoD checklist asks about automated tests and staging verification. Neither exists here:

- **No automated tests** — there is no test project in the solution, and `frontend/package.json` defines only `dev`, `build`, `preview`.
- **No staging environment** — nothing is deployed anywhere.

Left unadapted, those two make every ticket permanently NOT DONE and the checklist stops telling good work from bad. The skill itself notes the base list "can be adapted for different team contexts". For this project:

- The **automated tests** criterion maps to the mandatory manual smoke check below, plus a clean `dotnet build subscription-manager.sln` and `npm run build`. Count it met once those are done *and recorded* — and still name the underlying gap rather than pretending it is absent.
- The **staging** criterion maps to local end-to-end verification against a running stack (`dotnet run` + `npm run dev`, or `docker-compose up`).
- Both stay **recorded gaps, not endorsed practice.** Adding a test project or a CI pipeline is never out of scope by default, and when either lands, this section must be updated.

### These gates are instructions, not enforcement

Nothing in this file can technically stop a step from being skipped — `CLAUDE.md` is guidance, and guidance can be missed. Concrete evidence: the "comment on the ticket, *then* open the PR" order documented below was violated on ALE-12, in the very session that wrote these gates.

Making a gate genuinely unskippable requires a hook in `.claude/settings.json`, which the harness executes rather than the model. That decision has not been made. Until it is, read every MUST here as a binding instruction whose observance is auditable after the fact — not as a guarantee enforced in advance.

## Workflow — branches & pull requests

All features and bug fixes are developed on a dedicated branch and merged into `main` through a GitHub pull request. **Do not commit features or fixes directly to `main`.**

**Prerequisite: the DoR gate above must have returned READY before the branch is created.**

- Create a new branch off the latest `main` for every Linear issue, feature, or bug fix. Prefer the branch name Linear suggests (`gitBranchName` field on the issue, e.g. `alexeytopchilko/ale-5-fix-502-status-code`); otherwise use `feature/<short-slug>` or `fix/<short-slug>`.
- Push the branch and open a PR against `main` with `gh pr create`. Reference the Linear issue ID (e.g. `ALE-5`) in the PR title or body so Linear auto-links it.
- Keep `main` exclusively for merge commits / squashes from PRs.
- Direct commits to `main` are reserved for trivial repo-meta changes (root `README.md`, `.gitignore`, `.mcp.json`, CLAUDE.md, docs) — when in doubt, branch.

## Workflow — post-fix verification & PR creation

Once a fix or feature has been implemented locally, follow this sequence **in order** before the work is considered ready for review:

1. **Test the change end-to-end.** Reproduce the original scenario from the ticket and confirm every acceptance criterion. For backend changes, exercise the relevant endpoints; for frontend changes, drive the UI in a browser. If the ticket lists explicit AC, hit each one; if there are no formal AC, at minimum cover the original repro steps.
2. **Comment on the Linear ticket** summarizing what was tested and the outcome. Include the concrete results — endpoints hit and their status codes, UI flows exercised, log lines that prove the bug is gone. This comment is the testing record; do not skip it.
3. **Run the `DoD` gate.** Feed it the ticket's acceptance criteria and what was actually delivered, including the verification results from step 1. This is the gate described above; it belongs here in the sequence.
4. **Only after the comment is posted and the DoD gate has run, create the pull request yourself** (via `gh pr create` or the GitHub MCP — do not just hand a "create PR" URL to the user). Reference the Linear issue ID in the PR title or body so Linear auto-links it.

If verification fails at step 1, do not move on — fix the implementation and re-test. If you cannot run the verification (no Docker, no browser, environment unavailable), say so explicitly in the Linear comment rather than silently skipping.

If step 3 returns NOT DONE, do not proceed to step 4 on the assumption it is close enough. Either close the gap and re-run, or record on the ticket which criterion is unmet and why shipping anyway is the right call. The steps run **in this order** — opening the PR first and back-filling the comment afterwards defeats the point, since the record is what makes the PR reviewable by someone who cannot reproduce the original problem.

## Commands

### Backend (.NET 8 Web API)
```powershell
# Run dev server (http://localhost:5000, Swagger at /swagger)
dotnet run --project backend/src/SubscriptionManager.Api

# Build
dotnet build subscription-manager.sln

# Restore packages
dotnet restore subscription-manager.sln
```
No test project is configured.

### Frontend (React + Vite)
```powershell
cd frontend
npm install
npm run dev       # http://localhost:5173, proxies /api -> :5000
npm run build     # production bundle
npm run preview   # serve built bundle
```
No lint, formatter, or test script is configured.

### API baseline (regression checking without tests)
```powershell
# Start the API fresh first — storage is in-memory, so leftover mutations shift the seed data
./tools/capture-api-baseline.ps1                       # regenerates docs/api-baseline.md

# After a change that could alter the wire contract:
./tools/capture-api-baseline.ps1 -OutFile after.md
git diff --no-index docs/api-baseline.md after.md      # any diff is a contract change
```
`docs/api-baseline.md` is a committed snapshot of all 29 endpoint cases — status codes and response shapes, including negative paths and the full status state machine. GUIDs, timestamps and trace ids are normalised so reruns are byte-identical. It is **not** a test suite: it asserts nothing and fails no build. It is the substitute reference this repo needs while it has no tests, and it is how the "no automated verification" gap is partially mitigated.

### Full stack via Docker
```powershell
docker-compose up        # backend :5000, frontend (nginx) :80
```

## Architecture

### Layout
- `backend/src/SubscriptionManager.Api/` — single ASP.NET Core project. Layered: `Controllers/` → `Services/` → `Repositories/`, with `Models/` (entities), `DTOs/` (request/response shapes), `Middleware/`, `Extensions/`.
- `frontend/src/` — React SPA. `pages/` are routed views, `components/{customers,subscriptions,shared}/` are presentational, `hooks/` hold data-fetching logic, `api/index.js` is the single HTTP client.
- `subscription-manager.sln` ties the backend project together.

### Backend data model — Customer is the aggregate root
`Subscription` is stored **inside** the parent `Customer.Subscriptions` list. There is no separate subscription store. Consequences:
- `InMemorySubscriptionRepository` is a façade over `ICustomerRepository`: every operation loads the customer, mutates its list, and saves the customer back.
- Repositories are registered as **Singleton** (in `Extensions/ServiceCollectionExtensions.cs`) so in-memory state survives across requests. All data is lost on process restart.
- Services are **Scoped**.
- API routes reflect the aggregate: subscriptions live under `/api/customers/{customerId}/subscriptions/...` and always require a `customerId`.

### Service result + error handling pattern
Services return `ServiceResult` / `ServiceResult<T>` (see `Services/ServiceResult.cs`) with a `ResultType` enum: `Ok | NotFound | Conflict | BadRequest`. Controllers translate via a `switch` expression on `ResultType` into `Ok` / `NotFound` / `BadRequest` etc. — do not throw from services for expected validation failures; return a `ServiceResult.X(message)` instead.

`GlobalExceptionMiddleware` (registered first in `Program.cs`) is the safety net for unexpected exceptions and maps `ArgumentException`/`InvalidOperationException` → 400, `KeyNotFoundException` → 404, anything else → 500 with a generic message.

### Subscription business rules (in `SubscriptionService`)
- **Status state machine** enforced by `ValidateStatusTransition`:
  - `Future` → `Active` | `Cancelled`
  - `Active` → `Paused` | `Cancelled`
  - `Paused` → `Active` | `Cancelled`
  - `Cancelled` is terminal.
  Status changes go through `PATCH .../{id}/status`, **not** through `PUT` (the full-update endpoint deliberately does not touch `Status`).
- On create, status is auto-set: `Future` if `StartDate > UtcNow`, otherwise `Active`. Clients should not send `Status` on create.
- `BillingCycle` is constrained to `monthly` | `annual` (lower-cased on write).
- `EndDate`, if provided, must be strictly greater than `StartDate`.
- `AllowedPlans` is declared but currently not enforced — adjust `ValidateSubscriptionDto` if you need to lock it down.

### JSON conventions
Configured in `Program.cs`: camelCase property names, enums serialized as strings (`JsonStringEnumConverter`). Keep DTOs in sync — Swagger uses inline enum definitions.

### Frontend ↔ backend wiring
- Dev: Vite proxies `/api` → `http://localhost:5000` (`vite.config.js`). The frontend always calls relative `/api/...` paths via `frontend/src/api/index.js` — never hard-code the backend URL.
- Prod (docker-compose): the frontend container is nginx serving the built bundle; check `frontend/nginx.conf` for the `/api` proxy rule when changing ports.
- CORS in `Program.cs` allows `http://localhost:5173` and `http://localhost:3000` — add new origins there if needed.

### Adding a new endpoint — typical path
1. Add/extend a DTO in `DTOs/`.
2. Add the method to the service interface (`Services/I*Service.cs`) and implementation, returning `ServiceResult<T>` for outcomes the controller needs to distinguish.
3. Add the controller action and `switch` on `result.ResultType`.
4. Add the call in `frontend/src/api/index.js`, then consume it from a hook in `frontend/src/hooks/`.
