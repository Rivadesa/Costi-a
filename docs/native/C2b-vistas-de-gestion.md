# C2b — Caja, Catálogo, Oferta, Salas y Dispositivos con el patrón del Comedor

Fecha: 2026-10-15. Rama `feat/c2b-vistas-de-gestion` sobre `develop` (`44fe713`, C2 y C2.1 mezcladas). Tercer corte del plan aprobado `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md`, sección C2b, aprobado el 15-10 («Adelante», con permiso para subir y mezclar en cuanto pase la CI). Versión **`0.38.0-c2b`**. **Sin cambio de esquema ni del motor** (sigue la v8): solo cambian el puesto principal (`Costina.Desktop`), sus checks y las versiones.

Lo que verá el promotor:
- **Las cinco vistas de gestión con el mismo patrón que el Comedor de C2**: cabecera de página con el resumen y la acción principal, lista a la izquierda con buscador, ficha a la derecha con el nombre y el estado en palabras, una sola barra de desplazamiento por columna y los botones grandes abajo, siempre a la vista.
- **El programa ya no se cierra al guardar sin «Código (fijo)»** (el fallo que encontró el 15-10 en Oferta). Cualquier dato mal escrito se cuenta en la barra de estado y no se envía nada. **Un código vacío se genera del nombre.**
- **Caja** con las cuentas abiertas en tarjetas (mesa, sala, personas, total y pendiente), las tres cifras, los consumos en tabla, «Todo lo pendiente», las formas de pago en botones y «Otras operaciones».
- **Catálogo** con pestañas Productos · Categorías · Tarifas · Impuestos, lista con miniaturas y buscador, árbol de categorías y la ficha con foto, datos, presentaciones y «Fijar precio».
- **Oferta** con chips de tipo, pases numerados y los platos desplegados dentro del pase elegido.
- **Salas, mesas y estaciones** con las salas a la izquierda, las mesas de la sala como tarjetas en el centro y la ficha a la derecha; «Estaciones» en su pestaña.
- **Dispositivos** con la misma cabecera, tarjetas con el punto y la palabra de la conexión y la estación por su nombre.
- **Fechas como se leen** (dd/mm/aaaa) en Oferta y en «Fijar precio».

## Qué cambia

### Piezas comunes (`Themes/Components.xaml`, `Ui.cs`)

- **`PageBar`, `PageTitle`, `PageSummary`, `PageAction` y `PageNote`**: la cabecera de página sobre el fondo gris, con el aviso a la derecha cuando lo hay (Caja).
- **`ListPane`, `PaneList`, `CardRows`, `RowTitle`, `RowDetail` y `CardRowTitle`**: la columna de lista (360 px, `ListWidth`) con filas de dos líneas o tarjetas grises; la elegida, blanca con borde de acento.
- **`Ficha`, `FichaContext`, `FichaTitle`, `FichaChip`, `FichaScroll`, `FichaActions`, `FichaSecondary` y `FichaDanger`**: la ficha con su encabezado, su único desplazamiento y los botones abajo.
- **`EmptyState` y `FichaEmpty`**: el vacío explicado (icono, título y texto). El de la ficha queda a la misma altura que el de la lista, que lleva debajo su botón de alta.
- **`Segmented`** (de C2) para las pestañas internas y las formas de pago; **`ToneChip`** para los estados con palabra.
- **`Ui.KeepSelectionInView`**: una lista cuya elección la mueve el modelo («Ver cuenta» desde el Comedor, un alta confirmada, «Ver todos», una mesa que cambió de sala) desplaza la fila elegida a la vista. Está encendida en `PaneList` y, por herencia, en `CardRows`. Solo pinta.
- **Filas desactivadas en gris** (`InkSoft`), no atenuadas con opacidad: el detalle de 12 px quedaba por debajo de 4,5:1.

### El cierre al guardar (`ShellViewModel.Attempt`, `Codes.cs`)

