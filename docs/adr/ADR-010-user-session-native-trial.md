# ADR-010 — Bounded native installation trial

Date: 2026-09-16. Status: implementation for isolated user testing, not production approval.

ADR-008 (Windows SCM service + PostgreSQL + WPF/PWA) remains the product target. D1.3 delivers a per-user, visible trial supervisor and NSIS installer so the owner can exercise real PostgreSQL without Docker or manual setup. It does NOT implement Windows service registration, boot startup, LAN, failover or production device identities. This is a staged packaging test, not an architectural switch to an embedded desktop database.

The supervisor manages only bundled native processes and a dedicated user-owned directory. No elevation, firewall rules, autorun registry entries, legacy data changes or external downloads at runtime. Closing a client does not close the engine. The visible supervisor must be stopped explicitly after clients close. Ending the Windows session stops service availability.

Config is protected with DPAPI CurrentUser and a user/SYSTEM directory ACL. DB bootstrap, schema owner and runtime roles are separate; runtime has no DDL/delete/audit rewrite permissions. Lab role tokens remain lab credentials, not production auth. A local user who owns this trial can open all test roles. Never claim that as trusted terminal pairing.

Preparation is explicit. Normal startup only reads/starts existing state. Repeat preparation on a completed installation is a no-op. Unknown or incomplete directories are never deleted/reset. Port collisions fail safely rather than killing other programs. Data stays outside program files and survives uninstall. No backup claim.

Release gate: actual Windows package installation, native PostgreSQL init, WPF authenticated business commands, independent roles, restart with unchanged records, runtime privilege checks and uninstall preserving data. Record exact evidence; physical clean Windows 11 and Windows service deployment remain separate gates.
