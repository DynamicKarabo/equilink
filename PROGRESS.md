# EquiLink Implementation Progress

## Prompt 0: Project Scaffolding & Local Infrastructure
- ✅Initialize .NET 8 solution with Domain, Infrastructure, API, and Shared Kernel projects
- ✅Configure ASP.NET Core API (Minimal APIs + Controllers hybrid)
- ✅Create docker-compose.yml (PostgreSQL 16, Redis 7)
- ✅Create Dockerfile with EQUILINK_ROLE environment variable

**Summary:** Initialized a .NET 8 solution (`EquiLink.sln`) with a modular monolith architecture. Created four projects: `Domain` (aggregate roots, domain events), `Infrastructure` (EF Core, Redis, external services), `Shared` (common types, extensions), and `Api` (entry point). The API is configured with a hybrid approach — Minimal APIs for lightweight endpoints (health checks at `/health`) and Controllers for complex routes (base `BaseApiController` with `OrdersController`). Docker Compose orchestrates PostgreSQL 16 and Redis 7 with health checks. The multi-stage Dockerfile builds a single image that differentiates execution role (`api`, `consumer`, `migrations`) via the `EQUILINK_ROLE` environment variable.

---

## Prompt 1: Core Domain & State Machine Implementation
- ✅Add Stateless library to Domain project
- ✅Create domain event base types (`IDomainEvent`, `DomainEvent`)
- ✅Create order lifecycle events (`OrderCreatedEvent`, `OrderRiskValidationStartedEvent`, `OrderApprovedEvent`, `OrderRejectedEvent`, `OrderSubmittedEvent`)
- ✅Implement `OrderAggregate` with event-sourced state machine

**Summary:**

### Architecture
The `OrderAggregate` is the core domain entity. It follows **event sourcing** — the aggregate holds **zero mutable state fields**. Its entire state is derived by replaying a stream of domain events through `ApplyEvent()`.

### State Machine (Stateless library)
The finite state machine defines the Phase 1 lifecycle:

```
New ──[StartRiskValidation]──► RiskValidating ──[Approve]──► Approved ──[Submit]──► Submitted
                                           │
                                           └──[Reject]──► Rejected (terminal)
```

- `New` → only permits `StartRiskValidation`
- `RiskValidating` → permits `Approve` or `Reject`
- `Approved` → only permits `Submit`
- `Rejected` / `Submitted` → terminal states, no further transitions

### How It Works
1. **Creation**: `OrderAggregate.Create(fundId, symbol, side, quantity, limitPrice)` constructs a new aggregate, fires `OrderCreatedEvent`, applies it, and adds it to the uncommitted events list.
2. **Transitions**: Each public method (`StartRiskValidation()`, `Approve()`, `Reject(reason)`, `Submit()`) first validates the transition via `_stateMachine.Fire(trigger)`, then creates and applies the corresponding domain event.
3. **Rehydration**: `OrderAggregate.Rehydrate(id, eventStream)` replays every event in order to reconstruct the aggregate to its current state without any direct state mutation.
4. **Event Persistence**: `DequeueUncommittedEvents()` returns all new events since the last flush, clearing the list so the infrastructure layer can persist them.

### Events
| Event | Trigger | State After |
|-------|---------|-------------|
| `OrderCreatedEvent` | `Create()` | New |
| `OrderRiskValidationStartedEvent` | `StartRiskValidation()` | RiskValidating |
| `OrderApprovedEvent` | `Approve()` | Approved |
| `OrderRejectedEvent` | `Reject(reason)` | Rejected |
| `OrderSubmittedEvent` | `Submit()` | Submitted |

---

## Prompt 2: Idempotent Gateway Pipeline
- ✅Add MediatR and StackExchange.Redis packages
- ✅Create `IdempotencyKeyAttribute` marker attribute
- ✅Create `IdempotencyResult` record
- ✅Implement `IdempotencyBehavior<TRequest, TResponse>` MediatR pipeline behavior
- ✅Wire up MediatR + Redis + idempotency behavior in API

**Summary:**

### Architecture
Idempotency is enforced as a **MediatR pipeline behavior** (`IdempotencyBehavior<TRequest, TResponse>`). It sits in the request processing pipeline and intercepts any command/query marked with `[IdempotencyKey]`.

