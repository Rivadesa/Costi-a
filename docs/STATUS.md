# Estado del corte D0 — 2026-09-15

**Arquitectura objetivo aceptada; implementación Windows nativa todavía no creada.** ADR-008 registra .NET/WPF/PostgreSQL + Vue PWA. El código nuevo de este corte corrige la referencia PHP y prepara una migración verificable de servicio/ocupación/cuenta (ADR-009).

- Implementado en rama D0: operaciones y permisos separados, auditoría de mutaciones, cuenta accesible tras liberar mesa, índice único de ocupación, snapshots históricos y revisión de paid/pending_payment ambiguos.
- Comprobado localmente: sintaxis PHP y smoke tests de dominio/aplicación/proyecciones, más 30 comprobaciones del contrato de ciclo de vida.
- Validación PostgreSQL/HTTP del commit D0: **pendiente de CI al redactar este documento**. Consultar PR/checks y comentario de validación; no convertir pendiente en completado sin resultado.
- No hay EXE nuevo. 0.1.1 sigue siendo artefacto histórico; el cierre antiguo devuelve conflicto y no es interfaz completa del contrato D0.
- Emparejamiento de dispositivos, TLS LAN, cola durable de cliente, SignalR, backups/restauración Windows, WPF y empaquetado .NET continúan pendientes. No sustituir Verial ni usar datos reales.

## Próxima entrega

D1: solución .NET, port del circuito vertical, cliente WPF y servidor Windows con PostgreSQL en una instalación limpia sin Docker. Ver [D0](phases/D0.md) y los nuevos ADRs. El issue #17 solo se cerrará tras la verificación de código, migración y pruebas; la prueba física es otra puerta.

---

## Evidencia histórica anterior (no equivale a CI D0)

# Estado verificable del desarrollo

Actualizado: 2026-09-14. Leer junto a `AGENTS.md`. Este documento sustituye al estado antiguo de PR #7; distingue código, pruebas automáticas y validación física.

## Integración confirmada

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
