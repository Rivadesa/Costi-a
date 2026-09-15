# ADR-008 — Native Windows transition without Docker

Date: 2026-09-15. Status: target architecture approved by the user; D0 implementation under review. Related: PR #19 and issue #17.

## Decision

For new development, use C#/.NET for the local business engine, an ASP.NET Core Windows service, PostgreSQL, WPF for the native Windows workstation, and Vue/TypeScript PWA for Android/iOS/tablets. Docker is not a restaurant runtime dependency. The server is independent of the client window.

This supersedes ADR-002/003 stack choices for the new native implementation only. It does not claim that Laravel/Tauri 0.1.1 has been migrated. Existing runtime and configuration remain untouched until equivalence, persistence, import, recovery and installation tests pass.

## Transition boundary

D0 recovers separated domain objects, a read-only legacy state planner and executable acceptance cases under `dotnet/`. No second application writes to the legacy database. No deploy, migration, fiscal change or deletion occurs as part of SDK/CI recovery.

Preserve local-primary authority, scope, outbox/idempotency requirements, menu snapshots, station-level readiness, financial isolation (ADR-007), audit and the ban on synchronous external calls in live-service commands. Changing the language does not satisfy any unimplemented reliability requirement.

## Validation gate

Compile and run the actual D0 scenarios on Windows and Linux using a pinned stable SDK. Then implement application/persistence and authoritative APIs; integrate WPF and PWA against those APIs. A green domain run is not installer/LAN/production validation.

The existing repository-wide status describes the old runtime. Branch-specific status is in `docs/native/D0-build-recovery.md`; update the root onboarding documents at integration, without presenting future components as implemented.