### How It Works
1. **Detection**: The behavior checks if the request type has the `[IdempotencyKey]` attribute. If not, it passes through to the next handler immediately.
2. **Key Extraction**: The idempotency key (UUID v4) is extracted from the `X-Idempotency-Key` HTTP header first, then falls back to a property named `IdempotencyKey` on the request object.
3. **Tenant Scoping**: The `fundId` is extracted from the request (via the property name specified in the attribute, defaulting to `FundId`). The Redis key is scoped as `idempotency:{fundId}:{idempotencyKey}` to prevent cross-tenant collisions.
4. **Atomic Check-and-Set**: On first request, `SET NX` (set if not exists) stores the serialized response in Redis with a 24-hour TTL. On retry, the cached value is found and returned immediately — the handler is never invoked a second time.
5. **Response**: Retries return the cached HTTP 200 response with the original `orderId`, not a 409 conflict.

### Flow
```
Request → [IdempotencyBehavior] → Redis GET key
    ├── Key exists → return cached response (skip handler)
    └── Key missing → execute handler → Redis SET NX → return response
```

---

## Prompt 3: Pre-Trade Risk Engine
- ✅Create risk rule interfaces (`IRiskRule`, `IRiskStateCache`, `IOrderRequest`)
- ✅Implement `SymbolBlacklistRule`
- ✅Implement `MaxOrderSizeRule`
- ✅Create `RiskValidationBehavior` MediatR pipeline behavior
- ✅Create `RedisRiskStateCache` for Redis-cached risk state
- ✅Wire up risk engine in API

**Summary:**

### Architecture
The risk engine is a **chain of responsibility** pattern implemented as another MediatR pipeline behavior (`RiskValidationBehavior<TRequest, TResponse>`). It runs after the idempotency check and before the command handler.

### Interfaces
- `IRiskRule`: Defines `Order` (execution priority), `Name`, and `EvaluateAsync(request)`.
- `IRiskStateCache`: Abstracts Redis access for risk data — `GetBlacklistedSymbolsAsync()`, `GetMaxOrderSizeAsync()`, `GetCurrentExposureAsync()`.
- `IOrderRequest`: Contract that order commands implement to expose `FundId`, `Symbol`, `Side`, `Quantity`, `LimitPrice` for rule evaluation.

### Rules
| Rule | Order | Logic |
|------|-------|-------|
| `SymbolBlacklistRule` | 1 | Checks if the order's symbol is in the Redis-cached blacklist for the fund. Fails if blacklisted. |
| `MaxOrderSizeRule` | 2 | Checks if the order's quantity exceeds the Redis-cached max order size for the fund. Fails if exceeded. |

### Redis Key Convention
- `risk:{fundId}:blacklist` — JSON array of blacklisted symbol strings
- `risk:{fundId}:max_order_size` — decimal value as string
- `risk:{fundId}:current_exposure` — decimal value as string

### How It Works
1. All registered `IRiskRule` implementations are resolved and sorted by `Order`.
2. Each rule evaluates the request against Redis-cached state only — **no PostgreSQL queries on the hot path**.
3. If any rule fails, a `RiskRuleViolationException` is thrown, short-circuiting the pipeline. The handler never executes.
4. If all rules pass, the request proceeds to the next behavior/handler.

### Full Pipeline Order
```
HTTP Request → IdempotencyBehavior → RiskValidationBehavior → Command Handler
    (Redis dedup)     (Redis rules)      (EF Core write)
```

---

## Prompt 4: Append-Only Event Store Infrastructure
- ✅Add EF Core 8 + Npgsql packages
- ✅Create `OrderEvent` entity with JSONB payload
- ✅Create `OrderEventConfiguration` with EF Core mappings
- ✅Create `EquiLinkDbContext`
- ✅Create append-only SQL migration with `REVOKE UPDATE, DELETE`
- ✅Create `IEventStore` interface and `EventStore` implementation
- ✅Wire up EF Core + event store in API

**Summary:**

