# E1a — Arquitectura modular en el código: núcleo, contrato de módulo y el módulo Dining

Fecha: 2026-09-22. Rama `feat/e1-modular-architecture` sobre `develop` (`fe5ee70`). Primer corte del **Hito 6 (núcleo del ERP)**, aplica el ADR-012. **Sin funcionalidad nueva ni cambio de esquema**: reorganización del código con la batería actual como red de seguridad. La parte de esquema (`core`/`dining` por `upgrade`, copias y `grants.sql` por módulo) es **E1b**.

## Qué cambia

**Proyectos .NET** (antes: `Costina.Domain`, `Costina.Persistence`, `Costina.Server`; el dominio de Costiña y la plataforma juntos):

| Proyecto | Contenido | Referencia a |
| --- | --- | --- |
| `Costina.Core.Domain` | `SettlementAccount` (cuenta: cargos, cobros, cierre, reapertura, devolución), primitivas (`BusinessScope`, `CommandStamp`, `DomainEvent`, `Aggregate`, `RuleViolation`) | — |
| `Costina.Modules.Dining.Domain` | `DiningService`, `TableOccupancy`, pases, elaboraciones, restricciones, snapshots | `Core.Domain` |
| `Costina.Core.Persistence` | `PostgresStore` y la `Unit` (transacción, idempotencia, auditoría, outbox), identidad, dispositivos, copias, esquema y concesiones; `TableDefinition`/`ProductDefinition` (configuración del núcleo); `CommandGuards` | `Core.Domain` |
| `Costina.Core.Hosting` | **`IModule`** (contrato de módulo), `ModuleHost`, `ModuleRegistry`, `RouteAccess` (+ `ActionPolicy`), `Requests` (tubería común: identidad, JSON, escritura idempotente) | `Core.Persistence` |
| `Costina.Modules.Dining` | `DiningModule : IModule`, `DiningOperations` (abrir, comandos, consumo, liberar), `Affordances` (matriz rol × estación), `DiningUnit` (acceso a `services`/`occupancies` como extensiones de la `Unit`), `MenuDefinition` | `Core.Hosting`, `Dining.Domain` |
| `Costina.Server` | Host: arranque, ciclo de vida, middleware de identidad y autorización, rutas del **núcleo** (identidad, `/session`, `/configuration`, `/catalog`, `/checkout/*`, `/commands/{key}`, diagnóstico, hub), `CheckoutOperations`, fixtures del núcleo; **raíz de composición**: es el único que conoce los módulos | `Core.Hosting`, `Modules.Dining` |

Regla de dependencias (ADR-012): un módulo referencia el núcleo; el núcleo **nunca** referencia un módulo; `Program.cs` ya no conoce `fire-next`.

**Contrato de módulo** (`IModule`): nombre estable, acciones de sesión por rol, claves que añade a `GET /configuration`, fixtures demo (dentro del **mismo** comando idempotente `lab-fixtures-v1`: la marca de demo de D5.6 no cambia) y rutas. El host activa los módulos compilados (`COSTINA_MODULES` acota; E1b lo lleva a la base) y los compone: `/session` publica **`modules`** y las acciones de sesión se calculan por módulo.

**Autorización por acción**: la matriz rol × acción que el middleware aplicaba con literales de cocina/sala vive ahora en la ruta del módulo (`RouteAccess.ActionPolicy = Affordances.Allows`): **la misma función** que filtra las affordances anunciadas. Ninguna acción anunciada puede dar 403 y ninguna no anunciada pasa.

**Rutas**: las de Dining pasan a `/api/native/v1/dining/…` (`board`, `services`, `services/{id}`, `services/{id}/commands/{action}`, `occupancy/{id}/release`). Las rutas anteriores siguen respondiendo **como alias, idénticas y con la misma autorización**, durante una versión (un dispositivo con la PWA en caché no debe romperse). WPF, PWA, tests HTTP, E2E y checks del instalador usan ya las nuevas.

**Clientes**: `SessionInfo.Modules` (WPF) y `Session.modules` (PWA). La pestaña "Comedor y cocina" existe solo si `dining` está activo; sin él el WPF no lee el tablero y `Configuration.Menus` puede faltar. La PWA monta comandero y cocina solo con `dining` y, si no, lo dice.

**Versión** `0.26.0-e1`: los cortes del Hito 6 se numeran `-eN` (E de ERP), como los anteriores `-dX.Y`; la comprobación de formato de `desktop_reads.py` admite una letra de hito.

## Qué NO cambia

Esquema (`native_d1` sigue siendo el único esquema hasta E1b), contrato de datos, dominio, instalador, copias, PWA y WPF salvo lo dicho. `DesktopReadRepository.OpenAccounts` sigue uniendo `accounts` con `services` para mostrar la mesa: **deuda para E1b** (la cuenta del núcleo no debe leer una tabla del módulo; la referencia a la mesa se guardará en la cuenta).

## Comprobaciones automáticas

Todo en local (`sh tools/agent/local-tests.sh`), **sin cambiar ninguna aserción salvo rutas y el formato de versión**:

- Dominio 68/68 (solución `Costina.slnx` con los dos proyectos de dominio), `ClientChecks` 70, `PersistenceChecks`, checks del WPF (+ `HasDining` según `session.modules`), `RealtimeChecks` 10.
- Batería HTTP completa con el rol de ejecución. **Antes de tocar los tests se pasó entera contra las rutas antiguas**: prueba de que los alias son idénticos. `desktop_reads.py` añade: `/dining/board` ≡ `/board` y `/dining/services/{id}` ≡ `/services/{id}`; cocina recibe 403 por los dos caminos; `/session` lista `modules: ["dining"]` con las acciones por rol; `/configuration` trae la clave `menus` aportada por el módulo.
- vitest 60, build de la PWA. Playwright (CI): los tres specs con las rutas nuevas.

## Guion de prueba manual

Instalación demo. 1) Todo igual que antes: el WPF muestra las tres pestañas y la PWA el comandero o la cocina; en el WPF, la pestaña "Comedor y cocina" sigue ahí porque `/session` lista `dining`. 2) En `config/server.json` poner `"COSTINA_MODULES": "nada"` y reiniciar el servicio: **se niega a arrancar** y el log dice `Unknown module in COSTINA_MODULES`. Quitar la clave. 3) Hasta E1b no hay forma soportada de desactivar Dining en una instalación (la lista de módulos activos pasa a la base y a la pantalla de Configuración).

## Límites

Un solo esquema todavía (E1b). Sin licencia ni activación por pantalla (E1b/hito de licencia). Los alias de compatibilidad se retiran en el corte siguiente al que publique la primera PWA con rutas nuevas en una instalación real.
