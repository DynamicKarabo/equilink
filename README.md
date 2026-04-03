<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/PostgreSQL-16-4169E1?style=for-the-badge&logo=postgresql&logoColor=white" alt="PostgreSQL 16" />
  <img src="https://img.shields.io/badge/Redis-7-DC382D?style=for-the-badge&logo=redis&logoColor=white" alt="Redis 7" />
  <img src="https://img.shields.io/badge/Docker-Compose-2496ED?style=for-the-badge&logo=docker&logoColor=white" alt="Docker Compose" />
  <img src="https://img.shields.io/badge/CQRS-Architecture-2D8CFF?style=for-the-badge&logo=architect&logoColor=white" alt="CQRS" />
  <img src="https://img.shields.io/badge/Event-Sourcing-Enabled-0052CC?style=for-the-badge&logoColor=white" alt="Event Sourcing" />
</p>

<h1 align="center">EquiLink</h1>

<p align="center">
  <strong>Modular Monolith Order Management System with Event Sourcing, CQRS, Multi-Tenancy, and Pre-Trade Risk Controls</strong>
</p>

<p align="center">
  EquiLink is a production-grade institutional trading platform designed for asset managers, hedge funds, and prop trading firms. It provides a fully auditable, append-only event store for order lifecycle management with real-time pre-trade risk validation, multi-tenant data isolation, and compliance-grade archival.