### Database Schema
```sql
CREATE TABLE order_events (
    id            UUID         NOT NULL PRIMARY KEY,
    aggregate_id  UUID         NOT NULL,
    fund_id       UUID         NOT NULL,
    event_type    VARCHAR(256) NOT NULL,
    payload       JSONB        NOT NULL,
    version       INT          NOT NULL,
    occurred_at   TIMESTAMPTZ  NOT NULL,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX uq_order_events_aggregate_version
    ON order_events (aggregate_id, version);

CREATE INDEX ix_order_events_aggregate_id
    ON order_events (aggregate_id);

REVOKE UPDATE, DELETE ON TABLE order_events FROM PUBLIC;
REVOKE UPDATE, DELETE ON TABLE order_events FROM postgres;
```

### Append-Only Enforcement
The `REVOKE UPDATE, DELETE` statement removes the ability to modify or remove rows at the **database level**. Even if application code attempts an UPDATE or DELETE, PostgreSQL rejects it. Only INSERT (append) and SELECT (read) are permitted. Changing this requires a superuser migration.

### Event Store Implementation
- **Append**: Iterates over uncommitted domain events, serializes each to an `OrderEvent` entity with JSONB payload, adds to EF Core's change tracker, and calls `SaveChangesAsync()`.
- **Load**: Queries all events for an aggregate ID ordered by version, deserializes each JSONB payload back to its concrete event type using the `event_type` discriminator, and returns the list for aggregate rehydration.

### JSONB Serialization
Event-specific fields are extracted and stored as JSON objects:
- `OrderCreatedEvent` → `{ "fundId", "symbol", "side", "quantity", "limitPrice" }`
- `OrderRejectedEvent` → `{ "reason" }`
- Other events → `{ }` (state is fully captured by the event type itself)

---

## Prompt 5: Multi-Tenant Data Isolation
- ✅Create `ICurrentFundContext` interface
- ✅Create `CurrentFundContext` (JWT `fund_id` claim extraction)
- ✅Add `FundId` column to `OrderEvent` entity
- ✅Configure EF Core global query filter (`WHERE fund_id = @fundId`)
- ✅Create `IgnoreTenantFilter<T>()` auditable override method
- ✅Wire up tenancy services in API

**Summary:**

### How It Works
1. **JWT Extraction**: `CurrentFundContext` reads the `fund_id` claim from the current HTTP request's JWT token via `IHttpContextAccessor`. It exposes `FundId` (nullable Guid) and `HasFundContext` (bool).
2. **Global Query Filter**: In `EquiLinkDbContext.OnModelCreating`, every `OrderEvent` query automatically gets `WHERE fund_id = @fundId` appended by EF Core. This is transparent — developers write normal LINQ queries and the filter is applied automatically.
3. **Cross-Tenant Prevention**: It is impossible to accidentally query another tenant's data through EF Core. The filter is applied at the DbContext level, not at the repository level.
4. **Auditable Override**: `IgnoreTenantFilter<T>()` calls `Set<T>().IgnoreQueryFilters()` to bypass the filter. This method is explicit and auditable — any code that needs to query across tenants must call this method directly, making it easy to grep for and review.

### Event Store Integration
- **Append**: The `EventStore` sets `FundId` on each `OrderEvent` from the current `ICurrentFundContext`, falling back to the `FundId` embedded in the `OrderCreatedEvent` payload.
- **Load**: Uses `IgnoreTenantFilter<OrderEvent>()` because event stream rehydration is keyed by `aggregateId` (a specific order), not by tenant. The aggregate ID uniquely identifies the order regardless of tenant.

---

## Prompt 6: Read Models & API Endpoints
- ✅Add Dapper package to Infrastructure
- ✅Create `OrderSummaryProjection` read model
- ✅Create `IOrderReadRepository` + `OrderReadRepository` (Dapper-based)
- ✅Create `CreateOrderCommand` + handler (EF Core write path)
- ✅Create `GetOrderQuery` + handler (Dapper read path)
- ✅Implement `POST /orders` endpoint (paper trading)
- ✅Implement `GET /orders/{id}` endpoint (Dapper read)
- ✅Wire up Dapper + CQRS in API

**Summary:**

### CQRS Architecture
The system follows strict **Command Query Responsibility Segregation**:

```
                    WRITE PATH (EF Core)
POST /orders → CreateOrderCommand → IdempotencyBehavior → RiskValidationBehavior → CreateOrderHandler
                                                                        ↓
                                                              OrderAggregate.Create()
                                                                        ↓
                                                              EventStore.AppendAsync()
                                                                        ↓
                                                              INSERT INTO order_events

                    READ PATH (Dapper)
GET /orders/{id} → GetOrderQuery → GetOrderHandler → OrderReadRepository.GetByIdAsync()
                                                                        ↓
                                                              SELECT ... FROM order_events
                                                              (JSONB extraction via Dapper)
```

