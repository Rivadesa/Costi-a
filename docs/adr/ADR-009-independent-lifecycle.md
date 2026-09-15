# ADR-009 — Servicio, ocupación y cuenta independientes (D0 / issue #17)

Estado: aceptada. Implementada en el corte D0 de referencia, pendiente de CI/validación según STATUS.

## Por qué

La versión 0.1.1 asignaba `paid`/`pending_payment` al estado del servicio al cobrar. Esto bloqueaba el siguiente pase y perdía la distinción entre mesa abierta, activa y pausada. Además, completar el servicio liberaba implícitamente la mesa al desaparecer del tablero.

## Contrato

| Eje | Estado/fuente | Acción |
| --- | --- | --- |
| Servicio | prepared/open/in_service/paused/closed/cancelled | `complete`, pausa/reanudación; `closed` significa completado operativo, no cobrado |
| Ocupación por esta visita | occupied/released; released_at | `release-table` tras completar/cancelar sin trabajo de cocina activo |
| Cuenta provisional | account_closed_at nullable; saldo derivado de consumos y pagos | `close-account` solo con saldo exactamente cero; `reopen-account` autorizado y con motivo |
| Liquidación calculada | unpaid/partially_paid/paid/overpaid | No conduce estados de cocina; saldo negativo visible solo en checkout |

La ocupación representa la visita, no limpieza/disponibilidad final de la mesa: ese flujo se añadirá cuando exista interfaz de acondicionamiento.

Pago parcial/total antes o durante un pase no cambia cocina ni ocupación. Una cuenta cerrada anticipadamente no impide servir; un consumo posterior exige reapertura explícita si la cuenta fue finalizada. Un pago por sí solo NO cierra la cuenta.

Completar no exige cobrar, no libera, no factura y no omite automáticamente pases pendientes. Completar exige servir, cancelar o saltar explícitamente todos los pases. Liberar no descuenta ni borra cargos. Las cuentas abiertas de mesas liberadas se consultan mediante `/checkout/services` paginado.

Anulación operativa no borra consumos/pagos. Se rechaza con cocina activa. No se implementan devoluciones fiscales o de medios de pago en D0. Una cuenta sobrepagada debe resolverse, no ocultar el exceso mediante `max(0, saldo)`.

## Compatibilidad

`POST /checkout/services/{id}/close` devuelve 409 `lifecycle_upgrade_required` sin mutar, en vez de cambiar silenciosamente su significado. Nuevas acciones e idempotency keys mantienen nombres distintos; las respuestas históricas almacenadas no se reescriben. Cliente 0.1.1 no es cliente completo de D0: no se distribuye un nuevo binario en este corte.

Nuevos permisos: service.complete, table.release, account.close, account.reopen, service.lifecycle.review. Los dos últimos son privilegios administrativos por defecto. No se resembran roles existentes. Perfil visual no demuestra puesto físico autorizado; el emparejamiento permanece pendiente de D2.

## Migración conservadora 000008

- Ejecutar offline, sin escritores 0.1.1/.NET concurrentes, previa copia restaurable.
- Archivar snapshot de TODOS los table_services existentes en service_lifecycle_legacy, incluyendo valores de fechas/versiones originales. No tocar pagos, consumos, auditoría, outbox ni respuestas de idempotencia históricas.
- prepared/open/in_service/paused: se conservan y siguen ocupados.
- paid/pending_payment: estado provisional paused + lifecycle_review_required=true. Un pago borró información: NO inferir estado a partir de started_at (el runtime antiguo podía escribirlo al cobrar).
- Review explícito por administrador, con motivo, elige open/in_service/paused según verificación. No reactivar automáticamente mesas antiguas. Open se rechaza si ya hay pases progresados; activo exige menú.
- closed: se conserva, ocupación released y account_closed_at igual al cierre histórico. cancelled: se conserva, liberado conforme al flujo 0.1.1; cuenta sigue independiente.
- Duplicados de ocupación, cocina activa en registros terminados o closed sin fecha: abortar transacción completa. No fusionar/borrar ni inventar fechas.
- Índice único parcial prohíbe dos ocupantes simultáneos por mesa; un choque de apertura devuelve conflicto sin consumos/huéspedes duplicados.
- Down automático se rechaza porque combinar ejes perdería información. Recuperar copia con escritores parados o realizar una corrección hacia adelante revisada.

Nuevos eventos separados: service.completed, table.released, account.closed, account.reopened, service.lifecycle_reviewed. Se registran con usuario y dispositivo en auditoría y outbox en la misma transacción. Un reintento exitoso no agrega otra auditoría/evento.

## Implementación y deuda

D0 mantiene un agregado y unidad transaccional para reducir riesgo en la referencia PHP. Se separan los ejes y proyecciones, no se afirma haber extraído módulos financieros completos. .NET debe respetar este contrato y los casos de prueba, no copiar los defectos históricos.

API adicional en `docs/api/D0-lifecycle.yaml`; validación y paso a D1 en `docs/phases/D0.md`.
