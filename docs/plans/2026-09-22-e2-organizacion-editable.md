# Plan E2 — Organización editable del núcleo (salas, mesas, estaciones) y tema visual

Fecha: 2026-09-22. Tercer corte del Hito 6 (núcleo del ERP), sobre ADR-012 y tras E1a (código modular, PR #58) y E1b (esquema por módulo, PR #59). Versión de paquete `0.28.0-e3`. El promotor delegó las decisiones de este corte en el agente el 22-09-2026 ("Decide tú, para lo que tengas todo documentado y puedas seguir en cualquier momento"), con una exigencia explícita: **pantallas cuidadas, no una pantalla plana gris**.

## Objetivo

Que un negocio configure su organización **desde la pantalla del puesto principal** en vez de vivir de fixtures: salas (zonas), mesas y estaciones de trabajo. Es el bloqueo n.º 1 de `docs/STATUS.md` y lo primero que mira un comprador. Sin plano gráfico de sala (corte propio más adelante).

## Decisiones tomadas

1. **Datos en `core`** (esquema versión 3, migración `upgrade-v3.sql` encadenada tras la v2):
   - `core.zones` (`tenant,company,location,id`, `name`, `sort`, `active`).
   - `core.tables` (`id` = código estable, p. ej. `M1`; `name`; `capacity`; `zone_id`; `sort`; `active`). Sustituye a `core.configuration` con `kind='table'`: la migración crea la zona `sala` ("Sala") y mueve allí las mesas fixture con su aforo; la restricción de `kind` queda en `('product')` hasta que E3 lleve los productos a su tabla.
   - `core.stations` (`id` = código estable, p. ej. `hot`, `cold`, `pase`, `sala-1`; `name`; `kind` en `('kitchen','pass','bar','room')`; `sort`; `active`). La migración crea las estaciones que ya existen en los datos (las de las elaboraciones de los menús y las de los puestos emparejados) con `kind` deducido: `pase`→`pass`, `sala-*`→`room`, resto→`kitchen`.
2. **Reglas**: nunca se borra, se **desactiva** (la historia queda y los servicios antiguos siguen legibles). Una mesa con ocupación viva (`dining.occupancies` en `Occupied`) no se desactiva: el núcleo lo comprueba preguntando a los módulos (`IModule.TableInUseAsync`), nunca leyendo sus tablas. Códigos estables e inmutables, nombres editables. Aforo entre 1 y 60. Zona obligatoria para una mesa. La estación de un puesto emparejado se elige de la lista de estaciones activas (ya no se teclea); la validación de pase sigue exigiendo una estación de tipo `pass` (hoy solo el código `pase`, que se mantiene).
3. **API** (rol principal, misma tubería que todo: idempotencia, auditoría, outbox, una orden incierta en el cliente):
   - `GET /api/native/v1/organization` → zonas con sus mesas (activas e inactivas, marcadas) y estaciones.
   - `POST /api/native/v1/organization/commands/{action}` con `action` en `zone-create`, `zone-update`, `zone-deactivate`, `table-create`, `table-update`, `table-deactivate`, `station-create`, `station-update`, `station-deactivate` (y `*-reactivate`). Matriz rol × acción en `RouteAccess.ActionPolicy` (solo `main`).
   - `GET /configuration.tables` sigue existiendo para todos los clientes con las mesas **activas** (añade `zoneId` y `zoneName`); `GET /auth/pairings/pending` y la aprobación usan estaciones activas.
   - Eventos: `organization.zone_created`, `…updated`, `…deactivated`, y lo mismo para `table` y `station`; realtime avisa y los clientes releen.
4. **Pantalla WPF** (puesto principal): pestaña **"Configuración"** con dos secciones, **"Salas y mesas"** y **"Estaciones"**. Patrón lista + ficha: lista a la izquierda agrupada por zona (chip de estado activa/inactiva, aforo), ficha a la derecha con formulario y botones "Guardar", "Desactivar", "Reactivar" (con confirmación). Estados vacíos explicados ("Aún no hay mesas: crea la primera"). Mensajes de rechazo del servidor en lenguaje llano ("La mesa M3 está ocupada: libera la mesa antes de desactivarla").
5. **Tema visual** (`Theme.xaml`, diccionario de recursos de la aplicación, aplicado también a las pestañas existentes en este mismo corte):
   - Paleta: **verde bosque** `#1F4D3A` (principal y cabeceras), **crema** `#F6F1E7` (fondo), **cobre** `#B5673A` (acento y botón principal), grises cálidos para texto secundario, **rojo** `#8C1D1D` reservado a alergias y acciones de peligro, ámbar `#C98A1B` a avisos. Los avisos de alergia siguen con tipo, sustancia y severidad **escritos**, nunca solo por color.
   - Tipografía Segoe UI Variable: título 24 semibold, sección 16 semibold en versalitas, cuerpo 14, secundario 12.
   - Rejilla de 8 px; tarjetas con borde suave y sombra ligera; botones `Primary` (cobre), `Secondary` (borde verde), `Danger` (rojo) y `Ghost`; cabeceras de pestaña planas con subrayado; filas de lista con chips de estado; campos con etiqueta encima y foco visible.
   - Captura de cada pestaña en la PR antes de dar el corte por cerrado (verificación visual del promotor).
6. **Cliente WPF, contrato**: `Costina.Client` añade `OrganizationView` (zonas, mesas, estaciones) y `OrganizationCommand`; `Configuration.Tables` gana `ZoneId`/`ZoneName`. La PWA no cambia en E2 (usa `/configuration` como hasta ahora).
7. **Pruebas**: `organization_http.py` (altas, ediciones, desactivación rechazada con ocupación viva, códigos duplicados 409, idempotencia, 403 para sala y cocina, mesas inactivas fuera de `/configuration`), ClientChecks para el ViewModel de organización, DesktopChecks para la pestaña (solo `main`), y `packaging_http.test_06e` ampliada: la base v1 real acaba en **v3** con zona `sala`, mesas en `core.tables` y estaciones deducidas. Dominio: reglas de organización en `Costina.Core.Domain` (`Organization.cs`) con casos en `Costina.Acceptance`.
8. **Fuera de E2**: plano gráfico de sala; productos y categorías (E3); menús y pases como configuración del módulo (E4); usuarios con pantalla (E5); PWA con tema (cuando le toque un corte de PWA); reordenar por arrastre (basta `sort` numérico).

## Orden de trabajo (cada paso un commit; una PR)

1. Dominio y esquema: `Organization.cs`, `schema.sql` v3, `upgrade-v3.sql`, grants, `IModule.TableInUseAsync`, migración probada con la base v1 real (06e).
2. API: lecturas y comandos de organización; `configuration.tables` con zona; estaciones en el emparejamiento; `organization_http.py`.
3. Cliente y tema: `Theme.xaml`, pestaña Configuración con sus dos secciones, aplicación del tema a las pestañas actuales; ClientChecks y DesktopChecks; capturas.
4. Docs: `docs/native/E2-organization.md` con guion manual, `dotnet/AGENTS.md`, `docs/STATUS.md`, versión `0.28.0-e3`.

## Riesgos y cómo se cubren

- Cambiar `configuration.tables` rompe clientes con caché: los campos actuales se conservan y solo se añaden (`zoneId`, `zoneName`).
- Una estación desactivada con puestos emparejados: los puestos siguen funcionando (su estación es un dato del dispositivo); la pantalla de Puestos la muestra como "estación inactiva" y no se puede aprobar un puesto nuevo con ella.
- Migración de estaciones deducidas: si un menú fixture nombra una estación que no existe en `core.stations`, `upgrade` la crea (nunca falla por datos antiguos) y lo anota en la salida.
