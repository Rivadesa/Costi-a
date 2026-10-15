# E4d — Menús y cartas: palabras claras, alta sin códigos y menú del día

Fecha: 2026-10-15. Rama `feat/e4d-menu-de-la-casa` sobre `develop` (`6be2e2f`, C2b mezclada). Primer corte del plan aprobado `docs/plans/2026-10-15-menus-carta-y-bodega.md`, sección 4.1, con las palabras que el promotor fijó al aprobarlo («Sube y mergea y adelante», 15-10). Versión **`0.39.0-e4d`**. **Sin cambio de esquema** (sigue la v8). El motor solo añade.

Lo que verá el promotor:
- **Palabras claras.** Su menú sigue siendo **«Menú degustación»** (es el nombre de su menú, con estrella: no se renombra). El menú en el que cada persona elige su plato se llama **«Menú con elección»** y queda en «Más opciones». La sección «Oferta: cartas y menús» pasa a llamarse **«Menús y cartas»** («oferta» sonaba a rebaja), en el menú, la barra lateral, Inicio y el Comedor.
- **Alta sin inventar nada.** «Nuevo menú o carta» pide el nombre, el tipo en dos botones (Menú degustación o Carta), el **precio por persona** si es un menú y nada más; «Cuándo se ofrece» dice «Siempre». Código, orden, servicio, fechas, días, «Menú con elección» y el producto existente van plegados en **«Más opciones»**.
- **Menú del día.** El primer menú o carta vigente ahora lleva la marca **«Menú del día»** (en su fila y en su ficha) y es el que **se propone siempre al abrir mesa**, en el puesto y en el comandero. En cualquier otro, el botón **«Hacerlo menú del día»** lo pone el primero.
- **Pases y platos** se ordenan con **«Subir»** y **«Bajar»**; ya no hay un número de orden que escribir.
- **El precio por persona de un menú se cambia desde su ficha**: se escribe y «Guardar» lo fija en la General desde hoy.

## Qué cambia

### Motor (solo añade; `Catalog.FreeCode`, `Catalog.NextSort`, `OfferOperations`, `CatalogOperations`)

- **Código opcional en todas las altas**: `offer-create`, `course-create`, `dish-create`, `category-create`, `product-create`, `presentation-create`, `tariff-create` y `tax-create` admiten `id` vacío. El servidor lo genera del nombre con la regla de la importación (`Catalog.Slug`: minúsculas ASCII, dígitos y guiones), **único entre sus hermanos** (sufijos `-2`, `-3`… que siguen cabiendo) y lo devuelve en la respuesta. Un `id` enviado se respeta, como siempre (los clientes y las pruebas anteriores no cambian). Es la función `Catalog.FreeCode`, sacada del `EnsureCategory` de la importación.
- **Orden opcional**: sin `sort`, el alta va **al final** de sus hermanos (el mayor + 10, tope 9999: `Catalog.NextSort`); antes quedaba en 0.
- **Código de una oferta generada**: 27 caracteres como máximo, porque su producto-menú se llama `menu-<código>` y un código admite 32. Si ese nombre no cabe o está ocupado, el producto-menú se deriva del nombre («menu <nombre>»), siempre dentro de 32. Un nombre de 60 caracteres se crea sin `invalid_code`.
- **`offer-create` deja auditoría del catálogo**: `catalog.product_created` y, con precio, `catalog.price_set`, en la misma transacción que `offer.offer_created` (cierra el hueco que señaló el estudio del plan).
- **`offer-update` admite `priceCents`**: fija el precio por persona del producto-menú en la General desde hoy, solo si cambia (`catalog.price_set`); en una carta se rechaza con `product_not_allowed`. El cuerpo sin precio es el de siempre.
- **`offers-reorder {offerIds:[…]}`**, solo responsable: las ofertas indicadas pasan delante, en ese orden, y las demás detrás como estaban; una orden `offer.offer_updated` por cada una que cambia de sitio. Idempotente con la misma clave; `404` con un código desconocido; `409 duplicate_offer` si se repite uno; `403` para sala y cocina (la ruta es solo del responsable).
- **Nada cambia** en la apertura, los pases, las órdenes de sala ni las lecturas.