- **El fallo.** En Oferta, Catálogo y Salas, cada «Guardar» comprobaba el código, el nombre, el orden, las fechas, el precio o los días **antes** de entrar en `shell.Run`, con una excepción. Esa excepción salía del comando sin capturar y Windows cerraba el programa. Caja, Comedor y Dispositivos comprobaban dentro de `Run` y no les pasaba.
- **`Attempt`.** Los once «Guardar» y «Fijar precio» van ahora por `shell.Attempt`: una comprobación fallida se cuenta en la barra de estado («El nombre es obligatorio.», «Orden: introduce un número entre 0 y 9999.», «Plazas: introduce un número entre 1 y 60.», «Vuelve a elegir la tarifa: el desplegable se quedó sin elección.») y no se envía nada. No cabe dentro de `Run`, porque `Run` no hace nada si ya está ocupado.
- **Código generado.** Un código vacío se genera del nombre con la regla de la importación del catálogo (`Catalog.Slug` del motor, copiada en `Codes` porque el puesto no referencia el dominio): minúsculas sin tildes, dígitos y guiones. Es único entre sus hermanos (sufijos `-2`, `-3`…): las ofertas entre todas, los pases dentro de su oferta, los platos dentro de su pase, las presentaciones dentro de su producto, las mesas entre todas las mesas. **El código de una oferta se corta a 27 caracteres**, porque su producto-menú se llama `menu-<código>` y un código admite 32; los demás, a 32. Un código tecleado se comprueba como siempre y se respeta tal cual.
- **Los campos** siguen diciendo «Código (fijo)»; su ayuda dice que es opcional y que, una vez creado, no se puede cambiar.
- **El servidor sigue comprobando** cada código y su unicidad: esto solo ahorra inventarlo.

### Cuenta / Caja (`CheckoutView`, `CheckoutViewModel`)

- **Lista:** una tarjeta por cuenta abierta con mesa, total, sala y personas (del plano del Comedor ya leído), cobertura con su palabra y pendiente. Buscador por mesa o sala, que se esconde cuando no hay cuentas. El resumen de la cabecera suma lo pendiente cuando todas las tarjetas se han leído.
- **Orden:** el servidor lista las cuentas por identificador de servicio (un GUID: orden aleatorio). Las tarjetas y la cuenta elegida por defecto siguen el plano del Comedor (salas y mesas en su orden) y, para una cuenta que el plano no lista, el nombre de su mesa.
- **Lecturas:** el listado del servidor solo trae servicio, mesa y estado. El total y el pendiente de cada tarjeta salen de leer cada cuenta abierta (de cuatro en cuatro); una lectura que falla deja la tarjeta «Sin leer», nunca un importe inventado. Elegir otra tarjeta relee solo la elegida.
- **Ficha:** cabecera con «Mesa 4 · Abierta · Pago parcial» y las tres cifras (Total, Pagado y Pendiente grande); los consumos en tabla, con la fila «Añadir un consumo» encima; pagos y devoluciones; «Otras operaciones» (devolver lo pagado de más y reabrir, con motivo); abajo y siempre a la vista, el cobro.
- **Línea anulada:** entera en gris y tachada, también su cantidad y su importe; el título cuenta las líneas que suman y, aparte, las anuladas («Consumos · 3 líneas · 1 anulada»).
- **«Todo lo pendiente»** escribe el pendiente de la cuenta leída en el importe; no envía nada. **El importe y el motivo escritos para una cuenta se vacían al elegir otra tarjeta** (o cuando una relectura elige otra): el pendiente de una mesa nunca se cobra ni se devuelve en otra.
- **Pagado de más:** el botón grande no es «Registrar pago de prueba» (tomaría aún más), sino «Devolver lo pagado de más…», que baja a «Otras operaciones» y enfoca el motivo; registrar otro pago queda al lado. Con la cuenta saldada, el grande sigue siendo «Cerrar cuenta saldada».
- **No cambia:** «Ver cuenta» desde el Comedor (`Checkout.Request`), el aviso de registro de ensayo (ahora en la cabecera), F01 (la cuenta elegida dirige la lectura; la ficha se compone solo de la cuenta leída que coincide con la elegida), las órdenes, sus cuerpos y lo que anuncia el servidor.

### Catálogo y tarifas (`CatalogView`, `CatalogViewModel`)

