# E4b — Carta libre: grupos con ítems del catálogo, platos pedidos y cobrados al vuelo

Fecha: 2026-09-23. Rama `feat/e4b-carta` sobre `develop` (E4a mezclada en PR #64). Sexto corte del **Hito 6 (núcleo del ERP)**, segunda mitad del plan `docs/plans/2026-09-23-e4-oferta-configurable.md` (sección "Decisiones de E4b"). Versión **`0.31.0-e6`**. Con E4a un negocio vende degustaciones y menús cerrados; con E4b vende **a la carta**: el camarero pide platos del catálogo por grupos (entrantes, principales, postres), cocina los recibe por su estación y la cuenta se apunta al pedirlos.

## Qué cambia

**Datos (esquema versión 6, `upgrade-v6.sql`, encadenado tras la v5)**: `core.offer_items` (`offer_id`, `course_id`, `product_id`, `presentation_id`, orden, activo): lo que se puede pedir en cada grupo de una carta; `core.products.station_id` opcional (FK a `core.stations`): **estación de cocina** del producto cuando es un plato; sin estación es bebida o consumo directo (va a la cuenta sin pase, como `add-consumption`). No mueve datos.

**Reglas**: una oferta `a-la-carte` **no tiene producto-menú** (`product_not_allowed`): no hay cargo al abrir la mesa; sus pases son grupos **opcionales** (`optional`) que nacen vacíos. Un ítem se añade a un grupo si el producto y la presentación están activos y el producto tiene estación (`station_required`); los ítems se retiran y vuelven (nada se borra). Una carta se publica y se abre si tiene un grupo activo con un ítem activo (`offer_incomplete`). `add-dish` (`courseId`, `productId`, `presentationId`, `quantity` 1–20, `guestPosition` opcional) desde sala o principal mientras el grupo está pendiente: el ítem tiene que estar en ese grupo de la oferta de la mesa (`item_unavailable`); crea la elaboración `producto-presentacion-n` con la estación del producto y **apunta el cargo** en la cuenta al precio vigente de la tarifa de la sala (E3), congelado con su origen; después de disparar el grupo ya no admite platos (`course_not_pending`). Un grupo vacío no se dispara (`course_empty`): se añaden platos o se omite; terminar el servicio exige omitir los grupos vacíos. Disparar, preparar, validar, servir, alergias y revisión siguen igual; cocina no pide platos.

**API**: `item-create|deactivate|reactivate` en `POST /erp/offers/commands/{action}` (`offerId`, `courseId`, `productId`, `presentationId`); `GET /erp/offers` lista `items` por grupo (con nombre, presentación y estación); `product-create|update` aceptan `stationId` (cadena vacía = sin estación; `station_inactive` si está desactivada) y `GET /erp/catalog` lo devuelve. `GET /configuration.offers` añade `items` por grupo en las cartas (`{productId, presentationId, name, presentation}`, sin dinero). `POST /dining/services/{id}/commands/add-dish` (main y sala) devuelve la vista del servicio (sin dinero); el pase anuncia `add-dish` mientras está pendiente. Eventos `offer.item_*` y `course.dish_added`.

**Puesto principal (WPF)**: ERP › Catálogo: "Estación de cocina (si es un plato)" en la ficha del producto. ERP › Oferta: en una carta, el tercer panel lista los ítems del grupo seleccionado y permite añadir un vendible del catálogo o retirarlo. Comedor: fila "Carta: pedir platos de este grupo" (ítem, comensal opcional, cantidad) cuando el pase anuncia `add-dish`. **PWA**: por grupo con `add-dish`, selector de plato, comensal y cantidad con el botón "Pedir plato"; la carta se distingue al abrir la mesa.

**Cliente (`Costina.Client`)**: `OfferItemChoice` en `OfferCourseChoice.Items`, `OfferItemDto` en `OfferCourseDto.Items`, `ProductDto.StationId`, `CourseDto.Optional`.

Capturas (compuestas por los checks del WPF con datos leídos, sin servidor): [oferta con carta](images/e4b/offers.png) · [catálogo con estación](images/e4b/catalog.png).

## Qué NO cambia

Degustaciones y menús cerrados (E4a), catálogo y tarifas (E3), identidad, tiempo real, copias (la tabla nueva entra sola; `grants.sql` concede INSERT/UPDATE), instalador. Sin combinados ni modificadores (E4c), sin fotos ni botones de familia (Hito 7), sin retirar un plato ya pedido (se anula el cargo desde Caja; la elaboración se omite con el pase).

## Comprobaciones automáticas

`sh tools/agent/local-tests.sh` completo (ahora en el puerto **5090**: en este PC hay una instalación real de Costiña escuchando en 5088, ver `COSTINA_TEST_PORT`): dominio **79** (+1: grupo de carta que admite platos mientras está pendiente, `course_empty`, `duplicate_preparation`, `invalid_guest`, cierre al disparar, omisión y restauración; carta sin producto), ClientChecks 75, checks del WPF (carta con ítems en la vista de oferta, fila de pedido solo con `add-dish` anunciado; capturas), **14 suites HTTP** (`offers_http` +1 historia: carta configurada con ítems y estación obligatoria, producto que pasa a plato desde el catálogo, apertura sin cargo, `course_empty`, `add-dish` con elaboración y cargo congelado, ítem ajeno, cantidad, cocina 403, otra ración, cierre al disparar, cocina y servicio completos, carta sin ítems no se publica), `packaging_http.test_06e` hasta **v6**, Realtime 10, vitest 60.

## Guion de prueba manual

Instalación demo, puesto principal como `main`. 1) **ERP › Oferta**: "Carta de ensayo" (carta) con Entrantes (Croquetas de la casa), Principales (Lubina a la sal) y Postres (Tarta de queso). 2) **ERP › Catálogo**: crear `pulpo` "Pulpo a feira", categoría Comida, estación Cocina caliente, precio `14,00`; en Oferta › Carta de ensayo › Entrantes, "Añadir al grupo" con Pulpo a feira · Ración. 3) Comedor: abrir una mesa con "Carta de ensayo" para 2: Cuenta / Caja a 0,00 €; en Entrantes, pedir 2 × Croquetas (comensal 1) y 1 × Pulpo: la cuenta suma 30,00 € y cocina ve dos elaboraciones en fría y una en caliente; "Enviar siguiente pase" sin platos en un grupo es rechazado con texto; omitir Postres para terminar. 4) Lo mismo desde el comandero: el grupo muestra "pedir platos de la carta". 5) Retirar el pulpo de la carta: ya no se puede pedir; sin ítems en ningún grupo, la carta desaparece del desplegable. 6) Actualizar una instalación anterior: `upgrade` a v6 sin mover datos.

## Límites

Un plato pedido no se retira del pase (se omite el grupo o se anula el cargo en Caja). La cantidad es por línea; sin modificadores ni combinados (E4c). Los alias `menus`/`menuId` siguen una versión más.
