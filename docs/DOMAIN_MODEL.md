# Domain Model — V1A

## 1. Purpose

This document defines the shared vocabulary, aggregates, state machines and invariants for the fine-dining service domain. It is a contract for backend, frontend, tests and future integrations.

## 2. Core aggregate: TableService

`TableService` represents one concrete dining service instance for one table/party.

It is **not** the physical table itself and **not** the fiscal document.

Typical fields/concepts:
- service id (global identifier);
- tenant/company/location context;
- table reference/snapshot;
- opened/started/closed timestamps;
- guest count / guest positions;
- selected menu snapshot;
- course execution instances;
- consumptions/extras;
- operational payment state;
- service notes / pause state;
- audit-relevant changes.

A physical table may host many TableService instances over time.

## 3. Guest

V1A does not require all diners to be identified customers.

A guest can be represented by position:
- PAX 1;
- PAX 2;
- ...

Optional customer identity may be attached later.

### Guest restrictions

Restrictions are structured and attached to a guest:
- `ALLERGY`;
- `INTOLERANCE`;
- `PREFERENCE`.

Severity should at least distinguish critical operational visibility where applicable.

Free text may supplement but must not replace structured restriction type.

## 4. MenuTemplate

A menu template is reusable configuration:
- name;
- base price metadata;
- ordered courses;
- default preparations;
- station routing;
- possible service tasks.

Changing a MenuTemplate must not retroactively modify an active/historic service.

## 5. Menu snapshot / ServiceMenu

When a menu is assigned to a service, the relevant definition is snapshotted/instantiated into the service context.

Why:
- chef may change next month's menu;
- historic service must remain reproducible;
- one table may receive an exception without changing master menu.

## 6. CourseTemplate vs ServiceCourse

`CourseTemplate` describes the configured course in a menu.

`ServiceCourse` is the execution instance for one table service.

ServiceCourse stores execution-specific data:
- ordinal/order;
- status;
- fired/start/ready/served timestamps;
- substitutions;
- skip reason;
- extra-course marker;
- preparation instances.

## 7. Course state machine

Normal path:

```text
PENDING
  → FIRED
  → PREPARING
  → READY
  → SERVED
```

`EATING` is a UI-derived state/timer after SERVED until next course is fired; it should not require an extra write/button unless later evidence shows need.

Exceptional terminal/alternate states:
- `SKIPPED`;
- `CANCELLED`.

Transitions must be validated server-side.

## 8. Preparation

A course can contain multiple `ServiceCoursePreparation` instances, possibly routed to different kitchen stations.

A preparation may apply to:
- whole table;
- quantity N;
- one specific guest position.

Example:

```text
Course 5 — Fish
- 2 x standard fish → Fish station
- 1 x adapted fish for PAX 2 → Fish station
- garnish → Pass/Hot station
```

### Preparation state

Minimum useful lifecycle:

```text
PENDING → STARTED → READY
```

Additional incident/rework states may be added with explicit semantics.

## 9. Course ready invariant

A ServiceCourse may be considered ready only when all **required** preparations are ready.

A single kitchen station marking its work READY must never implicitly mark the whole course READY when another required preparation is pending.

Chef/pass validation can be configured as:
- automatic when all required preparations ready;
- explicit manual validation.

Retiro initial preference: support explicit chef/pass validation.

## 10. Fire course invariant

Normal course firing is a deliberate service action.

V1A default: manual firing by an authorized role.

Future assisted/automatic pacing may suggest or trigger according to configuration, but must not be baked into V1A domain assumptions.

## 11. Service exceptions

### Skip course

- requires authorization;
- reason required/recommended according to policy;
- changes only this ServiceCourse;
- master menu remains unchanged.

### Add extra course

Creates a service-specific course not present in original menu snapshot/template.

### Substitute preparation

Original relation must remain auditable. A substitution should preserve what was originally expected and what replaced it.

### Pause service

Pausing affects pacing/UI, not ability to record unrelated consumptions.

## 12. Consumption

A `Consumption` represents drinks/extras added outside the fixed menu execution.

Examples:
- wine bottle;
- water;
- coffee;
- extra service/product.

Consumption is separate from courses even though both contribute to the provisional account.

Cancellation of consumption must be non-destructive:
- mark cancelled/voided;
- retain original amount/item;
- actor/time/reason in audit.

## 13. Account / provisional settlement

The account is an operational calculation of current service charges.

It may include:
- menu amount × guests;
- consumptions;
- extras;
- future voucher/prepayment deductions;
- future discounts/invitations.

In V1A it is **not a fiscal invoice**.

## 14. Payment

Operational payment in V1A is intentionally limited.

Conceptually payment is independent from fiscal document generation.

Future payment model may include split/mixed methods, deposits, vouchers and gateway references. Do not force those concepts into current simple UI unless needed.

## 15. AuditLog

Critical changes require append-oriented audit information:
- actor;
- timestamp;
- action;
- entity id/type;
- relevant before/after values;
- reason when required.

Audit is distinct from domain event/outbox delivery.

## 16. Domain events / outbox events

Example event names:
- `service.created`;
- `service.started`;
- `service.paused`;
- `service.resumed`;
- `service.guest_restriction_added`;
- `menu.assigned`;
- `course.fired`;
- `preparation.started`;
- `preparation.ready`;
- `course.ready`;
- `course.served`;
- `course.skipped`;
- `course.modified`;
- `consumption.added`;
- `consumption.cancelled`;
- `payment.recorded`;
- `service.closed`;
- `service.cancelled`.

Events must include stable IDs and enough context for consumers to locate current state. They are not necessarily the sole reconstruction source.

## 17. Multi-tenant/multi-company context

Even if V1A uses one active restaurant configuration, persistent business records should have explicit tenant/company/location ownership as appropriate.

Future V1C requires:
- Retiro legal entity;
- AÑITSOC legal entity;
- shared group/tenant catalogue;
- independent fiscal identities;
- shared physical inventory locations with separate legal ownership.

Do not conflate tenant, legal entity and physical location.

## 18. Future wine/inventory concepts (not V1A implementation)

Important reserved concepts:
- `Product` master;
- `Wine` specialization;
- `Vintage` as first-class entity/version;
- `Packaging` (bottle/case/pallet);
- `StockLocation`;
- `StockOwnerCompany`;
- availability/commitment/reservation by channel.

Bottle remains the natural closed-stock unit. For opened wine, a future `OpenContainer` may track remaining ml/cl separately.

## 19. Non-negotiable invariants

1. Active service changes never mutate menu master configuration.
2. A course cannot become READY while a required preparation is not ready.
3. A critical guest restriction must be propagated to affected preparation views.
4. Cancelled consumptions are not physically erased from business history.
5. Service/account/payment/fiscal document are separate concepts.
6. Authorization is enforced server-side.
7. Retried commands must not duplicate business actions.
8. External provider identifiers do not become domain primary keys.