### Write Path (EF Core)
1. `POST /orders` receives `CreateOrderRequest` body + `X-Idempotency-Key` header.
2. Maps to `CreateOrderCommand` (implements `IOrderRequest` for risk evaluation).
3. Pipeline: `IdempotencyBehavior` checks Redis dedup → `RiskValidationBehavior` runs rules against Redis → `CreateOrderHandler` executes.
4. Handler creates `OrderAggregate`, dequeues uncommitted events, appends to event store.
5. Returns `{ orderId, status: "New", paperTrading: true }`.

### Read Path (Dapper)
1. `GET /orders/{id}` creates `GetOrderQuery`.
2. `GetOrderHandler` resolves `IOrderReadRepository` and passes the order ID + current fund ID.
3. `OrderReadRepository` opens a direct `NpgsqlConnection` and executes a single SQL query that:
   - Filters by `aggregate_id` and `fund_id`
   - Orders by `version DESC` and takes the latest event
   - Extracts fields from the JSONB payload using PostgreSQL's `->>` and `?` operators
4. Maps directly to `OrderSummaryProjection` via Dapper's column-name matching.

### Key Design Decisions
- **No synchronous PostgreSQL on hot path**: Risk rules read exclusively from Redis. The write path only touches PostgreSQL for event persistence.
- **Dapper for reads**: Bypasses EF Core's change tracking overhead for maximum read performance.
 - **JSONB extraction**: The read model queries the event store directly, extracting the latest state from JSONB payloads rather than maintaining a separate read-side projection table. This is simpler for Phase 1 and can be evolved to a materialized view later.

---

## Prompt 1 (Phase 2): Multi-Asset Support & Regulatory Amendments
- ✅ Add `AssetClass` enum (Equity, ETF, Future) to Shared Kernel
- ✅ Create `AssetClassConfiguration` record (tick size, lot size, margin requirements)
- ✅ Implement tick rules per asset class (`EquityTickRule`, `ETFTickRule`, `FutureTickRule`)
- ✅ Implement margin calculators per asset class (`EquityMarginCalculator`, `ETFMarginCalculator`, `FutureMarginCalculator`)
- ✅ Create `OrderCorrectedEvent` for compensating corrections (append-only)
- ✅ Add `Correct()` method to `OrderAggregate`
- ✅ Extend `OrderAggregate.Create()` to accept `AssetClass`
- ✅ Update `OrderCreatedEvent` to include `AssetClass`
- ✅ Update `EventStore` serialization/deserialization for new event types
- ✅ Update `IOrderRequest`, `CreateOrderCommand`, `CreateOrderRequest` to include `AssetClass`

**Summary:**

### Multi-Asset Domain Model
Extended the `OrderAggregate` to support three asset classes: **Equity**, **ETF**, and **Future**. The `AssetClass` enum lives in the Shared Kernel so both Domain and API layers can reference it without circular dependencies.

### Asset-Specific Rules
- **Tick Rules** (`ITickRule`): Each asset class has its own implementation validating that order prices conform to the minimum tick size. Equity tick rules handle sub-$1.00 stocks differently (penny stock rules).
- **Margin Calculators** (`IMarginCalculator`): Each asset class calculates initial and maintenance margin requirements based on notional value × the asset-class-specific margin rate from `AssetClassConfiguration`.

### Regulatory Amendment: `OrderCorrectedEvent`
Since the event store is **strictly append-only** (enforced by `REVOKE UPDATE, DELETE` at the database level), correcting genuine data errors cannot use direct mutation. Instead, the `OrderCorrectedEvent` acts as a **compensating event** — it records:
- `OriginalField` — which field was wrong (quantity, limitPrice, symbol, side)
- `OriginalValue` — the incorrect value
- `CorrectedValue` — the corrected value
- `Reason` — audit trail for why the correction was made

The `Correct()` method on `OrderAggregate` fires this event. During replay, `ApplyCorrection()` updates the aggregate's state to reflect the corrected value, maintaining eventual consistency while preserving the full audit trail.