### Puesto principal (WPF)

**Vocabulario** (`Labels.cs`, para todo el puesto): `tasting` = «Menú degustación», `set-menu` = «Menú con elección», `a-la-carte` = «Carta»; vista «Menús y cartas»; rechazos «Un menú necesita su precio por persona.», «Ese menú o carta no se ofrece ahora.»… Inicio cuenta «2 menús degustación · 2 con elección · 3 a la carta».

**Menús y cartas** (`OffersView`, `OffersViewModel`):
- **Lista**: chips Todos · Menú degustación · Menú con elección · Carta, con recuento; cada fila lleva nombre, tipo, servicio, pases, vigencia y los chips **«Menú del día»** y «Desactivado».
- **Camino principal de la ficha**: nombre; tipo en botones (`Segmented`: Menú degustación o Carta); **precio por persona** (solo en un menú; obligatorio al crear, «vacío = sin cambio» al guardar, con «Hoy: 65,00 € por persona» cuando el catálogo está leído); «Cuándo se ofrece» en una línea («Siempre», o «Comida · de lunes a viernes · Del 01/09/2026 al 31/12/2026»).
- **«Más opciones»** (plegado): «Menú con elección» como casilla (traduce a `set-menu`; apagada en una carta), servicio, código (editable solo en el alta, opcional: se genera del nombre), orden, fechas (dd/mm/aaaa) y días, y, al crear un menú, el código de un producto existente que se venda «Por persona» en vez del precio.
- **Botones**: «Crear» / «Guardar»; **«Hacerlo menú del día»** cuando no lo es (manda `offers-reorder` con esa oferta delante y relee todo); «Desactivar» / «Reactivar»; «Cancelar» en el alta.
- **Pases y platos**: «Subir» y «Bajar» en los datos del pase y en la ficha del plato. Con órdenes crecientes se intercambian los dos (dos órdenes `course-update` o `dish-update`, una detrás de otra, cada una con su clave); si la lista tenía empates se renumera de 10 en 10 (una orden por hermano). Las altas de pase y plato ya no llevan `sort`: el servidor las pone al final.
- **Un menú nuevo sin precio** ni producto existente se rechaza en la barra de estado antes de enviar nada: «Un menú necesita su precio por persona (por ejemplo 65,00).»
- **F01**: cada orden sale de la oferta, el pase o el plato elegidos y leídos; «Subir/Bajar» y «Hacerlo menú del día» usan el orden de la última lectura correcta.

**Comedor** (`ServiceViewModel`): «Abrir mesa» (desde la cabecera o desde una mesa libre del plano) propone **siempre** el menú del día, la primera oferta vigente que da `/configuration`; hoy repetía la elegida la vez anterior. El texto de la elección dice «Menú con elección: cada persona elige su plato antes de enviar el pase». El campo del formulario se llama «Menú o carta».

**Supuesto**: el menú del día es la primera oferta **vigente ahora** del orden de Menús y cartas. Con un menú de comida y otro de cena, cada servicio propone el suyo sin configurar nada; una marca en la base necesitaría esquema y no resolvería ese caso.

### Comandero (PWA), cambio mínimo hasta C3

- `src/offers.ts`: `proposedOffer` (la primera oferta vigente, aunque haya varias; antes solo se proponía cuando había una) y `offerKindSuffix` («(con elección)», «(carta)»; el menú degustación no lleva coletilla). Sin DOM: se prueba con vitest.
- `BoardView.vue` los usa; los selectores de las E2E no cambian (`commands.spec.ts` sigue eligiendo `LAB-TASTING`, que además es la primera).

## Capturas

- [Alta de un menú](images/e4d/offers-new.png): nombre, tipo en botones, precio por persona, «Cuándo se ofrece» y «Más opciones» plegado.
- [Degustación Costiña, menú del día](images/e4d/offers.png): la marca en la fila y en la ficha; «Hacerlo menú del día» apagado.
- [Menú del día (con elección)](images/e4d/offers-menu.png)
- [Carta](images/e4d/offers-carta.png)
- [Abrir mesa en el Comedor](images/e4d/service-open.png): «Menú o carta» con el menú del día propuesto.

