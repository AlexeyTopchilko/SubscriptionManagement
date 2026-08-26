<!--
Sync Impact Report
==================

v1.0.0 → v1.1.0  (amendment, ALE-12)
------------------------------------
Version change: 1.0.0 → 1.1.0
Bump rationale: MINOR. Judged against this file's own versioning policy:
  - Not MAJOR — no principle is removed, and no previously compliant code becomes
    non-compliant. The existing in-memory façade satisfies both the old and the new
    wording.
  - Not PATCH — PATCH is reserved for changes that alter no obligation. This one
    frees the physical storage layout and sharpens the access-path rule, so
    obligations do change.
  - MINOR — guidance is materially expanded by a distinction that was previously
    absent: the aggregate's consistency boundary and access path versus its schema.

Modified principles:
  - I. Customer Is the Aggregate Root — title unchanged, body restated.

What changed and why: the v1.0.0 text required a Subscription to be "stored inside
  its parent's Customer.Subscriptions list" and required ISubscriptionRepository to
  "load the customer, mutate its list, save the customer back". Both clauses described
  the in-memory implementation that existed at ratification and mistook it for the
  rule. Read literally they forbid relational persistence outright, because rows
  cannot be stored inside a list — which would have made the persistence feature
  (ALE-9) non-compliant before any code was written.

  The principle now binds what it was always meant to protect: subscription routes
  stay nested, no access path bypasses the aggregate root, repository signatures
  always carry a customerId, and the aggregate is written as one consistency unit.
  It now explicitly frees the physical layout — a separate Subscriptions table with a
  foreign key is permitted and expected for a relational provider, and the choice
  belongs in plan.md.

Added sections: none. Removed sections: none.
Unchanged: Principles II-VI, Additional Constraints, Development Workflow, Governance.
Follow-up TODOs: none.

Process note: this amendment was surfaced by a Definition of Ready check on ALE-9
  before any spec was generated, not by a failing build. Had it been caught after
  speckit-specify ran, the Development Workflow rule below (fix the spec and re-run,
  never patch generated code) would have required restarting the cycle.

v1.0.0  (initial ratification)
------------------------------
Version change: (unpopulated template) → 1.0.0
Bump rationale: Initial ratification. The prior file was the unmodified
  constitution-template scaffold containing only [ALL_CAPS] placeholders and no
  governing content, so this is an initial adoption rather than an amendment.

Principles:
  - Template slots [PRINCIPLE_1..5] replaced by six named principles. The project
    requires six non-negotiables, so a sixth section (VI) was added following the
    template's established heading pattern.
  - I.   Customer Is the Aggregate Root                (new)
  - II.  Expected Failures Are Returned, Not Thrown   (new)
  - III. Strict Layering                              (new)
  - IV.  Subscription Status Is a State Machine       (new)
  - V.   Stable API Contract, Relative Frontend Paths (new)
  - VI.  Branch, Verify, Then Pull Request            (new)

Added sections:
  - Additional Constraints (was [SECTION_2_NAME])
  - Development Workflow (was [SECTION_3_NAME])
  - Governance (populated)

Removed sections: none.

Follow-up TODOs: none. All placeholders resolved; no bracket tokens deferred.

Verification note: every principle below was checked against source before being
  written, not taken from CLAUDE.md prose alone. One divergence was found and is
  encoded accurately in Principle IV - SubscriptionService.ValidateStatusTransition
  returns null when current == next, so a same-status PATCH is a permitted no-op
  even from the terminal Cancelled state. CLAUDE.md does not mention this.
-->

# Subscription Manager Constitution

## Core Principles

### I. Customer Is the Aggregate Root

`Customer` is the only aggregate root. A `Subscription` is reachable only through its parent
customer, and the two are made durable together.

What this binds:

- Every subscription route MUST be nested under `/api/customers/{customerId}/subscriptions/...`
  and MUST require a `customerId`. A top-level `/api/subscriptions` route is forbidden.
- A subscription MUST NOT be reachable without resolving its parent customer first. No second
  access path may bypass the aggregate root — this holds regardless of whether a direct lookup
  would be faster, or whether the storage engine could support one.
- Repository interfaces MUST NOT expose any operation that retrieves or mutates a subscription
  without a `customerId` in its signature.
- A customer and its subscriptions MUST be written as a single consistency unit, so a
  subscription can never be persisted in a state its parent does not reflect.

What this explicitly does not bind:

- The physical storage layout is not a constitutional matter. A separate `Subscriptions` table
  keyed by a foreign key to `Customers` is permitted, and is the expected relational mapping; an
  in-memory `List<Subscription>` is equally permitted. Choosing between them is a `plan.md`
  decision.