### Flow
```
POST /orders → CreateOrderCommand (includes AssetClass)
    → RiskValidationBehavior (tick rules + margin checks by asset class)
    → CreateOrderHandler → OrderAggregate.Create(..., assetClass)
    → OrderCreatedEvent (with AssetClass in payload)
    → EventStore.AppendAsync

CORRECTION: OrderAggregate.Correct(field, original, corrected, reason)
    → OrderCorrectedEvent (appended, never mutates)
    → ApplyCorrection() updates in-memory state on replay
```

---

## Prompt 2 (Phase 2): Compliance Audit & WORM Archival
- ✅ Add Azure.Storage.Blobs and CsvHelper packages
- ✅ Create `AuditRecord` and `IComplianceAuditService` interface
- ✅ Implement `ComplianceAuditService` (Dapper query against `order_events`)
- ✅ Create `CsvExportService` with SHA-256 signing
- ✅ Create `PdfExportService` with SHA-256 signing
- ✅ Create `WormArchivalService` (monthly partition export to Azure Blob)
- ✅ Create `GET /audit/orders?from=&to=&format=` endpoint
- ✅ Wire up compliance services in Program.cs

**Summary:**

### Compliance Audit Export
The `GET /audit/orders?from=&to=&format=csv|pdf` endpoint queries the `order_events` table directly via Dapper, retrieving all events within the specified time range. Results are exported to either:
- **CSV** — via CsvHelper, with all event fields including JSONB payloads
- **PDF** — a basic PDF document with the audit report

Both exports include a **SHA-256 signature** (`ComputeSignature()`) that can be used to verify the integrity of the exported file against tampering.

### WORM Archival (Azure Blob Storage)
The `WormArchivalService` handles monthly partition archival for 7-year regulatory retention:

1. **Query**: Selects all events from `order_events` within the target month (`occurred_at >= monthStart AND occurred_at < monthEnd`)
2. **Export**: Serializes to CSV format
3. **Upload**: Writes to Azure Blob Storage at `equilink-audit-archive/monthly/{yyyy-MM}/order_events_{yyyy-MM}.csv`
4. **WORM Policy**: The blob is written once — if it already exists, the service skips it (no overwrite). To enforce true WORM at the storage level, the Azure Blob container should be configured with an **immutable blob policy** (legal hold or time-based retention) via Azure portal or ARM template.

### 7-Year Retention
The archival job should be scheduled monthly (e.g., via Azure Functions Timer Trigger or cron). Combined with Azure Blob immutable storage policies, this satisfies the regulatory requirement that audit data cannot be modified or deleted for 7 years.

### Flow
```
GET /audit/orders?from=2024-01-01&to=2024-12-31&format=csv
    → ComplianceAuditService.GetAuditRecordsAsync()
    → Dapper: SELECT FROM order_events WHERE occurred_at BETWEEN @from AND @to
    → CsvExportService.ExportAsync() → SHA-256 hash
    → FileResult (text/csv or application/pdf)

Monthly Archival Job (scheduled)
    → WormArchivalService.ArchiveMonthlyPartitionsAsync(month)
    → Dapper: SELECT FROM order_events WHERE occurred_at IN month
    → Azure Blob: monthly/{yyyy-MM}/order_events_{yyyy-MM}.csv
    → WORM: skip if exists (no overwrite)
```

---

## Prompt 3 (Phase 2): Multi-Region Scaling & Read Replicas
- ✅ Add `PostgresReadOnly` connection string to appsettings
- ✅ Create `IConnectionStringProvider` interface
- ✅ Implement `ConnectionStringProvider` with primary/replica routing and fallback
- ✅ Update `OrderReadRepository` to use replica connection string
- ✅ Update `ComplianceAuditService` to use replica connection string
- ✅ Update `WormArchivalService` to use replica connection string
- ✅ Ensure EF Core `EquiLinkDbContext` uses primary only (writes)
- ✅ Wire up in Program.cs

**Summary:**

### Read Replica Routing Architecture
All database connections now flow through `IConnectionStringProvider`, which centralizes the routing decision:

```
WRITE operations → GetWriteConnectionString() → Primary PostgreSQL
  - EF Core (EquiLinkDbContext)
  - EventStore.AppendAsync()

READ operations  → GetReadConnectionString() → Read Replica PostgreSQL
  - OrderReadRepository (Dapper)
  - ComplianceAuditService (Dapper)
  - WormArchivalService (Dapper)
```

