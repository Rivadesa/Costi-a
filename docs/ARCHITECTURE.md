# Architecture

## 1. Architectural objective

Hospitality OS must remain operational inside the restaurant even when Internet access is unavailable. At the same time, it must evolve toward cloud backup, reporting and integrations without turning V1A into a distributed-systems project.

The chosen strategy is **local-primary with cloud replica/services**.

## 2. Runtime topology

```text
                 INTERNET / CLOUD
        backup · monitoring · reporting
          future external integrations
                    ▲
                    │ async replication
                    │
          ┌─────────┴─────────┐
          │   LOCAL SERVER    │
          │ Laravel           │
          │ PostgreSQL        │
          │ Redis             │
          │ Realtime          │
          └─────────┬─────────┘
                    │ LAN
      ┌─────────────┼──────────────┐
      ▼             ▼              ▼
 Desktop/Tauri   PWA waiter      KDS/PWA
 Vue 3           Vue 3           Vue 3
```

The local PostgreSQL database is the operational source of truth during V1A.

## 3. Desktop application

The Windows application is not Laravel embedded in each workstation.

- UI: Vue 3 + Vite.
- packaging: Tauri 2;
- communicates with local Laravel API;
- may cache non-authoritative UI state and pending commands;
- must not become a second database authority.

If a workstation fails, a replacement workstation connects to the local server and continues.

## 4. Waiter/KDS clients

The same Vue domain-facing contracts should be reusable for:
- Tauri desktop;
- waiter PWA;
- KDS PWA/Tauri kiosk.

Do not fork business logic across clients. Clients issue commands and render projections; invariants belong to backend/domain.

## 5. Backend layering

Target layout:

```text
backend/
  app/ or src/
    Domain/
      Service/
      Menu/
      Kitchen/
      BillingBoundary/
    Application/
      Commands/
      Queries/
      DTO/
    Infrastructure/
      Persistence/
      Realtime/
      Outbox/
    Interfaces/
      Http/
```

Laravel provides transport, dependency injection, persistence infrastructure, queues and integration points. Domain rules should avoid unnecessary Laravel coupling.

## 6. Modular monolith

V1 is a modular monolith.

Do not introduce microservices because a feature has a different name. Extraction may be considered only after a measured need such as:
- independent scaling;
- independently deployable integration worker;
- organizational ownership boundary;
- significantly different availability/security requirement.

Each module should nevertheless expose explicit application contracts rather than arbitrary cross-module database writes.

## 7. Data authority

### Local operations

Authoritative locally in V1A:
- table services;
- guests/restrictions;
- course execution;
- preparation state;
- consumptions;
- provisional account;
- operational payments;
- audit/outbox.

### Cloud

Cloud may contain replicated copies and derived views. During V1A it must not independently edit the same operational aggregate and then require merge resolution.

## 8. Transactional Outbox

When a transaction changes business state and must produce a durable integration/realtime event:

1. business changes are persisted;
2. an outbox row is persisted in the **same database transaction**;
3. worker publishes/replicates event;
4. delivery state is recorded/retried.

This prevents "database committed but event lost" failure modes.

Outbox is not event sourcing. Current state remains stored in normal relational tables.

## 9. Idempotency

Commands likely to be retried by PWA/Tauri must carry an idempotency key.

Examples:
- fire course;
- mark preparation started/ready;
- validate course ready;
- mark served;
- add consumption;
- record payment.

Server must return the original successful outcome or reject duplicate processing safely.

## 10. Realtime

Realtime transports UI updates but does not define truth.

Expected flow:

```text
HTTP/API command
→ DB transaction
→ domain/application event/outbox
→ realtime publisher
→ subscribed clients update projections
```

If realtime delivery fails, clients must be able to refresh current state from API.

## 11. Connectivity failure modes

### Internet fails

Core LAN operation continues.

Unavailable/degraded:
- cloud sync;
- WooCommerce/TheFork;
- external messaging;
- remote reporting.

### One client loses Wi-Fi briefly

Client may retain pending command locally and retry with same idempotency key. Backend remains authority.

### Local server fails

Operation is unavailable until server is restored/replaced. Mitigation belongs to durability/backup/UPS/monitoring, not multi-master complexity.

Future target: rebuild a replacement local node from cloud replica + configuration, accepting that events never replicated from a physically destroyed disk cannot magically be recovered.

## 12. PostgreSQL

PostgreSQL is selected both for local production data and cloud data services where applicable.

Reasons:
- transactions and constraints;
- concurrency;
- mature Laravel support;
- JSONB where appropriate without abandoning relational modelling;
- same database family across local/cloud reduces environmental drift.

SQLite is not the primary restaurant database. It may be used by clients only as cache/pending-command storage if needed.

## 13. Redis

Redis may be used for:
- queues;
- cache;
- pub/sub/realtime infrastructure;
- locks where justified.

Correctness must not depend solely on volatile Redis data. Durable business state remains PostgreSQL.

## 14. Security boundaries

- tenant/company context must be explicit;
- authorization enforced server-side;
- clients cannot rely on hiding buttons as authorization;
- allergy data and customer information require appropriate access control;
- audit records are not normal editable content;
- future cloud connections must use encrypted transport and rotated credentials.

## 15. Fiscal boundary

V1A must not implement a fake "ticket" abstraction that later becomes impossible to reconcile with fiscal requirements.

Keep explicit concepts:

```text
TableService
  ↓
Account / Settlement
  ↓
Payment
  ↓
FiscalDocument (V1B)
  ↓
FiscalRecord (V1B)
```

The operational service may be cancelled before fiscalization under business permissions/audit. Once fiscal records exist, fiscal correction rules apply instead of destructive deletion.

## 16. External integrations

All external systems are adapters/connectors, not core domain owners.

Examples:
- WooCommerce;
- TheFork/CoverManager;
- Redsys/payment terminals;
- messaging;
- accounting exports.

An external API timeout must never block a restaurant course from being sent to kitchen.

## 17. Architecture anti-patterns for this project

Avoid without explicit ADR:
- distributed transactions across local/cloud;
- cloud/local multi-master;
- business rules in Vue components;
- direct database writes by frontend;
- WooCommerce-specific fields in core service aggregates;
- generic `Ticket` object representing service + payment + fiscal document;
- deleting audited business records to simplify UI;
- synchronous external API calls in critical service path.
