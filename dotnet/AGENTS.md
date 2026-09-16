# Native transition instructions

Read root instructions, ADR-008/009, docs/native/D1-postgres-api.md and docs/native/D1.2-desktop.md. The approved native transition is .NET/PostgreSQL/WPF plus Vue/TypeScript mobile clients. Legacy Laravel/Tauri must remain unchanged and must never write concurrently into this isolated native database.

D1.2 adds a real WPF client, a typed HTTP library and four authenticated read endpoints. Operational DTOs/configuration contain no money. Checkout is separate and server-authorized, including read APIs and main-only UI. Never treat a client profile as authorization or accept client prices for normal catalogue sales. Keep service, occupancy and settlement independent.

D1 restrictions remain: isolated *_d1_lab/*_d1_test database, loopback HTTP, explicit random role keys, no production identities/device pairing or complete allergy handling. Normal startup never migrates or seeds. No migration of real data is implied. PRs #19–#22 are integrated in `develop` (merge `0ae4e71`); root `AGENTS.md` and `docs/STATUS.md` describe both stacks, with the legacy runtime frozen.

Compile the desktop and run tests/Costina.ClientChecks and tests/Costina.DesktopChecks on Windows; the latter instantiates a real window but does not claim a live end-to-end backend service. Run prior D0 acceptance, snapshot tests and HTTP tests including desktop_reads.py against PostgreSQL. Every write retains audit/outbox/idempotency in one transaction.

D2 (docs/native/D2-realtime.md, issue #24) adds a supervised outbox publisher and a SignalR channel at /api/native/v1/events. Notices are thin (id/type/aggregate/timestamp), never payload or money; account.*/payment.* types reach only main-role connections; delivery is at-least-once and consumers only trigger the same idempotent authoritative HTTP re-read, mandatory after every reconnection. Never put state, prices or kitchen-delivery confirmation into the channel, and keep manual refresh as the fallback. Run tests/Costina.RealtimeChecks against PostgreSQL with the real server binary.

Uncertain commands retain the original bytes/key during the session; forced shutdown loses this pending state (durable client queue is #25). No Windows/PostgreSQL automatic installer claims. Publish the entire self-contained directory, not the executable alone. Record exact CI commit, verified artifacts and physical testing separately. #17 remains open until actual legacy migration/integration.
