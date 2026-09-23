# E1b — Esquema por módulo: `core` y `dining`, `upgrade` v1→v2, copias multi-esquema y concesiones por módulo

Fecha: 2026-09-22. Rama `feat/e1b-schema-per-module` encadenada sobre `feat/e1-modular-architecture` (E1a, PR #58). Segundo corte del **Hito 6 (núcleo del ERP)**, cierra la parte de esquema del ADR-012. Sin funcionalidad nueva para el usuario: la base de datos toma la misma forma que el código.

## Qué cambia

**Esquemas** (versión de esquema **2**):

| Esquema | Tablas | Quién lo aporta |
| --- | --- | --- |
| `core` (antes `native_d1`) | `schema_version`, `accounts`, `commands`, `outbox`, `audit`, `configuration` (`table`, `product`), `installation`, `users`, `sessions`, `devices`, `pairings` | `Costina.Core.Persistence/schema.sql` + `grants.sql` |
| `dining` | `services`, `occupancies`, `configuration` (`menu`) | `Costina.Modules.Dining/schema.sql` + `grants.sql` (recursos embebidos; `IModule.Schema()`) |

- **La cuenta ya no depende del módulo**: `core.accounts` pierde la clave foránea a `services` y guarda **`table_id`** (la mesa, organización del núcleo, en la que el módulo la abrió; `''` si el origen no tiene mesa). `GET /checkout/accounts` lista las cuentas abiertas **sin leer ninguna tabla de módulo** (deuda de E1a saldada). El contrato JSON (`serviceId`, `tableId`, `state`) no cambia.
- **Menús** pasan a `dining.configuration` (misma forma que la del núcleo, esquema del módulo): `Unit.ModuleConfiguration<T>(módulo, kind, id)` y `DesktopReadRepository.ModuleConfiguration`. Mesas y productos siguen en `core.configuration`.
- **`upgrade`** (rol propietario) transforma una base v1 en v2 **en una sola transacción** (`upgrade-v2.sql`): renombra `native_d1`→`core`, mueve `services`/`occupancies` a `dining`, quita la FK de la cuenta y rellena `table_id` desde los servicios, traslada los menús, añade `installation.modules` y sube `schema_version` a 2. Después aplica `schema.sql` del núcleo, el de **cada módulo compilado** (activo o no: desactivar un módulo nunca hace desaparecer sus datos) y, si hay rol de ejecución, las concesiones del núcleo y de cada módulo. O todo o nada. Idempotente: repetir `upgrade` no cambia nada.
- **Arranque normal** exige versión 2 y el esquema de cada módulo compilado; con una base v1 se niega y dice que hay que ejecutar `upgrade` (el instalador de actualización ya hace copia + `upgrade`, D5.5).
- **Módulos activos en la base**: `core.installation.modules` (`text[]`; `NULL` = todos los compilados). El motor los lee al arrancar; un nombre desconocido impide arrancar. **`COSTINA_MODULES` solo acota en laboratorio/CI**; en una instalación se ignora con aviso en el log. No hay todavía comando ni pantalla para cambiarlos (E2: pantalla de Configuración); mientras tanto, solo con SQL como propietario.
- **Copias** (`BackupRunner`): `pg_dump` de `core` y de cada esquema de módulo compilado; manifiesto **formato 2** con claves `esquema.tabla`. **Las copias v1 siguen restaurándose**: `restore` verifica con su manifiesto (formato 1, claves sin esquema, un único `native_d1`) **antes** de tocar nada y después `upgrade` deja la base en v2 (el mensaje lo dice). Un manifiesto de formato desconocido se rechaza.
- `provision` reconoce como esquema propio `core` (v2) o `native_d1` (v1 sin migrar) y sigue sin adoptar nunca un esquema de otro propietario.
- Versión de paquete **`0.27.0-e2`**.

## Qué NO cambia

Dominio, contrato HTTP, clientes (WPF y PWA no se tocan), identidad, tiempo real, instalador (solo se beneficia: al actualizar hace copia y `upgrade`), TLS. Las rutas alias de E1a siguen vivas.

## Comprobaciones automáticas

`sh tools/agent/local-tests.sh` completo en local (dominio 68, ClientChecks 70, checks del WPF, 11 suites HTTP, Realtime 10) y CI. `packaging_http.py` añade dos historias:

- **`06d` módulos activos en la instalación**: con `modules='{}'` el motor arranca, `/session` publica `modules: []` y `actions: []`, `/dining/board` y su alias responden como una ruta inexistente, `/configuration` trae mesas pero no `menus`, y los datos del módulo siguen en `dining.services`; con `modules='{nope}'` no arranca (`Unknown module`); en una instalación `COSTINA_MODULES=nope` se ignora.
- **`06e` base v1 real → `upgrade`, y copia v1 → `restore`**: se construye una base con el **DDL y las concesiones v1 exactos** (`tests/http/fixtures/schema-v1.sql`, `grants-v1.sql`, que nunca cambian) y con filas reales copiadas de la batería (instalación, usuario, configuración con menú, servicio, ocupación, cuenta, comandos). Los binarios nuevos se niegan a servirla; `upgrade` la migra: quedan solo `core` y `dining`, versión 2, `accounts.table_id='M1'`, el menú en `dining.configuration`, sin FK, misma identidad de instalación; repetir `upgrade` es inocuo; el rol de ejecución lee `dining.services` y sigue sin poder borrar auditoría ni truncar el módulo; el motor arranca sobre ella con la marca demo, el tablero muestra M1 y `/checkout/accounts` lista `M1`/`Open`. De esa base v1 se hace antes un `pg_dump` de `native_d1` con un **manifiesto de formato 1** calculado por la prueba; `restore` sobre otra base vacía lo verifica, migra a v2 y la base resultante tiene, tabla a tabla, el **mismo contenido** que la migrada en sitio; una segunda restauración se rechaza.

La suite existente cubre además: manifiesto formato 2 con `core.users`, `dining.services`, `core.configuration` (11) y `dining.configuration` (1); comparación por contenido de las 15 tablas de `core` y `dining` entre original y restaurada; el rol de ejecución sin DDL en ninguno de los dos esquemas.

## Guion de prueba manual

1. **Actualización sobre una instalación existente** (0.26.0-e1 o anterior): ejecutar el instalador nuevo sobre el mismo PC. Hace copia y `upgrade`; `Costina.Server.exe status` sigue diciendo *ready* y el WPF muestra las mismas mesas, cuentas y puestos. En `psql` como propietario: `\dn` lista `core` y `dining`, no `native_d1`.
2. **Copia antigua**: coger un `.backup` + `.json` hecho por la versión anterior; en un equipo limpio `provision` con el mismo ámbito y `Costina.Server.exe restore <fichero>` → "Restored and verified … Schema upgraded from v1 to v2". Entrar con el mismo usuario.
3. **Módulos**: como propietario, `UPDATE core.installation SET modules='{}'`, reiniciar el servicio: el WPF no muestra la pestaña "Comedor y cocina" y la PWA dice que no hay módulos con pantalla. Volver a `NULL` y reiniciar.

## Límites

Sin comando ni pantalla para cambiar los módulos activos (E2). Un solo `upgrade-v2.sql`: cuando llegue v3 se encadenarán migraciones por versión. Los alias de rutas de E1a se retiran en el corte siguiente al que publique la primera PWA con rutas nuevas en una instalación real.
