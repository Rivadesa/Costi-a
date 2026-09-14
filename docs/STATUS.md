# Estado de desarrollo

Última actualización: 2026-09-14.

## Rama activa

- `develop`: desarrollo e integración activa.
- `main`: reservado para cortes estables; todavía no representa V1A terminada.

## Implementado en `develop`

### Documentación / fuente de verdad

Existe documentación autosuficiente para incorporar otras IAs/desarrolladores sin depender del chat original:

- `README.md`
- `AGENTS.md`
- `CONTRIBUTING.md`
- `docs/AI_CONTEXT.md`
- `docs/PRODUCT_SCOPE.md`
- `docs/ARCHITECTURE.md`
- `docs/DOMAIN_MODEL.md`
- `docs/WORKFLOWS.md`
- `docs/UX_PRINCIPLES.md`
- `docs/API_CONVENTIONS.md`
- `docs/DATA_AND_SYNC.md`
- `docs/DEVELOPMENT.md`
- `docs/TESTING.md`
- `docs/SECURITY.md`
- `docs/DEPLOYMENT.md`
- `docs/GLOSSARY.md`
- `docs/DECISION_LOG.md`
- `docs/BACKLOG.md`
- `docs/HANDOFF_CHECKLIST.md`
- ADR-001 ... ADR-006.

### Dominio V1A

Implementado como kernel PHP desacoplado de Laravel:

- `TableService` como agregado principal.
- contexto tenant/company/location explícito.
- creación mediante `TableService::open()`.
- rehidratación mediante `TableService::reconstitute()` sin generar eventos falsos.
- menú como plantilla y ejecución/snapshot por servicio.
- comensales por posición.
- alergias/intolerancias/preferencias estructuradas.
- pases secuenciales.
- preparaciones independientes por estación.
- preparación por comensal o cantidad fija.
- validación de pase únicamente con todas las preparaciones obligatorias listas/canceladas de forma válida.
- sustitución de una preparación sin modificar la plantilla maestra.
- salto de pase con motivo.
- pase extra sin modificar plantilla.
- pausa/reanudación del ritmo.
- cambio de mesa.
- consumos adicionales.
- anulación no destructiva de consumos.
- pago operativo.
- cierre/cancelación de servicio.
- eventos de dominio.

### Primitivas compartidas

- ULID.
- DomainEvent.
- enums persistibles para estados/tipos/severidades.

### Proyecciones de aplicación

- cola KDS por estación (`KdsProjector`).
- restricciones críticas trasladadas al item del comensal afectado.
- vista global de servicio (`ServiceBoardProjector`).

### Persistencia diseñada

- `backend/database/v1a-schema.sql` como esquema PostgreSQL de referencia.
- tenant/company/location/usuarios/roles/salas/mesas/estaciones.
- menu templates y snapshots de servicio.
- guests/restrictions/courses/course-items.
- consumptions/payments.
- idempotency keys.
- transactional outbox.
- audit log.
- separación explícita entre operación y futura fiscalidad.

El SQL es **modelo de referencia**; todavía debe convertirse en migraciones Laravel reales.

### API

- `docs/api/V1A-api.yaml` define el contrato OpenAPI inicial.
- comandos explícitos para transiciones de servicio/pase/preparación.
- `Idempotency-Key` requerido en mutaciones reintentables.
- proyecciones KDS y service-board definidas.

### Tests

- PHPUnit configurado en `backend/`.
- tests de invariantes principales de `TableService`.
- smoke runner sin Composer (`backend/tests/run.php`).
- test de proyecciones sin Composer (`backend/tests/projections.php`).
- GitHub Actions configurado para lint + smoke + Composer + PHPUnit.

CI detectó inicialmente que faltaba declarar la licencia del paquete; corregido a `proprietary`. Se está validando la siguiente ejecución antes de considerar CI verde.

### Frontend

- scaffold Vue 3 + Vite existente.
- scaffold Tauri 2 para aplicación de escritorio Windows.
- rutas/superficies iniciales de operador preparadas.

Aún **no** existe una aplicación de escritorio de producción compilada ni conectada a un backend Laravel real.

## Issues de ejecución

- #1 — Complete domain kernel and reconstitution path.
- #2 — PostgreSQL persistence, migrations and transactional outbox.
- #3 — Local API commands and operational projections.
- #4 — Operator desktop, waiter and KDS realtime UX.
- #5 — Local authentication, roles and permissions.

## Pendiente inmediato — orden recomendado

1. Terminar Issue #1 y dejar CI de dominio verde.
2. Crear capa `Application` framework-light: repository/transaction/idempotency/outbox + command handlers.
3. Materializar aplicación Laravel real alrededor del kernel.
4. Convertir `v1a-schema.sql` en migraciones Laravel y repositorio PostgreSQL/Eloquent/DBAL.
5. Implementar transacción + idempotencia + outbox y sus tests de integración.
6. Implementar endpoints OpenAPI locales.
7. Añadir autenticación/roles mínimos.
8. Conectar Vue/Tauri al API real.
9. Implementar realtime LAN + refresh fallback.
10. Primera prueba en LAN con PC + tablet + KDS y corte de Internet exterior.

## No implementado todavía

- Laravel completo ejecutable.
- migraciones Laravel reales.
- repositorio PostgreSQL real.
- Redis/outbox worker real.
- WebSockets reales.
- autenticación real.
- `.exe` de producción.
- fiscalidad/VERI*FACTU.
- inventario/PIM/multiempresa operacional.
- WooCommerce/bonos.
- reservas propias.

No describir estas áreas como terminadas hasta que existan código, tests y/o despliegue verificable.
