# Customer Service API

A Clean Architecture .NET 10 REST API for managing customer data, using Entity Framework Core, SQL Server, and a swappable Redis/in-memory caching layer.

## Project Overview

This solution demonstrates a layered, testable microservice implementing **Clean Architecture** principles, with a caching layer added via the **Decorator pattern** rather than mixed into core business logic.

## Solution Structure

```
CustomerService/                                  (Solution Root)
│
├── src/
│   ├── CustomerService.API/                      (REST API Layer)
│   │   ├── Endpoints/
│   │   │   └── CustomerEndpoints.cs
│   │   ├── Properties/
│   │   │   └── launchSettings.json
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   ├── Program.cs
│   │   └── CustomerService.API.csproj
│   │
│   ├── CustomerService.Application/              (Business Logic, DTOs, Caching Contracts)
│   │   ├── CustomerManagement/
│   │   │   ├── Dtos/
│   │   │   │   ├── CreateCustomerDto.cs
│   │   │   │   ├── UpdateCustomerDto.cs
│   │   │   │   └── CustomerDto.cs
│   │   │   ├── Interfaces/
│   │   │   │   ├── ICustomerAppService.cs
│   │   │   │   ├── ICustomerRepository.cs
│   │   │   │   └── ICacheService.cs
│   │   │   ├── Services/
│   │   │   │   ├── CustomerAppService.cs
│   │   │   │   └── CachedCustomerAppService.cs
│   │   │   └── Exceptions/
│   │   │       └── CustomerValidationException.cs
│   │   └── CustomerService.Application.csproj
│   │
│   ├── CustomerService.Infrastructure/            (Data Access & Caching Implementations)
│   │   ├── Data/
│   │   │   └── CustomerDbContext.cs
│   │   ├── Repositories/
│   │   │   └── CustomerRepository.cs
│   │   ├── Design/
│   │   │   └── DesignTimeDbContextFactory.cs
│   │   ├── Caching/
│   │   │   ├── RedisCacheService.cs
│   │   │   └── MemoryCacheService.cs
│   │   ├── Migrations/
│   │   │   ├── 20260729023233_InitialCreate.cs
│   │   │   ├── 20260729023233_InitialCreate.Designer.cs
│   │   │   └── CustomerDbContextModelSnapshot.cs
│   │   └── CustomerService.Infrastructure.csproj
│   │
│   └── CustomerService.Domain/                    (Core Entity, no external dependencies)
│       ├── Entities/
│       │   └── Customer.cs
│       └── CustomerService.Domain.csproj
│
├── tests/
│   └── CustomerService.UnitTests/
│       ├── CustomerManagement/Services/
│       │   └── CustomerAppServiceTests.cs         (21 tests — core business logic)
│       ├── CachedCustomerAppServiceTests.cs        (8 tests — caching decorator behavior)
│       └── CustomerService.UnitTests.csproj
│
├── .dockerignore
├── .env                                           (real secrets — git-ignored, never committed)
├── .env.example                                   (template with placeholder values)
├── .gitignore
├── CustomerService.sln
├── docker-compose.yml                             (SQL Server + Redis for local dev)
├── Dockerfile
└── README.md
```

> **Note on test coverage:** only `CustomerService.UnitTests` currently exists (29 tests total, all passing, no database or Redis required — everything is mocked). An integration-test project is a reasonable future addition but has not been built yet; this README won't claim otherwise.

## Dependency Flow (Clean Architecture)

```
API  ──depends on──▶  Application  ──depends on──▶  Domain
                            ▲
                            │ implements interfaces defined here
                     Infrastructure
```

- **API** calls `ICustomerAppService` only — never touches the repository or DbContext directly
- **Application** owns business rules, DTOs, and the caching *abstraction* (`ICacheService`) — it has no idea whether caching is backed by Redis or memory
- **Infrastructure** implements `ICustomerRepository` (EF Core) and `ICacheService` (Redis or in-memory) — these are the only two places that know about SQL Server or Redis specifically
- **Domain** has zero dependencies on any other layer

## Request Flow

```
HTTP Request
    ↓
CustomerEndpoints            (routes to the right handler)
    ↓
ICustomerAppService           (resolves to CachedCustomerAppService)
    ↓
CachedCustomerAppService       (checks cache; on miss, calls the real service)
    ↓
CustomerAppService             (validation, DTO mapping, business rules)
    ↓
ICustomerRepository → CustomerRepository → CustomerDbContext → SQL Server
```

## API Endpoints

| Method | Route | Description | Cached? |
|---|---|---|---|
| GET | `/` | Health check | — |
| GET | `/customers` | List all customers | No |
| GET | `/customers/{id}` | Get one customer | **Yes** (5 min TTL) |
| POST | `/customers` | Create a customer | — (nothing to cache yet) |
| PUT | `/customers/{id}` | Update a customer | Invalidates cache entry |
| DELETE | `/customers/{id}` | Delete a customer | Invalidates cache entry |

**Example — create a customer:**
```http
POST /customers
Content-Type: application/json

{
  "name": "Jane Smith",
  "email": "jane@example.com"
}
```
Returns `201 Created` with a `Location` header pointing at `/customers/{id}`, or `400 Bad Request` with an error message if name/email fail validation.

**Example — get one customer:**
```http
GET /customers/550e8400-e29b-41d4-a716-446655440000
```
Returns `200 OK` with the customer, or `404 Not Found`.

## Caching Layer

Caching is implemented as a **Decorator**, not mixed into `CustomerAppService` directly:

```
ICustomerAppService (interface)
        ▲
        │ implements
CachedCustomerAppService ──wraps──▶ CustomerAppService (untouched, unaware caching exists)
        │
        ▼
ICacheService (abstraction) ──implemented by──▶ RedisCacheService or MemoryCacheService
```

This means the 21 tests covering `CustomerAppService`'s actual business logic never had to change when caching was added — the decorator is tested completely separately (8 tests in `CachedCustomerAppServiceTests.cs`), using mocks for both the inner service and the cache.

**Why only `GetByIdAsync` is cached:** a single customer lookup is a realistic repeated-read pattern. Caching the full `ListAsync` result was deliberately skipped — it would require invalidating that cached list on *every* create/update/delete, which is a lot of added complexity for an endpoint that, in a real system, would usually be paginated/filtered rather than "return everyone" anyway.

**Provider selection** — set in `appsettings.json`:
```json
"Caching": {
  "Provider": "Redis"
}
```
Valid values: `"Redis"` (needs the `redis` container running) or `"Memory"` (default fallback, zero extra infrastructure, cache lost on app restart).

**Cache key format:** `customer:{id}` — e.g. `customer:550e8400-e29b-41d4-a716-446655440000`

**Verifying it live:** connect to the running Redis container and watch traffic in real time:
```powershell
docker exec -it customerservice-redis-1 redis-cli
127.0.0.1:6379> MONITOR
```
Hitting `GET /customers/{id}` the first time shows an `HMGET` (miss) followed by an `HMSET` (cache populated). Hitting it again shows only `HMGET` — served from cache, no database round-trip.

## Getting Started

### Prerequisites
- .NET 10 SDK
- Docker Desktop (for SQL Server and Redis containers)

### 1. Copy the environment template
```powershell
Copy-Item .env.example .env
```
Edit `.env` and set a real `SA_PASSWORD` (this file is git-ignored and never committed).

### 2. Start SQL Server and Redis
```powershell
docker-compose up -d db redis
```

### 3. Store your local connection string in user-secrets
```powershell
dotnet user-secrets set "ConnectionStrings:CustDb" "Server=localhost,1433;Database=CustDb;User Id=sa;Password=<your-password>;TrustServerCertificate=True;Encrypt=False;" --project src/CustomerService.API
```

### 4. Apply the database migration
```powershell
dotnet ef database update --project src/CustomerService.Infrastructure --startup-project src/CustomerService.API
```

### 5. Run the app
```powershell
dotnet run --project src/CustomerService.API
```
Or press **F5** in Visual Studio using the `http`/`https` profile (the `db`/`redis` containers must already be running from step 2 — F5 does not start them automatically).

### 6. Access it
- API: `http://localhost:5269`
- Swagger UI: `http://localhost:5269/swagger`

> **Note:** `docker-compose.yml` also defines an `app` service mapped to host port 5000, intended for running the whole stack (API included) via `docker-compose up -d`. That path hasn't been exercised yet in this project's actual development flow — the API has only been run directly via `dotnet run`/F5 against the containerized `db`/`redis`. Worth testing before relying on it.

## Running Tests

```powershell
dotnet test
```
Expected: **29 passed, 0 failed** — runs in well under a second, no database or Redis required (both `ICustomerRepository` and `ICacheService` are mocked throughout).

## Key Design Decisions

- **Repository pattern** (`ICustomerRepository`) abstracts EF Core away from the Application layer, making it mockable for tests
- **DTOs** (`CreateCustomerDto`, `UpdateCustomerDto`, `CustomerDto`) decouple the API's request/response shape from the `Customer` domain entity
- **Partial-update semantics on `PUT`:** a field left `null` in the request body means "don't change this field"; an explicitly empty string means "attempt to clear it" (and fails validation, since name/email can't be blank) — this distinction is covered by a dedicated test
- **Local secrets via `dotnet user-secrets`**, not `appsettings.json` — nothing sensitive is ever committed to git. `appsettings.json`/`appsettings.Development.json` only contain empty placeholder keys
- **SQL Server retry policy** — `EnableRetryOnFailure` configured on the DbContext (5 retries, up to 10s delay) to tolerate transient connection issues
- **Caching as a Decorator**, not baked into `CustomerAppService` — keeps the caching concern fully separable and independently testable

## Known Gaps / Honest Limitations

- No authentication or authorization yet (planned: Microsoft Entra ID)
- No integration test project yet (unit tests only, all business logic mocked)
- Not yet deployed to Azure — still running locally via Docker Compose
- The `app` service in `docker-compose.yml` (full containerized stack) is defined but not yet verified working end-to-end

## Troubleshooting steps

**Database connection error** — confirm the `db` container is running (`docker ps`) and that `dotnet user-secrets list --project src/CustomerService.API` shows a `ConnectionStrings:CustDb` value.

**EF Core migration tooling can't find the connection string** — `DesignTimeDbContextFactory.cs` reads from `appsettings.json` → user-secrets → environment variables, matching the running app. If this ever breaks, check that the hardcoded `UserSecretsId` in the factory still matches the one in `CustomerService.API.csproj`.

**Redis shows no activity** — confirm `Caching:Provider` is set to `"Redis"` (not `"Memory"`) in `appsettings.json`, and that you restarted the app after changing it (config is only read at startup). Also confirm you're calling `GET /customers/{id}`, not `GET /customers` — the list endpoint is intentionally not cached.

**Port already in use** — the local dev port is `5269` (HTTP) / `7146` (HTTPS), set in `launchSettings.json`, not 5000 (that's only the Docker container mapping).