</p>

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Key Features](#key-features)
- [System Architecture](#system-architecture)
- [Event Flow](#event-flow)
- [Project Structure](#project-structure)
- [Quick Start](#quick-start)
- [API Endpoints](#api-endpoints)
- [Database Schema](#database-schema)
- [Multi-Tenancy](#multi-tenancy)
- [Idempotency](#idempotency)
- [Pre-Trade Risk Engine](#pre-trade-risk-engine)
- [Compliance & Audit](#compliance--audit)
- [Deployment](#deployment)
- [Development](#development)

---

## Architecture Overview

```mermaid
graph TB
    subgraph Client
        API_Client[API Client / Trading Terminal]
    end

    subgraph "API Layer (ASP.NET Core 8)"
        Controllers[Controllers Orders, Funds, Audit]
        MinimalAPIs[Minimal APIs Health Checks]
        Middleware[Global Exception Handler]
    end

    subgraph "MediatR Pipeline"
        Idempotency[Idempotency Behavior Redis SET NX]
        RiskValidation[Risk Validation Behavior Chain of Responsibility]
    end

    subgraph "CQRS - Write Path"
        Commands[Commands CreateOrder, CreateFund]
        Handlers[Command Handlers]
        Domain[Domain Model OrderAggregate + FSM]
        EventStore[Event Store EF Core + PostgreSQL]
    end

    subgraph "CQRS - Read Path"
        Queries[Queries GetOrder, GetAudit]
        ReadHandlers[Query Handlers]
        Dapper[Dapper Read Repository Direct SQL + JSONB]
    end

    subgraph "Infrastructure"
        PostgreSQL[(PostgreSQL 16 Primary + Read Replica)]
        Redis[(Redis 7 Idempotency + Risk Cache)]
        AzureBlob[(Azure Blob Storage WORM Archival)]
    end

    API_Client --> Controllers
    API_Client --> MinimalAPIs
    Controllers --> Middleware
    Controllers --> Commands
    Controllers --> Queries
    Commands --> Idempotency
    Idempotency --> RiskValidation
    RiskValidation --> Handlers
    Handlers --> Domain
    Domain --> EventStore
    EventStore --> PostgreSQL
    Queries --> ReadHandlers
    ReadHandlers --> Dapper
    Dapper --> PostgreSQL
    Idempotency -.-> Redis
    RiskValidation -.-> Redis
    EventStore -.-> AzureBlob
```

---

## Key Features

| Feature | Description |
|---------|-------------|
| **Event Sourcing** | All order state changes are persisted as immutable domain events in an append-only PostgreSQL table with `REVOKE UPDATE, DELETE` enforcement |
| **CQRS** | Strict separation of write (EF Core → Event Store) and read (Dapper → JSONB extraction) paths |
| **Multi-Tenancy** | Fund-level data isolation via EF Core global query filters with auditable `IgnoreTenantFilter<T>()` override |
| **Idempotency** | Redis-based deduplication using `SET NX` with 24h TTL, scoped per fund to prevent cross-tenant collisions |
| **Pre-Trade Risk Engine** | Chain-of-responsibility pipeline with pluggable risk rules (symbol blacklist, max order size) evaluated against Redis-cached state |
| **Multi-Asset Support** | Equities, ETFs, and Futures with asset-specific tick rules, margin calculations, and regulatory compliance |
| **Regulatory Amendments** | `OrderCorrectedEvent` for compensating corrections — errors are appended, never mutated |
| **Compliance Audit** | `GET /audit/orders` endpoint with CSV/PDF export and SHA-256 signing |
| **WORM Archival** | Monthly partition archival to Azure Blob Storage with immutable blob policy for 7-year retention |
| **Read Replica Routing** | `IConnectionStringProvider` routes all Dapper queries to PostgreSQL read replicas, EF Core writes go exclusively to primary |
| **Fund Onboarding** | `POST /funds` with risk limit templating (MaxOrderSize, DailyLossLimit, ConcentrationLimit) |

---

## System Architecture

### Request Pipeline

```mermaid
sequenceDiagram
    participant Client
    participant Controller
    participant Idempotency
    participant RiskEngine
    participant Handler
    participant Domain
    participant EventStore
    participant PostgreSQL

    Client->>Controller: POST /orders
    Controller->>Idempotency: Check Redis SET NX
    alt Key exists
        Idempotency-->>Client: Return cached response (200)
    else Key missing
        Idempotency->>RiskEngine: Evaluate risk rules
        loop Each IRiskRule
            RiskEngine->>RiskEngine: Check Redis cache
            alt Rule fails
                RiskEngine-->>Client: 400 RiskRuleViolation
            end
        end
        RiskEngine->>Handler: Execute handler
        Handler->>Domain: OrderAggregate.Create()
        Domain->>Domain: Fire OrderCreatedEvent
        Domain->>EventStore: Append uncommitted events
        EventStore->>PostgreSQL: INSERT INTO order_events
        EventStore-->>Idempotency: Success
        Idempotency->>Idempotency: Cache response in Redis
        Idempotency-->>Client: 200 { orderId, status, paperTrading }
    end
```

### Read Path

```mermaid
sequenceDiagram
    participant Client
    participant Controller
    participant QueryHandler
    participant ReadRepo
    participant PostgreSQL

    Client->>Controller: GET /orders/{id}
    Controller->>QueryHandler: GetOrderQuery
    QueryHandler->>ReadRepo: GetByIdAsync(orderId, fundId)
    ReadRepo->>PostgreSQL: SELECT FROM order_events WHERE aggregate_id = ? AND fund_id = ? ORDER BY version DESC LIMIT 1
    PostgreSQL-->>ReadRepo: Latest event + JSONB payload
    ReadRepo-->>QueryHandler: OrderSummaryProjection
    QueryHandler-->>Controller: Order data
    Controller-->>Client: 200 { orderId, symbol, side, ... }
```

---

## Event Flow

### Order Lifecycle State Machine

```mermaid
stateDiagram-v2
    [*] --> New: OrderAggregate.Create()
    New --> RiskValidating: StartRiskValidation()
    RiskValidating --> Approved: Approve()
    RiskValidating --> Rejected: Reject(reason)
    Approved --> Submitted: Submit()
    Rejected --> [*]
    Submitted --> [*]

    note right of New
        OrderCreatedEvent
        (FundId, Symbol, Side,
        Quantity, LimitPrice, AssetClass)
    end note

    note right of RiskValidating
        OrderRiskValidationStartedEvent
    end note

    note right of Approved
        OrderApprovedEvent
    end note

    note right of Rejected
        OrderRejectedEvent
        (Reason)
    end note

    note right of Submitted
        OrderSubmittedEvent
    end note
```

### Regulatory Correction Flow

```mermaid
stateDiagram-v2
    New --> RiskValidating: StartRiskValidation()
    RiskValidating --> Approved: Approve()
    Approved --> Approved: Correct(quantity, old, new, reason)
    Approved --> Approved: Correct(limitPrice, old, new, reason)
    Approved --> Submitted: Submit()

    note right of Approved
        OrderCorrectedEvent is appended
        State is updated via ApplyCorrection()
        Original value is preserved in event
    end note
```

---

## Project Structure

```
EquiLink/
├── src/
│   ├── Domain/                    # Core domain (no external dependencies)
│   │   ├── Aggregates/
│   │   │   ├── Order/
│   │   │   │   ├── Events/        # Domain events (OrderCreatedEvent, etc.)
│   │   │   │   ├── AssetClasses/  # AssetClassConfiguration
│   │   │   │   ├── TickRules/     # ITickRule + implementations
│   │   │   │   ├── Margin/        # IMarginCalculator + implementations
│   │   │   │   └── OrderAggregate.cs
│   │   │   └── Fund/
│   │   │       ├── Fund.cs
│   │   │       ├── FundRiskLimits.cs
│   │   │       └── FundRiskLimitTemplate.cs
│   │   ├── EventStore/
│   │   │   └── IEventStore.cs
│   │   └── Events/
│   │       ├── IDomainEvent.cs
│   │       └── DomainEvent.cs
│   │
│   ├── Infrastructure/            # EF Core, Redis, external services
│   │   ├── Behaviors/
│   │   │   └── IdempotencyBehavior.cs
│   │   ├── Compliance/
│   │   │   ├── Export/            # CsvExportService, PdfExportService
│   │   │   ├── ComplianceAuditService.cs
│   │   │   └── WormArchivalService.cs
│   │   ├── DataTier/
│   │   │   └── ConnectionStringProvider.cs
│   │   ├── Migrations/            # EF Core migrations
│   │   ├── Persistence/
│   │   │   ├── EventStore/        # OrderEvent entity, EventStore impl
│   │   │   ├── Funds/             # Fund EF configurations
│   │   │   ├── Configurations/
│   │   │   └── EquiLinkDbContext.cs
│   │   ├── ReadModels/
│   │   │   └── OrderSummaryProjection.cs
│   │   ├── ReadRepositories/
│   │   │   └── OrderReadRepository.cs
│   │   ├── RiskEngine/
│   │   │   ├── SymbolBlacklistRule.cs
│   │   │   ├── MaxOrderSizeRule.cs
│   │   │   ├── RedisRiskStateCache.cs
│   │   │   └── RiskValidationBehavior.cs
│   │   └── Tenancy/
│   │       └── CurrentFundContext.cs
│   │
│   ├── Shared/                    # Shared kernel (cross-cutting types)
│   │   ├── AssetClasses/
│   │   │   └── AssetClass.cs
│   │   ├── Idempotency/
│   │   │   └── IdempotencyKeyAttribute.cs
│   │   └── Risk/
│   │       ├── IRiskRule.cs
│   │       ├── IRiskStateCache.cs
│   │       ├── IOrderRequest.cs
│   │       └── RiskRuleResult.cs
│   │
│   └── Api/                       # Entry point
│       ├── Controllers/
│       │   ├── OrdersController.cs
│       │   ├── FundsController.cs
│       │   └── AuditController.cs
│       ├── Endpoints/
│       │   └── HealthCheckEndpoints.cs
│       ├── Features/
│       │   ├── Orders/
│       │   │   ├── Commands/
│       │   │   ├── Queries/
│       │   │   └── Dtos/
│       │   ├── Funds/
│       │   │   ├── Commands/
│       │   │   └── Dtos/
│       │   └── Audit/
│       ├── Middleware/
│       │   └── GlobalExceptionHandler.cs
│       ├── Program.cs
│       └── appsettings.json
│
├── docker-compose.yml
├── Dockerfile
├── .gitignore
├── .dockerignore
├── PROGRESS.md
└── EquiLink.sln
```

---

## Quick Start

### Prerequisites

- Docker & Docker Compose
- .NET 8 SDK (for local development)

### One-Command Deployment

```bash
docker compose up -d
```

This starts:
- **PostgreSQL 16** on port `5432`
- **Redis 7** on port `6379`
- **EquiLink API** on port `8080`

### Verify Deployment

```bash
curl http://localhost:8080/health
# {"status":"Healthy","timestamp":"..."}
```

### Apply Database Migrations

```bash
dotnet ef database update \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/Api/Api.csproj
```

---

## API Endpoints

### Orders

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/orders` | Create a new order (paper trading) | JWT `fund_id` claim |
| `GET` | `/api/orders/{id}` | Get order details (Dapper read) | JWT `fund_id` claim |

**Create Order Request:**
```json
{
  "symbol": "AAPL",
  "side": "Buy",
  "quantity": 100,
  "limitPrice": 150.00,
  "assetClass": "Equity"
}
```

**Headers:**
| Header | Required | Description |
|--------|----------|-------------|
| `X-Idempotency-Key` | Yes | UUID v4 for deduplication |
| `Authorization` | Yes | Bearer token with `fund_id` claim |

**Response:**
```json
{
  "orderId": "5131a0e0-49a5-4627-b701-afa3c84deef6",
  "status": "New",
  "paperTrading": true
}
```

### Funds

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `POST` | `/api/funds` | Onboard a new fund with risk limits | Admin |
| `GET` | `/api/funds/{id}` | Get fund details | Admin |

**Create Fund Request:**
```json
{
  "name": "Alpha Fund",
  "managerName": "John Doe",
  "riskLimitTemplateName": "Conservative",
  "maxOrderSize": 10000,
  "dailyLossLimit": 50000,
  "concentrationLimit": 0.25
}
```

### Audit

| Method | Endpoint | Description | Auth |
|--------|----------|-------------|------|
| `GET` | `/audit/orders?from=&to=&format=` | Compliance export (CSV/PDF) | Compliance Officer |

**Parameters:**
| Parameter | Required | Description |
|-----------|----------|-------------|
| `from` | Yes | Start date (ISO 8601) |
| `to` | Yes | End date (ISO 8601) |
| `format` | No | `csv` (default) or `pdf` |

### Health

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/health` | Health check |

---

## Database Schema

```mermaid
erDiagram
    order_events {
        uuid id PK
        uuid aggregate_id
        uuid fund_id
        varchar event_type
        jsonb payload
        int version
        timestamptz occurred_at
        timestamptz created_at
    }

    funds {
        uuid id PK
        varchar name UK
        varchar manager_name
        varchar status
        timestamptz created_at
    }

    fund_risk_limits {
        uuid fund_id PK "FK → funds"
        decimal max_order_size
        decimal daily_loss_limit
        decimal concentration_limit
    }

    fund_risk_limit_templates {
        uuid id PK
        varchar template_name UK
        decimal max_order_size
        decimal daily_loss_limit
        decimal concentration_limit
    }

    funds ||--o| fund_risk_limits : "has"
    fund_risk_limit_templates ||--o{ funds : "applied to"
```

### Append-Only Enforcement

```sql
REVOKE UPDATE, DELETE ON TABLE order_events FROM PUBLIC;
REVOKE UPDATE, DELETE ON TABLE order_events FROM postgres;
```

Only `INSERT` (append) and `SELECT` (read) are permitted. Modifying this requires a superuser migration.

---

## Multi-Tenancy

```mermaid
graph LR
    subgraph "Request Flow"
        JWT[JWT Token fund_id claim] --> Context[CurrentFundContext]
        Context --> Filter[EF Core Global Query Filter]
        Filter --> Query[WHERE fund_id = @fundId]
    end

    subgraph "Override"
        Bypass[IgnoreTenantFilter] -.-> Query
    end

    style Filter fill:#e1f5fe
    style Bypass fill:#fff3e0
```

- **Automatic**: Every EF Core query on `OrderEvents` automatically includes `WHERE fund_id = @fundId`
- **Override**: `IgnoreTenantFilter<T>()` provides an auditable bypass for cross-tenant queries (e.g., event stream rehydration by aggregate ID)
- **Extraction**: `CurrentFundContext` reads the `fund_id` claim from the JWT at the start of each request

---

## Idempotency

```mermaid
graph TD
    A[Request with X-Idempotency-Key] --> B{Key in Redis?}
    B -->|Yes| C[Return cached response]
    B -->|No| D[Execute handler]
    D --> E[Cache response in Redis SET NX, 24h TTL]
    E --> F[Return response]

    style B fill:#fff9c4
    style C fill:#c8e6c9
    style E fill:#c8e6c9
```

- **Key extraction**: `X-Idempotency-Key` header (UUID v4)
- **Tenant scoping**: Redis key format `idempotency:{fundId}:{idempotencyKey}`
- **Atomic operation**: `SET NX` (set if not exists) prevents race conditions
- **TTL**: 24 hours (configurable via `[IdempotencyKey(ttlHours)]`)
- **Retry behavior**: Returns cached HTTP 200 with original `orderId`, not 409

---

## Pre-Trade Risk Engine

```mermaid
graph LR
    A[Order Request] --> B[Idempotency Check]
    B --> C[Risk Rule 1 SymbolBlacklist]
    C --> D{Pass?}
    D -->|No| E[400 RiskRuleViolation]
    D -->|Yes| F[Risk Rule 2 MaxOrderSize]
    F --> G{Pass?}
    G -->|No| E
    G -->|Yes| H[Execute Handler]

    C -.-> Redis1[(Redis risk fundId blacklist)]
    F -.-> Redis2[(Redis risk fundId max_order_size)]

    style C fill:#e8f5e9
    style F fill:#e8f5e9
    style E fill:#ffebee
    style H fill:#c8e6c9
```

### Risk Rules

| Rule | Order | Redis Key | Logic |
|------|-------|-----------|-------|
| `SymbolBlacklistRule` | 1 | `risk:{fundId}:blacklist` | Rejects if symbol is in the fund's blacklist |
| `MaxOrderSizeRule` | 2 | `risk:{fundId}:max_order_size` | Rejects if quantity exceeds max order size |

### Redis Key Convention

| Key | Type | Description |
|-----|------|-------------|
| `risk:{fundId}:blacklist` | JSON array | Blacklisted symbols |
| `risk:{fundId}:max_order_size` | Decimal string | Maximum order quantity |
| `risk:{fundId}:current_exposure` | Decimal string | Current portfolio exposure |
| `idempotency:{fundId}:{key}` | JSON object | Cached idempotent response |

---

## Compliance & Audit

### Export Flow

```mermaid
graph LR
    A[GET /audit/orders] --> B[Query order_events WHERE occurred_at BETWEEN]
    B --> C{Format?}
    C -->|csv| D[CsvHelper Export]
    C -->|pdf| E[PDF Export]
    D --> F[SHA-256 Signature]
    E --> F
    F --> G[File Download]

    style D fill:#e3f2fd
    style E fill:#e3f2fd
    style F fill:#fff3e0
```

### WORM Archival

```mermaid
graph TD
    A[Monthly Cron Job] --> B[Query order_events WHERE occurred_at IN month]
    B --> C[Serialize to CSV]
    C --> D{Blob exists?}
    D -->|Yes| E[Skip - WORM policy]
    D -->|No| F[Upload to Azure Blob monthly/yyyy-MM/order_events_yyyy-MM.csv]
    F --> G[7-year retention immutable blob policy]

    style D fill:#fff9c4
    style E fill:#c8e6c9
    style F fill:#e8f5e9
```

---

## Deployment

### Docker Compose

```yaml
services:
  postgres:
    image: postgres:16-alpine
    ports: ["5432:5432"]
    volumes: [postgres_data:/var/lib/postgresql/data]

  redis:
    image: redis:7-alpine
    ports: ["6379:6379"]
    volumes: [redis_data:/data]

  api:
    build:
      context: .
      dockerfile: Dockerfile
    environment:
      - EQUILINK_ROLE=api
      - ConnectionStrings__Postgres=Host=postgres;Port=5432;...
      - ConnectionStrings__Redis=redis:6379
    ports: ["8080:8080"]
    depends_on:
      postgres: { condition: service_healthy }
      redis: { condition: service_healthy }
```

### Environment Variables

| Variable | Required | Description |
|----------|----------|-------------|
| `EQUILINK_ROLE` | Yes | `api`, `consumer`, or `migrations` |
| `ConnectionStrings__Postgres` | Yes | Primary PostgreSQL connection |
| `ConnectionStrings__PostgresReadOnly` | No | Read replica (falls back to primary) |
| `ConnectionStrings__Redis` | Yes | Redis connection |
| `AzureBlob__ConnectionString` | No | Azure Blob for WORM archival |
| `ASPNETCORE_ENVIRONMENT` | No | `Development` or `Production` |

### Multi-Region Read Replicas

```mermaid
graph TD
    subgraph "Region A (Primary)"
        A1[API Instance] --> A2[PostgreSQL Primary]
        A1 --> A3[Redis Primary]
    end

    subgraph "Region B (Read Replica)"
        B1[API Instance] --> B2[PostgreSQL Replica]
        B1 --> B3[Redis Replica]
    end

    A2 -.->|Streaming Replication| B2
    A3 -.->|Redis Replication| B3

    style A2 fill:#c8e6c9
    style B2 fill:#e3f2fd
```

- **Writes**: EF Core `EquiLinkDbContext` → Primary only
- **Reads**: Dapper `OrderReadRepository`, `ComplianceAuditService`, `WormArchivalService` → Replica (via `IConnectionStringProvider`)
- **Fallback**: If `PostgresReadOnly` is not configured, reads fall back to primary with a warning log

---

## Development

### Local Setup

```bash
# Clone repository
git clone https://github.com/DynamicKarabo/equilink.git
cd equilink

# Start infrastructure
docker compose up -d postgres redis

# Apply migrations
dotnet ef database update \
  --project src/Infrastructure/Infrastructure.csproj \
  --startup-project src/Api/Api.csproj

# Run API
dotnet run --project src/Api/Api.csproj
```

### Build

```bash
dotnet build EquiLink.sln
```

### Run Tests

```bash
dotnet test EquiLink.sln
```

### Docker Build

```bash
docker build -t equilink:latest .
docker run -p 8080:8080 \
  -e EQUILINK_ROLE=api \
  -e ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=equilink;Username=postgres;Password=postgres" \
  -e ConnectionStrings__Redis="localhost:6379" \
  equilink:latest
```

---

## License

MIT License — see [LICENSE](LICENSE) for details.