Rationale: the aggregate boundary governs *who may reach* a subscription and *when it becomes
durable* — not how many tables it occupies. The v1.0.0 wording required a subscription to be
"stored inside its parent's `Customer.Subscriptions` list" and required repositories to "load the
customer, mutate its list, save the customer back". Both described the in-memory implementation
that happened to exist at ratification and mistook it for the rule. Taken literally they forbid
relational persistence entirely, since rows cannot live inside a list. The invariant worth
protecting survives any storage engine; the mechanism never was the invariant.

### II. Expected Failures Are Returned, Not Thrown

Services MUST report expected outcomes as `ServiceResult` / `ServiceResult<T>` values, using
`ServiceResultType.Ok | NotFound | Conflict | BadRequest`.

- Services MUST NOT throw for any failure a caller can anticipate — missing entity, invalid
  input, forbidden state transition. Return `ServiceResult.NotFound(...)` or
  `ServiceResult.BadRequest(...)` instead.
- Controllers MUST translate outcomes by switching on `result.ResultType`. A controller MUST NOT
  inspect exception types to decide a status code.
- `GlobalExceptionMiddleware` is reserved for genuinely unexpected exceptions. Routing an
  expected validation failure through it is a violation even though the resulting status code
  would be identical.

Rationale: exceptions used for control flow make the set of possible outcomes invisible at the
call site. An explicit result type makes every branch a controller must handle enumerable.

### III. Strict Layering

The dependency direction is Controllers → Services → Repositories, and MUST NOT be short-circuited.

- Controllers MUST contain no business logic: they bind input, call one service, and map
  `ResultType` to an `IActionResult`.
- Services MUST NOT reference HTTP concerns — no `IActionResult`, no `HttpContext`, no status
  codes.
- Repositories MUST NOT validate. They persist and retrieve; rule enforcement lives in services.
- A controller MUST NOT call a repository directly, and a repository MUST NOT call a service.

Rationale: each layer is only substitutable while its responsibilities stay disjoint. Validation
duplicated into a repository is validation that will eventually disagree with the service.

### IV. Subscription Status Is a State Machine

Subscription status transitions MUST be enforced by `ValidateStatusTransition` and MUST follow
exactly this graph:

- `Future` → `Active` | `Cancelled`
- `Active` → `Paused` | `Cancelled`
- `Paused` → `Active` | `Cancelled`
- `Cancelled` → terminal; no outbound transition is permitted
- Any status → the same status is a permitted no-op, including `Cancelled` → `Cancelled`

Additional binding rules:

- Status MUST change only through `PATCH /api/customers/{customerId}/subscriptions/{id}/status`.
- The full-update `PUT` endpoint MUST NOT read or write `Status`; it updates `Plan`, `Price`,
  `BillingCycle`, `StartDate`, `EndDate`, and `Notes` only. This omission is deliberate, not an
  oversight to be "fixed".
- On create, status MUST be derived by the service — `Future` when `StartDate > UtcNow`,
  otherwise `Active`. A client-supplied `Status` on create MUST be ignored.
- A rejected transition MUST surface as `BadRequest`, never as a thrown exception (see
  Principle II).

Rationale: a subscription's status drives billing consequences, so an unreachable or reversible
terminal state is a correctness bug rather than a cosmetic one. Confining mutation to a single
endpoint keeps the transition table the only place the rule exists.

### V. Stable API Contract, Relative Frontend Paths

The wire contract MUST stay consistent across every endpoint.

- JSON property names MUST be camelCase, and enums MUST serialise as strings via
  `JsonStringEnumConverter`. Both are configured centrally in `Program.cs` and MUST NOT be
  overridden per-endpoint.
- DTOs in `DTOs/` are the contract surface. Entities MUST NOT be returned directly from a
  controller, and any DTO change MUST keep Swagger accurate.
- The frontend MUST call relative `/api/...` paths only, through the single client in
  `frontend/src/api/index.js`. Hard-coding a backend host or port anywhere in the SPA is a
  violation — dev routing is Vite's proxy, production routing is nginx.
- New browser origins MUST be added to the CORS policy in `Program.cs`, which currently allows
  `http://localhost:5173` and `http://localhost:3000`.

Rationale: one serialisation policy and one HTTP client mean a contract change has exactly one
place to be made and one place to be reviewed. A hard-coded backend URL works in the environment
it was written in and fails silently everywhere else.

### VI. Branch, Verify, Then Pull Request

Every feature and every fix MUST be developed on a dedicated branch and merged into `main`
through a GitHub pull request, in this order:

1. Branch from the latest `main`, preferring the branch name the tracking issue suggests.
2. Verify end-to-end against the original scenario — exercise the endpoints for backend work,
   drive the UI in a browser for frontend work, and cover every stated acceptance criterion.