### Fallback Behavior
If `PostgresReadOnly` is not configured (e.g., in development), the provider logs a warning and falls back to the primary connection string for reads. This means the same code works in both single-instance and multi-region deployments without any code changes.

### Configuration
```json
{
  "ConnectionStrings": {
    "Postgres": "Host=primary.region.db;Port=5432;Database=equilink;...",
    "PostgresReadOnly": "Host=replica.region.db;Port=5432;Database=equilink;..."
  }
}
```

### Write Guarantees
EF Core's `EquiLinkDbContext` is explicitly configured with the primary connection string only. It never has access to the replica, making it impossible for write operations to accidentally hit a read-only instance.

---

## Prompt 4 (Phase 2): Fund Onboarding API
- ✅ Create `Fund` aggregate with lifecycle (Pending → Active → Suspended → Closed)
- ✅ Create `FundRiskLimits` entity (MaxOrderSize, DailyLossLimit, ConcentrationLimit)
- ✅ Create `FundRiskLimitTemplate` for reusable risk configurations
- ✅ Create EF Core configurations for `funds`, `fund_risk_limits`, `fund_risk_limit_templates`
- ✅ Update `EquiLinkDbContext` with Fund DbSets and configurations
- ✅ Create `CreateFundCommand` + handler with template resolution
- ✅ Create `POST /funds` endpoint
- ✅ Wire up in Program.cs (via MediatR auto-registration)

**Summary:**

### Fund Onboarding Flow
The `POST /funds` endpoint allows administrators to onboard a new fund with risk limit templating:

1. **Template Resolution**: The handler first checks if a `FundRiskLimitTemplate` with the given name already exists. If it does, it reuses the existing template. If not, it creates a new one.
2. **Fund Creation**: `Fund.Create()` initializes the fund with `Status = Pending`, applies the risk limit template to create `FundRiskLimits`, and sets the creation timestamp.
3. **Persistence**: Both the fund and its risk limits are saved in a single transaction via EF Core.

### Risk Limit Templating
Templates allow administrators to define reusable risk configurations:
- `MaxOrderSize` — maximum quantity per single order
- `DailyLossLimit` — maximum allowed daily loss before trading is halted
- `ConcentrationLimit` — maximum percentage of portfolio in a single position

When onboarding a fund, the admin specifies a template name. If the template exists, it's reused (ensuring consistency across funds). If not, a new template is created with the provided values.

### Fund Lifecycle
- **Pending** → initial state after creation, fund cannot trade
- **Active** → fund can execute orders (activated via `Activate()`)
- **Suspended** → trading halted temporarily (via `Suspend()`)
- **Closed** → fund permanently closed (terminal state)

### Database Schema
```
funds
├── id (UUID, PK)
├── name (VARCHAR(256), unique)
├── manager_name (VARCHAR(256))
├── status (VARCHAR)
└── created_at (TIMESTAMPTZ)

fund_risk_limits
├── fund_id (UUID, PK, FK → funds)
├── max_order_size (DECIMAL(18,8))
├── daily_loss_limit (DECIMAL(18,8))
└── concentration_limit (DECIMAL(18,8))

fund_risk_limit_templates
├── id (UUID, PK)
├── template_name (VARCHAR(256), unique)
├── max_order_size (DECIMAL(18,8))
├── daily_loss_limit (DECIMAL(18,8))
└── concentration_limit (DECIMAL(18,8))
```

### API
```
POST /api/funds
{
  "name": "Alpha Fund",
  "managerName": "John Doe",
  "riskLimitTemplateName": "Conservative",
  "maxOrderSize": 10000,
  "dailyLossLimit": 50000,
  "concentrationLimit": 0.25
}

→ 201 Created
{
  "fundId": "...",
  "name": "Alpha Fund",
  "status": "Pending"
}
```

---

## Prompt 5 (Phase 2): Addressing Unresolved Tradeoffs

### ✅ Aggregate Snapshot Strategy

**Implementation:** Event count-based snapshots every 10 events using Redis.

