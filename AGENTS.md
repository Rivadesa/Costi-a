# AGENTS.md — Instructions for AI collaborators

This repository is maintained by humans and AI assistants. Treat repository documentation as the authoritative context.

## Mandatory reading order

1. `README.md`
2. `docs/AI_CONTEXT.md`
3. `docs/STATUS.md`
4. `docs/PRODUCT_SCOPE.md`
5. `docs/ARCHITECTURE.md`
6. `docs/DOMAIN_MODEL.md`
7. relevant ADRs, especially `ADR-007-service-control-vs-checkout.md` for any UI/account work

## Current product priority

V1A fine-dining service orchestration. Do not broaden the MVP to inventory, full reservations, ecommerce, fiscal engine or hotel unless the issue explicitly belongs to a later phase or only a boundary is being prepared.

## Approved D0/D1 transition (2026-09-15)

Read `docs/adr/ADR-008-windows-native-transition.md`, `ADR-009-independent-lifecycle.md` and `docs/phases/D0.md` FIRST. User approved phase-by-phase development. Target: .NET 10 / ASP.NET Core Windows Service + PostgreSQL + WPF Windows client, Vue/TypeScript PWA for mobile/KDS. No Docker/WSL/Redis requirement for restaurant installation. D0 corrects the PHP behavioral reference, NOT a new Laravel product commitment. Do not develop new business modules in PHP; port verified contracts in D1. Do not claim .NET/WPF/installer built merely because the ADR exists.

Service pacing, party occupancy and provisional account are independent. Paid/pending_payment are legacy values, never operational statuses. No silent legacy-state guesses; new commands and conservative migration are in ADR-009. Historical idempotency/outbox/payments stay intact. Never run both runtimes as writers. A D0 migration is not compatible with an old writable backend.

## Original implementation (historical, superseded as target by ADR-008)

Reference runtime until D1 only:
- local-primary restaurant server;
- Laravel + PostgreSQL + Redis backend;
- Vue 3 clients;
- Tauri desktop on Windows;
- PWA possible for waiter/KDS;
- modular monolith;
- transactional outbox;
- no multi-master cloud/local;
- no event sourcing as primary persistence;
- no synchronous external API dependency on live-service path.

## Non-negotiable domain rules

- menu template and service execution are distinct;
- guest restrictions are structured;
- preparation states are independent per station/guest;
- a course cannot be ready while required preparations are not ready;
- account, payment and fiscal document are distinct concepts;
- audited cancellations are not silent hard deletes;
- retried commands must be idempotent where network uncertainty exists;
- catalog prices are snapshotted into service consumptions and historical accounts are not rewritten by later tariff changes.

## Financial/UI boundary — do not violate

`Control de servicio`, waiter/comandero views and KDS are **operational-only**. They must not expose:
- provisional account;
- menu prices;
- consumption prices;
- subtotal/balance;
- payments or payment actions.

Financial data belongs to the main-terminal `Cuenta / Caja` context and `/checkout/...` API projection. See ADR-007.

Do not "simplify" by reusing a financial service projection in waiter/KDS UI. The separation exists in API projections as well as UI on purpose.

Terminal profiles are local UX restrictions:
- `main`: operation + KDS + Cuenta/Caja when authorized;
- `service`: sala/maître operational tracking only;
- `kds`: kitchen only.

Server permissions remain the security boundary; terminal profile alone is not authorization.

## UX rules

- frequent live-service action should be one tap when possible;
- allergy alerts cannot rely on color only;
- do not require manual EATING/FINISHED taps solely for metrics;
- exceptional actions are secondary; routine actions are prominent;
- keep financial workflow out of table pacing screens even on the main PC; it has its own `Cuenta / Caja` surface.

## Before coding

Check:
- correct roadmap phase;
- existing issue/acceptance criteria;
- domain invariant impact;
- schema/API impact;
- offline/retry/idempotency impact;
- whether the change belongs to service-control, KDS or checkout context.

## When making architecture changes

Do not silently rewrite prior decisions. Create a new ADR that supersedes an old one and update documentation links/status.

## When finishing work

Update:
- tests;
- `docs/STATUS.md`;
- OpenAPI/contracts;
- relevant docs/ADR.

## Prohibited shortcuts

Do not:
- put business invariants only in Vue;
- use floating point for money;
- make WooCommerce the product master;
- write directly to DB from client;
- couple core IDs to external provider IDs;
- hide critical allergy data behind notes;
- expose financial data in service-board/comandero/KDS projections;
- allow clients to submit arbitrary product prices for normal restaurant consumptions;
- introduce microservices/CRDTs because they seem architecturally sophisticated.

Prefer the simplest architecture that protects the documented business invariants.
