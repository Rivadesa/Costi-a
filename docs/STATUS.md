# Estado verificable del desarrollo

Actualizado: 2026-09-16. Leer junto a `AGENTS.md`. Este documento sustituye al estado antiguo de PR #7; distingue código, pruebas automáticas y validación física.

## Transición nativa .NET integrada (ADR-008/009)

El desarrollo nuevo usa el motor nativo C#/.NET + PostgreSQL + WPF bajo `dotnet/` (ADR-008). El runtime Laravel/Tauri 0.1.1 queda **congelado**: no se amplía, no se migra y no se borra hasta que el motor nativo lo sustituya con pruebas. Leer `dotnet/AGENTS.md` antes de tocar `dotnet/`.

PRs nativas integradas en `develop` (merge `0ae4e71`, 2026-09-16):

- PR #19: plan de producto Windows/multidispositivo (`docs/plans/2026-09-15-windows-multidevice-erp.md`), propuesta, no implementación.
- PR #20 (D0): dominio con agregados independientes `DiningService`/`TableOccupancy`/`SettlementAccount`, 52 escenarios de aceptación, planner legacy de solo lectura.
- PR #21 (D1.1): persistencia Npgsql con transacciones, bloqueo optimista, idempotencia por clave, outbox y auditoría; API `/api/native/v1` solo loopback con claves de rol de laboratorio.
- PR #22 (D1.2): cliente WPF con biblioteca `Costina.Client`, cuatro lecturas autenticadas nuevas; refresco manual.
- D2 (realtime, issue #24): publicador supervisado del outbox y canal SignalR con notificaciones finas y separación económica; suscripción WPF con reconexión y relectura autoritativa. Ver `docs/native/D2-realtime.md` y el CI de su PR para la evidencia ejecutada.

Evidencia ejecutada sobre el estado integrado (todas en verde):

| Comprobación | GitHub Actions |
| --- | --- |
| D0 dominio en `develop` (merge `0ae4e71`) | https://github.com/Rivadesa/Costi-a/actions/runs/35067724791 |
| D1 PostgreSQL/HTTP en `25d62f9` (contenido de #22) | https://github.com/Rivadesa/Costi-a/actions/runs/35067731398 |
| WPF desktop en `25d62f9` | https://github.com/Rivadesa/Costi-a/actions/runs/35067731433 |

Límites vigentes del motor nativo: solo loopback, claves de rol de laboratorio (no usuarios/dispositivos), realtime at-least-once sin cola durable en cliente, sin restricciones por comensal, sin instalador de producción ni backups, code-behind en WPF. Hoja de ruta: issues #25 (MVVM/restricciones/cola durable), #26 (identidad), #27 (empaquetado), #28 (PWA); #24 (realtime) cubierto por D2.

PR #23 (D1.3, instalador de ensayo por usuario, ADR-010) sigue **abierta y sin integrar**: su job `windows-installation` no tiene ejecución verde sobre su commit de cabeza.

## Integración confirmada (runtime legado, congelado)

`main` sigue reservada para versiones estables. `develop` integra el trabajo de pruebas, no una versión de producción.

- PR #14 integrada: punto de entrada HTTP de Laravel, inicialización explícita y reinicio sin resembrar datos.
- PR #15 integrada en `4e771ad07a69c9bf742d4673ac87440b84c93b17`: administración del catálogo real y aislamiento económico de servicio/KDS.
- Head de PR #15 validado: `1f1100a197dad16cd81d7a79b9f710c40e79b330`.

Evidencia de PR #15 (todas completadas con éxito):

| Comprobación | GitHub Actions |
| --- | --- |
| Backend, Laravel, PostgreSQL, autenticación e integración | https://github.com/Rivadesa/Costi-a/actions/runs/34863998883 |
| HTTP real, catálogo, reinicio, persistencia y reintento | https://github.com/Rivadesa/Costi-a/actions/runs/34863998796 |
| Vue, tests del cliente y comprobación Rust/Tauri | https://github.com/Rivadesa/Costi-a/actions/runs/34863998774 |

El fallo inicial de PR #15 comparaba el orden de claves de una respuesta JSONB, no solo su contenido. La corrección ordena las claves del objeto plano antes de comparar tipos y valores estrictamente. Se mantienen las verificaciones de no duplicar artículo, auditoría y outbox.

## Código disponible

| Área | Implementación actual | Límite importante |
| --- | --- | --- |
| Backend | Laravel 13, migraciones PostgreSQL, configuración, autenticación local y permisos por ámbito | No es fiscalidad ni ERP completo |
| Servicio | Mesa, comensales, restricciones, snapshot de menú, pases y preparaciones por estación | No todas las excepciones de dominio tienen interfaz/API |
| Fiabilidad | Transacciones, bloqueo optimista, idempotencia, auditoría y outbox persistentes | Publicador realtime/cloud y cola durable de dispositivo pendientes |
| Cliente | Vue/Tauri conectado a API real, control de servicio y KDS | Actualización por polling, no WebSockets finalizados |
| Cuenta/Caja | Superficie separada en equipo principal; consumiciones desde catálogo | Pago es registro operativo de pruebas, no factura ni datáfono |
| Catálogo | Alta/edición de presentación vendible, código, categoría existente, formato, precio EUR y disponibilidad en tarifa | No PIM, stock, gestión de categorías ni editor de nuevas tarifas |
| Separación económica | Servicio/KDS y configuración operativa sin precios; `/checkout/...` separado y protegido | Perfil de terminal es UX, no identidad física fiable de dispositivo |
| Despliegue de ensayo | Docker: Laravel/PostgreSQL/Redis; puerto del host limitado a 127.0.0.1 | Solo ensayo en un PC, sin datos reales y sin exposición LAN |

Los artículos capturan nombre, presentación/formato, precio y referencias de catálogo al incorporarse a una cuenta. Editar después la tarifa no revaloriza ese consumo.

## Corte cliente 0.1.1 — comprobar su PR y manifiesto

Cambios del corte: versión visible; selector explícito del servidor real de pruebas que limpia el ámbito de la demo; validación de dirección de API; instalador en español/inglés para el usuario actual; lanzador Windows del servidor con acciones separadas `init/start/stop/status`; manifiesto con versión, commit y SHA-256 de los ejecutables.

Los tests Node de versión/conexión y dinero pueden ejecutarse sin instalar dependencias. Los tests del lanzador usan CMD real en CI Windows con sustitutos inocuos de Docker/curl: **no prueban Docker Desktop instalado en el PC del usuario**. La compilación de Vue/Tauri y el empaquetado deben verificarse en la ejecución correspondiente antes de distribuir sus binarios.

El manifiesto de cada build es la referencia exacta de versión/commit. No identificar un binario solo por el nombre del ZIP. La comprobación local o un CI anterior no certifica un commit posterior.

## Prueba siguiente

Seguir `docs/LOCAL_TEST_0.1.1.md`. Objetivo: cliente Windows + servidor real, artículo nuevo en catálogo, selección desde Cuenta/Caja, cuenta conservada al reiniciar y control de servicio sin información económica.

Se puede ensayar con varias sesiones de cliente en el mismo PC. La prueba física con varios dispositivos en LAN sigue pendiente; no abrir el puerto de la configuración de ensayo para simular que ya existe despliegue seguro.

## Bloqueos antes de piloto operativo

1. Vinculación segura de dispositivos y permisos: una preferencia `main` no autoriza físicamente al equipo principal.
2. Separar el estado operativo del servicio del pago dentro del dominio; la separación de API/UI ya existe, pero el agregado aún contiene acoplamientos de cierre/pago.
3. Cola cliente durable, reintentos tras cerrar la aplicación y mensajes inequívocos de orden pendiente/confirmada.
4. Realtime con recuperación mediante lectura autoritativa; medir latencia y concurrencia con dispositivos físicos.
5. Administración editable de mesas, estaciones y menús; hoy se consulta configuración persistida y existen fixtures iniciales.
6. Servidor de producción, credenciales únicas, TLS local, red, backups/restauración, observabilidad y actualización/rollback.
7. Firma de distribución Windows y prueba del instalador/actualización en equipos reales.

## Fuera de este corte

VERI*FACTU/SIF, facturas, bonos, stock/bodega/PIM avanzado, WooCommerce, reservas propias, intercompany, hotel y réplica cloud productiva. No se ha retirado Verial de producción.