- **Pestañas** Productos · Categorías · Tarifas · Impuestos, con recuento.
- **Productos:** lista virtualizada con miniatura, nombre, categoría (los dos últimos niveles), presentaciones y precio General; buscador sin tildes (`TextSearch`), filtro de categoría que incluye las subcategorías y «Ver todos». La fila toma el precio General de la **primera presentación activa que lo tenga** y solo dice «Sin precio» si ninguna lo tiene: lo mismo que cuenta el resumen y avisa la ficha.
- **Ficha del producto:** ruta de su categoría, nombre y estado; foto con «Cambiar imagen…» y «Quitar imagen» (E3.2); datos; presentaciones con su precio en cada tarifa activa («—» = cobra el de la General; «Sin precio» en la General, en ámbar); la presentación elegida con sus datos y «Fijar precio» (tarifa, importe y «Desde (dd/mm/aaaa)», que propone el hoy del servidor); abajo, guardar y desactivar o reactivar.
- **Categorías:** árbol con sangría y conector, recuento «16 categorías · 1 desactivada», ficha con «Ver sus productos».
- **Tarifas:** lista con ficha; la General siempre presente y sin desactivar; las salas que usan cada tarifa.
- **Impuestos:** lista con ficha y los productos que lo llevan.
- **Altas:** empezar un alta deja la lista sin elección y abre su pestaña; «Cancelar» vuelve a la ficha vacía; lo creado queda elegido cuando el servidor lo confirma. El producto nuevo nace en la categoría del filtro activo.
- **Desplegables estables:** una lista rehecha pierde su elección y no la recupera si el valor enlazado sigue igual. Las listas de los desplegables solo se sustituyen cuando cambia su contenido, y la elección se vuelve a dar (vacía y luego la buena).
- **`NameOfProduct`:** el nombre de un producto del catálogo leído, para que la ficha de una oferta nombre su producto-menú (que Caja no vende).

### Oferta: cartas y menús (`OffersView`, `OffersViewModel`)

- **Lista:** buscador y chips de tipo con recuento (Todas · Degustación · Menú cerrado · Carta). Cada fila lleva nombre, «Desactivada» si lo está, tipo, servicio y pases activos, y **la vigencia en su propia línea** («Sin fechas», «Del 01/09/2026 al 31/12/2026»).
- **Ficha:** contexto (tipo, servicio y días), nombre y estado; los **pases numerados** como tarjetas; el pase elegido se despliega con sus platos (o los artículos de la carta) en tabla, «＋ Plato» o la fila de añadir, y sus datos; «Datos de la oferta» con tipo, servicio, nombre, fechas (dd/mm/aaaa), días como botones y orden; abajo, «Guardar oferta» y desactivar o reactivar.
- **Producto-menú:** la ficha lo nombra por el catálogo leído («Se vende por persona como el producto «Degustación Costiña»…») y solo cae al código si ninguna lectura lo conoce.
- **Carta:** un artículo que ya no se ofrece es «retirado» (chip, botones y recuento del pase), no «desactivado».
- **Altas:** empezar una oferta, un pase o un plato deja esa elección vacía y «Cancelar» vuelve a la anterior; lo creado queda elegido cuando el servidor lo confirma; crear una oferta borra la búsqueda y el chip de tipo.

### Salas, mesas y estaciones (`OrganizationView`, `OrganizationViewModel`)

- **Pestañas** «Salas y mesas · 2» · «Estaciones · 5».
- **Salas:** una tarjeta por sala (nombre; mesas activas y plazas; sus chips «Desactivada» y «Tarifa Terraza» en su fila, que en 250 px no caben junto al nombre). Pulsar otra vez la sala elegida vuelve a su ficha desde la de una mesa.
- **Mesas:** las de la sala elegida como tarjetas en columnas (nombre, plazas y estado con palabra), con «Mesa» para añadir. **Una mesa desactivada es una tarjeta gris con el nombre en gris**; el borde discontinuo queda para «Libre» en el Comedor.
- **Ficha:** la de la mesa elegida (o la nueva) o, si no hay mesa, la de la sala (o la nueva): datos, la tarifa de la sala (E3.2), sus cifras de mesas y plazas y la lista de las desactivadas; «Ver la sala» desde la mesa. Después de una relectura la vista sigue a una mesa que una orden llevó a otra sala.
- **Estaciones:** lista con ficha; la estación Pase, marcada «Reservada»; el tipo con su ayuda.
- **Sin elección previa** se elige la primera sala **activa**.

### Dispositivos (`DevicesView`, `DevicesViewModel`)

