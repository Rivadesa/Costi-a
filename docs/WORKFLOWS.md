# Operational Workflows — V1A

## 1. Service preparation

Before lunch/dinner, staff can review the expected service:
- tables/parties;
- expected pax;
- selected menu;
- known restrictions;
- operational notes.

Kitchen may view a preparation summary, but V1A does not calculate procurement/stock needs.

## 2. Guest arrival

1. Locate/import/create party.
2. Confirm actual pax.
3. Confirm/change table.
4. Review restrictions.
5. Add new restriction if discovered.
6. Open `TableService`.
7. Assign menu snapshot.
8. Start service.

The service must not depend on a reservation provider-specific object.

## 3. Normal course cycle

### Sala

- current course is served;
- when timing is appropriate, authorized user taps **SEND NEXT COURSE** once.

### Backend

- validate service state;
- validate no unfinished course blocks progression;
- mark course FIRED;
- instantiate/activate required preparation work;
- persist outbox/realtime event atomically.

### Kitchen station

- receives only relevant preparations;
- taps START;
- taps READY when work is complete.

### Pass / chef

When all required preparations are ready:
- system may show course eligible for ready validation;
- chef/pass validates READY if manual validation configured.

### Sala

- receives READY;
- after delivering dishes, taps SERVED once.

From SERVED until next course is fired, UI may display `Eating · mm:ss` as derived state.

## 4. Multiple stations

Example Course 6:
- fish station: main protein;
- hot/pass station: garnish;
- cold station: sauce/finishing.

Each preparation state is independent. Course cannot become READY while any required preparation is not ready.

## 5. Restriction adaptation

Example:
- table 4, 3 guests;
- PAX 2 has critical shellfish allergy.

Course instance may contain:
- 2 × standard preparation;
- 1 × adapted preparation assigned to PAX 2.

KDS must show restriction directly on the affected item. Do not rely on opening a hidden note.

## 6. Add drinks/extras

At any point while service is operable:
1. tap `+ Consumption`;
2. use frequent/recent item or search;
3. add quantity;
4. account projection updates.

Adding a drink does not alter course execution state.

## 7. Skip a course

Authorized role:
1. open exceptional actions;
2. choose Skip course;
3. provide reason according to policy;
4. service course becomes SKIPPED;
5. master menu remains unchanged;
6. audit/event retained.

## 8. Extra course

Chef/sala authorized user may add a service-specific course. It has its own preparations and pricing metadata if needed later. It does not modify the reusable menu template.

## 9. Substitute preparation

When chef changes a dish for one guest or table:
- keep reference/description of original expected preparation;
- record substitute;
- record actor/time/reason where relevant;
- KDS displays only actionable current preparation while audit preserves history.

## 10. Pause rhythm

Pause is an operational pacing signal.

While paused:
- no automatic next-course action;
- consumptions can still be added;
- table remains visible with PAUSED alert;
- resume returns to IN_SERVICE.

## 11. Kitchen incident

Kitchen may signal an incident such as:
- unavailable ingredient/product;
- preparation error;
- rework;
- delay;
- other.

Incident should surface to chef/sala without destroying the preparation record.

## 12. Move table

`TableService` is independent from the physical table identifier.

Moving M4 → M6 changes the table reference for the active service; consumptions, guests and course history remain attached to the service id.

## 13. Provisional account review

Account is calculated from service/menu + non-cancelled consumptions.

Before closing:
- review menu amount;
- review consumptions;
- cancel mistaken item via audited cancellation rather than deletion;
- record operational payment.

V1A account is not a fiscal document.

## 14. Close service

Expected checks:
- payment condition satisfied according to V1A rule;
- no active kitchen course that would make closure inconsistent;
- service becomes CLOSED;
- operational history remains queryable.

## 15. Cancel service

Before fiscalization, an authorized user may cancel operational service with reason. It should disappear from normal active/valid sales projections but remain available to audit/admin views.

A CLOSED/fiscalized service cannot later be treated as if it never existed; V1B fiscal correction rules apply once fiscal records exist.

## 16. Internet outage

If Internet fails but LAN/local server works:
- all normal workflows above continue;
- cloud/integrations show degraded/pending status;
- pending outbox records remain durable;
- reconnect resumes delivery.

## 17. Client Wi-Fi interruption

If a waiter device sends a command and loses connection before receiving acknowledgement:
- command remains locally pending if client supports it;
- retry uses same idempotency key;
- server must not duplicate course firing/consumption/etc.

## 18. V1A acceptance scenario

A complete real service must be possible using the system for sala/kitchen coordination without paper/verbal course state as the primary source of truth, while maintaining enough flexibility for chef exceptions.
