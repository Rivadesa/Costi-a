# Estado de desarrollo

Última actualización: 2026-09-14.

## Ramas

- `main`: reservado para cortes estables.
- `develop`: integración activa.
- `feat/v1a-persistence`: bloque Laravel/PostgreSQL/API validado en PR #7, pendiente de integración en `develop`.

## Estado de CI

PR #7 (`feat/v1a-persistence` → `develop`) validado correctamente con GitHub Actions y PostgreSQL 17.

La ejecución verde comprueba, en este orden:

1. sintaxis PHP de `app/`, `src/`, tests, migraciones, rutas, config y bootstrap;
2. smoke tests de dominio sin Composer;
3. tests de proyecciones KDS/service-board;
4. tests de capa Application (idempotencia/outbox);
5. `composer validate --strict`;
6. instalación completa de Laravel 13;
7. arranque de `artisan` y carga de rutas API;
8. `migrate:fresh` contra PostgreSQL real;
9. PHPUnit;
10. flujo de integración completo de un servicio hostelero contra PostgreSQL.

## Implementado y validado

### Dominio V1A

Kernel PHP independiente de Laravel:

- `TableService` como agregado principal;
- `tenant_id`, `company_id` y `location_id` explícitos;
- `TableService::open()` para creación;
- `TableService::reconstitute()` sin eventos falsos;
- comensales por posición;
- alergias, intolerancias y preferencias estructuradas;
- menú configurable y snapshot de ejecución;
- pases secuenciales;
- preparaciones por estación;
- preparación por comensal o cantidad fija;
- pase listo únicamente con preparaciones obligatorias resueltas;
- salto de pase con motivo;
- pase extra;
- sustitución de preparación en dominio (todavía no expuesta por API hasta completar guard de estación);
- pausa/reanudación;
- consumos adicionales;
- anulación no destructiva de consumos;
- pagos operativos;
- cierre/cancelación;
- eventos de dominio.

### Capa Application

Implementados contratos y servicios framework-light:

- `TableServiceRepository`;
- `MenuTemplateRepository`;
- `DiningTableRepository`;
- `KitchenStationRepository`;
- `ActiveTableServiceRepository`;
- `TransactionManager`;
- `IdempotencyStore`;
- `OutboxStore`;
- `ServiceMutationExecutor`;
- `TableServiceSetupService`;
- `TableServiceCommandService`;
- `TableServiceQueryService`;
- `OperationalReadService`.

Las mutaciones de servicio siguen el patrón:

`idempotency check → load aggregate → domain rule → optimistic save → outbox → remember result → commit`.

### Laravel 13

El backend ya es una aplicación Laravel arrancable, no solo un scaffold:

- `artisan` funcional en CI;
- bootstrap y providers;
- configuración PostgreSQL;
- configuración mínima de cache/queue/logging;
- `.env.example` local-primary;
- rutas API V1;
- respuestas JSON deterministas para conflictos de dominio, idempotencia y concurrencia.

### PostgreSQL

Migraciones reales, verificadas con `migrate:fresh`:

- tenants;
- empresas;
- localizaciones;
- usuarios/roles base;
- salas/mesas;
- estaciones de cocina;
- plantillas de menú/pases/preparaciones;
- servicios de mesa;
- snapshot de menú;
- comensales/restricciones;
- pases/items de cocina;
- consumos;
- pagos;
- idempotency keys;
- transactional outbox;
- audit log.

`backend/database/v1a-schema.sql` sigue siendo el modelo de referencia; las migraciones son ya la implementación ejecutable.

### Persistencia y concurrencia

- repositorio PostgreSQL real de `TableService`;
- rehidratación completa del agregado;
- upsert de snapshots/ejecución;
- aislamiento tenant/company/location en lecturas;
- validación de mesa por scope al abrir servicio;
- validación de menú por scope;
- validación de estación KDS por scope;
- bloqueo optimista mediante `table_services.version`;
- conflicto explícito en lugar de `last write wins`;
- Transactional Outbox en la misma transacción que la operación;
- idempotencia persistida en PostgreSQL.

### API V1A implementada

Lecturas:

- `GET /api/v1/meta`
- `GET /api/v1/service-board`
- `GET /api/v1/kds/stations/{stationId}`
- `GET /api/v1/services/{serviceId}`

Mutaciones principales:

- abrir servicio;
- asignar menú;
- añadir comensal;
- añadir restricción;
- iniciar/pausar/reanudar servicio;
- disparar siguiente pase;
- iniciar/terminar preparación;
- validar pase listo;
- marcar pase servido;
- saltar pase;
- añadir/anular consumo;
- registrar pago;
- cerrar servicio.

Todas las mutaciones HTTP deben usar `Idempotency-Key`.

Contexto provisional de desarrollo: `X-Tenant-Id`, `X-Company-Id`, `X-Location-Id`, `X-User-Id`, `X-Device-Id`, con fallback a variables de entorno para primera instalación. Esto **no sustituye autenticación**; Issue #5 debe vincular contexto a identidad/roles de servidor.

### Proyecciones

- detalle completo de servicio para comandero;
- service-board global;
- cola KDS por estación;
- restricciones críticas trasladadas al item del PAX afectado.

### Test de integración real

`backend/tests/integration.php` valida contra PostgreSQL:

`abrir mesa → asignar menú → añadir PAX → alergia crítica → iniciar → enviar pase → preparar en varias estaciones → KDS → validar listo → servir → bebida → reintento idempotente → cuenta → pago → cierre`.

También verifica:

- que la alergia crítica llega al item correcto de KDS;
- que el retry no duplica consumo;
- que se persiste una sola idempotency key;
- que el outbox recibe eventos;
- que la versión optimista avanza;
- que el servicio cerrado desaparece del service-board activo.

## Frontend

Existe scaffold Vue 3 + Vite + Tauri 2, pero todavía no está conectado a esta API real.

No existe aún `.exe` de producción.

## Issues

- #1 — Domain kernel + reconstitution: funcionalmente completado; cerrar tras integrar PR #7.
- #2 — PostgreSQL persistence/migrations/outbox: muy avanzado; falta prueba explícita de concurrencia/rollback y worker de publicación.
- #3 — Local API/projections: muy avanzado; falta test HTTP y realtime.
- #4 — Desktop/waiter/KDS realtime UX: siguiente gran bloque.
- #5 — Authentication/roles: pendiente antes de piloto real.

## Pendiente inmediato

1. integrar PR #7 en `develop`;
2. añadir prueba PostgreSQL explícita de conflicto concurrente y rollback;
3. actualizar OpenAPI con endpoints reales y códigos de error;
4. implementar autenticación/roles locales mínimos;
5. conectar Vue/Tauri a `service-board`, detalle de mesa y KDS;
6. añadir WebSockets/realtime con refetch fallback;
7. prueba LAN PC + tablet + KDS sin Internet exterior;
8. empaquetado Tauri Windows para primer piloto.

## Fuera de V1A actual

- fiscalidad/VERI*FACTU propia;
- PIM/inventario/bodega;
- multiempresa operacional de stock;
- WooCommerce/bonos;
- compras/escandallos;
- reservas propias;
- hotel/PMS;
- cloud replica productiva.

No describir esas áreas como terminadas hasta que existan código, tests y despliegue verificable.
