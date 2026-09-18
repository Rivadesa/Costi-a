# ADR-011 — Installation layout, database roles and explicit lifecycle

Date: 2026-09-18. Status: accepted for milestone 4 (issue #27). **Supersedes ADR-010**, which only ever lived on the unmerged branch of PR #23 (per-user trial supervisor); that PR is closed as superseded and its reusable ideas are rebuilt here on `develop`.

ADR-008 remains the target: the engine is a Windows service with native PostgreSQL. This ADR fixes the rules every packaging cut (D5.1–D5.6) must respect.

## Decisions

1. **Program and data are separate.** Binaries are replaceable and never written at runtime. Everything mutable lives under one data root: `%ProgramData%\Costina` on Windows (override `COSTINA_DATA`), with `config\`, and in later cuts `pg\`, `logs\`, `backups\`, `tls\`. The engine refuses a data root inside its own program directory. Uninstalling or replacing the program never touches the data root.
2. **Three PostgreSQL roles, three moments.**
   - *bootstrap* (cluster superuser): used only by `provision`, never stored by the engine.
   - `costina_owner`: owns the database and the `native_d1` schema; used only by `init`, `upgrade` and `init-lab`. Its credentials live in `config\owner.json`, which a normal start never reads.
   - `costina_runtime`: what the running engine uses. No DDL, no `DELETE` anywhere, no rewriting of `commands` or `audit`; explicit grants only (`grants.sql`). In installation mode the engine **refuses to serve** with a connection that can alter the schema or the audit trail.
   One installation per PostgreSQL cluster (role names are fixed).
3. **Explicit lifecycle, never implicit.** `provision` → `init` → `create-user` → normal start. `upgrade` is the only way a schema changes on an existing installation. A normal start never migrates, seeds or repairs. `init` on an existing schema changes nothing; `upgrade` without a schema fails; `init-lab` (fictitious fixtures) is refused outside laboratory mode. `provision` refuses to overwrite an existing configuration and never takes over a schema it does not own.
4. **Configuration by file, environment on top.** `config\server.json` holds the runtime settings (same `COSTINA_*` names as the environment); environment variables override it so the laboratory and CI keep working unchanged. Secrets are never printed. On Windows the directory ACL is applied when the service account exists (D5.2); on Unix the files are `0600`.
5. **Two modes.** `laboratory` (`COSTINA_LAB_MODE=true`, database `*_d1_lab`/`*_d1_test`, single role tolerated with a warning) and `installation` (`COSTINA_MODE=installation`, provisioned roles mandatory, no fixtures). `/health` reports which one is running.

## Not decided here

Service registration and service account (D5.2), LAN/TLS with a local CA (D5.3), backups and restore (D5.4), the installer and its modes (D5.5), the physical clean-Windows script (D5.6). Loopback remains the only listener until D5.3.

## Consequences

Every new table must be reviewed in `grants.sql` or the runtime role cannot use it — the HTTP battery runs with the runtime role in CI, so a missing grant fails loudly. The engineering laboratory (`docs/native/lab-engineering.md`) keeps its single-role shortcut and is never a template for a real installation.
