# ADR-009 — Independent dining, occupancy and settlement lifecycles

Date: 2026-09-15. Status: accepted domain direction; D0 in-memory implementation under review. Related: issue #17, ADR-005, ADR-007 and ADR-008.

## Problem

Legacy `TableService.recordPayment()` overwrites operational state with Paid/PendingPayment and close requires payment. UI/API financial isolation does not resolve that internal coupling.

## Decision

- `DiningService`: Open, InService, Paused, Completed, Cancelled. Owns courses/preparation state and never depends on SettlementAccount.
- `TableOccupancy`: Occupied/Released; explicit authorized release after the matching service is completed or cancelled. Does not settle an account.
- `SettlementAccount`: Open/Closed; coverage is derived as Unpaid/PartiallyPaid/Paid/Credit. Payments do not change pacing or occupancy.

A full prepayment does not close the account; later legitimate extras may alter the balance until explicit economic close. Charge voiding retains history and does not imply cash refund or stock restocking. Unresolved credit blocks closing rather than being hidden. Prices are server-authorized catalogue snapshots.

Completing dining does not collect payment, release a table or issue a fiscal document. Release is a separate action with actor/time/reason. Physical exclusivity and transactionally safe release require persistence and permissions beyond D0.

## Kitchen rules

No next course while another is fired/preparing/ready. Required preparations must be ready before chef validation. Pausing blocks new firing but permits acknowledgement of sent work. D0 skip only applies to unsent courses; recalls/late allergen changes need an explicit kitchen acknowledgement protocol. No extra manual eating/finished step for metrics alone.

## Legacy mapping

A financial legacy state is not evidence of the dining state it overwrote. The planner must return ambiguous cases for review and preserve original identities/totals/status. Legacy close is not reliable proof of physical vacancy or explicit economic close. The planner is read-only, not a database importer; issue #17 stays open until persisted/API migration and regression tests pass.

## Evidence required

Prepayment during preparation, pause/payment independence, extras after payment, unpaid completion/release, payment after release, kitchen readiness, non-destructive void, duplicate financial IDs, overflow atomicity, scope checks and money-free operational projections. Memory tests do not prove API idempotency, PostgreSQL transactions, backups or fiscal correctness.
