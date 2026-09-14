# Product Scope

## Product thesis

Hospitality OS is not intended to be another generic bar POS. The initial wedge is **orchestration of fine-dining service**: table state, course pacing, kitchen stations, dietary restrictions, service exceptions and provisional settlement, all operating locally without depending on Internet.

Retiro da Costiña is the first real implementation. AÑITSOC SL expands the future product toward gourmet commerce, wine PIM, inventory and multi-company operations.

## V1A — in scope

### Core
- tenant/company/location identifiers prepared from day one;
- users, roles and minimum permissions;
- audit trail for critical actions;
- local-primary operation.

### Dining room
- configurable dining areas and tables;
- open/close table service;
- guest count and guest positions;
- structured allergy/intolerance/preference flags;
- configurable menu template;
- service-specific menu snapshot;
- manual course firing;
- course exceptions per service;
- drinks/extras;
- provisional account;
- operational payment record.

### Kitchen
- configurable kitchen stations;
- preparation routing;
- independent preparation state per station/guest;
- KDS station projection;
- chef/pass validation;
- global service board;
- realtime updates on LAN.

### Reliability
- local server remains authoritative if Internet is unavailable;
- idempotent commands for retryable UI operations;
- transactional outbox for future cloud/integration delivery;
- no destructive deletion of audited business operations.

## V1A — explicitly out of scope

- full reservation engine;
- tax/fiscal document issuance;
- VERI*FACTU implementation;
- stock/inventory accounting;
- PIM and ecommerce publishing;
- vouchers/gift cards engine;
- purchasing/procurement;
- advanced CRM/marketing;
- hotel PMS;
- intercompany automation;
- AI-driven automatic service decisions.

Temporary adapters/imports are allowed where needed, provided they do not leak external provider concepts into the core domain.

## V1B — fiscal replacement

Goal: remove dependency on the current fiscal system for the restaurant.

Includes:
- cash sessions/registers;
- full payment model;
- document series;
- invoices / simplified invoices as legally applicable;
- correction/rectification flows;
- fiscal records and SIF/VERI*FACTU implementation;
- immutable/auditable fiscal boundary.

The domain boundary must remain:

`TableService → Settlement/Account → Payment → FiscalDocument → FiscalRecord`

## V1C — PIM, wines, inventory, multi-company

Includes:
- master product catalogue;
- wine specialization;
- vintage as first-class concept;
- packaging/sales units (bottle, case, pallet);
- media/images/SEO/channel metadata;
- warehouse/location hierarchy;
- physical stock separated from legal owner;
- Retiro and AÑITSOC as independent legal entities;
- manual audited intercompany ownership transfer;
- basic inventory movements.

Important rule: a shared physical wine cellar does **not** imply shared legal inventory ownership.

## V1D — ecommerce and vouchers

Includes:
- WooCommerce Gourmet connector;
- WooCommerce voucher connector;
- PIM → Woo publishing;
- Woo orders → platform ingestion;
- webhook inbox;
- async queues;
- idempotency;
- periodic reconciliation;
- internal voucher/prepayment engine.

## Future modules

Potential future modules include:
- reservation engine;
- TheFork/CoverManager connectors;
- hotel/PMS;
- purchasing;
- costing/recipes;
- digital wine list;
- BI;
- AI recommendations and natural-language analytics.

They must not distort V1A architecture unless a current invariant requires preparation.

## Product acceptance principle

A feature is considered properly generalized when a normal hospitality variation can be handled by configuration rather than customer-specific code.

Examples that must be configurable:
- 8 vs 80 tables;
- 5 vs 15 courses;
- one vs several kitchen stations;
- chef validation on/off;
- who may fire a course;
- menu structure;
- role permissions.

## Product success criterion

V1A succeeds only if staff actually prefer using it during service. Technical correctness alone is insufficient.

Failure signals include:
- staff returning to verbal course calls because UI is slower;
- chef ignoring the global/KDS screens;
- allergy information being hard to notice;
- repeated duplicate taps or navigation during peak service;
- Internet outage stopping core service.
