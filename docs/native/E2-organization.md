# E2 — Organización editable (salas, mesas, estaciones) y tema visual del puesto principal

Fecha: 2026-09-22. Rama `feat/e2-organization` sobre E1b (`feat/e1b-schema-per-module`, PR #59). Tercer corte del **Hito 6 (núcleo del ERP)**, según `docs/plans/2026-09-22-e2-organizacion-editable.md` (decisiones delegadas por el promotor). Versión **`0.28.0-e3`**. Primera pieza del ERP con pantalla: la organización del negocio deja de ser un fixture.

## Qué cambia

**Datos (esquema versión 3, `upgrade-v3.sql`, encadenado tras la v2)**: tablas relacionales del núcleo `core.zones` (salas), `core.tables` (código estable, nombre, aforo 1–60, sala, orden, activa) y `core.stations` (código, nombre, tipo `kitchen`/`pass`/`bar`/`room`, orden, activa). Las mesas fixture de `core.configuration` pasan a `core.tables` en la sala `sala`; `configuration` queda solo con productos (hasta E3). Las estaciones se deducen de datos del núcleo: puestos emparejados y, en una instalación de demostración, las de sus menús (`cold`, `hot`, `pase`, `sala-1`). La estación **`pase`** existe siempre en cada ámbito (`init`/`upgrade`/`restore` la garantizan) y no se puede desactivar ni cambiar de tipo: es la de validación y revisión de pases (D4.3). Las migraciones son ahora **encadenadas por versión** (`InitializeAsync` aplica `upgrade-v{n}.sql` desde la versión guardada hasta la de los binarios); un binario más viejo que la base se niega.

**Reglas**: nada se borra, se **desactiva** (y se reactiva). Una mesa con ocupación viva no se desactiva: el núcleo pregunta a cada módulo activo por contrato (`IModule.TableInUseAsync`), nunca lee sus tablas (`table_in_use`). Una sala con mesas activas no se desactiva (`zone_in_use`); una mesa no se reactiva ni se crea en una sala desactivada (`zone_inactive`). Abrir un servicio exige una mesa **activa** (`table_inactive`). Códigos: 1–32 caracteres, letras, dígitos, `-` y `_`; duplicados → `duplicate_code` (409). Aprobar un puesto exige una **estación activa** de la lista (422 si no).

**API** (núcleo, solo `main`): `GET /api/native/v1/organization` (salas con sus mesas, activas e inactivas marcadas, y estaciones) y `POST /api/native/v1/organization/commands/{action}` con `zone|table|station` × `create|update|deactivate|reactivate`, cuerpo `{id, name, capacity, zoneId, sort, kind}`; misma tubería (idempotencia, auditoría `organization.*`, outbox). Sin versión esperada: datos maestros, última edición gana; el cliente relee tras cada comando. `GET /configuration.tables` sigue para todos los roles con las mesas **activas** y añade `zoneId` y `zoneName` (campos anteriores intactos).

**Puesto principal (WPF)**: pestaña **Configuración** con "Salas", "Mesas de la sala" y "Estaciones" en patrón lista + ficha (código fijo, nombre, aforo, orden, sala o tipo; "Guardar", "Desactivar / reactivar"; estados vacíos explicados). En **Puestos**, la estación se elige de las activas. **Tema visual** (`Theme.xaml`, único diccionario de la aplicación): verde bosque, crema y cobre; rojo solo para alergias y acciones irreversibles; ámbar para avisos; tipografía Segoe UI Variable; tarjetas, chips de estado con texto, botones principal/secundario/peligro/fantasma; aplicado también a Comedor y cocina, Cuenta / Caja y Puestos. Los avisos de alergia siguen con texto siempre.

**Cliente (`Costina.Client`)**: `OrganizationDto`, `ZoneDto`, `TableDto`, `StationDto`; `TableChoice` con sala opcional. Una respuesta de comando se reconoce también por el **eco de su `Idempotency-Key`** (además de `version`/`serviceId`): mismo invariante D3.4, generalizado a comandos sin versión.

Capturas (compuestas por los checks del WPF con datos leídos, sin servidor): [configuración](images/e2/configuration.png) · [comedor y cocina](images/e2/service.png) · [cuenta / caja](images/e2/checkout.png) · [puestos](images/e2/devices.png).

## Qué NO cambia

Dominio del módulo Dining, PWA (usa `/configuration` como antes; los campos nuevos no son económicos), identidad, tiempo real, copias (las tablas nuevas entran solas en la copia y en `grants.sql`), instalador. Sin plano gráfico de sala. Los productos siguen como fixture hasta E3.

## Comprobaciones automáticas

`sh tools/agent/local-tests.sh` completo: dominio **71** (+3 de organización: códigos, nombres, aforo, orden y estación de pase reservada), ClientChecks **73** (+3: lectura de organización, comando con clave persistida antes de enviar y reconocido por el eco, mesa con sala y sin ella), checks del WPF (pestaña solo `main`, formularios rellenados desde lo leído, `pase` nunca desactivable desde pantalla, ocupado y desconectado bloquean; **capturas** de las cuatro pestañas en `artifacts/desktop/*.png`), **12 suites HTTP** (nueva `organization_http.py`, 5 historias: organización de la demo y 403 para sala y cocina; altas, ediciones, desactivación/reactivación y códigos únicos; mesa en uso no desactivable por contrato del módulo; estaciones y aprobación de puestos; idempotencia y auditoría), `packaging_http.test_06e` con la base v1 real migrada **hasta v3** (sala `sala`, 8 mesas en `core.tables`, estaciones deducidas, `/configuration` con sala) y su copia v1 restaurada, Realtime 10.

## Guion de prueba manual

Instalación demo, puesto principal como `main`. 1) Pestaña **Configuración**: aparece la sala "Sala" con M1–M8 y las estaciones pase, cold, hot y sala-1. 2) "＋ Sala" → `terraza`, "Terraza" → Guardar: aparece en la lista; "＋ Mesa" en Terraza → `T1`, aforo 4 → Guardar. 3) En Comedor y cocina, el desplegable de mesas muestra "Terraza · T1". 4) Abrir T1 con el menú de ensayo; volver a Configuración y "Desactivar" T1 → el servidor lo rechaza con texto (mesa en uso). Liberar la mesa y repetir: se desactiva y desaparece del desplegable; "Reactivar" la devuelve. 5) Estación `pase`: "Desactivar / reactivar" deshabilitado. Crear `barra` (Barra); en Puestos, aprobar una solicitud eligiendo `barra`. 6) Actualizar una instalación anterior con el instalador nuevo: copia + `upgrade`; sus mesas aparecen en Configuración dentro de "Sala". 7) Aspecto: las cuatro pestañas con el tema (fondo crema, cabecera verde, botón principal cobre).

## Límites

Sin plano de sala ni reordenación por arrastre (orden numérico). Sin edición de productos (E3). La estación `pase` es un código reservado; un segundo pase tendría que llegar con E4. La PWA sigue con su aspecto de Hito 5 hasta un corte de PWA.
