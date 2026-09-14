# Costi-a / Hospitality OS

Plataforma modular para restauración gastronómica, iniciada con Retiro da Costiña / AÑITSOC SL y diseñada como producto exportable a otros negocios.

> **Rama de trabajo:** `develop`  
> **Rama estable:** `main`

## Antes de continuar con otra IA o desarrollador

Leer primero [`AGENTS.md`](AGENTS.md) y [`docs/STATUS.md`](docs/STATUS.md), comprobar el commit y sus pruebas en GitHub. Las especificaciones de producto describen capacidades objetivo; no implican que todas estén implementadas. El estado verificado y el código prevalecen sobre listas antiguas de tareas.

## Prueba local 0.1.1

Guía: [`docs/LOCAL_TEST_0.1.1.md`](docs/LOCAL_TEST_0.1.1.md). Cliente Windows y servidor real de ensayo en el mismo PC; no una instalación LAN/producción. El catálogo real está integrado por PR #15; el lanzador y cliente 0.1.1 por PR #16. El manifiesto del build identifica el commit y los hashes de cada ejecutable. Compilar no sustituye probar físicamente la aplicación.

## Objetivo inmediato — V1A

Coordinar un servicio gastronómico por pases entre sala y cocina con mínima fricción, operación local incluso sin Internet exterior y trazabilidad de las acciones relevantes.

El objetivo de V1A **no** es reemplazar todo el ERP. Primero se valida el núcleo diferencial del producto:

`mesa → comensales → restricciones → menú → pases → KDS → servido`

En una superficie separada del equipo principal: `catálogo → consumiciones → cuenta provisional → cobro operativo`. El seguimiento de mesas y KDS no muestran importes ni pagos; ver ADR-007. El desacoplamiento interno de estados de servicio/pago continúa pendiente en [#17](https://github.com/Rivadesa/Costi-a/issues/17).

La fiscalidad propia, inventario, PIM, WooCommerce, intercompany y reservas avanzadas llegan en fases posteriores, aunque sus fronteras se contemplan desde el diseño.

## Arquitectura acordada

- **Servidor local-primary:** Laravel + PostgreSQL + Redis.
- **Escritorio Windows:** Tauri 2 + Vue 3/Vite.
- **Comanderos / KDS:** Vue PWA o Tauri según hardware.
- **Backoffice objetivo:** Filament; la administración mínima actual usa Vue.
- **Realtime objetivo:** WebSockets; el cliente actual usa polling.
- **Cloud objetivo:** réplica, backup, monitorización e integraciones; no autoridad simétrica durante V1A. No implementado como recuperación productiva.
- **Persistencia de eventos:** Transactional Outbox; no event sourcing completo.
- **Fiscalidad:** frontera diseñada desde V1A, implementación SIF/VERI*FACTU en V1B.
- **Identificadores:** UUID/ULID globales para entidades sincronizables.

## Documentación para humanos e IA

Después de `AGENTS.md` y `STATUS.md`:

1. [`docs/AI_CONTEXT.md`](docs/AI_CONTEXT.md) — contexto y decisiones de producto, no sustituto del estado verificado.
2. [`docs/PRODUCT_SCOPE.md`](docs/PRODUCT_SCOPE.md) — qué es el producto, qué entra y qué no entra en V1A.
3. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — arquitectura y límites entre componentes.
4. [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) — entidades, agregados, estados e invariantes.
5. [`docs/WORKFLOWS.md`](docs/WORKFLOWS.md) — flujo operativo objetivo.
6. [`docs/UX_PRINCIPLES.md`](docs/UX_PRINCIPLES.md) — interacción de sala, cocina y chef.
7. [`docs/API_CONVENTIONS.md`](docs/API_CONVENTIONS.md) — contratos HTTP/realtime e idempotencia.
8. [`docs/DATA_AND_SYNC.md`](docs/DATA_AND_SYNC.md) — persistencia local, outbox y sincronización objetivo.
9. [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) — onboarding técnico.
10. [`docs/TESTING.md`](docs/TESTING.md) — estrategia de pruebas.
11. [`docs/GLOSSARY.md`](docs/GLOSSARY.md) — vocabulario compartido.
12. [`docs/BACKLOG.md`](docs/BACKLOG.md), [`docs/ROADMAP.md`](docs/ROADMAP.md) — trabajo pendiente y fases.
13. [`docs/adr/`](docs/adr/) — decisiones de arquitectura.

## Reglas del producto

Retiro da Costiña es la primera implantación, no un modelo rígido. Mesas, pases, estaciones, permisos, empresas y canales se resuelven por configuración cuando sea razonable.

En servicio, una acción frecuente debe requerir una sola interacción siempre que sea posible. La riqueza funcional pertenece a administración, no a la navegación cotidiana de sala y cocina.

Servicio operativo, cuenta provisional, pago y documento fiscal son conceptos distintos con ciclos de vida distintos. No eliminar auditoría ni registros económicos para simplificar una pantalla.

## Estructura actual

```text
backend/            aplicación Laravel, dominio y migraciones PostgreSQL
frontend/operator/  cliente Vue/Tauri para operación
deploy/local-test/  servidor de ensayo y lanzador Windows
docs/               especificación, estado verificable y ADRs
```

## Flujo Git

`main`: cortes estables. `develop`: integración del trabajo. Ramas por funcionalidad/corrección, PR hacia `develop`; PR de release hacia `main`. Cambios de arquitectura requieren ADR. Cada entrega debe identificar pruebas, commit, limitaciones y guía de ensayo; no llamar producción a una demo o a un build sin validación física.
