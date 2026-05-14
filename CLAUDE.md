# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Codebase Overview

Small full-stack subscription manager: ASP.NET Core 8 Web API backend with in-memory storage, paired with a React 18 + Vite SPA. The backend is organized as Controllers → Services → Repositories with `Customer` as the aggregate root — `Subscription` lives inside `Customer.Subscriptions` and is always accessed under `/api/customers/{customerId}/subscriptions/...`.

**Stack**: .NET 8, ASP.NET Core, Swashbuckle/Swagger; React 18, Vite, React Router 6 (no UI library, inline styles).
**Structure**: `backend/src/SubscriptionManager.Api/` (single project) + `frontend/src/` (pages, hooks, components, single `api/index.js` client) + `docker-compose.yml` for full-stack run.

For detailed architecture, route table, state machine, data flow diagrams, and a navigation guide, see [docs/CODEBASE_MAP.md](docs/CODEBASE_MAP.md).

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
