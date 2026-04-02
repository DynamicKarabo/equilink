Overview
EquiLink is a production-grade C#/.NET 8 middleware layer that sits between alpha-generating trading strategies and live financial exchanges. It exists to solve a critical gap in modern algorithmic trading systems: the strategy layer and the exchange layer are typically bolted together with fragile, bespoke glue code. EquiLink replaces this with a hardened, observable, and auditable gateway that treats every order as a first-class domain object with a full, trackable lifecycle. Its core design principle prioritizes correctness over throughput, ensuring that a missed order is always preferable to a double-filled order.
Business Rules

    Idempotency Guarantees: Order idempotency keys must be caller-generated (e.g., UUID v4 by the bot) to ensure stability across network retries. Deduplication must be enforced atomically via Redis SET NX with a 24-hour TTL, and cached responses must be returned for retries to prevent duplicate orders.
    State Transition Rules: Order lifecycles must be governed by an explicit finite state machine (using the Stateless library). No code may mutate an order's state directly; instead, valid triggers must fire domain events that update the state. Illegal state transitions must be impossible to compile.
    Tenant Isolation Rules: EquiLink is multi-tenant strictly at the Fund level. Isolation must be enforced automatically via Entity Framework Core global query filters on the fund_id claim. The system must validate the fund_id server-side on every request to prevent clients from self-elevating, and cross-tenant data access is strictly prohibited without explicit overrides.
    Risk Engine Sequencing: Pre-trade risk validation must execute as a chain of responsibility on the MediatR pipeline, blocking any invalid orders before they reach the wire. The risk engine must evaluate limits (e.g., MaxPositionSize, DailyLoss) exclusively against Redis-cached state, never reading synchronously from PostgreSQL on the hot execution path.
    Append-Only Ledger Contract: The OrderAggregate is the consistency boundary and must hold no mutable state fields; its state is entirely derived by replaying an event stream. The underlying event store (order_events) must be strictly append-only. UPDATE and DELETE operations are prohibited by contract and must be physically blocked via database-level REVOKE privileges. Correcting errors requires appending a compensating event.

Out of Scope
EquiLink is strictly a backend trading gateway. It explicitly does NOT provide:

    Alpha strategy development or trade generation.
    Portfolio management, tax lot accounting, or settlement / T+2 processing.
    Exchange matching engine functionality.
    Financial accounting or regulatory reporting systems (e.g., MIFID II / ASIC), though it provides the underlying audit data for these systems.
    Client-facing order management portals or mobile applications (it only provides a monitoring dashboard).
    A native FIX protocol engine (it delegates to the QuickFIX/n library as an adapter).

Owner
Karabo Oliphant