- La misma cabecera; las tarjetas con el punto y la palabra de la conexión, el tipo y la estación por su nombre, quién lo aprobó y cuándo; ordenadas por tipo y nombre, como en Inicio.
- «Mostrar también los quitados» solo cuando hay alguno, con su recuento; el botón de quitar nombra el dispositivo y se esconde mientras está la barra de confirmación. Los nombres van entre «» en todos los textos.
- El asistente, la elección de estación y la conexión sencilla (B.2) no cambian.

### Fechas

Las fechas se escriben como se leen, **dd/mm/aaaa** (se admite también AAAA-MM-DD). Lo que viaja en la orden sigue siendo AAAA-MM-DD, byte a byte como antes.

## Revisiones

Antes de cerrar hubo tres revisiones independientes sobre la punta de las cinco etapas: aspecto frente a C2 y la maqueta, comportamiento e invariantes, y textos, dinero y pruebas (ningún `assert` debilitado, ninguna llamada a `ApiClient` cambiada). Se aplicaron sus **19 hallazgos**. Los más importantes:
- Caja listaba las cuentas en el orden del servidor (un GUID) y elegía por defecto una mesa cualquiera; la muestra de los checks lo escondía.
- El importe rellenado con «Todo lo pendiente» sobrevivía a elegir otra tarjeta: se habría cobrado en otra cuenta.
- Una línea anulada conservaba su importe en negrita, y el título contaba 3 líneas sobre una tabla de 4.
- Ninguna lista desplazaba a la vista la fila elegida por el modelo.
- Con dinero de más, el botón grande era «Registrar pago de prueba».
- La fila de un producto miraba solo la primera presentación y contradecía al resumen y a la ficha.

## Capturas

Las componen los checks del WPF con datos de ejemplo coherentes: la carta de vinos con fotos, tarifas General y Terraza e impuestos; las ofertas Degustación Costiña, Menú del día y Carta; las salas y mesas de C2; cuentas que cuadran con el tablero de C2.

**Caja:**
- [Mesa 4](images/c2b/checkout.png)
- [Pagado de más](images/c2b/checkout-credit.png)
- [Sin cuentas abiertas](images/c2b/checkout-empty.png)

**Catálogo:**
- [Productos, con el Albariño](images/c2b/catalog.png)
- [Fijar precio](images/c2b/catalog-price.png)
- [Categorías](images/c2b/catalog-categories.png)
- [Tarifas](images/c2b/catalog-tariffs.png)
- [Impuestos](images/c2b/catalog-taxes.png)
- [Catálogo vacío](images/c2b/catalog-empty.png)

**Oferta:**
- [Degustación Costiña, con el pase Pescado desplegado](images/c2b/offers.png)
- [Menú del día](images/c2b/offers-menu.png)
- [Plato](images/c2b/offers-dish.png)
- [Carta, con un plato retirado](images/c2b/offers-carta.png)
- [Sin ofertas](images/c2b/offers-empty.png)

**Salas, mesas y estaciones:**
- [Sala principal](images/c2b/configuration.png)
- [Mesa 4](images/c2b/configuration-table.png)
- [Estaciones](images/c2b/configuration-stations.png)
- [Sala desactivada](images/c2b/configuration-inactive.png)
- [Sin salas](images/c2b/configuration-empty.png)

**Dispositivos:**
- [Lista](images/c2b/devices.png)
- [Quitar, con confirmación](images/c2b/devices-confirm.png)
- [Sin dispositivos](images/c2b/devices-empty.png)

## Qué NO cambia

- El motor, el esquema (v8), `grants.sql`, las copias y el instalador. Solo cambian las versiones.
- Las órdenes, sus cuerpos y sus `CanExecute`: cada botón sigue con su orden y lo que anuncia el servidor. Lo comprobó la revisión de pruebas, llamada a llamada, contra la punta de C2.1.
- La orden incierta, los niveles Puesto y Responsable (ADR-013) y el invariante F01.
- Inicio y Comedor (C2), la barra de menús y la PWA (C3).

## Comprobaciones automáticas

