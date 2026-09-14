# Decision Log

Architecture decisions are recorded as ADRs in `docs/adr/`.

| ADR | Status | Decision |
|---|---|---|
| ADR-001 | Accepted | Local-primary runtime; cloud is replica/services, not equal live master |
| ADR-002 | Accepted | Laravel + PostgreSQL + Redis modular-monolith backend |
| ADR-003 | Accepted | Vue 3 + Tauri desktop; PWA where appropriate for waiter/KDS |
| ADR-004 | Accepted | Transactional Outbox + idempotent commands; no full event sourcing |
| ADR-005 | Accepted | Service/account/payment/fiscal document remain separate bounded concepts |
| ADR-006 | Accepted | Menu templates snapshot into per-service execution; kitchen preparations have independent station state |

## ADR lifecycle

Statuses:
- Proposed
- Accepted
- Deprecated
- Superseded by ADR-XXX

Never edit history to make an old decision look as if it never existed. When direction changes, add a new ADR that supersedes the previous one and update this index.

## Decisions intentionally deferred

The following are not yet closed and should not be guessed without an issue/ADR:
- exact production Laravel minor/version at first deploy;
- realtime server/provider implementation;
- local node identity/credential mechanism for cloud sync;
- cloud provider;
- payment terminal integration;
- production packaging/update mechanism for Tauri clients;
- full V1B fiscal implementation details;
- V1C inventory valuation method;
- reservation provider strategy.
