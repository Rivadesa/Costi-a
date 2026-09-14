# ADR-004 — Transactional Outbox and idempotent commands

**Status:** Accepted

## Context

The system must tolerate temporary client Wi-Fi loss, realtime failures and later cloud/integration delivery without duplicating business actions or losing events.

## Decision

- Persist business state and outbox event in one PostgreSQL transaction.
- Publish asynchronously after commit.
- Retryable commands use stable idempotency keys.
- Realtime is a notification/projection mechanism, not the source of truth.
- Do not implement full event sourcing in V1.

## Consequences

Positive:
- avoids DB-committed/event-lost gap;
- safe command retry after uncertain network response;
- cloud/integration consumers can be idempotent on event id;
- current state remains easy to query relationally.

Trade-offs:
- requires outbox worker/cleanup/monitoring;
- idempotency retention and payload fingerprint rules must be defined;
- consumers must handle at-least-once delivery semantics.

## Rejected alternatives

- publish directly to WebSocket/queue before/after DB without atomic intent;
- full event sourcing as primary persistence;
- CRDT-based merge for V1 live service.
