# Contributing to Costi-a / Hospitality OS

## Before starting

Read:
1. `README.md`
2. `docs/AI_CONTEXT.md`
3. `docs/STATUS.md`
4. `docs/PRODUCT_SCOPE.md`
5. `docs/DOMAIN_MODEL.md`
6. relevant ADRs

Do not start a broad feature from chat/context alone; GitHub documentation is the source of truth.

## Scope discipline

Check whether work belongs to current V1A. If it belongs to V1B+ but affects current architecture, document only the necessary boundary now rather than implementing the full future module.

## Branches

- stable: `main`
- active integration: `develop`
- optional work branches: `feature/*`, `fix/*`, `docs/*`

Never use `main` as scratch development.

## Pull requests

PR description should include:
- user/business problem;
- implementation summary;
- domain model impact;
- API/schema impact;
- tests;
- screenshots/video for UI if useful;
- offline/retry implications;
- migration/deployment implications;
- docs/ADR updates;
- known limitations.

## Architecture decisions

Create/supersede an ADR when changing:
- local-primary model;
- main persistence technology;
- sync semantics;
- module boundaries;
- fiscal boundary;
- identification strategy;
- desktop/runtime strategy;
- external integration ownership.

Small implementation details do not require ADRs.

## Domain rule changes

Any change to course/service/payment invariants should:
1. be described in `DOMAIN_MODEL.md` if durable;
2. have automated test coverage;
3. expose explicit error semantics through API;
4. consider historical/audit impact.

## User experience changes

Live-service UX is constrained by `docs/UX_PRINCIPLES.md`.

Adding a click/tap to a frequent action requires justification. Prefer observation of real service over theoretical metric collection.

## Database

- migrations only;
- no production manual schema edits as normal process;
- constraints/indexes are part of domain protection;
- use integer minor units for money;
- do not hard-delete audited business history by default.

## External integrations

Keep provider code behind adapters. Do not add WooCommerce/TheFork-specific identifiers as core primary keys or make external API availability a prerequisite for restaurant service operations.

## Security/privacy

Do not commit secrets or real sensitive customer datasets. Use synthetic/demo fixtures.

## Documentation upkeep

At the end of significant work update at least:
- `docs/STATUS.md`;
- OpenAPI if API changed;
- ADR if decision changed;
- relevant domain/workflow docs.

Stale documentation is treated as a defect because multiple AIs/developers rely on it.
