# V1A Backlog

This file is the narrative backlog. GitHub Issues are the executable work queue.

## P0 — Make domain kernel complete and executable

- [ ] Split missing value objects/entities into explicit PHP files.
- [ ] Add shared ULID and DomainEvent primitives.
- [ ] Add domain unit tests for all non-negotiable invariants.
- [ ] Make composer test/autoload workflow reproducible.

## P0 — Application layer

- [ ] Define TableServiceRepository port.
- [ ] Define transaction boundary/use-case handlers.
- [ ] Define idempotency contract.
- [ ] Define outbox contract.
- [ ] Implement fire/start-ready/serve/consumption/payment commands.

## P0 — PostgreSQL persistence

- [ ] Laravel migrations for tenant/company/location/table/service/menu snapshots/guests/restrictions/courses/preparations/consumptions/payments/audit/outbox/idempotency.
- [ ] Database constraints/indexes.
- [ ] Repository implementation.
- [ ] Transaction + outbox atomicity tests.

## P0 — Local HTTP API

- [ ] Create/start/read service.
- [ ] Add guest/restriction.
- [ ] Assign menu.
- [ ] Fire course.
- [ ] Start/ready preparation.
- [ ] Validate/serve/skip course.
- [ ] Add/cancel consumption.
- [ ] Record payment/close service.
- [ ] KDS station projection.
- [ ] Global service board projection.
- [ ] Stable error codes + idempotency header.

## P1 — Authentication/authorization

- [ ] Local user login/session/token approach.
- [ ] Roles: administrator, maître, waiter, chef, kitchen.
- [ ] Server-side permission checks for course/service/financial actions.

## P1 — Realtime

- [ ] WebSocket/realtime server choice finalized.
- [ ] Publish committed service changes.
- [ ] Client reconnect/refetch behavior.
- [ ] LAN latency test.

## P1 — Operator UI

- [ ] Service board.
- [ ] Table detail contextual action.
- [ ] Guest restrictions.
- [ ] Consumption quick-add.
- [ ] Connection/local/cloud status.

## P1 — KDS UI

- [ ] Station queue cards.
- [ ] START/READY.
- [ ] allergy/adaptation visibility.
- [ ] incident entry.
- [ ] chef/pass validation view.

## P1 — Reliability

- [ ] Client pending command store.
- [ ] Idempotency retry UX.
- [ ] Outbox publisher monitoring.
- [ ] Internet outage test.
- [ ] client Wi-Fi flap test.

## P2 — V1A backoffice

- [ ] Configure dining areas/tables.
- [ ] Configure menu/courses/preparations.
- [ ] Configure kitchen stations.
- [ ] Configure users/roles.
- [ ] Audit viewer.

## Exit criteria V1A

- A Retiro-like full service can be operated with multiple simultaneous tables and kitchen stations.
- Internet exterior can fail without stopping live service.
- Critical allergy handling is visible and guest-specific.
- Course fire/ready/serve is idempotent and auditable.
- Staff can operate normal pacing without unnecessary navigation.
