# ADR-002 — Backend stack and modular monolith

**Status:** Accepted

## Context

The product needs fast business iteration, strong transactional persistence, local deployment and eventual cloud integrations. V1A does not require independently deployable services.

## Decision

Use:
- Laravel as application/API framework;
- PostgreSQL as authoritative relational database;
- Redis for queue/cache/realtime support where needed;
- modular-monolith organization;
- framework-light domain layer where practical.

## Consequences

Positive:
- high development velocity;
- mature transaction/auth/queue ecosystem;
- same PostgreSQL technology local/cloud;
- easier deployment than microservices;
- clear path to extract services only if measured need appears.

Trade-offs:
- module boundaries require discipline because process/database are shared;
- Laravel upgrade lifecycle must be managed;
- realtime worker/process supervision is operational responsibility.

## Rejected alternatives for V1

- Go/Node solely because system is event-driven: no demonstrated need that outweighs team/product velocity.
- SQLite as restaurant authority: weaker fit for concurrent multi-terminal server workload and creates local/cloud DB divergence.
- microservices: unjustified operational complexity.
