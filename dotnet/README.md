# Costina Native — D0.1 recovered for compilation

This directory recovers the C# domain source and acceptance runner delivered in `Costina-D0.1-native-core.zip`. It is NOT a new desktop application, HTTP server, installer or migration of existing production data. The Laravel/Tauri 0.1.1 runtime is untouched.

## Scope

- `Costina.Domain`: independent dining, occupancy and settlement state; courses and station preparation; operational read models with no money fields.
- `Costina.Migration`: read-only planner for ambiguous legacy states. No database writes or imports.
- `Costina.Acceptance`: 37 named scenarios plus 15 legacy-mapping fixtures. Custom executable runner with nonzero exit on failure and JSON/JUnit reports; not `dotnet test`.

## Build

The SDK version is pinned in `global.json`. This cut uses no external NuGet packages; `NuGet.Config` intentionally clears package sources.

```powershell
cd dotnet
dotnet --info
dotnet build Costina.slnx --configuration Release
dotnet run --project tests/Costina.Acceptance --configuration Release --no-build -- artifacts/d0
```

GitHub Actions installs the SDK automatically through `actions/setup-dotnet`. A missing SDK or restricted Internet in the conversational development environment must not be treated as proof that the user's repository has no write permissions. Read the actual action result.

## Verification

The original D0.1 package was delivered without executed C# tests. This branch adds a real CI path on Windows and Linux. Only a completed run on the exact commit demonstrates compilation and executed scenarios. `artifacts/d0/sdk-info.txt` records SDK details; `build-context.json` records commit/run; `acceptance.json` and `acceptance.xml` are created by the test process itself.

Do not label these as UI, LAN, PostgreSQL, backup or installer tests. They are domain/planner tests. Do not close issue #17 merely because they pass: the old runtime still has the documented coupling until the persistent/API migration is complete.

## Boundaries

No WPF, HTTP, authentication, PostgreSQL persistence, transactional API idempotency, durable outbox publisher, SignalR, device pairing, installer, backup, stock, fiscal issuance, WooCommerce or TheFork is implemented by this cut. Guest restrictions remain a required port from the existing reference and are not yet present here.

`CommandStamp` must come from an authenticated/authorized server context. Scope checks in domain objects are not authorization. `AddCharge` accepts an already authorized catalogue price, not a free price entered by a waiter. Duplicate ID checks in memory do not replace API idempotency.

## Decisions

See ADR-008 and ADR-009. Do not run both backends as writers against the same schema. The next executable cut needs transactional persistence/API and then a WPF client, rather than another in-memory demonstration presented as a real server.
