# Local test server — initialization versus restart

Date: 2026-09-14. Development/test use only. No real customer or fiscal data.

## Defects addressed

The prior container built and migrated correctly, but `artisan serve` failed because `backend/public/index.php` was absent. In-process API tests did not exercise the real HTTP entry point. The previous startup also invoked the demo seed on every restart, potentially resetting catalog/configuration/passwords.

## Commands from repository root

First use on a dedicated test database:

```sh
docker compose -f deploy/local-test/docker-compose.yml build backend
docker compose -f deploy/local-test/docker-compose.yml up -d postgres redis
docker compose -f deploy/local-test/docker-compose.yml run --rm backend init
docker compose -f deploy/local-test/docker-compose.yml up -d backend
```

Normal stop/start, preserving data:

```sh
docker compose -f deploy/local-test/docker-compose.yml down
docker compose -f deploy/local-test/docker-compose.yml up -d
```

Do not use `down -v`: it deletes this test installation's database volumes. Only CI may discard its isolated database this way.

The normal entry point never migrates or seeds. Schema updates require an explicit `run --rm backend migrate`, after backup and while the application is stopped. `init` initializes an empty test database transactionally; repeated initialization detects an existing demo and does not reseed. It refuses a non-demo occupied database. It is not a repair or reset command.

## Connect desktop, not demo adapter

API: `http://127.0.0.1:8000/api/v1`.
Test login: `demo@hospitality.local` / `demo1234`.
The desktop application's built-in demo is separate and does not test PostgreSQL persistence. Use the normal login form with this server address.

The Compose port is bound to loopback on purpose. This file contains known development credentials and uses PHP's development HTTP server. It is not a production/LAN deployment. No database or Redis ports are published. A subsequent controlled LAN package must provide unique credentials, server-authorized roles/devices and suitable transport/server configuration.

## Evidence requirements

`Local real-stack smoke test` must create a table/account over real HTTP, modify configuration, repeat initialization, replace the containers while preserving volumes and re-read the same service/account. Replaying the original command must not duplicate its consumption.

The workflow archives tracked source only (no working-directory secrets, tokens or `.git`) as a private repository artifact to support developer/AI handoff. Passing this test is not evidence of a physical Windows/tablet/KDS LAN test, backup restoration, WebSockets or production suitability.
