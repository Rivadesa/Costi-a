# Testing Strategy

## 1. Testing objective

The product must be correct under domain rules **and** usable during a real service. Automated tests protect invariants; live-service tests protect usability and operational fit.

## 2. Test pyramid

### Domain unit tests — highest volume
Cover:
- service lifecycle;
- guest limits;
- restrictions;
- menu snapshot behavior;
- course state transitions;
- preparation readiness invariant;
- skip/extra course rules;
- consumption cancellation;
- account calculation;
- payment/close preconditions.

These should run fast without database where possible.

### Application/integration tests
Cover:
- PostgreSQL persistence;
- transactions;
- outbox atomicity;
- idempotency;
- authorization;
- API error mapping;
- projections/KDS queries.

### UI/component tests
Cover critical interactions:
- one-tap fire course;
- KDS START/READY;
- allergy rendering;
- pending/retry indicators;
- global board projection.

### End-to-end LAN tests
Cover complete flows against real backend and clients.

## 3. Non-negotiable domain tests

At minimum:

1. Cannot add more guest positions than pax.
2. Cannot start service without assigned menu.
3. Cannot fire next course while current course remains active/unserved according to rule.
4. One station READY does not mark multi-station course READY.
5. Course READY rejected until all required preparations ready.
6. Guest-specific restriction appears only on affected preparation projection where appropriate.
7. Skip course does not modify menu template.
8. Extra course does not modify menu template.
9. Cancelled consumption contributes zero to active total but remains queryable/auditable.
10. Duplicate idempotency key does not duplicate consumption/course/payment.
11. Cannot close service before payment rule satisfied.
12. Cannot operationally cancel already closed service under V1A rule.

## 4. Outbox atomicity test

Simulate failure cases:
- business state write succeeds only if outbox insert also succeeds;
- rollback removes both;
- publisher failure leaves outbox pending;
- retry does not duplicate cloud/realtime semantic event when consumer is idempotent.

## 5. Concurrency tests

Important scenarios:
- two devices attempt to fire next course simultaneously;
- two kitchen clients mark same item READY;
- repeated waiter command after timeout;
- payment command retried after uncertain response.

Expected result: domain action occurs once or second actor receives stable conflict/current-state response.

## 6. Offline/network tests

### Internet outage
Disconnect WAN while LAN remains up.
Verify:
- service continues;
- waiter can fire courses;
- KDS receives state through local server;
- consumptions work;
- cloud status becomes pending/degraded;
- no external timeout blocks operation.

### Client Wi-Fi flap
Disconnect one waiter client during command acknowledgement.
Verify:
- pending state is visible;
- retry same idempotency key;
- no duplicate action.

### Realtime disconnect
Drop WebSocket/realtime connection.
Verify:
- HTTP command remains durable;
- reconnect/refetch restores correct projection.

## 7. Performance targets — initial

Targets should be measured on representative LAN/hardware, not guessed permanently.

Initial UX expectations:
- normal local command acknowledgement feels immediate;
- KDS update after committed command should be sub-second under normal LAN conditions;
- global board can render all active Retiro tables without pagination;
- no external Internet dependency on live command path.

Record measured values before setting formal SLOs.

## 8. Real-service acceptance test

Before V1A is considered validated, run a controlled real or realistic service with:
- 8 configured tables;
- multiple simultaneous active tables;
- multi-station course;
- at least one critical restriction;
- drink additions;
- one skip/substitution/extra exception;
- temporary Internet outage;
- account/payment/close flow.

Observe staff, not only logs.

## 9. UX acceptance questions

- Can waiter fire next course in one obvious action?
- Can chef understand all table pacing in a few seconds?
- Can kitchen identify its tasks without irrelevant data?
- Is a critical allergy impossible to overlook under normal viewing?
- Do staff revert to verbal coordination because software is slower?
- Are retries/disconnects understandable without technical training?

## 10. Regression fixtures

Maintain fixture scenarios such as:

### Scenario A — normal table
2 pax, 6 courses, one wine, card payment.

### Scenario B — allergy
3 pax, PAX2 critical shellfish allergy, one adapted preparation.

### Scenario C — multi-station
4 pax, one course requiring Fish + Hot + Pass; early ready attempt rejected.

### Scenario D — exception
2 pax, skip course 4, add chef extra course, cancel mistaken water.

### Scenario E — network retry
fire course request loses response then retries same key; only one course.fire event.

## 11. Migration tests

Before production migration:
- test against copy/synthetic production-sized data;
- verify constraints/index creation time;
- backup/restore path;
- ensure old client/API compatibility when rolling deployment requires it.

## 12. Definition of done for a feature

A feature is not done until:
- domain behavior has tests;
- API/persistence behavior has integration coverage where relevant;
- happy path and failure path are considered;
- docs/OpenAPI updated;
- offline/idempotency impact considered;
- real-service UX remains within interaction budget.
