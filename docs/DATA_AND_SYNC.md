# Data, Persistence and Synchronization

## 1. Authority model

V1A uses **local-primary** data authority.

The restaurant's local PostgreSQL instance is authoritative for live operational state. Cloud receives replicated events/data for backup, reporting and future integrations, but does not independently edit the same live aggregate and then merge it back.

## 2. Why not multi-master

Multi-master/cloud-local merge would introduce:
- conflict resolution;
- clock/order ambiguity;
- duplicate command risk;
- much harder recovery semantics.

That complexity is not justified for the first single-site fine-dining implementation.

## 3. Transactional Outbox

Business write and event publication intent must be atomic.

Example transaction:

```text
BEGIN
  update service_course set status='FIRED' ...
  insert into audit_log ...
  insert into outbox_events ...
COMMIT
```

A worker later publishes the outbox event to realtime/cloud/integration consumers.

## 4. Outbox suggested fields

- id (ULID/UUID);
- tenant_id;
- company_id;
- location_id;
- event_type;
- aggregate_type;
- aggregate_id;
- payload JSONB;
- occurred_at;
- available_at;
- attempts;
- published_at nullable;
- last_error nullable.

Do not delete immediately after delivery; retain according to operational/debug policy.

## 5. Idempotency storage

For commands that may be retried, persist:
- idempotency key;
- actor/device/context;
- command name;
- request fingerprint where useful;
- resulting resource/event/response reference;
- created/expires timestamps.

Unique constraints protect duplicate processing.

## 6. Realtime vs durability

Realtime bus/WebSocket is not a durable database.

If a realtime message is missed:
- current state remains queryable;
- reconnect should refresh affected projection;
- outbox can republish where appropriate.

## 7. Client pending commands

Tauri/PWA may store a command locally only to survive temporary LAN/Wi-Fi interruption.

Client cache is not canonical.

Suggested flow:
1. generate idempotency key;
2. store pending command locally;
3. send to local API;
4. on acknowledged success, clear pending entry;
5. on uncertain network failure, retry same key.

## 8. Ordering

Within one `TableService`, state transitions should be guarded by current persisted state and transaction/locking strategy. Do not rely only on event arrival order at clients.

Potential implementation tools:
- row-level locking for critical transitions;
- optimistic version column;
- unique/idempotency constraints.

Choose the simplest approach that protects real races.

## 9. Cloud replication

Initial direction:

`local outbox → cloud ingestion`

Cloud ingestion must itself be idempotent on event id.

Cloud can build:
- backups/snapshots;
- reporting projections;
- monitoring;
- integration queues.

Future commands from cloud back to local require a separate, explicitly designed command channel and are not assumed in V1A.

## 10. External ecommerce conflict example

Future V1C/V1D stock cannot guarantee perfect real-time mutual exclusion if the local restaurant is offline from WooCommerce.

Use business availability rules rather than pretending distributed consensus exists:
- physical stock;
- committed stock;
- reserved stock;
- available-to-promise;
- optional per-channel safety stock.

Example: keep last rare bottle reserved to restaurant while Woo can sell only quantity above safety threshold.

## 11. Backup strategy target

At maturity:
- PostgreSQL local backups;
- encrypted off-site/cloud backup;
- tested restore procedure;
- configuration export;
- monitoring of backup freshness;
- UPS/server health monitoring.

Recovery documentation must be tested, not theoretical.

## 12. Data deletion

Operational/audit/fiscal retention differs from UI visibility.

An entity may be hidden/cancelled without physical deletion. Future privacy retention requirements must be implemented deliberately rather than using hard deletes indiscriminately.

## 13. Schema evolution

Use migrations. Never modify production schema manually as the normal deployment path.

Migration rules:
- forward-safe where possible;
- explicit defaults/backfills;
- avoid long blocking migrations during service windows;
- include rollback/restore plan for risky migrations.

## 14. Local installation identity

Each installed location/node should eventually have stable identity separate from tenant/company/location business IDs. This allows:
- sync tracking;
- device/node health;
- credential rotation;
- recovery/replacement.

Exact implementation remains to be finalized by ADR before cloud sync productionization.