3. Record the verification result on the tracking issue, with concrete evidence: endpoints hit
   and their status codes, UI flows exercised, log lines proving the defect is gone.
4. Only then open the pull request, referencing the issue ID so it auto-links.

- Direct commits to `main` are permitted ONLY for trivial repository metadata — root `README.md`,
  `.gitignore`, `.mcp.json`, `CLAUDE.md`, `docs/`. When in doubt, branch.
- If verification cannot be run because the environment is unavailable, that MUST be stated
  explicitly on the issue. Silently skipping step 2 or 3 is a violation.

Rationale: the verification record is what makes the PR reviewable by someone who cannot
reproduce the original bug. Writing it before the PR exists is what stops it from becoming a
retrospective guess.

## Additional Constraints

**Technology stack.** Backend is .NET 8 / ASP.NET Core with Swashbuckle, as a single project at
`backend/src/SubscriptionManager.Api/`. Frontend is React 18 + Vite + React Router 6, with no UI
library and inline styles. Introducing a UI framework, a state-management library, or a second
backend project is a structural change that MUST be justified against Principle III before it is
proposed.

**Persistence.** Repositories are registered as Singleton in
`Extensions/ServiceCollectionExtensions.cs` specifically so in-memory state survives between
requests; services are Scoped. A consequence is that all data is lost on process restart. Any
change to storage MUST revisit these lifetimes — Singleton repositories holding external state
would be a defect.

**Known gap: there is no automated verification.** The solution has no test project, and the
frontend has no lint, format, or test script — `package.json` defines only `dev`, `build`, and
`preview`. The acceptance gate today is therefore the manual smoke check required by Principle
VI step 2.

This is recorded as a gap, not endorsed as a practice. It has two binding consequences:

- The manual smoke check is MANDATORY, not a courtesy. With no test suite, an unverified change
  is an unverified release; there is no safety net underneath it.
- Adding automated tests MUST NOT be treated as out of scope by default. Any contributor adding
  a test project or a lint script is extending the quality gates below, and this section MUST be
  amended when that happens.

## Development Workflow

**Adding an endpoint** follows a fixed path, which exists to keep Principle III intact:

1. Add or extend a DTO in `DTOs/`.
2. Add the method to the service interface (`Services/I*Service.cs`) and its implementation,
   returning `ServiceResult<T>` for every outcome the controller must distinguish.
3. Add the controller action and switch on `result.ResultType`.
4. Add the call to `frontend/src/api/index.js`, then consume it from a hook in
   `frontend/src/hooks/`.

Skipping step 1 or 2 to let a controller talk to a repository directly violates Principle III
even when the endpoint works.

**Quality gates before requesting review:**

- `dotnet build subscription-manager.sln` succeeds with no new warnings.
- `npm run build` succeeds in `frontend/`.
- The manual smoke check from Principle VI has been run and recorded.
- Swagger reflects any changed or added contract.
- Documentation that the change invalidates has been updated — `CLAUDE.md` and
  `docs/CODEBASE_MAP.md` describe current behaviour, so a change that makes them wrong is
  incomplete until they are corrected.

**Spec-Driven Development.** When a feature is built through the Spec Kit workflow, the spec is
the primary artifact. If the implementation is wrong, the spec MUST be corrected and the relevant
step re-run; patching generated code directly defeats the method and is a violation of the
workflow, though not of the code principles above.

## Governance

This constitution supersedes ad-hoc convention and prior practice. Where it conflicts with a
habit, a code comment, or an older document, this file wins, and the conflicting source MUST be
corrected.

**Authority and precedence.** `CLAUDE.md` remains the runtime development guide and
`docs/CODEBASE_MAP.md` the navigation reference; both are descriptive. This constitution is
prescriptive. When the descriptive documents disagree with it, they are out of date and MUST be
updated rather than followed.

**Amendment procedure.** An amendment MUST be proposed in a pull request that changes this file
and nothing unrelated, MUST state which principle is affected and why, and MUST record the
version bump with its rationale in the Sync Impact Report comment at the top of this file. A
principle MUST NOT be weakened silently as a side effect of a feature pull request.

**Versioning policy** follows semantic versioning:

- MAJOR — a principle is removed, or redefined so that previously compliant code is now
  non-compliant.
- MINOR — a principle or section is added, or its guidance materially expanded.
- PATCH — clarification, wording, or typo fixes that change no obligation.

**Compliance review.** Every pull request MUST be checked against these principles before merge.
A reviewer finding a violation MUST either request a change or require an amendment to this file;
merging a known violation without one is not permitted. Added complexity MUST be justified — the
stack is deliberately small, and "it might be useful later" is not a justification.

**Version**: 1.1.0 | **Ratified**: 2026-08-26 | **Last Amended**: 2026-08-26
