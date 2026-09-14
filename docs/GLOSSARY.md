# Glossary

## Tenant
Top-level customer/group boundary in the SaaS/product model. May contain several legal entities.

## Company / Legal Entity
A fiscal/legal company that buys, sells and invoices independently. Example: Retiro da Costiña vs AÑITSOC SL.

## Location / Establishment
A physical or operational site such as restaurant, shop, office, hotel or warehouse.

## Dining Area
Restaurant room/terrace/private area containing tables.

## Table
Physical/configurable restaurant table. Not the same as a service instance.

## TableService
One concrete dining service for one party/table over time. Central V1A aggregate.

## Guest / PAX
One diner position inside a TableService. Name/customer identity is optional; position is enough for operational restrictions.

## Restriction
Structured dietary/safety information attached to a guest, such as allergy, intolerance or preference.

## MenuTemplate
Reusable definition of a menu/experience and its ordered courses.

## ServiceMenu / Menu Snapshot
Immutable-ish service-specific copy/instantiation of relevant menu configuration, protecting historical services from later template edits.

## CourseTemplate
Configured course inside a menu template.

## ServiceCourse
Execution instance of a course for one TableService.

## Preparation
An actionable kitchen item belonging to a ServiceCourse, routed to a station and optionally to a guest position.

## Kitchen Station
Operational kitchen area/queue such as Fish, Meat, Cold, Pastry or Pass.

## KDS
Kitchen Display System. Screen/interface showing actionable kitchen preparations.

## Pass / Chef Validation
Final coordination point where chef/pass confirms a course can leave kitchen once required components are ready.

## Fire Course
Explicitly request/start the next course preparation for a table.

## Served
Sala has delivered the course to the table.

## Eating
Derived display state/timer after Served and before next course fire. Not a manual persisted action in current design.

## Consumption
Drink or extra added to the service independently from fixed menu courses.

## Provisional Account / Settlement
Operational running total before fiscal document generation.

## Payment
Money settlement record. Separate from fiscal document.

## FiscalDocument
Future V1B invoice/simplified invoice/rectification representation governed by fiscal rules.

## Audit Log
Append-oriented record of who changed/cancelled critical business data, when and why.

## Domain Event
Semantic notification that something meaningful happened in the domain.

## Transactional Outbox
Database pattern that stores the event-to-publish in the same transaction as business state, enabling reliable async delivery.

## Idempotency Key
Client-generated stable key allowing safe command retry without duplicating the business action.

## Local-primary
Architecture where the restaurant local server is authoritative for live service while cloud is replica/services rather than equal write master.

## PIM
Product Information Management. Future V1C master source for product/wine metadata, media, SEO and channels.

## Vintage
Wine year/harvest represented as a first-class concept, not mere free text.

## Packaging
Commercial/logistic presentation such as bottle, case of 6 or pallet.

## Physical Stock
Quantity physically present at a location.

## Stock Ownership
Legal/company ownership of that stock; can differ while goods share the same physical cellar.

## Intercompany Transfer
Movement of inventory ownership between legal entities, potentially without physical movement. Manual/audited first; automation later.

## Available-to-Promise
Future inventory concept: quantity a channel is allowed to sell after reservations/commitments/safety stock.
