# ADR-012 — Núcleo del ERP y módulos: qué es cada cosa y cómo se enchufan

Fecha: 2026-09-22. Estado: **aceptado por el promotor el 2026-09-22** (Hito 6, corte E1). Decisiones tomadas al aceptarlo: esquema PostgreSQL por módulo desde E1b; las rutas de Dining se mueven a `/dining/…` con alias de compatibilidad durante una versión. Desarrolla el §6 de `docs/ARCHITECTURE.md` (monolito modular) y el §5 del plan `docs/plans/2026-09-15-windows-multidevice-erp.md`; no sustituye ningún ADR vigente. ADR-007 (separación operativa/económica), ADR-009 (ciclos de vida independientes) y ADR-011 (instalación y roles) siguen intactos y este ADR los da por supuestos.

## Contexto

El producto se concibió modular desde el principio (`README.md`, `docs/AI_CONTEXT.md`, `docs/ARCHITECTURE.md` §6) y la primera implantación es Retiro da Costiña. Lo construido en los hitos 0–5 es **un módulo** — el servicio gastronómico por pases: mesas, comensales, restricciones, pases, estaciones, cuenta por mesa — sobre una **infraestructura de plataforma** completa y probada: identidad de usuarios y dispositivos, tiempo real, orden incierta durable, auditoría, idempotencia, instalador con PostgreSQL, servicio de Windows, HTTPS en LAN, copias con restauración verificada, PWA alojada.

Lo que falta es el **núcleo del ERP**: lo que un comprador mira primero y de lo que cuelgan los módulos — configuración editable del negocio, usuarios y permisos con pantalla, catálogo y tarifas, caja, fiscal, informes. Hoy mesas, estaciones, menús y productos son fixtures (`native_d1.configuration`, JSONB por `kind`), los usuarios se crean por CLI y el código no tiene fronteras de módulo: un proyecto por capa (`Costina.Domain`, `Costina.Persistence`, `Costina.Server`) con el dominio de Costiña y la plataforma juntos.

El promotor quiere vender el producto a terceros. Antes de construir el núcleo hay que fijar qué es núcleo, qué es módulo, cómo se comunican y cómo se activan — y reorganizar el código actual en esa forma, para que Costiña quede como módulo enchufable y no como centro.

## Decisiones

### 1. Reparto: núcleo y módulos

**Núcleo** (`Core`): lo que todo negocio necesita y ningún módulo puede sustituir.

| Área del núcleo | Contenido | Estado hoy |
| --- | --- | --- |
| **Plataforma** | Instalación y ciclo de vida, esquema y roles de PostgreSQL, comandos idempotentes, auditoría, outbox y tiempo real, copias y restauración, TLS, servicio de Windows, alojamiento de la PWA, diagnóstico | Hecho (hitos 1–5) |
| **Identidad y permisos** | Usuarios, sesiones, dispositivos emparejados, roles, estación; **pantalla de administración** | Motor hecho; pantalla de usuarios pendiente (E5) |
| **Organización** | Grupo/tenant, empresa (entidad jurídica), establecimiento; **zonas y mesas**; **estaciones de trabajo** (cocina, barra…) | Fixtures → editable (E2) |
| **Catálogo y tarifas** | Categorías, productos, presentaciones vendibles, impuestos, tarifas con vigencia; precio congelado al consumir | Fixtures → editable (E3) |
| **Ventas / Cuenta / Caja** | Cuenta por servicio (cargos, cobros, cierre, reapertura, devolución) — hoy `SettlementAccount`; caja: apertura/cierre, arqueo, turnos, medios de pago, tickets | Cuenta hecha; caja pendiente (Hito 7) |
| **Fiscal** | Series, facturas simplificadas y completas, rectificativas, VERI*FACTU | Pendiente (Hito 8) |
| **Informes** | Ventas, ocupación, productos, por periodo y empresa | Pendiente |
| **Licencia** | Activación por instalación y por módulo (offline, fichero firmado) | Pendiente (hito propio) |

**Módulos** (`Modules.*`): capacidades de un tipo de negocio o de una integración. Un módulo puede no estar instalado y el núcleo sigue completo.

