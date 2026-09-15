# D0 build/publication recovery — 2026-09-15

## Observed environment

The conversational container has no `dotnet` executable. A direct HTTPS download of Microsoft's install script failed with `curl: (6) Could not resolve host: dot.net`; the direct SDK archive download also failed. No local SDK installation is claimed.

The active GitHub connector now exposes write actions. Repository metadata returned push permission and branch creation succeeded for `build/d0-sdk-recovery-20260915`. Earlier `Resource not found` errors concerned action availability; they were not evidence that the owner must make the repository public or supply a personal access token. The underlying cause of the earlier capability change is not established.

## Recovered code

The source core and acceptance runner from the delivered D0.1 archive are imported under `dotnet/`, without rewriting the legacy backend. The legacy JSON fixtures are reformatted only. The SDK is pinned to stable 10.0.401 and installed automatically by CI. The main implementation status remains the legacy 0.1.1 status until an explicitly reviewed integration.

## Verification status at commit creation

- GitHub write test: branch creation succeeded.
- Local SDK download: failed; not installed.
- C# compilation/52 acceptance scenarios: pending the actual GitHub Actions run.
- Existing PHP suites: not rerun in this recovery cut.
- WPF, HTTP, PostgreSQL integration, Windows installer and physical LAN: not included, not validated.

The workflow publishes SDK details, commit/run context and executed JSON/JUnit test reports. Results must be checked for the exact commit before making any success claim. No new restaurant installer is produced by this workflow.

## Reproducible route

`actions/setup-dotnet@v5` reads `dotnet/global.json`; `dotnet build` compiles the three projects; `dotnet run` executes the custom acceptance runner and returns failure on failed scenarios. There are no remote package dependencies in D0.

An SDK on the user's PC is optional for local development and does not install an SDK in this container. The user should not need to install development tooling merely to unblock the project's CI.

References: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script ; https://github.com/actions/setup-dotnet ; https://dotnet.microsoft.com/en-us/download/dotnet/10.0
