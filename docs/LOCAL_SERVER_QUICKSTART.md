# Quickstart — servidor real de pruebas

La vía de ensayo Windows vigente es [LOCAL_TEST_0.1.1.md](LOCAL_TEST_0.1.1.md). El lanzador está en `deploy/local-test/test-server.cmd`; necesita Docker Desktop/Compose y no es un instalador comercial del servidor.

La configuración Docker publica únicamente `127.0.0.1:8000`. No reutilizar las credenciales de ejemplo en una LAN ni abrir ese puerto a Internet.

## Alternativa para desarrolladores sin Docker

Requiere PHP 8.4, Composer 2, PostgreSQL y `pdo_pgsql`. Crear una base exclusiva de desarrollo; no apuntar a datos existentes de otro sistema. Desde `backend/`:

```sh
cp .env.example .env
composer install
php artisan key:generate
```

Configurar en `.env` la base nueva, credenciales propias y el ámbito de desarrollo:

```dotenv
DB_CONNECTION=pgsql
DB_HOST=127.0.0.1
DB_PORT=5432
DB_DATABASE=hospitality_test_local
DB_USERNAME=hospitality
DB_PASSWORD=CAMBIAR
HOSPITALITY_TENANT_ID=01DEMO00000000000000000000
HOSPITALITY_COMPANY_ID=01DEMO00000000000000000001
HOSPITALITY_LOCATION_ID=01DEMO00000000000000000002
```

Inicialización explícita, solo en local/testing:

```sh
php artisan migrate
php artisan hospitality:initialize-demo
```

Arranque normal, sin repetir la inicialización:

```sh
php artisan hospitality:check-installation
php artisan serve --host=127.0.0.1 --port=8000
```

Cliente: API `http://127.0.0.1:8000/api/v1`; ámbitos del formulario vacíos; usuario ficticio `demo@hospitality.local` y contraseña `demo1234`. Para comprobar catálogo/caja, usar Equipo principal y formulario normal, no el adaptador de demo.

No utilizar `migrate:fresh --seed` como instalación o arranque habitual. Las operaciones destructivas de CI se ejecutan únicamente en bases aisladas y desechables.

No confundir esta receta de desarrollo con el despliegue físico pendiente de LAN: TLS, credenciales por instalación, dispositivos autorizados, backup/restauración, actualizaciones y servidor HTTP de producción todavía requieren su propio corte.