## Qué NO cambia

- El esquema (v8), `grants.sql`, las copias y el instalador. Solo cambian las versiones.
- Los códigos de la API (`erp/offers`, `tasting`, `set-menu`, `a-la-carte`) y los cuerpos de las órdenes existentes: solo se añaden campos y órdenes.
- La orden incierta, los niveles Puesto y Responsable y el invariante F01.
- Las fechas siguen como texto dd/mm/aaaa (el selector de calendario del plan queda para más adelante).

## Comprobaciones automáticas

**`sh tools/agent/local-tests.sh` completo** (puerto 5090; en este PC con `PG_BIN` al PostgreSQL 17 instalado): dominio 81, ClientChecks 123 (la tabla del vocabulario pide «Menú degustación|Menú con elección|Carta»), las 14 suites HTTP y Realtime 12. Nuevas:
- `offers_http.test_06`: alta sin código (`menu-degustacion-de-otono-c`, producto `menu-menu-degustacion-de-otono-c`), repetición con la misma clave, choque con sufijo que cabe, alta sin orden al final, nombre de 60 caracteres con producto-menú válido, auditoría `catalog.product_created` y `catalog.price_set` del alta, `offer-update` con precio (vigente hoy y evento), sin precio (nada), en una carta (`product_not_allowed`), pases y platos sin código, nombre vacío rechazado, `offers-reorder` (orden nuevo, idempotente, 404, `duplicate_offer`, 403 para sala y cocina, auditoría).
- `catalog_http.test_08`: impuesto, categoría (con sufijo y al final), producto, presentación (al final) y tarifa sin código; nombre vacío y código que choca.

**vitest** (`frontend/pwa`): 65 pruebas; `tests/offers.test.ts` prueba la propuesta con varias ofertas, con una y con ninguna, y las coletillas.

**`sh tools/agent/local-tests.sh desktop`**, con estos checks nuevos o cambiados:
- la primera oferta vigente lleva la marca «Menú del día» en su fila y su ficha, sin botón; en otra, «Hacerlo menú del día» manda `offers-reorder {"offerIds":["CARTA"]}` y relee la configuración;
- «Subir» en el segundo pase manda dos `course-update` con los órdenes intercambiados; el primero no puede subir; con empates se renumera de 10 en 10;
- «Abrir mesa» propone el menú del día aunque antes se eligiera otra oferta;
- el alta de un menú sin precio se rechaza en la barra de estado y no envía nada; con precio, el código sale del nombre;
- la alta de un pase viaja sin `sort`; el tipo son dos botones y «Menú con elección» traduce a `set-menu`;
- todos los textos nuevos (lista, ficha, chips, vacíos, Inicio, Comedor) y las capturas.

## Guion para el promotor

Con la instalación actualizada a esta versión:
1. **Menús y cartas › Nuevo menú o carta**: escribe «Menú degustación de otoño», deja «Menú degustación», pon 65 en el precio y pulsa «Crear». Nace con su código y su producto-menú; en «Más opciones» verás el código generado.
2. **Añade un pase** con «Pase» (solo el nombre) y dos platos con «Plato»; pulsa «Subir» en el segundo plato.
3. **Pulsa «Hacerlo menú del día»** en el nuevo menú: la marca pasa a su fila.
4. **Comedor › Abrir mesa**: el campo «Menú o carta» ya propone el nuevo menú. En el comandero, «Abrir mesa» lo propone también.
5. **Cambia el precio**: en la ficha del menú escribe 70 en «Precio por persona» y «Guardar»; en Catálogo, su producto-menú tiene el precio nuevo desde hoy.

## Límites

- **Fechas** como texto dd/mm/aaaa: el selector de calendario queda para un corte posterior.
- **IVA** del producto-menú: sigue el IVA 10 % por defecto (`taxId` de `offer-create`); no hay campo en la ficha.
- **El producto de un plato** se teclea como código, como antes.
- **El comandero** cambia de aspecto en C3a; aquí solo propone el menú del día y cambia una palabra.
- **El código lo generan los dos**: el puesto lo propone del nombre (como C2b) y el motor también lo haría si viajara vacío; así otros clientes y las pruebas ya pueden omitirlo.