| Módulo | Contenido | Estado |
| --- | --- | --- |
| **Dining** — servicio gastronómico por pases | Menús con pases y elaboraciones por estación, ejecución del servicio por mesa (`DiningService`, `TableOccupancy`), restricciones por comensal y su revisión, KDS por estación, comandero y cocina en la PWA | **Hecho** (Costiña) |
| Bar / carta libre | Pedido por líneas sin pases, barra, para llevar | Futuro |
| Reservas | Motor propio y conectores (TheFork/CoverManager) | Futuro |
| Inventario y bodega | Movimientos, ubicaciones, propietario, botellas abiertas | Futuro (V1C) |
| Compras | Proveedores, pedidos, recepciones, costes | Futuro |
| Bonos y ecommerce | Vales, WooCommerce | Futuro (V1D) |
| Hotel/PMS, intercompany, BI | | Futuro |

Regla de reparto: **si dos tipos de negocio distintos lo necesitarían igual, es núcleo; si depende de cómo se sirve, se vende o se abastece un negocio concreto, es módulo.** El promotor puede mover un área de columna con un ADR breve; la caja y el fiscal quedan en el núcleo porque cualquier negocio vende y factura.

### 2. Un proceso, una base: monolito modular de verdad

- **Un solo motor** (`Costina.Server.exe`), **una base**, un instalador, una versión de producto. Los módulos **no** se despliegan por separado (ARCHITECTURE §6): se distribuyen con el producto y se **activan** por instalación.
- **Fronteras en el código**: cada módulo es un proyecto .NET propio (`Costina.Modules.Dining`) y el núcleo se reparte en `Costina.Core.*`. Un módulo referencia el núcleo; el núcleo **nunca** referencia un módulo; los módulos no se referencian entre sí (si dos necesitan hablar, lo hacen por contratos del núcleo o por eventos).
- **Fronteras en la base**: un esquema PostgreSQL por módulo (`core`, `dining`, …). Un módulo **solo escribe en su esquema**; lo que necesita del núcleo lo pide por contrato (§3). `grants.sql` se compone por módulo (cada uno aporta sus concesiones al rol de ejecución) y la regla de ADR-011 sigue: tabla nueva ⇒ concesión explícita.
- **Ámbito**: toda tabla de negocio sigue llevando `tenant/company/location`; los módulos no inventan otro.

### 3. Cómo se enchufa un módulo (contrato de módulo)

Cada módulo implementa un registro (`IModule`) que declara, y el motor compone al arrancar:

1. **Identidad**: nombre estable (`dining`), versión mínima de núcleo, dependencias de otros módulos (ninguna por ahora).
2. **Esquema**: sus scripts de creación/actualización y sus concesiones; el núcleo los aplica en `init`/`upgrade` en orden de dependencia, dentro de la misma transacción y con `schema_version` por módulo.
3. **Rutas**: sus endpoints bajo `/api/native/v1/<módulo>/…`, con la misma autenticación, ámbito, idempotencia (`Idempotency-Key`, eco, `commands`), auditoría y outbox del núcleo. Las rutas actuales de Dining se mueven a `/dining/…` **con alias de compatibilidad** durante una versión (los clientes se actualizan a la vez, pero un dispositivo con la PWA en caché no debe romperse).
4. **Permisos y affordances**: sus acciones y a qué roles/estaciones se anuncian; el núcleo aplica la matriz (D3.1/D4.3a) igual para todos.
5. **Eventos**: qué tipos publica en el outbox y a qué audiencia (`main`-solo si hay dinero, como en D2).
6. **Superficies**: qué pestañas del WPF y qué vistas de la PWA aporta, con el rol que las ve. El WPF y la PWA **leen la lista de módulos activos en `/session`** y montan solo esas superficies: sin módulo Dining no hay pestaña "Comedor y cocina" ni comandero, y sí hay Puestos, Caja, Configuración.
7. **Datos demo**: sus fixtures para `load-demo` (D5.6), marcados igual que hoy.

