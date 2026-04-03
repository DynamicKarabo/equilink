-- Migration: 20240101000000_CreateOrderEventsTable
-- Creates the append-only event store table with database-level write protection

CREATE TABLE IF NOT EXISTS order_events (
    id            UUID         NOT NULL PRIMARY KEY,
    aggregate_id  UUID         NOT NULL,
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

-- Enforce append-only contract at the database level
-- REVOKE UPDATE and DELETE privileges on the order_events table
-- so that no application user can modify or remove events.

REVOKE UPDATE, DELETE ON TABLE order_events FROM PUBLIC;
REVOKE UPDATE, DELETE ON TABLE order_events FROM postgres;

-- Only INSERT is allowed. SELECT is implicitly allowed for reads.
-- To modify this restriction, a superuser migration is required.
