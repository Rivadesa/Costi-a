# AGENTS.md — Instructions for AI collaborators

This repository is maintained by humans and AI assistants. Treat repository documentation as the authoritative context.

## Mandatory reading order

1. `README.md`
2. `docs/AI_CONTEXT.md`
3. `docs/STATUS.md`
4. `docs/PRODUCT_SCOPE.md`
5. `docs/ARCHITECTURE.md`
6. `docs/DOMAIN_MODEL.md`
7. relevant ADRs

## Current product priority

V1A fine-dining service orchestration. Do not broaden the MVP to inventory, full reservations, ecommerce, fiscal engine or hotel unless the issue explicitly belongs to a later phase or only a boundary is being prepared.

## Non-negotiable architecture

Unless superseded by ADR:
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
- retried commands must be idempotent where network uncertainty exists.

## UX rules

- frequent live-service action should be one tap when possible;
- allergy alerts cannot rely on color only;
- do not require manual EATING/FINISHED taps solely for metrics;
- exceptional actions are secondary; routine actions are prominent.

## Before coding

Check:
- correct roadmap phase;
- existing issue/acceptance criteria;
- domain invariant impact;
- schema/API impact;
- offline/retry/idempotency impact.

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
- introduce microservices/CRDTs because they seem architecturally sophisticated.

Prefer the simplest architecture that protects the documented business invariants.