**`sh tools/agent/local-tests.sh` completo** (puerto 5090; en este PC con `PG_BIN` al PostgreSQL 17 instalado): dominio 81, ClientChecks 123 (sin cambios: `Costina.Client` no se toca), las 14 suites HTTP (packaging 14 con 1 saltada, native 11, security 6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 8, offers 6, pwa 5 con 1 saltada) y Realtime 12.

**`sh tools/agent/local-tests.sh desktop`**, con estos checks nuevos o cambiados:
- **El cierre al guardar:** en Oferta, sin nombre y con el orden mal escrito no se envía nada y la barra lo dice; sin código se genera del nombre y una oferta se recorta a 27 («Menú de la noche de verano 2026» → `menu-de-la-noche-de-verano`); en Catálogo, una tarifa sin nombre se rechaza y sin código nace como `barra`; en Salas, unas plazas mal escritas se rechazan; fijar un precio sin tarifa va a la barra de estado; `Codes` a secas (sufijos, ASCII, código tecleado intacto).
- **Caja:** las tarjetas con el servidor al revés del plano, el orden y la cuenta por defecto; las lecturas de cada tarjeta, una por cuenta; el importe que no sobrevive al cambio de tarjeta; la tarjeta elegida por el modelo a la vista; «Todo lo pendiente», las formas de pago, el cobro con el cuerpo de siempre, la cuenta saldada, el pagado de más con su botón grande, la devolución; sin cuentas y sin elección.
- **Catálogo:** pestañas con recuento, filas, filtro con subcategorías, búsqueda sin tildes que conserva la elección, altas con cancelar, alta de tarifa, ficha de impuesto, el precio de la fila con dos presentaciones, las capturas de tarifas e impuestos con las mismas tres tarifas.
- **Oferta:** filas con la vigencia en su línea, chips con recuento, ficha, pases numerados, platos y artículos («retirado» en la carta), altas con cancelar y confirmadas, el producto-menú por su nombre.
- **Salas:** pestañas con recuento, tarjetas de sala y de mesa (la desactivada, gris), ficha de sala y de mesa, altas, desactivar y reactivar, la mesa que cambia de sala, estaciones.
- **Dispositivos:** cabecera, tarjetas, quitados, confirmación con «» y la conexión sencilla.
- **General:** `SameLayout` (cabecera, lista y ficha en el mismo sitio en las cinco vistas, con la barra desplegada y plegada), `Clipped` (ningún texto cortado), `NestedScrolls` (ninguna barra de desplazamiento anidada), encaje a 1.280 × 860, barrido de textos sin valores en crudo y ningún error de enlace.

## Guion para el promotor

Con la instalación actualizada a esta versión:
1. **Oferta › Nueva oferta**: escribe solo el nombre («Menú de la noche») y pulsa «Crear oferta». No se cierra nada: la oferta nace con el código `menu-de-la-noche`.
2. **Repite sin nombre**: la barra de estado dice «El nombre es obligatorio.» y no pasa nada más.
3. **Catálogo › Productos**: busca un vino sin tildes («albarino»), elígelo, pulsa su presentación y «Fijar precio» con una fecha como `20/10/2026`.
4. **Caja**: las cuentas salen en el orden del plano; pulsa «Todo lo pendiente» en una y elige otra tarjeta: el importe se vacía.
5. **Salas**: crea una mesa dejando el código vacío; mira que la desactivada sale en gris con su palabra.
6. **Dispositivos**: pulsa «Quitar …» y mira la confirmación antes de cancelarla.

## Límites

- **Lecturas de Caja.** Cada actualización hace N+2 lecturas (listado, catálogo de caja, la cuenta elegida y una por cada otra cuenta abierta). Un campo de importes en `GET /checkout/accounts` las evitaría: es un cambio del motor, fuera de C2b.
- **Reabrir una cuenta** desde la lista es casi inalcanzable, porque Caja solo lista las abiertas; se conserva y se prueba en el modelo.
- **Anular un consumo:** el servidor anuncia `void-charge`, pero el puesto no tiene botón. Hueco anterior a C2b.
- **El producto de un plato** se teclea como código; la ayuda lo nombra cuando es un vendible.
- **Nombres de dispositivo** en tres líneas, no en dos como decía el plan: quién lo aprobó y cuándo tenían que verse.
- **Códigos generados** solo en estas tres vistas; `organization-*` desde otros clientes sigue exigiendo código. E4d lleva la generación al motor.
