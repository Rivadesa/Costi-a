# Local real-backend test stack

This stack is the fastest route from the standalone Tauri demo to a **real Laravel + PostgreSQL + Redis backend** on one development PC.

It is for internal testing/pilot preparation. It is **not** the final production deployment model.

## Requirements

- Docker Desktop with Docker Compose.
- Ports `8000` free on the host.
- Hospitality OS Windows client built from `develop`.

## Start

From the repository root:

```bash
docker compose -f deploy/local-test/docker-compose.yml up --build -d
```

The first build installs PHP 8.4 dependencies, creates the PostgreSQL schema, applies migrations and loads the Retiro Demo seed.

API:

```text
http://127.0.0.1:8000/api/v1
```

Smoke check:

```text
http://127.0.0.1:8000/api/v1/meta
```

## Seeded context

- Tenant: `01DEMO00000000000000000000`
- Company: `01DEMO00000000000000000001`
- Location: `01DEMO00000000000000000002`
- User: `demo@hospitality.local`
- Password: `demo1234`

The seed includes:

- 8 restaurant tables;
- kitchen stations;
- one tasting menu;
- restaurant sale categories;
- waters;
- wines by the glass;
- wines by the bottle;
- other drinks/extras;
- default `Tarifa Restaurante` price list.

## Connect the Windows client

On the login screen open **Configuración del terminal** and choose:

- **Equipo principal** to access service control, KDS and `Cuenta / Caja`;
- **Sala / maître** for operational table tracking only;
- **Cocina / KDS** for kitchen only.

Set:

```text
Servidor API: http://127.0.0.1:8000/api/v1
Empresa: 01DEMO00000000000000000001
Local: 01DEMO00000000000000000002
```

Then sign in with the seeded credentials.

## Important financial boundary

`Control de servicio` and KDS deliberately do **not** expose prices, provisional account or payments.

`Cuenta / Caja` is available only on the main-terminal UI and remains protected by server permissions. Consumptions are selected from the active restaurant catalog. The server resolves the active price list and snapshots name/price at the time the item is added.

## Stop

```bash
docker compose -f deploy/local-test/docker-compose.yml down
```

To also destroy the local test database and Redis data:

```bash
docker compose -f deploy/local-test/docker-compose.yml down -v
```

## Current limitations

- Uses Laravel's development HTTP server; production local-server packaging will use a supervised service/reverse proxy.
- Queue execution is `sync` in this test stack; Redis is present for cache and future queue/realtime work.
- Cloud replication is not enabled yet.
- Fiscal/VERI*FACTU is not implemented in V1A.
