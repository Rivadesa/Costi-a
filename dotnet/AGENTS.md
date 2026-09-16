# Native transition instructions

Read root instructions, ADR-008/009, docs/native/D1-postgres-api.md and docs/native/D1.2-desktop.md. The approved native transition is .NET/PostgreSQL/WPF plus Vue/TypeScript mobile clients. Legacy Laravel/Tauri must remain unchanged and must never write concurrently into this isolated native database.

D1.2 adds a real WPF client, a typed HTTP library and four authenticated read endpoints. Operational DTOs/configuration contain no money. Checkout is separate and server-authorized, including read APIs and main-only UI. Never treat a client profile as authorization or accept client prices for normal catalogue sales. Keep service, occupancy and settlement independent.

D1 restrictions remain: isolated *_d1_lab/*_d1_test database, loopback HTTP, explicit random role keys, no production identities/device pairing or complete allergy handling. Normal startup never migrates or seeds. No migration of real data is implied. Root onboarding still describes the legacy branch until native PRs are integrated.

Compile the desktop and run tests/Costina.ClientChecks and tests/Costina.DesktopChecks on Windows; the latter instantiates a real window but does not claim a live end-to-end backend service. Run prior D0 acceptance, snapshot tests and HTTP tests including desktop_reads.py against PostgreSQL. Every write retains audit/outbox/idempotency in one transaction.

D1.2 refresh is manual. Uncertain commands retain the original bytes/key during the session; forced shutdown loses this pending state. No durable queue or WebSockets claims. No Windows/PostgreSQL automatic installer claims. Publish the entire self-contained directory, not the executable alone. Record exact CI commit, verified artifacts and physical testing separately. #17 remains open until actual legacy migration/integration.
