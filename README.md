# Costi-a / Hospitality OS

Plataforma modular para restauración gastronómica, iniciada con Retiro da Costiña / AÑITSOC SL y diseñada como producto exportable a otros negocios.

> **Rama de trabajo:** `develop`  
> **Rama estable:** `main`

## Objetivo inmediato — V1A

Coordinar un servicio gastronómico por pases entre sala y cocina con mínima fricción, operación local incluso sin Internet exterior y trazabilidad completa de las acciones relevantes.

El objetivo de V1A **no** es reemplazar todo el ERP. Primero se valida el núcleo diferencial del producto:

`mesa → comensales → restricciones → menú → pases → KDS → servido → consumos → cuenta provisional → cobro operativo`

La fiscalidad propia, inventario, PIM, WooCommerce, intercompany y reservas avanzadas llegan en fases posteriores, aunque sus fronteras se contemplan desde el diseño.

## Arquitectura acordada

- **Servidor local-primary:** Laravel + PostgreSQL + Redis.
- **Escritorio Windows:** Tauri 2 + Vue 3/Vite.
- **Comanderos / KDS:** Vue PWA o Tauri según hardware.
- **Backoffice:** Filament.
- **Realtime:** WebSockets en producto.
- **Cloud:** réplica, backup, monitorización e integraciones; no autoridad simétrica durante V1A.
- **Persistencia de eventos:** Transactional Outbox; no event sourcing completo.
- **Fiscalidad:** frontera diseñada desde V1A, implementación SIF/VERI*FACTU en V1B.
- **Identificadores:** UUID/ULID globales para entidades sincronizables.

## Documentación para humanos e IA

Leer en este orden:

1. [`docs/AI_CONTEXT.md`](docs/AI_CONTEXT.md) — resumen autosuficiente del proyecto.
2. [`docs/PRODUCT_SCOPE.md`](docs/PRODUCT_SCOPE.md) — qué es el producto, qué entra y qué no entra en V1A.
3. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — arquitectura técnica y límites entre componentes.
4. [`docs/DOMAIN_MODEL.md`](docs/DOMAIN_MODEL.md) — entidades, agregados, estados e invariantes.
5. [`docs/WORKFLOWS.md`](docs/WORKFLOWS.md) — flujo operativo real de Retiro da Costiña.
6. [`docs/UX_PRINCIPLES.md`](docs/UX_PRINCIPLES.md) — reglas de interacción de sala, cocina y chef.
7. [`docs/API_CONVENTIONS.md`](docs/API_CONVENTIONS.md) — contratos HTTP/realtime e idempotencia.
8. [`docs/DATA_AND_SYNC.md`](docs/DATA_AND_SYNC.md) — persistencia local, outbox, cloud y conflictos.
9. [`docs/DEVELOPMENT.md`](docs/DEVELOPMENT.md) — onboarding técnico y estructura del repositorio.
10. [`docs/TESTING.md`](docs/TESTING.md) — estrategia de pruebas y criterios de aceptación.
11. [`docs/GLOSSARY.md`](docs/GLOSSARY.md) — vocabulario compartido.
12. [`docs/ROADMAP.md`](docs/ROADMAP.md) y [`docs/STATUS.md`](docs/STATUS.md) — planificación y estado actual.
13. [`docs/adr/`](docs/adr/) — Architecture Decision Records.

## Regla principal del producto

**Retiro da Costiña es la primera implantación, no el modelo rígido del producto.**

Número de mesas, pases, estaciones de cocina, permisos, tipos de servicio, empresas y canales deben resolverse por configuración siempre que sea razonable.

## Regla principal de UX

Durante el servicio, una acción frecuente debe requerir **una sola interacción siempre que sea posible**. La riqueza funcional pertenece al backoffice; sala y cocina deben operar con interfaces contextuales y rápidas.

## Regla principal de datos

Nunca mezclar:

- servicio operativo,
- cuenta provisional,
- pago,
- documento fiscal.

Son conceptos distintos con ciclos de vida distintos.

## Estructura actual

```text
backend/            núcleo de dominio PHP y futura aplicación Laravel
frontend/operator/  cliente Vue/Tauri para operación
frontend/*           futuros clientes PWA/KDS si se separan
docs/               especificación viva del producto y ADRs
```

## Flujo Git

- `main`: cortes estables.
- `develop`: integración continua del trabajo en curso.
- `feature/*`: funcionalidades suficientemente grandes como para revisión aislada.
- PR hacia `develop`; PR de release hacia `main`.

No introducir cambios de arquitectura relevantes sin un ADR o actualización explícita del ADR correspondiente.
