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
