# Deployment and Recovery Target

## 1. Deployment model

V1A target installation consists of:

### Local server
- Laravel application/API;
- PostgreSQL;
- Redis;
- queue/outbox worker;
- realtime service;
- backup/monitoring process.

### Client devices
- Windows desktop: Tauri/Vue;
- waiter tablets: PWA or Tauri as chosen per hardware;
- KDS: kiosk/PWA/Tauri.

Clients do not contain the authoritative restaurant database.

## 2. Packaging goal

Installation must eventually be reproducible through automated scripts/containers/packages rather than manual server snowflake setup.

Docker may be used for server components if it simplifies support, updates and recovery; final restaurant operational packaging should be validated on target Windows/Linux hardware before commitment.

## 3. Configuration

Configuration classes:
- environment secrets;
- tenant/company/location bootstrap;
- LAN/realtime URLs;
- cloud endpoint/credentials;
- backup destination;
- device-specific UI mode/station assignment.

Business configuration (tables/menu/stations/users) belongs in application DB, not `.env`.

## 4. Update strategy

### Server
Target controlled updates outside service hours with:
- pre-update backup;
- migration dry-run/testing;
- app release version;
- rollback/restore procedure.

### Tauri client
Target signed release/update mechanism. Exact updater strategy is deferred and requires ADR before production.

### PWA
Service-worker cache/update behavior must not leave a client on incompatible API contracts. Coordinate API compatibility/versioning.

## 5. Health checks

Expose operational health for:
- API;
- PostgreSQL connectivity;
- Redis/queue;
- outbox backlog age/count;
- realtime service;
- cloud sync last success;
- backup last success.

User-facing status should simplify this into local/internet/cloud indicators.

## 6. Backup targets

At minimum before production:
- automatic local PostgreSQL backup;
- encrypted off-site copy;
- retention policy;
- periodic restore test;
- backup health alert.

## 7. Recovery scenarios

### Client PC failure
Install/reconnect client to local server. No business DB restore on client.

### KDS device failure
Open KDS on replacement device and assign station. Current queue comes from server projection.

### Internet outage
No recovery action needed for core service; sync resumes later.

### Local server software failure
Restart services / restore application release while preserving PostgreSQL.

### Local server disk/hardware failure
Replace server, restore latest durable database/cloud-backed state and configuration. Define maximum acceptable data-loss window before production SLA.

## 8. Restaurant maintenance window

Avoid schema/app deployments during live lunch/dinner. Product should eventually expose maintenance state and preflight checks.

## 9. Logging

Use structured logs with correlation IDs/event IDs where possible. Logs should allow tracing:
client command → API request → domain action → DB/outbox → realtime publish.

Do not log secrets or unnecessary customer-sensitive fields.
