# Native transition instructions

Read root instructions, ADR-008/009 and docs/native/D1-postgres-api.md. In dotnet/, the approved direction is .NET, PostgreSQL and WPF for Windows plus Vue/TypeScript mobile clients. D0 is the separated domain; D1.1 adds scoped PostgreSQL persistence and a loopback-only laboratory ASP.NET Core API. WPF is not implemented in this branch.

Do not modify or reseed the existing Laravel/Tauri installation. The native schema is isolated; no legacy migration is authorized by compilation. Do not close #17 until end-to-end product migration/integration exists.

Keep service, table occupancy and account independent. Service DTOs and responses contain no financial data. Endpoint metadata defines permissions: never rely on raw URL substring/casing or a client-selected profile. D1 role keys are lab-only; real users/device binding/TLS/permissions per station are future gates, not completed features.

Persistence is current-state per aggregate with versioned JSONB payloads and relational scope/FK/unique/version constraints. Snapshots are trusted internal data, never HTTP command input. No replay to rehydrate; no phantom events. Every write and its outbox/audit/idempotent response share a transaction. No publisher is implemented yet. Document any additional normalization or repository/ORM change.

Run D0 using dotnet run --project tests/Costina.Acceptance, not dotnet test. D1 uses tests/Costina.PersistenceChecks and tests/http/native_http.py + security_http.py with a dedicated *_d1_test PostgreSQL database. CI containers are test infrastructure, not a Windows deployment dependency. The Windows artifact is an engineering server, not a GUI/installer.

Before finishing: exact commit, executed CI evidence, docs/native/D1-postgres-api.md and PR state. Do not mark tests executed from code inspection. No production claims from a Linux HTTP test or Windows compilation alone.
