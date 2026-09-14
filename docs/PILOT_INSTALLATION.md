# V1A Pilot Installation Runbook

This document describes the first internal/pilot installation of Hospitality OS. It is **not** a production deployment guide.

## 1. What is installed

The pilot is split into two pieces:

1. **Local server**: Laravel + PostgreSQL. This is the operational authority and stores restaurant data.
2. **Windows client**: Tauri/Vue desktop application. It contains no authoritative business database.

Internet is not required for normal table/KDS operation once both pieces are running on the LAN.

## 2. Pilot-data warning

The `retiro-pilot` provisioning profile deliberately contains placeholder configuration:

- 8 tables;
- 6 kitchen stations;
- 9 generic tasting-menu courses;
- placeholder menu price of EUR 150;
- generic table capacities;
- pilot user accounts.

**Do not use those values as Retiro da Costiña production data without reviewing them with the restaurant.** The profile exists to make installation and end-to-end testing reproducible.

## 3. Local server prerequisites

For the current pilot:

- PHP 8.4;
- Composer 2;
- PostgreSQL 17 (PostgreSQL 16+ should be compatible but CI validates 17);
- LAN static IP or DHCP reservation for the server.

Redis/WebSockets/cloud sync are not required for this first pilot cut; the client currently uses reliable LAN polling as fallback.

## 4. Prepare PostgreSQL

Create a database and user, for example:

```sql
CREATE USER hospitality WITH PASSWORD 'CHOOSE-A-STRONG-DB-PASSWORD';
CREATE DATABASE hospitality OWNER hospitality;
```

Do not reuse the example password text.

## 5. Configure Laravel

From `backend/`:

```bash
cp .env.example .env
composer install
php artisan key:generate
```

Set at least these values in `.env`:

```dotenv
APP_ENV=local
APP_DEBUG=false
APP_URL=http://SERVER_LAN_IP:8000

DB_CONNECTION=pgsql
DB_HOST=127.0.0.1
DB_PORT=5432
DB_DATABASE=hospitality
DB_USERNAME=hospitality
DB_PASSWORD=YOUR_DB_PASSWORD
```

Then run:

```bash
php artisan migrate --force
php artisan hospitality:provision --profile=retiro-pilot --password="A-UNIQUE-PILOT-PASSWORD"
```

The password must be at least 12 characters. The command is idempotent: running it again updates the same pilot profile instead of duplicating tables/roles/menu/users.

The command prints `tenant_id`, `company_id` and `location_id`. Copy those values into `.env`:

```dotenv
HOSPITALITY_TENANT_ID=<tenant_id>
HOSPITALITY_COMPANY_ID=<company_id>
HOSPITALITY_LOCATION_ID=<location_id>
```

Restart Laravel after changing `.env`.

## 6. Pilot users

Provisioning creates these local accounts, all using the password passed to the command:

| User | Role |
| --- | --- |
| `admin@hospitality.local` | Administrador |
| `maitre@hospitality.local` | Maître |
| `camarero@hospitality.local` | Camarero |
| `chef@hospitality.local` | Chef |
| `cocina@hospitality.local` | Cocina |

These are pilot accounts only. Before production they must be replaced by named users and a production credential policy.

## 7. Start the local API for an internal test

For a controlled pilot/test LAN:

```bash
php artisan serve --host=0.0.0.0 --port=8000
```

Verify from another machine on the LAN:

```text
http://SERVER_LAN_IP:8000/api/v1/meta
```

A production service manager/reverse proxy is intentionally deferred until target server hardware is selected. `artisan serve` is acceptable for developer/internal pilot testing, not the final restaurant deployment.

## 8. Build/download the Windows desktop client

GitHub Actions workflow: **Pilot Windows installer**.

The workflow builds both:

- MSI installer;
- NSIS EXE installer.

It uploads them as the artifact:

```text
hospitality-os-windows-pilot
```

Artifacts are retained for 14 days.

For internal builds, open the workflow run in GitHub Actions and download the artifact ZIP. Do not distribute these unsigned pilot installers publicly.

## 9. First desktop launch

Install Hospitality OS on the Windows terminal and open it.

Set the local API base URL to:

```text
http://SERVER_LAN_IP:8000/api/v1
```

Login with one of the pilot accounts. The application fetches human-readable tables, menus and kitchen stations from the local API; operators do not need to enter ULIDs.

## 10. First acceptance test

Perform this sequence with at least two client devices/screens:

1. Login as Maître/Camarero.
2. Open Mesa 1 with 2 PAX and the pilot tasting menu.
3. Add one critical allergy to PAX 2.
4. Start the service.
5. Fire the first course.
6. Login/open KDS as Cocina and mark the preparation started then ready.
7. Validate pass/ready as Chef if required by the flow.
8. Mark the course served from sala.
9. Add a beverage consumption.
10. Repeat with a multi-station course (Pescado or Carne) and confirm the course cannot become ready before all mandatory station items are ready.
11. Register payment and close the service.
12. Confirm the service timeline/audit remains coherent.

## 11. WAN-outage test

After a successful LAN test:

1. Keep the server and clients on the same local network.
2. Disconnect the Internet/WAN uplink, **not** the LAN/Wi-Fi itself.
3. Repeat course fire → KDS start/ready → serve.
4. Confirm the restaurant flow continues.

This proves the local-primary architecture. It does not yet prove local-server hardware failover.

## 12. Current limitations before live restaurant use

The pilot installer/build does **not** mean the system is production-ready. Still required before live replacement of Verial include, among others:

- signed Windows releases/updater;
- production server service manager and recovery procedure;
- automatic PostgreSQL backup and tested restore;
- WebSockets/realtime layer (polling remains the current reliable fallback);
- printer/hardware fallback strategy;
- final Retiro table/menu/station configuration;
- fiscal V1B / VERI*FACTU;
- operational training and controlled shadow service.

## 13. Security notes

- Never commit `.env`, passwords or DB credentials.
- Pilot passwords must not be reused outside this installation.
- The generated Windows pilot installer is currently unsigned; Windows may show a publisher warning.
- Server port 8000 should only be reachable from the restaurant LAN during pilot testing.
- Do not expose the Laravel pilot API directly to the public Internet.