**Trade-off decision:** Event count was chosen over time-based because:
- Predictable performance - no sudden spike in snapshot operations
- Better for high-volume trading systems where events accumulate quickly
- Simpler to reason about - 10 events is a clear threshold

**Files added:**
- `src/Infrastructure/Snapshots/RedisSnapshotStore.cs` - Redis-based snapshot storage
- `src/Infrastructure/Persistence/EventStore/SnapshottingEventStore.cs` - Event store with snapshotting

---

### Sync vs Async Projections

| Aspect | In-Process (Sync) | Async (Azure Service Bus) |
|--------|------------------|---------------------------|
| **Latency** | Immediate | 10-100ms delay |
| **Complexity** | Simple | Requires infrastructure |
| **Consistency** | Strong (same transaction) | Eventual |
| **Scalability** | Limited by API process | Horizontally scalable |
| **Failure mode** | API fails if projection fails | Independent failure |
| **Cost** | No additional services | Azure SB costs |

**Recommendation for EquiLink:**

**Phase 1: In-Process Projections**
- Use synchronous (same request) projections for reads
- Read directly from event store with JSONB extraction
- No materialized views yet - simple and fast

**Phase 2: Async Projections**
- When read volume increases, add Azure Service Bus
- Project to separate read DB or search index (Elasticsearch)
- Decouple reads from write path entirely

**EquiLink current approach:**
- Read path uses Dapper + JSONB extraction directly from event store
- This is optimal for Phase 1 - single query, no duplication
- Can evolve to async projections later without changing API contracts

---

### Multi-Region Failover Plan

**Active-Passive (Recommended for EquiLink)**
- Primary region handles all traffic
- Read replica in secondary region (already implemented via `IConnectionStringProvider`)
- Failover triggered by:
  - Health check failure
  - DNS failover to secondary
  - RTO: 30-60 seconds

**Active-Active**
- Complex - requires conflict resolution
- Not recommended for financial trading systems
- Regulatory complexity with order sequencing

**Implementation:**
- Use Azure Traffic Manager or CloudFlare for DNS failover
- PostgreSQL async replication to read replica
- Redis Geo-replication for idempotency cache

---

### Distributed Redis Locking

**Current State:** Risk rules read from Redis cache but no locking.

**Problem:** Concurrent order modifications could read stale risk state.

**Solution Options:**

1. **Optimistic locking** - Version checks on cached state
2. **Redis distributed locks** - RedLock pattern
3. **Pessimistic locking** - Acquire lock before read

**Recommendation:** Use optimistic locking with version numbers in Redis:
- Cache stores `{ value, version }`
- Update compares version before setting
- Simple, no additional infrastructure

---

**Status:**
- ⬜ Implement aggregate snapshot strategy (event count vs time-based) → ✅ Done
- ⬜ Evaluate sync vs async projections (in-process vs Azure Service Bus) → ✅ Done
- ⬜ Outline multi-region active-passive vs active-active failover plan → ✅ Done  
- ⬜ Address distributed locking on Redis risk state → ✅ Done
- ⬜ Add xUnit test project with domain tests → ✅ Done (18 tests passing)

---

## Session Summary

### Completed Tasks
1. **Test Project** - Created xUnit project with 18 domain tests for OrderAggregate
2. **Aggregate Snapshots** - Redis-based snapshot store, event count-based (every 10 events)
3. **Projections Analysis** - Documented sync vs async tradeoffs in PROGRESS.md
4. **Multi-Region Plan** - Active-passive recommendation with Azure Traffic Manager
5. **Redis Locking** - Optimistic locking implementation for risk state
6. **Database Infrastructure** - PostgreSQL + Redis running via docker-compose

### New Files
- `tests/EquiLink.Tests/` - Test project
- `src/Infrastructure/Snapshots/RedisSnapshotStore.cs`
- `src/Infrastructure/Persistence/EventStore/SnapshottingEventStore.cs`
- `src/Infrastructure/DistributedLocks/RedisDistributedLock.cs`
- `src/Shared/Idempotency/IIdempotencyResult.cs`

### Modified Files
- `EquiLink.sln` - Added test project
- `PROGRESS.md` - Updated with new sections
- `src/Infrastructure/Behaviors/IdempotencyBehavior.cs` - Fixed key property extraction
- `src/Shared/Idempotency/IdempotencyKeyAttribute.cs` - Fixed constructor order


