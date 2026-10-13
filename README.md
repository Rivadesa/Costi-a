# Costi-a / Hospitality OS

Plataforma modular para restauración gastronómica, iniciada con Retiro da Costiña / AÑITSOC SL y diseñada como producto exportable a otros negocios.

> **Rama de trabajo:** `develop` · **Rama estable:** `main` (no representa el estado actual del desarrollo).

## Qué está ACTIVO y qué está CONGELADO

| | Dónde | Estado |
| --- | --- | --- |
| **Motor nativo** (ADR-008): servidor C#/.NET como servicio de Windows, PostgreSQL, cliente de escritorio WPF, PWA Vue/TypeScript para comanderos y pantallas de cocina, instalador NSIS | `dotnet/`, `frontend/pwa/`, `deploy/windows/` | **ACTIVO.** Todo el desarrollo nuevo va aquí. Leer `dotnet/AGENTS.md` antes de tocarlo. |
| Runtime legado 0.1.1: Laravel + PostgreSQL + Redis, cliente Vue/Tauri, Docker de ensayo | `backend/`, `frontend/operator/`, `deploy/local-test/` | **CONGELADO** (ADR-008). No se amplía, no se migra, no se borra hasta que el motor nativo lo sustituya con pruebas; el motor nativo nunca escribe en su base de datos. No se usa en ninguna instalación. |

Un agente o desarrollador nuevo que modifique `backend/` o `frontend/operator/` está trabajando sobre código muerto: el estado verificado vive en [`docs/STATUS.md`](docs/STATUS.md) y el código activo en `dotnet/`.

## Antes de continuar con otra IA o desarrollador

Leer primero [`AGENTS.md`](AGENTS.md), [`dotnet/AGENTS.md`](dotnet/AGENTS.md) y [`docs/STATUS.md`](docs/STATUS.md), y el último traspaso de [`docs/handoff/`](docs/handoff/) (trae el estado, las reglas de trabajo, las trampas del entorno y el prompt para continuar). Las especificaciones de producto describen capacidades objetivo; no implican que todas estén implementadas. El estado verificado y el código prevalecen sobre listas antiguas de tareas.

## Qué hace hoy el motor nativo

Sala y cocina por pases con restricciones por comensal, cuenta por mesa separada de la operación (ADR-007), identidad de personas y dispositivos con emparejamiento por QR, tiempo real que solo avisa, orden incierta durable en cada cliente, organización, catálogo, tarifas y ofertas editables, instalador de Windows con PostgreSQL propio, HTTPS en la red local, copias de seguridad verificadas y restauración. Pendiente (en este orden): caja completa y TPV táctil, fiscalidad (VERI*FACTU), informes, reservas, stock, compras y proveedores. Detalle en `docs/STATUS.md`.

## Arquitectura (ADR-008, ADR-009, ADR-011, ADR-012)

- **Servidor local-primary** en el restaurante: `Costina.Server` (.NET) como servicio de Windows sobre PostgreSQL; sin Docker, Redis ni WSL en el runtime.
- **Núcleo y módulos** (ADR-012): `Costina.Core.*` y `Costina.Modules.Dining*`; un módulo depende del núcleo, nunca al revés; un esquema PostgreSQL por módulo.
- **Clientes**: puesto principal WPF (`Costina.Desktop`), PWA del mismo origen para tablets y móviles (`frontend/pwa`), biblioteca tipada `Costina.Client`.
- **Fiabilidad**: transacciones, bloqueo optimista, idempotencia por ámbito+actor+clave, outbox transaccional, auditoría; una sola orden incierta persistida antes de enviar.
- **Seguridad**: usuarios con contraseña y dispositivos emparejados, TLS en LAN con CA local restringida por nombre, rol PostgreSQL de ejecución sin DDL, datos fuera del programa.
- **Fiscalidad**: frontera diseñada; implementación en el Hito 8.

## Documentación

Después de `AGENTS.md` y `STATUS.md`: [`docs/adr/`](docs/adr/) (decisiones), [`docs/native/`](docs/native/) (un documento por corte con su guion manual), [`docs/plans/`](docs/plans/) (planes aprobados antes de codificar), [`docs/handoff/`](docs/handoff/) (traspasos). Los documentos de producto (`docs/PRODUCT_SCOPE.md`, `docs/ARCHITECTURE.md`, `docs/DOMAIN_MODEL.md`, `docs/WORKFLOWS.md`, `docs/UX_PRINCIPLES.md`, `docs/GLOSSARY.md`) describen el objetivo; los que hablan de Laravel/Tauri (`docs/LOCAL_TEST_0.1.1.md`, `docs/INSTALLATION_ARCHITECTURE.md`, `docs/DEVELOPMENT.md`) son del runtime congelado.

## Reglas del producto

Retiro da Costiña es la primera implantación, no un modelo rígido. Mesas, pases, estaciones, permisos, empresas y canales se resuelven por configuración cuando sea razonable.

En servicio, una acción frecuente debe requerir una sola interacción siempre que sea posible. La riqueza funcional pertenece a administración, no a la navegación cotidiana de sala y cocina.

Servicio operativo, cuenta provisional, pago y documento fiscal son conceptos distintos con ciclos de vida distintos. No eliminar auditoría ni registros económicos para simplificar una pantalla.

## Estructura actual

```text
dotnet/             ACTIVO: motor nativo, cliente WPF, biblioteca cliente, pruebas (.NET, Python HTTP, PowerShell)
frontend/pwa/       ACTIVO: PWA de comanderos y pantallas de cocina (Vue/TypeScript, vitest, Playwright)
deploy/windows/     ACTIVO: instalador NSIS y empaquetado
tools/agent/        scripts de trabajo (pruebas locales, API de GitHub sin gh, parches)
docs/               especificación, estado verificable, ADR, planes, cortes y traspasos
backend/            CONGELADO: aplicación Laravel del runtime 0.1.1
frontend/operator/  CONGELADO: cliente Vue/Tauri del runtime 0.1.1
deploy/local-test/  CONGELADO: servidor de ensayo Docker y lanzador del runtime 0.1.1
```

## Flujo Git

`main`: cortes estables. `develop`: integración del trabajo. Una rama por corte, PR hacia `develop` con CI leído en sus logs; merge con merge commit `Merge PR #N: ...`. Cambios de arquitectura requieren ADR. Cada corte trae su documento en `docs/native/`, actualiza `dotnet/AGENTS.md` y `docs/STATUS.md`, y lleva la versión en los cuatro sitios (csproj del servidor y del escritorio, `MainWindow.xaml`, `frontend/pwa/package.json`). No llamar producción a una demo o a un build sin validación física.
