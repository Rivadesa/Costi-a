# Handoff Checklist

Use this whenever handing work to another developer or AI.

## Read first
- [ ] README.md
- [ ] docs/AI_CONTEXT.md
- [ ] docs/STATUS.md
- [ ] docs/PRODUCT_SCOPE.md
- [ ] docs/ARCHITECTURE.md
- [ ] docs/DOMAIN_MODEL.md
- [ ] docs/DECISION_LOG.md
- [ ] relevant ADRs

## Before changing code
- [ ] Identify current roadmap phase.
- [ ] Find/create GitHub Issue.
- [ ] Confirm acceptance criteria.
- [ ] Check whether change affects domain invariants.
- [ ] Check API/schema/migration impact.
- [ ] Check offline/idempotency/realtime impact.
- [ ] Create ADR if architecture changes.

## Before opening PR
- [ ] Tests added/updated.
- [ ] OpenAPI updated if endpoint changed.
- [ ] STATUS updated.
- [ ] Relevant workflow/domain docs updated.
- [ ] Screenshots for UI changes.
- [ ] No secrets/real personal data committed.
- [ ] Migration rollback/recovery considered.

## Context that must never be assumed silently
- Retiro has 8 tables today, but table count is configurable.
- Menu course count is configurable.
- Retiro fires courses manually today; automatic pacing is future/configurable.
- Cloud is not equal master in V1A.
- Verial/current fiscal system is temporary bridge; core must not depend on it.
- AÑITSOC multi-company/inventory belongs primarily to V1C.
- WooCommerce belongs primarily to V1D and is a channel, not master.

## End-of-work handoff note

A contributor should leave in PR/issue:
- what changed;
- why;
- what remains;
- tests/status;
- any new decision/debt/risk.