**Contratos del núcleo que un módulo consume** (interfaces C#, no tablas): lectura de organización (mesas, zonas, estaciones), lectura de catálogo y tarifa vigente, **apuntar cargos en la cuenta de un servicio** (`ISettlementCharges`: lo que hoy hace `add-consumption` escribiendo en `accounts`), identidad del actor, publicación de eventos, auditoría. Un módulo puede participar en la **misma transacción** que el núcleo (la `Unit` actual) — lo prohibido es tocar tablas ajenas, no compartir transacción.

**Lo que el núcleo NO sabe**: nada de pases, elaboraciones, restricciones ni estaciones de cocina como concepto de ejecución. Sabe que existen "estaciones de trabajo" (organización) y "cargos en una cuenta" (ventas); cómo se cocina es del módulo.

### 4. Activación y licencia

- La instalación tiene una lista de **módulos activos** (`core.installation.modules`, editable desde Configuración por `main`; por defecto, los que el instalador marque). `/session` la publica; un módulo inactivo no registra rutas ni superficies aunque su código esté en el binario.
- Cuando exista la licencia (hito propio, offline y firmada), la lista de módulos licenciados **acota** la de activos; nunca la amplía. Hasta entonces, activar un módulo es una decisión de configuración auditada.

### 5. Reorganización del código actual (E1) — sin cambio de comportamiento

| Hoy | Después de E1 | Notas |
| --- | --- | --- |
| `Costina.Domain` (todo junto) | `Costina.Core.Domain` (`SettlementAccount`, primitivas, ámbito, eventos, affordances base) + `Costina.Modules.Dining.Domain` (`DiningService`, `TableOccupancy`, `CourseExecution`, `Restrictions`, snapshots) | Movimiento de ficheros; namespaces nuevos |
| `Costina.Persistence` | `Costina.Core.Persistence` (store, unit, comandos, outbox, identidad, copias, esquema `core`) + repositorios de Dining en su módulo | La `Unit` y `ExecuteAsync` siguen siendo del núcleo |
| `Costina.Server` (host + rutas + operaciones) | `Costina.Server` (host, plataforma, registro de módulos, rutas del núcleo) + `Costina.Modules.Dining` (operaciones, rutas, affordances, KDS, fixtures) | `Program.cs` deja de conocer `fire-next` |
| Esquema `native_d1` | `core` (installation, users, sessions, devices, pairings, commands, audit, outbox, accounts, configuration → organización/catálogo en E2/E3) + `dining` (services, occupancies) | Vía `upgrade`: `ALTER TABLE … SET SCHEMA`; copias y `restore` pasan a multi-esquema y siguen verificando huellas por tabla |
| `Costina.Client` / `Costina.Desktop` | Igual, más: lectura de `session.modules` y montaje de pestañas por módulo | Las pestañas actuales se atribuyen: Comedor y cocina → Dining; Puestos y Cuenta/Caja → núcleo |
| PWA | Igual, más: `session.modules`; `BoardView`/`KitchenView` bajo el módulo Dining | Sin cambio funcional |

Se hace en **dos PR**: E1a (proyectos, namespaces, registro de módulos, rutas con alias, `session.modules`) y E1b (esquemas `core`/`dining` con `upgrade`, copias y `grants.sql` por módulo). La red de seguridad es la actual: dominio 68, ClientChecks 70, batería HTTP, Realtime, checks del WPF, vitest y Playwright deben pasar **sin cambios de aserción salvo rutas y nombres de esquema**. Versión `0.26.0-e1`.

### 6. Lo que este ADR no decide

El modelo concreto de organización, catálogo y tarifas (E2/E3, con ADR propio si hace falta), el diseño de caja y fiscal (hitos 7 y 8), la licencia (hito propio) y el módulo de carta libre. Tampoco decide extraer nada a otro proceso: sigue vigente ARCHITECTURE §6 (solo tras necesidad medida).

## Consecuencias

- Costiña pasa a ser el módulo `dining`, primero y de referencia: cualquier módulo nuevo se construye con el mismo contrato, y el núcleo se puede vender sin él.
- Añadir una tabla implica decidir su esquema (módulo o núcleo) y su concesión: la revisión de `grants.sql` de ADR-011 se hace por módulo.
- Una PR que toque `Costina.Core.*` afecta a todos los módulos: exige la batería completa; una que toque solo `Costina.Modules.Dining` puede acotar sus pruebas, pero CI sigue pasando todo.
- Los clientes dejan de tener pestañas fijas: la interfaz se compone según `session.modules`. Es el precio de poder vender el núcleo solo.
- Coste de E1: solo estructura, sin funcionalidad nueva. Se asume ahora porque es el momento más barato: no hay instalaciones reales que migrar.
