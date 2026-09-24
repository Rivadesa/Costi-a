# Plan E4 — Oferta configurable (cartas, menús cerrados, degustaciones) en el núcleo, ejecutada por Dining

Fecha: 2026-09-23 (borrador para arrancar tras mezclar la #62). Quinto corte del Hito 6 (núcleo del ERP), sobre E3 (catálogo y tarifas). Versión de paquete `0.30.0-e5`. Enfoque aprobado por el promotor el 23-09-2026: la **oferta** (lo que el negocio vende en sala) es configurable por instalación — carta libre, menú del día o cerrado, degustación o experiencia, combinados, experiencia con alojamiento — **definida en el núcleo y ejecutada por el módulo Dining**. Costiña usa degustaciones/experiencias; otro restaurante, carta y menú del día. Referencia de lo conocido: "Gestión de cartas" de Verial (grupos, artículos, sección de impresora 1.º/2.º/3.º plato).

## Objetivo

Que los menús dejen de ser un fixture del módulo (`dining.configuration`, `MenuDefinition`) y pasen a ser **ofertas** del ERP: qué se vende en sala, a qué precio (por el catálogo y las tarifas de E3), con qué pases y platos, y hacia qué estaciones va cada plato. Al abrir una mesa se elige la oferta y el módulo Dining la ejecuta con el mismo flujo de pases (disparar, preparar, validar, servir, alergias, revisión) que ya está probado.

## Decisiones propuestas

1. **Un menú es un producto**: cada oferta cerrada (menú del día, degustación, experiencia) se vende como un **producto del catálogo** (categoría "Menús") con la presentación `person` ("por persona") y su **precio en las tarifas de E3** (vigencia y tarifa por sala incluidas). Así el cargo de la mesa es `pax × precio vigente`, congelado como cualquier otro, sin un precio paralelo en la definición del menú.
2. **Datos en `core`** (esquema versión 5, `upgrade-v5.sql`):
   - `core.offers` (`id`, `name`, `kind` en `set-menu` (menú del día o cerrado: el comensal elige un plato por pase), `tasting` (degustación/experiencia: todos los platos, sin elección), `a-la-carte` (carta libre: los pases son grupos de la carta y el camarero añade platos del catálogo), `product_id` (el producto-menú; nulo en carta libre), `service` en `any|lunch|dinner`, `valid_from`/`valid_to` fechas opcionales, `weekdays` (máscara 7 bits, todos por defecto), `sort`, `active`).
   - `core.offer_courses` (`offer_id`, `id`, `name`, `sort`): los pases ("Aperitivos", "Primeros", "Postre"…).
   - `core.offer_dishes` (`offer_id`, `course_id`, `id`, `name`, `station_id` (estación de E2), `product_id` opcional (para stock/escandallo en E6), `sort`, `active`): los platos de un pase; en `set-menu` el comensal elige uno por pase; en `tasting` van todos.
   - `core.offer_items` (`offer_id`, `course_id`, `product_id`, `presentation_id`): lo que se puede pedir a la carta en cada grupo (la carta es una selección del catálogo; un producto puede estar en varias cartas).
   - `core.products.station_id` opcional: estación de cocina de un producto vendido a la carta (los platos); las bebidas no la necesitan (van a la cuenta sin pase, como hoy `add-consumption`).
   - Migración: cada menú de `dining.configuration` → producto `menu-<id>` (categoría `menus`, IVA 10 %, presentación `person` con su `unitPriceCents` en `general` desde 1970-01-01) + oferta `tasting` con sus pases y platos (estación de cada elaboración). `dining.configuration` deja de tener `kind='menu'` (la tabla puede quedar vacía para futuras claves del módulo).
3. **Reglas**: nada se borra, se desactiva; una oferta activa necesita al menos un pase con un plato (o un ítem, en carta); `set-menu` y `tasting` necesitan `product_id` con presentación `person` y precio vigente (si no, no se puede abrir con ella: `offer_unpriced`); la estación de un plato tiene que existir y estar activa; la oferta se muestra a la sala solo si está **vigente hoy** (fechas, día de la semana) y el servicio coincide (comida/cena por hora local configurable, o `any`).
4. **Ejecución en Dining** (contrato del núcleo `Unit.Offer(id)`, nunca SQL sobre tablas ajenas):
   - Abrir mesa: `offerId` en vez de `menuId` (alias `menuId` una versión). `tasting`: como hoy (una preparación por plato y comensal). `set-menu`: se crean los pases con **elección pendiente** por comensal; nuevo comando `choose` (`courseId`, `guestPosition`, `dishId`) desde sala o principal; un pase no se dispara con elecciones pendientes (`choice_missing`). `a-la-carte`: la mesa nace sin platos; `add-dish` (`productId`, `presentationId`, `courseId`, `guestPosition` opcional) crea la preparación en el pase indicado, la apunta en la cuenta al precio vigente (E3) y a partir de ahí disparar/preparar/validar/servir es igual; `add-consumption` sigue para bebidas (sin pase).
   - Cargo del menú: `pax × precio vigente` del producto-menú en la tarifa de la sala, congelado con `productId/presentationId/tariffId`. Cambiar el pax después es un corte aparte (hoy no existe).
   - Affordances y estaciones: sin cambios de matriz; `choose` y `add-dish` se anuncian para `main` y `service`.
5. **API**: `GET /erp/offers` y `POST /erp/offers/commands/{action}` (`offer|course|dish|item` × `create|update|deactivate|reactivate`, `dish-move`), solo `main`, auditado `offer.*`. `GET /configuration.offers` (todos los roles) sustituye a `menus`: ofertas **vigentes** con `{id, name, kind, courses:[{id,name}]}` y, para `set-menu`, los platos por pase (sin dinero); `menus` se mantiene una versión como alias de las `tasting`.
6. **Pantallas**: WPF **ERP › Oferta: cartas y menús** (entrada "Menús y pases" renombrada): lista de ofertas con tipo y vigencia; ficha; editor de pases y platos (estación por plato) o de ítems de carta (buscador del catálogo por categoría); "Comedor" elige la oferta al abrir y, en menú cerrado, muestra las elecciones por comensal. PWA: abrir mesa con oferta; en menú cerrado, elegir plato por comensal; en carta, "Añadir plato" (catálogo por grupo) además del consumo de bebidas.
7. **Pruebas**: `offers_http.py` (ofertas de la demo; edición; vigencia por fecha/día/servicio; abrir con `tasting` = comportamiento actual; `set-menu` con elección obligatoria antes de disparar; `a-la-carte` con `add-dish` y cargo al precio de la tarifa de la sala; oferta sin precio no abre; 403 por rol), dominio (elecciones, pases dinámicos), `packaging_http.test_06e` hasta **v5** (el menú LAB-TASTING migrado y una mesa abierta con él), PWA (vitest + Playwright con `offerId`), checks del WPF con captura.

## Resultado de E4a (23-09-2026)

Hecho según lo anterior con estas concreciones: `offer-create` crea el producto-menú (`menu-<id>`, categoría `menus`, presentación `person`, `taxId` opcional, `priceCents` opcional); los productos-menú no salen en los catálogos operativos; `configuration.offers` para todos los roles con `menus` y `menuId` como alias una versión; el servicio guarda `offerId`; los pases de menú cerrado nacen sin elaboraciones y `choose` (`courseId`, `guestPosition`, `dishId`) las crea, `fire-next` exige elección completa (`choice_missing`); `a-la-carte` existe como tipo pero se rechaza (`kind_unsupported`) hasta E4b. Ver `docs/native/E4a-offers.md`.

## Decisiones de E4b — carta libre (23-09-2026, versión `0.31.0-e6`)

1. **Esquema v6**: `core.offer_items` (`offer_id`, `course_id`, `product_id`, `presentation_id`, `sort`, `active`): lo que se puede pedir a la carta en cada grupo; `core.products.station_id` opcional (FK a `core.stations`): estación de cocina de un plato; un producto sin estación es bebida o consumo directo (va a la cuenta sin pase, como `add-consumption`).
2. **Oferta `a-la-carte`**: sin producto-menú (no hay cargo al abrir); sus pases son **grupos** de la carta (Entrantes, Principales, Postres) y se marcan `Optional`: un grupo vacío no se dispara (`course_empty`: añadir platos u omitirlo) y hay que omitirlo para terminar el servicio. Comandos `item-create|deactivate|reactivate` en `/erp/offers` (`offerId`, `courseId`, `productId`, `presentationId`).
3. **Dining**: `add-dish` (`courseId`, `productId`, `presentationId`, `guestPosition` opcional, `quantity` 1–20) desde sala o principal mientras el grupo está pendiente: el ítem tiene que estar activo en ese grupo de la oferta de la mesa (`item_unavailable`), el producto necesita estación (`station_required`); crea la elaboración en el pase (id `producto-presentacion-n`, estación del producto) y **apunta el cargo** en la cuenta al precio vigente de la tarifa de la sala (E3), congelado con su origen. Respuesta de sala sin dinero (`{version, dishId, productId, presentationId, quantity}`). El pase anuncia `add-dish` mientras está pendiente. Disparar, preparar, validar y servir siguen igual.
4. **Lecturas**: `configuration.offers` añade `items` por grupo (`{productId, presentationId, name, presentation}`) en las cartas; `dishes` sigue para menús cerrados. `GET /erp/catalog` incluye `stationId` del producto; `product-create|update` aceptan `stationId`.
5. **Pantallas**: ERP › Catálogo: estación de cocina en la ficha del producto; ERP › Oferta: en una carta, el tercer panel lista los ítems del grupo con un selector del catálogo; Comedor: fila «Añadir plato» (ítem, comensal, cantidad) cuando el pase anuncia `add-dish`; PWA: por grupo con `add-dish`, selector de ítem y botón. Demo: carta `LAB-CARTA` con Entrantes (croquetas, fría, 8,00), Principales (lubina, caliente, 24,00) y Postres (tarta, fría, 6,00).
6. **Fuera de E4b** (→ E4c): combinados y modificadores (opciones con suplemento sobre un producto), fotos y botones de familia (Hito 7).

## Corte en dos PR si hace falta

- **E4a**: ofertas en el núcleo (esquema v5, migración, API, pantalla ERP), `tasting` y `set-menu` con elecciones, apertura por oferta en WPF y PWA. Versión `0.30.0-e5`.
- **E4b**: carta libre (`a-la-carte`, `add-dish`, `products.station_id`, catálogo por grupo en la PWA) y combinados/modificadores (producto con opciones: "Gin X + tónica Y", suplementos). Versión `0.31.0-e6`.

## Fuera de E4

Reservas y experiencia con alojamiento (E8: una experiencia de villa será una oferta `tasting` ligada a una reserva), stock y escandallos (E6), cambio de pax en una mesa abierta, precios por franja horaria (basta `service` comida/cena), impresión de comandas en cocina (KDS ya cubre), fotos de plato.

## Riesgos

- Clientes con caché que abren con `menuId`: alias una versión; `configuration.menus` sigue con las `tasting` vigentes.
- Menús de demostración: la migración crea el producto-menú con IVA 10 %; el promotor revisa el tipo real (hostelería).
- Vigencia por servicio (comida/cena) necesita la hora local de la instalación: se toma la del servidor (Windows) y se documenta.
