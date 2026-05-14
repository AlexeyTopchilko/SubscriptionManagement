# Subscription Manager

Monorepo for managing customer subscriptions.

## Structure

```
subscription-manager/
├── backend/          # ASP.NET Core Web API (C#)
├── frontend/         # React + Vite
└── docker-compose.yml
```

## Quick Start

### Backend

```bash
cd backend
dotnet run --project src/SubscriptionManager.Api
# API will be available at http://localhost:5000
# Swagger UI: http://localhost:5000/swagger
```

### Frontend

```bash
cd frontend
npm install
npm run dev
# Application will be available at http://localhost:5173
```

### Docker Compose (both services)

```bash
docker-compose up
```

## API Endpoints

### Customers
| Method | URL | Description |
|--------|-----|----------|
| GET | /api/customers | List of customers |
| GET | /api/customers/{id} | Customer by ID |
| POST | /api/customers | Create a customer |
| PUT | /api/customers/{id} | Update a customer |
| DELETE | /api/customers/{id} | Delete a customer |

### Subscriptions
| Method | URL | Description |
|--------|-----|----------|
| GET | /api/customers/{customerId}/subscriptions | Customer's subscriptions |
| GET | /api/customers/{customerId}/subscriptions/{id} | Subscription by ID |
| POST | /api/customers/{customerId}/subscriptions | Create a subscription |
| PUT | /api/customers/{customerId}/subscriptions/{id} | Update a subscription |
| PATCH | /api/customers/{customerId}/subscriptions/{id}/status | Change status |
| DELETE | /api/customers/{customerId}/subscriptions/{id} | Delete a subscription |

## Subscription Statuses

- `Active` — active subscription
- `Paused` — paused
- `Cancelled` — cancelled
- `Future` — scheduled for the future
