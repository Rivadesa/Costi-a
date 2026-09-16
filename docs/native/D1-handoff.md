# D1.1 handoff

PR #21 is stacked on D0 PR #20. It is not merged into develop. Base D0 head f93e9c352b03e5b4d89a223f70dca4b322db48fe.

Read D1-postgres-api.md, dotnet/AGENTS.md and the PR evidence. The initial compile found ASP0016 in handlers with only HttpContext: overload selection could discard the returned HTTP result. Fixed with explicit Func<HttpContext,Task<IResult>> delegates; no analyzer suppression. Authorization was also changed from path string checks to matched endpoint metadata, with six additional HTTP regressions for mixed/uppercase paths and null requests.

The new native API has a separate /api/native/v1 contract. It is NOT drop-in compatible with the old Vue/Tauri API. The fixture catalogue is persisted and server-priced, but administration screens and the full catalogue model are not ported. No production clients should be pointed at this server.

Payloads retain names, prices and execution details as state snapshots; audit is separate and append-oriented. No public endpoint deletes history. Opening one service writes the three entities atomically. An unreconciled old account can survive table release and a new party at the same table.

This cut tests server process restart, not failure of a physical disk, PostgreSQL replication promotion or backup restore. Docker appears only in CI to run an isolated PostgreSQL service. Windows SCM installation, least-privilege provisioning and native PostgreSQL installation remain explicit next deliverables.

Keep Windows binary artifacts separate from test results and sources. The build manifest binds hashes to the exact CI checkout commit. Do not rename the server binary as a user-facing application or installer.

Next bounded cut: Windows client WPF talking to this backend, controlled user/device authentication and native installer/bootstrap. Retain initial lab restrictions until the new security boundary is validated. Stock, WooCommerce, TheFork, fiscal documents and live migration remain deferred.
