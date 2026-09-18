# Estado verificable del desarrollo

Actualizado: 2026-09-18. Leer junto a `AGENTS.md`. Distingue código, pruebas automáticas y validación física. La primera parte describe el motor nativo activo; la sección final conserva, claramente separado, el material del runtime legado congelado.

## Transición nativa .NET integrada (ADR-008/009)

El desarrollo nuevo usa el motor nativo C#/.NET + PostgreSQL + WPF bajo `dotnet/` (ADR-008). El runtime Laravel/Tauri 0.1.1 queda **congelado**: no se amplía, no se migra y no se borra hasta que el motor nativo lo sustituya con pruebas. Leer `dotnet/AGENTS.md` antes de tocar `dotnet/`.

PRs nativas integradas en `develop` (merge `0ae4e71`, 2026-09-16, y posteriores):

- PR #19: plan de producto Windows/multidispositivo (`docs/plans/2026-09-15-windows-multidevice-erp.md`), propuesta, no implementación.
- PR #20 (D0): dominio con agregados independientes `DiningService`/`TableOccupancy`/`SettlementAccount`, 52 escenarios de aceptación, planner legacy de solo lectura.
- PR #21 (D1.1): persistencia Npgsql con transacciones, bloqueo optimista, idempotencia por clave, outbox y auditoría; API `/api/native/v1` solo loopback con claves de rol de laboratorio.
- PR #22 (D1.2): cliente WPF con biblioteca `Costina.Client`, cuatro lecturas autenticadas nuevas; refresco manual.
- D2 (realtime, issue #24): publicador supervisado del outbox y canal SignalR con notificaciones finas y separación económica; suscripción WPF con reconexión y relectura autoritativa. Ver `docs/native/D2-realtime.md`. Evidencia: 9/9 checks en CI y guion manual completado por el promotor.
- D3.1 (issue #25, corte 1): affordances calculadas por el dominio y filtradas por rol; cliente WPF refactorizado a MVVM con habilitación mapeada del servidor. Ver `docs/native/D3.1-mvvm-affordances.md`.
- D3.2 (issue #25, corte 2): restricciones por comensal estructuradas con severidad, proyectadas por elaboración, y protocolo de acuse de cocina que bloquea validar/servir/disparar hasta el reconocimiento. Ver `docs/native/D3.2-guest-restrictions.md`.
- D3.3 (issue #25, corte 3): la orden incierta del cliente persiste cifrada (DPAPI) y sobrevive cierres forzados; al reconectar se restaura y solo admite el reintento idéntico. Ver `docs/native/D3.3-durable-pending.md` y el CI de su PR.
- PR #34: guía del laboratorio de ingeniería en Windows (`docs/native/lab-engineering.md`).
- D3.4 (corte correctivo tras la revisión externa `docs/reviews/2026-09-16-revision-seguridad-funcionamiento.md`): contexto de comando inmutable en WPF (F01), fallo cerrado y conciliación asistida ante un pendiente ilegible sin destruir evidencia (F02), almacén durable aislado por instalación/ámbito/rol/ventana (F03), rechazos definitivos solo con eco de la clave y código reconocible (F04), versión única `0.7.0-d3.4` desde los csproj (F08). Ver `docs/native/D3.4-client-hardening.md` y el CI de su PR. **Requiere repetir `init-lab` una vez** (tabla `native_d1.installation`). PR #35.
- D3.5 (issue #36, F05): reconocer un cambio de restricción obliga a revisar cada elaboración enviada afectada y decidir (no afecta / adaptar / rehacer, con nota); `remake` retira la validación del pase. Ver `docs/native/D3.5-restriction-review.md` y el CI de su PR (#38).
- D3.6 (issue #37, F07): una cuenta cerrada anticipadamente se reabre con motivo auditado (`reopen`, solo main) y admite cambios; `refund` devuelve solo crédito existente; consumos a mayores desde sala con `add-consumption` sin importes en la respuesta. Ver `docs/native/D3.6-account-reopen-refund.md` y el CI de su PR.

Evidencia ejecutada sobre el estado integrado hasta D3.3 (todas en verde):

| Comprobación | GitHub Actions |
| --- | --- |
| D0 dominio en `develop` (merge `0ae4e71`) | https://github.com/Rivadesa/Costi-a/actions/runs/35067724791 |
| D1 PostgreSQL/HTTP en `25d62f9` (contenido de #22) | https://github.com/Rivadesa/Costi-a/actions/runs/35067731398 |
| WPF desktop en `25d62f9` | https://github.com/Rivadesa/Costi-a/actions/runs/35067731433 |
| PR #33 (D3.3): D0, D1 y WPF | https://github.com/Rivadesa/Costi-a/actions/runs/35081131708 · 35081131717 · 35081131715 |

Límites vigentes del motor nativo: solo loopback, realtime at-least-once, sin instalador de producción ni backups (Hito 4 en curso; servicio de Windows desde D5.2 y HTTPS en LAN desde D5.3, solo en modo instalación).

## Hoja de ruta y pendientes

- #25 completa con D3.1–D3.3 (+ D3.4 correctivo); **pendiente de cierre con evidencia física**: guiones manuales de D3.2, D3.3 y D3.4 en el laboratorio (los ejecuta el equipo de laboratorio).
- Hallazgos de la revisión externa: **F05** → D3.5 (#36); **F07** → #37 (cuentas cerradas anticipadamente admiten reapertura auditada y devoluciones desde el PC principal; los comanderos solo marcan platos y consumos); **F06** (identidades y transporte de laboratorio) → #26 y #27. Deuda de mantenimiento señalada: lock de dependencias (#13), protección efectiva de ramas, audiencia explícita por tipo de evento, N+1 en `Board()`.
- Siguientes hitos: #26 identidad (usuarios, dispositivos, QR, tokens; retirada de `COSTINA_KEY_*`), #27 empaquetado, #28 PWA; #24 (realtime) cubierto por D2.
- D4.1 (issue #26, corte 1): usuarios con contraseña (PBKDF2, alta por CLI sin credenciales por defecto) y sesiones con token de corta vida, renovación deslizante, tope absoluto, logout y revocación; actor de auditoría real. Claves de laboratorio como respaldo hasta D4.3. Ver `docs/native/D4.1-users-sessions.md` y el CI de su PR.

- D4.2 (issue #26, corte 2): dispositivos como identidad separada, emparejados por código de un solo uso (5 min) aprobado por main con rol y estación; secreto entregado una única vez, solo hashes en reposo; revocación inmediata que aborta también las conexiones SignalR vivas. Ver `docs/native/D4.2-device-pairing.md` y el CI de su PR.

- D4.3a (issue #26, corte 3 servidor): claves de laboratorio retiradas (solo sesiones de usuario y dispositivos emparejados), matriz rol×estación en cocina espejada en affordances, billete efímero de un solo uso para el hub (preparación PWA) y suites migradas a identidades reales. El WPF necesita D4.3b para volver a conectar. Ver `docs/native/D4.3a-station-authz.md` y el CI de su PR.

- D4.3b (issue #26, cierre en código): el WPF entra por usuario+contraseña o como puesto emparejado (token DPAPI), empareja desde la pantalla de conexión y administra puestos (generar código, aprobar con rol y estación, revocar con expulsión inmediata). Ver `docs/native/D4.3b-wpf-login.md` y el CI de su PR.

- D5.1 (issue #27, corte 1 del Hito 4): roles de PostgreSQL separados (superusuario solo en `provision`, `costina_owner` para `init`/`upgrade`, `costina_runtime` sin DDL ni borrado para el motor), ciclo de vida explícito, modo `installation` que se niega a servir con una conexión sobreprivilegiada, separación programa/datos y configuración por fichero; toda la batería HTTP corre ya con el rol de ejecución. Ver `docs/native/D5.1-packaging-base.md`, ADR-011 y el CI de su PR.

- D5.2 (issue #27, corte 2): `install-service`/`uninstall-service` registran el motor como servicio de Windows bajo la cuenta virtual `NT SERVICE\Costina` (arranque automático retardado sin sesión de usuario, recuperación ante caída, ACL que deja `owner.json` fuera del alcance del motor), log en fichero, líneas de petición fuera del log (el billete del hub viaja en la query) y `GET /diagnostics` solo main. Probado en CI sobre un servicio real. Ver `docs/native/D5.2-windows-service.md` y el CI de su PR.

- D5.3 (issue #27, corte 3; transporte de F06): HTTPS en la LAN con una CA local de la instalación con restricciones de nombre críticas (`provision-tls`/`renew-tls`), HTTP solo en loopback, regla de firewall sin perfil público, freno de intentos de login/emparejamiento (429) y cliente WPF que acepta `https://NOMBRE:PUERTO` con validación normal de la cadena. Validado en CI con OpenSSL y con SChannel. Ver `docs/native/D5.3-lan-https.md` y el CI de su PR.

Hito 3 (#26): completo en código con D4.3b (PR #43); **su cierre espera el guion manual del promotor**. Hito 4 (#27) troceado en D5.1 base → D5.2 servicio de Windows → D5.3 HTTPS en LAN (CA local) → D5.4 backup/restauración → D5.5 instalador → D5.6 guion físico.

PR #23 (D1.3, instalador de ensayo por usuario, ADR-010) queda **cerrada como superada**: nunca se integró y su base no incluía D2–D4. Lo aprovechable (roles separados, PostgreSQL 17.11 fijado con hash, NSIS, datos fuera del programa) se rehace sobre `develop` en D5.1–D5.5 bajo ADR-011.

## Bloqueos antes de piloto operativo (motor nativo)

1. Vinculación segura de dispositivos y permisos por estación (#26): un perfil `main` no autoriza físicamente al equipo principal.
2. Servidor Windows como servicio, TLS local, LAN, backups/restauración, observabilidad y actualización/rollback (#27).
3. Comanderos y KDS en tablets/móviles (#28) y pruebas físicas de latencia/concurrencia.
4. Verificación física de D3.4–D3.6 en el laboratorio y decisión sobre devoluciones parciales por medio de pago.
5. Administración editable de mesas, estaciones y menús (hoy fixtures de laboratorio).
6. Migración/integración del legado (#17) con datos reales; no cargar datos reales sobre fixtures.

## Fuera del alcance actual

VERI*FACTU/SIF, facturas, bonos, stock/bodega/PIM avanzado, WooCommerce, reservas propias, intercompany, hotel y réplica cloud productiva. No se ha retirado Verial de producción.

---

## Runtime legado 0.1.1 (congelado) — material histórico

Todo lo que sigue describe el runtime Laravel/Tauri **congelado**. No es el estado activo ni instrucciones de trabajo; se conserva para trazabilidad de PRs #14–#16 y de la prueba local 0.1.1.

`main` sigue reservada para versiones estables. `develop` integra el trabajo de pruebas, no una versión de producción.

- PR #14 integrada: punto de entrada HTTP de Laravel, inicialización explícita y reinicio sin resembrar datos.
- PR #15 integrada en `4e771ad07a69c9bf742d4673ac87440b84c93b17`: administración del catálogo real y aislamiento económico de servicio/KDS. Head validado: `1f1100a197dad16cd81d7a79b9f710c40e79b330`.

Evidencia de PR #15 (todas completadas con éxito):

| Comprobación | GitHub Actions |
| --- | --- |
| Backend, Laravel, PostgreSQL, autenticación e integración | https://github.com/Rivadesa/Costi-a/actions/runs/34863998883 |
| HTTP real, catálogo, reinicio, persistencia y reintento | https://github.com/Rivadesa/Costi-a/actions/runs/34863998796 |
| Vue, tests del cliente y comprobación Rust/Tauri | https://github.com/Rivadesa/Costi-a/actions/runs/34863998774 |

El fallo inicial de PR #15 comparaba el orden de claves de una respuesta JSONB, no solo su contenido. La corrección ordena las claves del objeto plano antes de comparar tipos y valores estrictamente.

### Código legado disponible

| Área | Implementación | Límite importante |
| --- | --- | --- |
| Backend | Laravel 13, migraciones PostgreSQL, configuración, autenticación local y permisos por ámbito | No es fiscalidad ni ERP completo |
| Servicio | Mesa, comensales, restricciones, snapshot de menú, pases y preparaciones por estación | No todas las excepciones de dominio tienen interfaz/API |
| Fiabilidad | Transacciones, bloqueo optimista, idempotencia, auditoría y outbox persistentes | Publicador realtime/cloud y cola durable de dispositivo pendientes |
| Cliente | Vue/Tauri conectado a API real, control de servicio y KDS | Actualización por polling, no WebSockets finalizados |
| Cuenta/Caja | Superficie separada en equipo principal; consumiciones desde catálogo | Pago es registro operativo de pruebas, no factura ni datáfono |
| Catálogo | Alta/edición de presentación vendible, código, categoría existente, formato, precio EUR y disponibilidad en tarifa | No PIM, stock, gestión de categorías ni editor de nuevas tarifas |
| Separación económica | Servicio/KDS y configuración operativa sin precios; `/checkout/...` separado y protegido | Perfil de terminal es UX, no identidad física fiable de dispositivo |
| Despliegue de ensayo | Docker: Laravel/PostgreSQL/Redis; puerto del host limitado a 127.0.0.1 | Solo ensayo en un PC, sin datos reales y sin exposición LAN |

### Corte cliente 0.1.1 (legado)

Versión visible; selector explícito del servidor real de pruebas; validación de dirección de API; instalador en español/inglés para el usuario actual; lanzador Windows del servidor con acciones `init/start/stop/status`; manifiesto con versión, commit y SHA-256 de los ejecutables. Los tests del lanzador usan CMD real en CI Windows con sustitutos inocuos de Docker/curl: **no prueban Docker Desktop instalado en el PC del usuario**. El manifiesto de cada build es la referencia exacta de versión/commit; no identificar un binario solo por el nombre del ZIP. Guía de prueba: `docs/LOCAL_TEST_0.1.1.md`.
