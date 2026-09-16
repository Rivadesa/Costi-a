# Instructions for the isolated native transition

Read the repository root instructions and ADR-008 / ADR-009. Within `dotnet/`, the user-approved target is C#/.NET, a Windows service with PostgreSQL, a WPF Windows client, and Vue/TypeScript PWA for mobile devices. This directory currently contains only D0 domain and read-only planner code.

The existing Laravel/Tauri runtime remains the reference and must not be overwritten, reseeded or run concurrently as a writer with a new backend. No public API or schema migration is implied by the new records. Record integration status separately from compilation.

Do not expose money in service/KDS projections. Do not change dining state when recording payments or infer payment/physical vacancy from dining completion. Preserve traceability. Use the acceptance runner, not `dotnet test`, for this D0 suite. Update tests and `docs/native/D0-build-recovery.md` with exact evidence and limits; do not claim tests ran because their code exists.
