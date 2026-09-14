# Quickstart — servidor local para primera prueba LAN

Este procedimiento es para desarrollo/piloto técnico. No sustituye al instalador de servidor que deberá existir antes del despliegue comercial.

## Requisitos

- PHP 8.4
- Composer 2
- PostgreSQL 17
- extensión PHP `pdo_pgsql`

## Preparación

Desde `backend/`:

```bash
cp .env.example .env
composer install
php artisan key:generate
```

Crear una base PostgreSQL y configurar en `.env`:

```dotenv
DB_CONNECTION=pgsql
DB_HOST=127.0.0.1
DB_PORT=5432
DB_DATABASE=hospitality
DB_USERNAME=hospitality
DB_PASSWORD=CAMBIAR
```

Para el seed de desarrollo, fijar el ámbito local:

```dotenv
HOSPITALITY_TENANT_ID=01DEMO00000000000000000000
HOSPITALITY_COMPANY_ID=01DEMO00000000000000000001
HOSPITALITY_LOCATION_ID=01DEMO00000000000000000002
```

Después:

```bash
php artisan migrate:fresh --seed
php artisan serve --host=0.0.0.0 --port=8000
```

## Credenciales del seed

Solo para desarrollo:

- correo: `demo@hospitality.local`
- contraseña: `demo1234`

No reutilizar estas credenciales en un piloto real ni producción.

## Cliente Windows

En Hospitality OS:

1. abrir **Configuración del terminal**;
2. indicar `http://IP_DEL_SERVIDOR:8000/api/v1`;
3. iniciar sesión con las credenciales anteriores.

Si el servidor está en `192.168.1.20`, por ejemplo:

`http://192.168.1.20:8000/api/v1`

## Datos generados

`php artisan db:seed` crea un entorno reproducible con:

- tenant Retiro Demo;
- empresa y local demo;
- administrador demo;
- sala principal;
- 8 mesas;
- estaciones Fríos, Calientes, Pescados, Carnes, Postres y Pase;
- Menú Experiencia Demo;
- 7 pases y sus preparaciones;
- pescado y carne con preparación adicional en Pase para validar coordinación multiestación.

## Validación automatizada

GitHub Actions ejecuta el backend sobre PostgreSQL 17 y verifica:

- migraciones;
- PHPUnit;
- autenticación local;
- flujo completo del servicio;
- idempotencia/outbox;
- concurrencia/rollback;
- `migrate:fresh --seed`.

## Siguiente evolución

Para dejar de depender de instalación manual del backend se creará un provisionador/instalador del servidor local que gestione PostgreSQL, Laravel, Redis, workers, realtime, servicios de Windows/Linux, backups y configuración inicial.
