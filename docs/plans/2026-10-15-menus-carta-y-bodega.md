# Plan: menús, carta y bodega más intuitivos (cortes E4d, E3.3, C3a, C3b, E4e, C3c, E4c, E4f y E4g)

Fecha: 2026-10-15. **Estado: APROBADO por el promotor el 15-10-2026.** Contestó las siete preguntas del resumen; las respuestas y lo que cambian están en la sección 10, y las decisiones en la 9. El orden de entrega pasa a ser E4d, E3.3, C3a, C3b, E4e, C3c, **E4f, E4c**, E4g.

> **Nota del 22-10: el orden que queda lo sustituye otro plan.** E4d, E3.3, C3a y C3b están entregados. A partir de ahí manda `docs/plans/2026-10-22-sala-y-cocina.md`, APROBADO por el promotor el 22-10: C3c (dos aplicaciones, Costiña Sala y Costiña Cocina), E4e, C3d (Costiña Cocina), C3e (Costiña Sala), E4f, E4c y E4g, con versiones nuevas a partir de `0.43.0-c3c`. El C3c de la sección 4.6 se reparte entre C3d (cocina) y C3e (sala), y el nombre «C3c» pasa a las dos aplicaciones. Lo aprobado aquí para E4e, E4f, E4c y E4g sigue valiendo con las concreciones de ese plan.

**Maqueta del comandero:** `docs/plans/maquetas/2026-10-15-comandero.html`. Se abre en el navegador y tiene pestañas para abrir mesa, mesa, bodega, ronda, combinados, menú cerrado, carta y PC. Capturas en `docs/plans/maquetas/2026-10-15-comandero/`.

## Resumen para el promotor

Lo que vas a tener, en este orden, después de C2b (que ya arregla el fallo que encontraste: el programa se cerraba al guardar sin código):
1. **Palabras claras y menú de la casa (E4d):** al abrir una mesa ya sale propuesto el menú de la casa; crear un menú pide nombre, tipo y precio por persona, sin códigos.
2. **Cocina, Bodega y vinos por copa (E3.3):** el catálogo separado en Cocina y en Bodega y bebidas; marcas muchos vinos por copa y escribes sus precios de una vez; Caja con buscador.
3. **Comandero nuevo (C3a):** el móvil y la tableta con el mismo aspecto que el PC, y las mesas por sala y por su nombre.
4. **Bodega y bebidas (C3b):** buscador, Copa o Botella, fotos y la ronda entera de una vez; y en el PC, «Añadir consumición» en la propia mesa abierta, sin ir a Caja ni pedir contraseña; sin precios, como en el comandero: los importes, en Caja.
5. **Alergias (E4e):** cocina deja escrito qué adapta a cada comensal antes de que salga el pase, y marca los platos de una vez.
6. **Abrir y servir el menú (C3c):** abrir mesa en 3 toques, con las alergias en la misma hoja; platos agrupados («Ostra × 7») en el comandero y en cocina.
7. **Combinados (E4c):** «Gin tonic Premium» a precio fijo, eligiendo ginebra y tónica.
8. **El menú en una pantalla (E4f):** cambiar los platos del menú, o del menú del día, en un minuto.
9. **Carta por familias (E4g):** entrantes, carnes, pescados y postres, y la comanda entera de una vez.

**Cambia una palabra:** tu «menú cerrado» (todos comen lo mismo) se llamará así en el programa; lo que hoy el programa llama «Menú cerrado» (cada uno elige) pasa a ser «Menú con elección», guardado en «Más opciones».

**Se queda como está:** comanderos y cocina nunca ven precios ni importes; cobrar, anular y cerrar cuentas sigue en Caja; nada se borra, solo se desactiva.

**Preguntas** (basta una palabra o una línea; entre paréntesis, lo que proponemos):
1. Si un comensal tiene alergia o intolerancia, ¿cocina debe decidir cada plato suyo («no le afecta» o «adaptado: …») antes de que salga el pase, y el pase espera hasta entonces? (sí)
2. ¿Lo mismo para las intolerancias leves y las preferencias, como «no le gusta el cilantro»? (sí: cocina las despacha con un toque)
3. IVA de los vinos en sala: ¿21 %, como en la tienda, o 10 %? Pregúntalo a tu gestor.
4. ¿Las bebidas y los combinados necesitan una pantalla en la barra, o basta con verlos en la mesa y en Caja? (basta, por ahora)
5. ¿Necesitas ya que un comensal no tome el menú, o cambiar las personas con la mesa abierta? (más adelante)
6. ¿Tu menú lleva un vino incluido en cada pase (maridaje)? (si no lo lleva, nada cambia)
7. ¿Quieres «El menú en una pantalla» antes que los combinados? (sí, si ya usas el menú del día)

Contestadas el 15-10: 1 sí; 2 sí; 3 el IVA queda pendiente del gestor y configurable desde el programa; 4 basta por ahora; 5 más adelante; 6 un menú puede llevar maridaje o no; 7 sí, el programa sirve para distintos negocios y hay que poder añadir un menú del día.

## Base técnica

Base: `develop` en `8bb94a0` (C2.1 mezclada, versión `0.37.1-c2.1`, esquema v8). Va **justo después de C2b** (`0.38.0-c2b`) y **antes de E8 reservas**, e **incluye C3** (el aspecto nuevo de la PWA), como decidió el promotor el 15-10.

**Supuesto:** C2b solo reordena Caja, Catálogo, Oferta, Salas y Dispositivos del puesto principal (ficha a la derecha, listas con buscador) y arregla el cierre del programa al guardar (sección 4.1), sin tocar el motor ni el esquema. Si cambia algo de eso, este plan se ajusta.

## Origen

El 15-10, tras probar con su bodega real (los 1.037 vinos importados de su tienda), el promotor pidió:
- poder definir **menús** (cerrado, como uno de degustación, o un menú del día) y una **carta de comida** (entrantes, carnes, pescados y mariscos, postres…), **separados de la bodega y las bebidas**;
- **ver mejor la bodega** desde el comandero, donde hoy cuesta añadir una copa de vino sin un buscador o algo más intuitivo;
- en vinos y bebidas, poder elegir **copa** o **botella**, y pedir **combinados**.

Después aclaró:

> «Antes de que sigas, ten en cuenta que para Costiña, por ejemplo, el menú es cerrado y no se elige cada plato, los comensales comen todos lo mismo en cada uno de los pases a menos que hay intolerancias o alergias. Tenemos que hacer el sistema más intuitivo.»

Y el mismo 15-10, sobre el puesto principal:

> «para añadir una consumición a una mesa no tenemos por qué salir de la mesa e ir a caja, ¿no podemos hacerlo directamente en la mesa abierta?»

Respuesta ya dada al promotor: sí. La ficha de la mesa del Comedor tendrá **«Añadir consumición»**, con el mismo selector «Bodega y bebidas» del comandero, también en el nivel Puesto y sin contraseña; Caja queda para cobrar, anular y cerrar. Va en C3b (sección 4.4), porque usa la misma orden del motor y la misma regla de una orden por ronda.

Las referencias `fichero:línea` son de `8bb94a0` y salen de cinco lecturas hechas para este plan: comandero y cocina, catálogo, oferta, envío a estaciones y práctica del sector. Rutas cortas:
- `Dining/` = `dotnet/src/Costina.Modules.Dining/` y `Dining.Domain/` = `dotnet/src/Costina.Modules.Dining.Domain/`;
- `Core.Domain/`, `Core.Persistence/`, `Server/`, `Desktop/` y `Client/` = `dotnet/src/Costina.<nombre>/`;
- `pwa/` = `frontend/pwa/src/`.

## 1. Punto de partida

### Oferta y palabras

- **El motor ya hace lo que hace Costiña.** Una oferta `tasting` crea al abrir una elaboración por plato y por comensal, y cobra personas × precio del menú (`Dining/DiningOperations.cs:28-29,35-43`). Nadie elige nada.
- **Choque de palabras.** El sistema llama «Menú cerrado» al tipo en que cada persona elige (`set-menu`), y «Degustación» al que el promotor llama menú cerrado. Aparece en:
  - `Client/Labels.cs:36,106`;
  - `Desktop/Views/ServiceView.xaml:373`, `OffersView.xaml:9,25`, `HomeView.xaml:344` y `MainWindow.xaml:355` («Degustaciones, menús cerrados y carta.»);
  - `Desktop/ViewModels/HomeViewModel.cs:279` y `OffersViewModel.cs:117` (con sus plurales propios: «degustaciones», «menús cerrados»);
  - `pwa/views/BoardView.vue:70` (texto escrito en la plantilla: `pwa/labels.ts` aún no tiene tipos de oferta);
  - las aserciones que fijan esos textos: `tests/Costina.ClientChecks/Program.cs:463` y `tests/Costina.DesktopChecks/Program.cs:617`;
  - la tabla de vocabulario del plan del nuevo aspecto (`docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md:142`), que admite cambios antes de C3 (`:154`).
- **Campos técnicos en el camino principal**:
  - en ERP › Oferta: «Código (fijo)» en oferta, pase y plato (`OffersView.xaml:23,60,86`), números de «Orden» (`:33,61,87`), fechas escritas AAAA-MM-DD (`:31-32`) y el producto escrito como código (`:43,92`);
  - en Catálogo: código (`CatalogView.xaml:24,98,116,145,157`), orden (`:30,108,119,146`), color `#RRGGBB` (`:29`) y fecha del precio (`:128`);
  - el motor exige el código en cada alta (`Core.Domain/Offer.cs:26,38,41`; `Server/CatalogOperations.cs:87`), aunque ya sabe derivarlo de un nombre (`Catalog.Slug`, `Core.Domain/Catalog.cs:141-155`, que usa la importación en `Server/CatalogImport.cs:159-162`).
- **El precio del menú no se cambia desde la oferta:** `offer-update` ignora `priceCents` (`Server/OfferOperations.cs:64-75`).
- **Hueco de auditoría ya existente:** `offer-create` crea el producto-menú, su presentación y su precio (`OfferOperations.cs:48-55`) pero solo emite `offer.offer_created` (`:61`); no hay `catalog.product_created` ni `catalog.price_set`, en contra de «toda escritura deja auditoría y outbox» (`dotnet/AGENTS.md:9`).
- **El producto-menú se llama `menu-<código de la oferta>`** (`OfferOperations.cs:48`) y un código admite como mucho 32 caracteres (`Core.Domain/Organization.cs:15,22`). Con códigos tecleados cortos no pasa nada; con un código generado de un nombre largo, `menu-` + 32 caracteres no cabe.
- **Cambiar los platos del menú es plato a plato.** Cada plato nuevo pide código, nombre, orden y estación; cada plato viejo se desactiva aparte; y cada guardado relee el catálogo entero (`OffersViewModel.cs:185-191`).

### Abrir mesa y servir el menú de la casa

- **Propuesta del menú al abrir:**
  - la PWA solo lo propone si hay **exactamente una** oferta (`pwa/views/BoardView.vue:40`);
  - el formulario está en un `<details>` plegado (`:63-73`) y, tras abrir, no lleva a la mesa (`:31-32`);
  - el puesto principal propone **la que se eligió la vez anterior** y, si no hay, la primera por orden (`Desktop/ViewModels/ServiceViewModel.cs:228`): después de abrir una mesa de carta, la siguiente también sale con la carta;
  - `/configuration.offers` llega ordenada por `sort,id` (`Core.Persistence/OfferUnit.cs:120`).
- **Empezar son dos toques separados:** «Iniciar servicio» y «Enviar siguiente pase» (`pwa/views/ServiceActions.vue:25,77-78`).
- **Una línea por comensal.** Un menú de 8 personas y 3 platos son 24 líneas:
  - en el comandero (`BoardView.vue:88-93`);
  - en cocina, con «Empezar» y «Marcar lista» por línea (`Dining.Domain/AllowedActions.cs:55-58`; `pwa/views/KitchenView.vue:71-77`): hasta 48 órdenes antes de validar;
  - en el Comedor del puesto, que cuenta «N de M elaboraciones listas» por persona (`Client/FloorPlan.cs:95-115`).

### Alergias

- **Se declaran después de abrir**, una por orden, y por defecto para **toda la mesa** (`ServiceActions.vue:17,127-149`).
- **La revisión de cocina solo cubre lo ya enviado.** «No afecta / adaptar / rehacer», con nota, existe solo para pases enviados (`Dining.Domain/CourseExecution.cs:141-146,152`).
  - El caso normal de Costiña es que la alergia se conoce al sentarse. En ese caso no queda ninguna decisión registrada: la línea de esa persona solo lleva el aviso ⚠ y su plato conserva el nombre.
  - Nadie ve, por ejemplo, «Comensal 3: tosta sin gluten».
- **`fire-next` se bloquea si queda cualquier revisión pendiente**, de cualquier pase (`Dining.Domain/DiningService.cs:44`), y el servidor no lo anuncia mientras tanto (`Dining.Domain/AllowedActions.cs:18-19`).
- **Revisar exige una mesa en servicio:** `review-preparation` pasa por `KitchenAllowed`, que solo admite «En servicio» o «En pausa» (`Dining.Domain/Restrictions.cs:79`; `DiningService.cs:152-158`), y solo se anuncia en un pase activo (`AllowedActions.cs:58`). Con la mesa «Sentados» no se puede revisar nada.
- **Las instantáneas guardadas fijan la regla actual.** Al restaurar una mesa se rechaza (`invalid_snapshot`):
  - una elaboración por revisar en un pase que no está en cocina (`Dining.Domain/Snapshots.cs:80-81`);
  - un acuse pendiente en una mesa que no está en servicio (`Snapshots.cs:55-57`).

  La compatibilidad con D3.2 marca por revisar las elaboraciones de los pases activos cuando el acuse viejo no trae marcas (`Snapshots.cs:61-62`).
- **Una restricción de toda la mesa, o una elaboración sin comensal, se aplican a todo** (`Restrictions.cs:28-29`): un plato de carta «para toda la mesa» lleva el aviso de cualquier restricción de cualquier comensal.

### Bebidas en el comandero

- **Un único `<select>` con todo el catálogo.** Así es «Añadir consumo a mayores» (`ServiceActions.vue:115-125`):
  - con los 1.037 vinos son unos 2.000 renglones, sin buscador;
  - cada renglón muestra solo la hoja de la categoría («Rías Baixas · X · Botella 0.75 L»);
  - copa y botella salen como productos distintos.
- **`GET /catalog`** (`Core.Persistence/CatalogUnit.cs:183,218-227`):
  - da una fila por producto × presentación con precio en la tarifa General;
  - no lleva el padre de la categoría, ni la referencia, ni la estación;
  - corta en 5.000 filas;
  - se lee una vez al montar la pantalla y nunca se refresca (`BoardView.vue:41`).
- **`add-consumption` solo apunta un cargo** (`Dining/DiningOperations.cs:137-153`):
  - no crea elaboración ni mira la estación del producto, así que un plato pedido por aquí se cobra y la cocina no se entera;
  - devuelve la versión sin moverla;
  - su único evento es `account.charge_added`, que solo llega al rol principal (`Core.Persistence/OutboxPublisher.cs:48`; `Server/RealtimeEvents.cs:63`);
  - lo mismo pasa con `add-product` y `void-charge` de Caja (`Server/CheckoutOperations.cs:19-30`): solo emiten `account.*`, así que ningún comandero se entera de un consumo añadido o anulado en Caja.
- **La versión del servicio sube con cada toque de cocina** («Empezar», «Marcar lista»…), y `add-consumption` exige la versión exacta (`DiningOperations.cs:139`). Con la cocina trabajando, un consumo preparado contra una versión vieja recibe `version_conflict`.
- **`GET /dining/board` ya lee servicio y ocupación mesa a mesa** (`Dining/DiningUnit.cs:53-64`; deuda «N+1 en `Board()`» en `docs/STATUS.md`), y cada aviso hace que todos los comanderos lo relean entero.
- **El camarero no ve lo que ha pedido la mesa:** la vista del servicio no lleva consumos (`Dining.Domain/ServiceModels.cs:42-50`).
- **Una ronda de 4 bebidas son 4 idas y vueltas**, con la pantalla bloqueada entre ellas (`pwa/operations.ts:37`), porque solo hay una orden incierta cada vez (`dotnet/AGENTS.md`, D6.3).
- **La línea de la cuenta dice «Gin tonic Premium · Unidad»:** `add-consumption` no quita la presentación por defecto (`DiningOperations.cs:147`), aunque `add-dish` sí lo hace para cocina (`:129`).

### Catálogo

- **Copa y botella ya son el modelo.** Un producto puede tener varias presentaciones, cada una con su precio: la demo trae `glass`/Copa a 9,50 y `bottle`/Botella a 42,00 (`Server/LabConfiguration.cs:29-33`).
  - La importación de la tienda crea `botella-0-75-l` o `unit` (`tools/catalog/woocommerce-to-csv.py:235,239`; `CatalogImport.cs:185`).
  - Los vinos entran **con IVA 21 %** por defecto (`woocommerce-to-csv.py:213,240`).
- **Marcar un vino «por copa» hoy son dos órdenes:** `presentation-create` y `price-set` (`CatalogOperations.cs:113-118,149-157`), cada una con relectura completa.
  - La lista de productos no admite selección múltiple (`CatalogView.xaml:52`).
  - El filtro por categoría no incluye subcategorías (`CatalogViewModel.cs:231`).
- **No hay «Cocina» ni «Bodega».** Los tipos de vino (Tintos, Blancos…) son categorías raíz (`woocommerce-to-csv.py:229-238`). Lo único que distingue hoy un plato de una bebida es la estación: «sin estación: bebida o consumo directo» (`Core.Domain/Catalog.cs:10-11`; `CatalogViewModel.cs:120`).
- **Fallo encontrado:** reimportar un producto existente borra su estación (`CatalogImport.cs:177` no pasa `stationId`). Después, `add-dish` lo rechaza con `station_required`.
- **Reimportar también pisa lo ordenado a mano:** a un producto existente le vuelve a poner el nombre, la categoría y el IVA del fichero (`CatalogImport.cs:172-177`; sin columna `tax`, el 10 %: `:103`). Un vino movido de familia o con el IVA corregido en Costina vuelve a como estaba en la tienda.
- **Los productos-menú no tienen estación** (`OfferOperations.cs:52`): cualquier regla «sin estación = bebida» los mete en las bebidas si no se excluye la categoría `menus`, como ya hace `/catalog` (`CatalogUnit.cs:226-227`).
- **Las órdenes leen un registro plano y rechazan campos desconocidos** (`Core.Persistence/Contracts.cs:12`, `UnmappedMemberHandling.Disallow`): `DiningCommand` (`DiningOperations.cs:10-12`), `CatalogCommand` (`CatalogOperations.cs:12-14`) y `OfferCommand` (`OfferOperations.cs:13-15`). Una orden nueva con listas (`lines`, `items`, `productIds`…) necesita esos campos en el registro, o una ruta propia.
- **La Caja del puesto** elige el artículo en un desplegable sin buscador sobre todo el catálogo (`Desktop/Views/CheckoutView.xaml:21`).

### Consumos desde la mesa en el puesto principal

- **Hoy solo se añaden en Caja:** `Add` de `Desktop/ViewModels/CheckoutViewModel.cs:141-152` (orden `add-product` de `/checkout`, con `/checkout/catalog`, que lleva precios y es solo del principal: `Server/DesktopReadRoutes.cs:68-70`) y el desplegable de `CheckoutView.xaml:21`. La ficha de la mesa del Comedor no tiene esa acción (`Desktop/Views/ServiceView.xaml:197-241`).
- **En el nivel Puesto, Caja pide la contraseña del responsable:** «Ver cuenta» pasa por `RequestElevation` (`ServiceViewModel.cs:496-505`), y el saludo del Puesto lo dice («Cobrar, configurar o conectar dispositivos pide la contraseña del responsable», `HomeViewModel.cs:108`).
- **El motor ya lo permite en la mesa:** `add-consumption` es acción de sesión de sala y principal (`Dining/DiningModule.cs:20`; `Affordances.Allows`, `Dining/Affordances.cs:12-18`; fijado en `dotnet/tests/http/desktop_reads.py:94` y `checkout_http.py:40-41`), y la PWA la usa (`pwa/views/ServiceActions.vue:47`). El nivel Puesto es un dispositivo de sala, así que el servidor ya se la anuncia.
- **`GET /catalog`**, sin importes, admite principal y sala (`DesktopReadRoutes.cs:53-55`), y el puesto ya pide las fotos con su credencial, una vez por huella (`Client/ApiClient.cs:93-122`; `Desktop/ImageLoader.cs`).

### Combinados

- **No existen.** No hay opciones en el producto, ni campo para ellas en `add-consumption` (`DiningOperations.cs:10-12`), ni en la línea de la cuenta (`Core.Domain/SettlementAccount.cs:7-8`).
- **Estaban reservados como E4c**: `docs/plans/2026-09-23-e4-oferta-configurable.md:39`, `docs/native/E4b-carte.md:21,33` y `docs/native/E3-catalog.md:35`.
- **Verial ya agrupaba** «Combinados, Vinos copas, Vinos botellas» (`docs/plans/2026-09-23-referencia-verial.md:10`).

### Carta

Una carta ya se puede definir: tipo `a-la-carte`, con la demo `LAB-CARTA` (`LabConfiguration.cs:57-69`). Pero hoy tiene tres problemas:
- **cada grupo es un pase**, así que Carnes y Pescados saldrían en pases distintos (`DiningService.cs:46`);
- **un grupo sin platos hay que omitirlo a mano**, con motivo, antes de seguir y antes de terminar (`CourseExecution.cs:87`; `DiningService.cs:130`);
- **cada plato es una orden**.

### Reglas que ponen límites

- **Sin dinero en comanderos ni en cocina** (ADR-007, `docs/ADR/ADR-007-service-control-vs-checkout.md:29-36`). La PWA rechaza entera cualquier respuesta con una clave que contenga `price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff` (`pwa/money-guard.ts:4`).
  - ADR-007 tampoco deja mostrar la «cuenta provisional» en sala (`:31`). Enseñar lo que ha bebido una mesa, aunque sea sin importes, necesita dejarlo escrito en ese ADR.
  - **Ni siquiera las palabras:** las E2E fallan si el texto visible del comandero lleva `€`, «precio», «saldo», «pago» o «cuenta» (`frontend/pwa/e2e/board.spec.ts:103`), y en cocina basta con que aparezcan dentro de otra palabra (`e2e/kitchen.spec.ts:151`).
- **`call()` trata cualquier respuesta que no sea 2xx como error** (`pwa/api.ts:53-56`) y pide todo con `cache:'no-store'` (`:42`): un 304 hoy sería un fallo.
- **CSP estricta, sin `blob:`** (`Server/PwaHosting.cs:37-38`). Hoy la PWA no puede pintar las fotos que ya guarda el motor (E3.2), porque la imagen se pide con token Bearer.
- **Las pruebas E2E fijan la interfaz** por `select[name=table|menu|product]` y por `data-testid` (`frontend/pwa/e2e/commands.spec.ts:73-74,161`). Playwright solo corre en escritorio (`frontend/pwa/playwright.config.ts:12`).
- **vitest corre sin DOM** (`frontend/pwa/vite.config.ts:11`): solo se prueba lógica sacada a ficheros `.ts`, nunca un componente `.vue`.
- **No existe `dining_http.py`.** Las órdenes de sala se prueban en `checkout_http.py` y `pwa_http.py` (`add-consumption`), `offers_http.py` (`add-dish`, `choose`), `restrictions_http.py` y `station_http.py` (revisión y matriz de estación) y `affordances_http.py` (qué anuncia cada rol) (`dotnet/tests/http/`).

## 2. Qué significa «más intuitivo» en este plan

Reglas para todos los cortes:
1. **Lo normal, sin preguntar.** Al abrir una mesa se propone el menú de la casa (el primero vigente de la lista) y las personas empiezan en 2. Un vino con una sola presentación se añade con un toque.
2. **Lo de Costiña, en el camino principal; lo de otros restaurantes, en «Más opciones».** El «Menú con elección» sigue existiendo, pero:
   - al crear un menú solo se ofrece dentro de «Más opciones»;
   - en la mesa no se ve si no se usa.
3. **Nada técnico en el camino principal.**
   - Códigos fijos, números de orden, colores y fechas de vigencia se generan o van bajo «Más opciones».
   - El código se genera del nombre al crear y no cambia después: sigue siendo la clave estable.
4. **Palabras llanas y nombres, no códigos.**
   - Mesas, salas y estaciones se nombran por su nombre.
   - Para contar gente en la mesa se dice «personas»; una persona concreta es «Comensal n» (tabla del nuevo aspecto).
   - La PWA lleva tildes.
5. **Botones grandes y pocas pantallas.**
   - Nada de desplegables de cientos de filas: rejillas de botones, buscador y chips.
   - Objetivos táctiles de al menos 48 px en el comandero y 64 px en cocina.
6. **Una orden por gesto del usuario.** Una ronda de bebidas, una comanda o «todas listas» viajan como **una sola orden** con varias líneas. Así se mantiene una sola orden incierta.
   - La ronda o la comanda que se va llenando es un **borrador en memoria**, nunca una cola de órdenes: no se guarda en IndexedDB, se ve siempre como «Sin enviar» y, si la página se recarga, se pierde y la pantalla lo dice (D6.1: nada recordado se presenta como actual; D6.3: nunca una cola).
   - Mientras la orden anterior sigue incierta se puede seguir llenando el borrador, pero no enviarlo.
7. **Lo que el comandero no sabe, no lo inventa.** Ni alérgenos de un plato, ni uva, ni añada, ni «agotado», ni «más pedidos»: hoy el catálogo no guarda nada de eso (`Core.Domain/Catalog.cs:9-12`). Solo se pinta lo que da el servidor.

Criterios medibles. Se comprueban en las pruebas de cada corte y en el guion físico de C3:

| Tarea | Hoy | Objetivo |
|---|---|---|
| Abrir mesa con el menú de la casa | desplegar el formulario, 3 campos y botón; elegir la oferta si hay más de una | mesa libre → personas → «Abrir»: 3 toques |
| Declarar «Comensal 3, alergia al gluten» al abrir | abrir, desplegar, 4 campos, botón: otra orden | en la misma hoja de apertura y en la misma orden |
| Copa de un vino conocido | buscarlo a ojo en un desplegable de ~2.000 filas | Bebidas → 3-4 letras → «Copa» (suma 1 a la ronda) → «Enviar»: 4-5 toques |
| Ronda de 4 bebidas | 4 órdenes, con la pantalla bloqueada entre ellas | 4 toques + «Enviar»: 1 orden |
| Copa para una mesa abierta, desde el PC | salir a Caja con «Ver cuenta» (en el nivel Puesto, con la contraseña del responsable) y buscar el artículo en un desplegable de todo el catálogo | en la ficha de la mesa: «Añadir consumición» → 3-4 letras → «Copa» → «Enviar»: 1 orden, sin contraseña |
| Marcar listo en cocina un plato de 8 personas | 8 órdenes (16 con «Empezar») | 1 toque (2 con «Empezar») |
| Marcar 20 vinos por copa con su precio (y desmarcar otros) | 40 órdenes, cada una con relectura completa | marcar, escribir precios y «Guardar»: 1 orden |
| Cambiar los platos de un pase del menú | por plato: código, nombre, orden y estación; desactivar los viejos uno a uno | escribir los nombres y «Guardar»: 1 orden |

## 3. Vocabulario

Solo cambian las etiquetas. Los códigos internos (`tasting`, `set-menu`, `a-la-carte`) se quedan, así que no hay migración ni cambio de API.

| Código | Hoy | Propuesta | Ayuda que se muestra |
|---|---|---|---|
| `tasting` | Degustación | **Menú degustación** (decisión 15-10: es el nombre del menú de Costiña, con estrella; no se renombra) | «Todos comen lo mismo; se sirven todos sus platos, pase a pase.» |
| `set-menu` | Menú cerrado | **Menú con elección** (en «Más opciones») | «Cada persona elige un plato en cada pase, entre los que pongas.» |
| `a-la-carte` | Carta | **Carta** | «Platos sueltos que se cobran al pedirlos.» |

- **Decisiones del 15-10 (tarde), al aprobar E4d:**
  - el tipo `tasting` se llama **«Menú degustación»** (no «Menú cerrado»): es el nombre del menú de Costiña y así lo conoce el promotor;
  - la sección **«Oferta: cartas y menús» pasa a llamarse «Menús y cartas»** («oferta» suena a rebaja); en los textos, «menú» o «carta» según el tipo, y «menú o carta» cuando vale cualquiera; los códigos de la API (`offers`) no cambian;
  - el menú que se propone al abrir mesa se llama **«Menú del día»** (marca «Menú del día: se propone al abrir mesa» y botón «Hacerlo menú del día»), en lugar de «menú de la casa»;
  - «Más opciones» para lo técnico y «Subir/Bajar» para pases y platos: sí.
- **El menú del día** es la primera oferta vigente ahora, del tipo que sea (lo normal, un «Menú degustación» o un «Menú con elección»).
- **El texto de error** «Una degustación o un menú cerrado necesita su producto-menú» (`Labels.cs:106`) pasa a «Un menú, cerrado o con elección, necesita su precio por persona».
- **En la tabla del plan del nuevo aspecto**, la familia «Oferta» queda «Menú degustación · Menú con elección · Carta». El estado de pase «Falta elegir plato» solo aparece en un menú con elección.
- **Mesa:** «Toda la mesa» o «Comensal n», también en la carta; nada de «Para compartir», que es otra palabra para lo mismo. El botón que manda un pase es «Enviar siguiente pase» (`Labels.cs:54`), no «Marchar».
- **Bebidas:**
  - «Bodega y bebidas», para la entrada de la mesa y para la sección del catálogo;
  - «Añadir consumición» (la palabra del promotor), para el botón de la ficha de mesa del puesto que abre ese selector;
  - «Copa», y «Botella …» con el formato que venga;
  - «Combinado»;
  - «Ronda», para lo que se va a enviar;
  - «Enviar», el botón de la barra «Ronda · n sin enviar»;
  - «Lo que lleva la mesa», para lo ya enviado.
- **Alergias:**
  - «Adaptado: …», delante del texto que escribe cocina;
  - «Por revisar en cocina», mientras cocina no decide.

- **En el comandero no se escriben palabras de caja:** ni «precio», ni «cuenta», ni «pago», ni «saldo», ni «€» (regla de las E2E, sección 1). Por ejemplo, en vez de «Precio fijo» se dice «Elegir otra ginebra o tónica no cambia nada», y en vez de «la cuenta se ve en caja», «Los importes solo se ven en Caja».

**Supuesto:** el promotor prefiere «Menú cerrado» porque es su palabra. Si prefiere «Menú de la casa» o «Menú (todos lo mismo)», el cambio toca los mismos sitios de la sección 1 (unos diez textos y sus plurales, más `labels.ts`, que ganará la tabla en C3a), no una sola línea.

## 4. Cortes

Una PR por corte. Las versiones siguen a `0.38.0-c2b`.

**Desde el 22-10, el orden y las versiones de las filas 5 a 9 los fija `docs/plans/2026-10-22-sala-y-cocina.md`** (sección 4, APROBADO): esta tabla queda como historia.

| Orden | Corte | Versión | Esquema | Qué gana el promotor |
|---|---|---|---|---|
| 1 | **E4d** — Menú de la casa y palabras claras | `0.39.0-e4d` | sin cambio | Palabras que entiende, menú propuesto al abrir, formularios sin códigos |
| 2 | **E3.3** — Cocina, Bodega y vinos por copa | `0.40.0-e3.3` | sin cambio | Catálogo partido en dos, vinos por copa en bloque, buscador en Caja |
| 3 | **C3a** — Comandero nuevo: aspecto y mesas | `0.41.0-c3a` | sin cambio (motor intacto) | Aspecto nuevo, mesas por sala con nombres, ficha de mesa con sus dos entradas |
| 4 | **C3b** — Bodega y bebidas: comandero y mesa del puesto | `0.42.0-c3b` | sin cambio | Buscador, Copa/Botella, rondas, fotos y lo que lleva la mesa; en el PC, «Añadir consumición» en la mesa abierta, sin ir a Caja ni pedir contraseña |
| 5 | **E4e** — Alergias adaptadas antes de enviar y cocina por plato | `0.43.0-e4e` | sin cambio (sí cambian las reglas de las instantáneas) | La adaptación queda escrita; cocina marca por plato |
| 6 | **C3c** — Comandero y cocina: abrir mesa y servir el menú | `0.44.0-c3c` | sin cambio | Abrir en 3 toques, menú agrupado, cocina por plato |
| 7 | **E4f** — El menú en una pantalla | `0.45.0-e4f` | sin cambio | Cambiar los platos del menú, o del menú del día, en un minuto; vino incluido por pase (maridaje) |
| 8 | **E4c** — Combinados a precio fijo | `0.46.0-e4c` | **v9** | «Gin tonic Premium» con base y refresco a elegir |
| 9 | **E4g** — Carta por familias y comanda de una vez | `0.47.0-e4g` | sin cambio | Una carta de entrantes, carnes, pescados y postres que se usa bien |

- E4c conserva el nombre que los documentos ya daban a los combinados, aunque llegue después de E4d y E4e.
- **C3 se parte en tres** (revisión): el aspecto nuevo reescribe todos los selectores E2E y añade el proyecto móvil sin tocar el motor (C3a); la bodega trae los cambios del motor de las rondas (C3b); el menú y la cocina agrupada dependen de E4e (C3c). Así cada PR cambia una sola cosa difícil.
- El orden de 1 a 6 entrega antes lo que el promotor pidió: palabras, bodega y comandero. Los cortes 7, 8 y 9 se pueden reordenar entre sí sin tocar nada más. **Decidido el 15-10: E4f va antes que E4c**, porque el programa sirve para distintos tipos de negocio y el menú del día tiene que poder añadirse pronto. Las secciones 4.7 y 4.8 conservan su numeración; solo cambian las versiones.
- **Coordinación con C2b**, que está en curso: C2b solo recoloca Caja, Catálogo y Oferta, y arregla el cierre del programa al guardar (sección 4.1). Los campos de esos formularios (códigos, «Más opciones», buscador de Caja) los cambian E4d y E3.3; si C2b ya pone el buscador en Caja, E3.3 solo le añade la agrupación por vino.
- Cada corte lleva:
  - rama propia y un commit por paso;
  - su documento `docs/native/<corte>.md`;
  - su párrafo en `dotnet/AGENTS.md` y su línea en `docs/STATUS.md`;
  - su ADR cuando cambia una regla escrita: un apartado de ADR-007 en C3b («lo que lleva la mesa» sin importes) y un ADR nuevo en E4e (revisión de alergias antes de enviar);
  - capturas en `docs/native/images/<corte>/`;
  - permiso de subida y mezcla **pedido al promotor en cada corte**, como hasta ahora.

### 4.1. E4d — Menú de la casa y palabras claras (`0.39.0-e4d`, sin cambio de esquema)

**Entregado el 15-10** con las palabras del promotor («Menú degustación», «Menús y cartas», «Menú del día»): `docs/native/E4d-menu-del-dia-y-palabras-claras.md`. Quedan para después el selector de calendario en las fechas y el IVA del producto-menú en la ficha.

**Lo que ya arregla C2b (en curso).** El promotor encontró que el programa se cerraba al guardar.
- En Catálogo, Oferta y Salas, cada «Guardar» validaba los campos **antes** del manejador de errores del shell: `Code`, `Name` y las lecturas de números, fechas y precios lanzan `ArgumentException` fuera de `shell.Run` (`Desktop/ViewModels/CatalogViewModel.cs:336-353,357`; `OffersViewModel.cs:169-173,185`; `OrganizationViewModel.cs:140-144,148`). Un código fijo vacío (o un nombre, un orden, una fecha o un precio vacíos o mal escritos) cerraba el programa.
- C2b enseña el mensaje en su lugar y, si el código fijo está vacío, lo genera en el cliente a partir del nombre con la regla de la importación (`Catalog.Slug`, `Core.Domain/Catalog.cs:141-155`).
- **Código de una oferta, recortado a 27 caracteres en C2b:** el producto-menú se llama `menu-<código>` (`OfferOperations.cs:48`) y un código admite 32 caracteres (`Catalog.Slug` recorta a 32), así que C2b recorta a 27 el código que genera para una oferta. Con eso, un menú de nombre largo ya no recibe `invalid_code`. E4d lo sustituye por el punto «Código del producto-menú» de abajo.
- E4d da el paso siguiente: el código pasa a ser **opcional en el motor** (abajo), el cliente deja de generarlo, y los campos técnicos (código, orden, color, fechas) van bajo **«Más opciones»**.

**Motor (solo añade):**
- **Código opcional en las altas.** El `id` pasa a ser opcional en:
  - `offer-create`, `course-create` y `dish-create` (`Server/OfferOperations.cs:42,84,102`);
  - `category-create`, `product-create`, `presentation-create`, `tariff-create` y `tax-create` (`Server/CatalogOperations.cs`).

  Funciona así:
  - sin `id`, el servidor usa `Catalog.Slug(name)`;
  - si ese código ya existe en el ámbito, añade `-2`, `-3`… La idea es la de `CatalogImport.cs:159-162`, sacada a una función común del núcleo;
  - la respuesta devuelve el `id` creado;
  - un `id` enviado se sigue respetando, por compatibilidad con clientes y pruebas;
  - sin `sort`, el alta se pone al final (el máximo + 10, con tope en 9999: `Organization.cs`, `Sort`). Hoy un alta sin `sort` queda en 0: las pruebas que lo den por hecho se revisan;
  - la idempotencia no cambia: repetir la orden con la misma clave devuelve el mismo `id`;
  - dos altas simultáneas con el mismo nombre pueden calcular el mismo sufijo; la segunda recibe el `409 duplicate_code` de siempre (`CatalogUnit.cs:81`) y el puesto la repite con una clave nueva.
- **Código del producto-menú:** al generar, se deriva aparte con `Slug("menu " + nombre)` recortado a 32 caracteres y con sufijo si choca, en lugar de `"menu-" + código` (`OfferOperations.cs:48`), que con un código largo no cabe (`invalid_code`).
- **`offer-update` acepta `priceCents`.** Pone el precio por persona en la General desde hoy con `SetPrice`, solo si cambia, con el evento `catalog.price_set` en la misma transacción. En una carta se rechaza con `product_not_allowed`.
- **`offer-create` emite también** `catalog.product_created` y, si lleva precio, `catalog.price_set` (cierra el hueco de auditoría de la sección 1).
- **`offers-reorder {offerIds:[…]}`**, solo responsable: deja las ofertas en ese orden en una sola orden, con `offer.offer_updated` por cada una que cambie. Sirve para «Proponer al abrir mesa» y para «Subir/Bajar». Con `offer-update` haría falta una orden por oferta, porque el orden no baja de 0.
- Ningún cambio en la apertura, los pases ni las órdenes de sala.

**Puesto principal (WPF):**
- **Vocabulario** de la sección 3 en `Labels.cs`, Oferta, Comedor e Inicio.
- **ERP › Oferta, alta de un menú.** El camino principal pide:
  - el nombre;
  - el tipo, con dos botones grandes: «Menú cerrado — todos comen lo mismo» y «Carta»;
  - el **precio por persona**, obligatorio en un menú (sin él no se puede abrir mesa: `offer_unpriced`);
  - «Cuándo se ofrece», con «Siempre» por defecto.
- **«Más opciones»** guarda el resto:
  - el código (solo lectura una vez creado);
  - el tipo «Menú con elección»;
  - el IVA y el producto existente;
  - la vigencia con selector de fecha, no texto AAAA-MM-DD;
  - los días de la semana como botones y la elección de comida o cena.
- **Pases y platos:** nombre y estación (desplegable con nombres). «Subir» y «Bajar» sustituyen al número de orden (para pases y platos, con `course-update`/`dish-update`: son pocos).
- **Menú propuesto:** la primera oferta vigente ahora lleva la marca **«Se propone al abrir mesa»**. La acción «Proponer al abrir mesa» la pone la primera con `offers-reorder`.
- **Comedor:** abre la mesa **siempre** con el menú propuesto ya elegido; hoy repite el que se eligió la vez anterior (`ServiceViewModel.cs:228`) y eso cambia. La ficha usa la palabra nueva: «Menú con elección: cada persona elige su plato antes de enviar el pase», solo si la mesa es de ese tipo.
- **F01:** cada orden sale del contexto leído (la oferta o el pase seleccionados); «Subir/Bajar» y «Proponer» usan el orden de la última lectura correcta.

**Comandero (PWA), cambio mínimo hasta C3:**
- propone la primera oferta aunque haya más de una (`BoardView.vue:40`); la elección se saca a una función de `pwa/` sin DOM para poder probarla con vitest;
- la etiqueta «(menu cerrado)» pasa a «(con elección)» (`:70`);
- las pruebas E2E no cambian de selectores (`commands.spec.ts:74` sigue eligiendo `LAB-TASTING`, que además es la primera).

**Supuesto:** «el menú de la casa» es la primera oferta **vigente ahora** de la lista. Así, un local con un menú de comida y otro de cena tiene la propuesta correcta sin configurar nada. La alternativa, una marca `is_default` en `core.offers`, necesitaría esquema y no resolvería el caso comida/cena. Queda descartada salvo que el promotor la pida.

**Envío a estaciones:** sin cambio.

**Pruebas:**
- `offers_http.py`:
  - alta sin código → código derivado del nombre;
  - choque de código → sufijo;
  - repetición con la misma clave → mismo código;
  - un menú con un nombre de 60 caracteres se crea y su producto-menú tiene un código válido;
  - `offer-update` con precio → precio vigente hoy, `changed` y evento `catalog.price_set`; sin precio no cambia nada; en una carta, `product_not_allowed`;
  - `offer-create` deja `catalog.product_created` y `catalog.price_set` en la auditoría;
  - `offers-reorder`: orden nuevo, idempotente, 403 para sala y cocina.
- `catalog_http.py`: altas sin código.
- ClientChecks: la tabla de vocabulario recorrida entera; ningún «Degustación» como tipo; «Menú cerrado» = `tasting` (cambia `ClientChecks/Program.cs:463`).
- DesktopChecks: alta sin «Código» a la vista, «Más opciones» plegado, la marca «Se propone al abrir mesa» y el Comedor proponiendo la primera después de abrir una carta (cambia `DesktopChecks/Program.cs:617`).
- vitest: con dos ofertas, la propuesta es la primera (función pura).

**Capturas:** Oferta (lista, alta y ficha con «Más opciones» abierto) y Comedor.

### 4.2. E3.3 — Cocina, Bodega y vinos por copa (`0.40.0-e3.3`, sin cambio de esquema)

**Entregado el 15-10**: `docs/native/E3.3-cocina-bodega-y-copas.md`. Las dos pestañas grandes son chips de sección dentro de Productos (Todos · Cocina · Bodega y bebidas · Menús), «Sin colocar» es el par de botones de la ficha de una familia raíz, y la fecha «Desde» del precio sigue a la vista; el resto, como aquí.

**Motor:**
- **Dos secciones fijas del catálogo**, como categorías raíz reservadas: `_cocina` («Cocina») y `_bodega` («Bodega y bebidas»).
  - Se aseguran por ámbito, como `general` y `pase` (`PostgresStore.EnsureOrganizationAsync`). Ese método solo corre en `init`, `upgrade`, `restore` e `init-lab` (`Server/Program.cs:85,107`): en una instalación aparecen con el `upgrade` que hace el instalador al actualizar. Las pruebas que cuentan categorías (`catalog_http.py`, DesktopChecks) se ajustan.
  - No se desactivan ni se cuelgan de otra categoría (`category-update` y `category-deactivate` lo rechazan con `reserved_category`; `category-create` con ese código, también). Su nombre sí se puede cambiar.
  - **Supuesto:** el guion bajo garantiza que no chocan con nada importado ni generado, porque `Catalog.Slug` nunca lo produce (`Catalog.cs:141-155`). Un código tecleado a mano sí podría usarlo: el alta lo rechaza.
  - Con la sección como primer nivel solo quedan tres por debajo (`Catalog.MaxCategoryDepth = 4`, `Catalog.cs:25`). Lo importado de la tienda (tipo › región) cabe; un árbol propio de cuatro niveles no se puede mover y se queda «Sin colocar», clasificado por estación.
- **Sección de cada producto**, calculada al leer subiendo hasta 4 niveles:
  - la de su raíz, si es `_cocina` o `_bodega`;
  - los productos-menú (categoría `menus`, `Offer.cs:20`) no son de ninguna: siguen fuera de `/catalog` y de Caja, y en el Catálogo salen aparte, «Menús (se editan en Oferta)». Sin esta excepción caerían en Bodega por no tener estación;
  - si no, por la estación: estación de cocina o de pase → `cocina`; sin estación o con estación de barra → `bodega`. Es la regla actual: «sin estación, bebida o consumo directo».
  - Así los 1.037 vinos importados salen en Bodega **antes** de mover nada.
- **Claves nuevas en los catálogos.** `GET /catalog`, `/checkout/catalog` y `/erp/catalog` añaden a cada fila (en `/erp/catalog`, a cada producto):
  - `section`;
  - `family`: el nombre de la categoría bajo la sección (en una raíz sin sección, la propia raíz);
  - `categoryPath`: los nombres de la raíz a la hoja, sin la sección;
  - `reference`;
  - `glass: true` en la fila de la copa. Lo decide el servidor (presentación reservada `copa`, y también la `glass` de los datos de ensayo, `LabConfiguration.cs:30`), para que Caja y el comandero no tengan cada uno su regla.

  Ninguna lleva vocabulario de dinero. Son campos nuevos al final de `SellableView`/`PricedSellableView`/`ProductView` (`CatalogUnit.cs:180-184`): un cliente antiguo los ignora.
- **`GET /catalog` con `ETag`.** Responde con la huella del contenido y da 304 ante `If-None-Match`, así que la PWA puede revalidar sin bajar 2.000 filas. La huella incluye la fecha, porque el precio vigente (y con él lo que se puede vender) cambia de un día a otro (`CatalogUnit.cs:224-225`).
- **Órdenes nuevas en `/erp/catalog/commands`**, solo para el responsable, auditadas e idempotentes. Sus listas son campos nuevos de `CatalogCommand` (`items`, `stop`, `productIds`), porque la ruta rechaza campos desconocidos (`Contracts.cs:12`):
  - **`glass-sale-set {items:[{productId, priceCents}], stop:[productId…]}`.** En una transacción, por cada vino de `items`:
    - crea o reactiva la presentación reservada **`copa`** («Copa»; es la misma que ya crea un CSV con «Copa»), con orden 0;
    - pone su precio en la General desde hoy (la fecha «Desde» queda fuera: el promotor pone los precios a mano y valen ya);
    - emite por vino `catalog.presentation_created|reactivated` y `catalog.price_set`, con la misma clave de orden en la auditoría.

    Y por cada vino de `stop`, desactiva su `copa`; los precios se quedan (nada se borra). Así «Guardar» es **una** orden, como pide la tabla de la sección 2.

    Admite hasta **300 vinos por orden**: unos 21 KB, dentro de los 32 KiB de `Requests.MaxCommandBody`. El puesto parte una lista mayor en órdenes sucesivas, una detrás de otra (una sola incierta): si se corta a mitad, lo ya guardado queda y repetir «Guardar» no duplica nada.
  - **`products-move {productIds:[…], categoryId}`**: mueve varios productos de familia en una orden, para ordenar la bodega sin ir de uno en uno.
- **Arreglos de la importación** (`CatalogImport.cs:172-177`):
  - ya no borra la estación de un producto;
  - a un producto que ya existe solo le actualiza nombre, referencia, precio e imagen: **conserva su familia y su IVA**, que solo se toman del fichero al crearlo. Si no, cada reimportación de la tienda deshace lo que el promotor movió o corrigió en Costina (pregunta abierta 2). Cambia el comportamiento de `import-catalog`: va en el documento del corte y en `dotnet/AGENTS.md`.

  Las dos llevan prueba de regresión en `catalog_http.py`.
- **Avisos en ráfaga:** una orden de 300 vinos deja 600 eventos `catalog.*`, que son operativos y llegan a todos los equipos. Los clientes ya agrupan las ráfagas (D6.2); se comprueba en la prueba real.
- La copa hereda el IVA del vino (pregunta abierta 2).
- **`products-tax-set {productIds:[…], taxId}`** (decisión 15-10): cambia el IVA de varios productos de una vez, con un tipo ya definido en Impuestos (`TaxDefinition`). En el puesto, «Cambiar IVA de los marcados» junto a «Vinos por copa». Los vinos siguen entrando al 21 % hasta que el gestor diga otra cosa; cuando lo diga, el promotor lo cambia desde aquí sin reimportar.

**Puesto principal (WPF), ERP › Catálogo:**
- **Dos pestañas grandes, Cocina y Bodega y bebidas**, cada una con su árbol de familias.
  - Un apartado **«Sin colocar»** muestra las familias raíz de antes, con los botones «Mover a Bodega y bebidas» y «Mover a Cocina». Usan `category-update` con `parentId`, que ya existe.
  - La importación sigue encontrando esas familias por su nombre después de moverlas (`CatalogImport.cs:155-157`).
- **Pantalla «Vinos por copa»:** una tabla de Bodega con:
  - buscador por nombre, referencia y familia;
  - filtro por familia que **incluye las subfamilias**;
  - el precio de la botella a la vista;
  - por fila, la casilla «Por copa» y el precio de la copa;
  - el botón **«Mismo precio para los marcados»**.

  «Guardar» es una sola orden `glass-sale-set` con los marcados y los desmarcados.
  - **F01 en la tabla:** lo tecleado es un borrador por código de producto. Una relectura (por un aviso o tras otra orden) no lo pisa, y «Guardar» solo envía filas de vinos que siguen en la última lectura correcta. Si la lectura falla, no se puede guardar.
- **Lista de productos:**
  - con **selección múltiple**, para «Mover a…» y «Vender por copa»;
  - con el filtro de categoría incluyendo las subcategorías (`CatalogViewModel.cs:231`).
- **Ficha del producto:** Código, Orden, Color y la fecha «Desde» del precio pasan a «Más opciones». Al crear, el código se genera del nombre (E4d).
- **Caja:** el desplegable de artículos (`CheckoutView.xaml:21`) pasa a ser un buscador.
  - Los resultados se agrupan por vino, con un botón por presentación (Copa, Botella…).
  - Usa la misma regla de búsqueda que tendrá el comandero en C3b: sin tildes, por nombre, referencia y familia. Las dos aplicaciones comparten la tabla de casos de prueba.
  - Si C2b ya ha puesto el buscador, aquí solo se añade la agrupación por vino.

**PWA:** sin cambios visibles. Las claves nuevas se ignoran hasta C3b.

**Envío a estaciones:** sin cambio. La sección sirve para mirar el catálogo; la estación sigue decidiendo a qué cocina va un plato.

**Pruebas:**
- Dominio: sección por raíz y por estación; los productos-menú sin sección; las raíces reservadas no se desactivan, no se cuelgan de otra y su código no se puede usar en un alta.
- `catalog_http.py`:
  - `section`, `family`, `glass` y `reference` en los tres catálogos;
  - `glass-sale-set` crea, reactiva, desmarca con `stop` y es idempotente; con 301 vinos da 422;
  - `products-move`, también con un destino que pasaría de cuatro niveles (`category_depth`);
  - 403 para sala y cocina;
  - la reimportación conserva la estación, la familia y el IVA de un producto existente.
- `pwa_http.py`: `/catalog` con las claves nuevas sigue sin claves de dinero según la regla de la PWA; `ETag` y 304.
- DesktopChecks y ClientChecks: búsqueda sin tildes.
- **Prueba real** con la bodega completa en la base desechable: marcar 30 vinos por copa en una sola orden.

**Capturas:** Catálogo › Cocina, Catálogo › Bodega con «Sin colocar», «Vinos por copa» y Caja con buscador.

### 4.3. C3a — Comandero nuevo: aspecto y mesas (`0.41.0-c3a`, sin cambio de esquema ni de motor)

**Entregado el 20-10**: `docs/native/C3a-comandero-nuevo.md`, con los pasos de `docs/plans/2026-10-20-c3a-comandero-nuevo.md`. Como aquí, con estas diferencias:
- **«Abrir mesa»** ya queda en tres toques con el menú del día propuesto (mesa libre → personas → «Abrir»). Las alergias en la misma hoja siguen en C3c.
- **Las tablas de casos** son dos, comunes al comandero y al puesto (`dotnet/tests/fixtures/floorplan-cases.json` y `vocabulary-cases.json`).
- **Cada proyecto de Playwright tiene sus mesas:** escritorio M6, M7 y M8; móvil M3, M4 y M5.
- **Quedan para revisar con el promotor** cinco palabras o decisiones, en el documento del corte: el botón «Enviar siguiente pase · <pase>», la oferta que ya no está vigente, la mesa por su nombre en la orden pendiente, «¿Para quién?» sin valor por defecto y el aviso de mesa cerrada en Caja.

Es la primera parte de C3 (plan del nuevo aspecto, apartado C3): el aspecto y el vocabulario del puesto principal llegan a la PWA. No toca el motor, para que la reescritura de las E2E y el proyecto móvil se revisen solos.

**Comandero (PWA):**
- **Aspecto nuevo**, con la paleta y las palabras del puesto principal:
  - `style.css` con variables: fondo `#F3F4F6`, tarjetas blancas, acento `#0E7A5F` y rojo solo para alergias; **solo tema claro** (decisión del 14-10);
  - `labels.ts` con tildes y la tabla de vocabulario, también los tipos de oferta;
  - nombres de mesa, sala y estación desde `/configuration`, nunca `tableId` ni `stationId` a la vista (hoy aparecen en `BoardView.vue:56,78,90`);
  - tipos de estación en minúscula (`docs/native/C2-inicio-y-plano-de-mesas.md:240`);
  - iconos de trazo como SVG dentro de las plantillas: no son imágenes y no tocan la CSP;
  - ningún texto con «precio», «cuenta», «pago», «saldo» ni «€» (sección 3).
- **Mesas** en una rejilla de tarjetas por sala, también las libres, con el mismo estado escrito que el Comedor del puesto: Libre · Sentados · En cocina · Listo para servir · Comiendo · En pausa · Terminado, por liberar · Cancelado, por liberar. La lógica se porta a `floor.ts`, sin DOM y probada **con la misma tabla de casos que `Client/FloorPlan.cs`**, para que las dos no se separen.
- **Ficha de mesa**, como en la maqueta: arriba, las alergias en rojo y escritas; después, dos entradas grandes, **«Carta y menús»** y **«Bodega y bebidas»**; los pases en corto; y abajo un solo botón con la acción principal que anuncia el servidor, en el mismo orden que el Comedor (servir, validar, enviar siguiente pase, empezar, reanudar, terminar, liberar: C2). En C3a «Bodega y bebidas» abre lo de hoy con el aspecto nuevo; se rehace en C3b. «Carta y menús» se rehace en C3c.
- **Configuración al día:** `/configuration` (mesas, ofertas, estaciones) se relee al abrir la hoja de «Abrir mesa» y cada 5 minutos. Hoy solo se lee al montar la pantalla (`BoardView.vue:35-43`), y un menú cambiado por la mañana (E4f) no llegaría a un comandero abierto desde antes.
- **Cocina (`KitchenView`):** solo el aspecto nuevo y los nombres de estación. La agrupación por plato llega en C3c.

**Pruebas:**
- vitest: `floor.ts` con la tabla de casos común; etiquetas.
- Playwright **con un proyecto móvil nuevo** (375 × 812, táctil), además del de escritorio, con las historias principales de hoy (abrir, empezar, enviar, servir, pausa y orden incierta).
- Los selectores E2E pasan de `select[name=table|menu]` a un `data-testid` por tarjeta y por botón; el texto visible sigue sin palabras de caja (`board.spec.ts:103`).

**Capturas** a 375 × 812, a 820 × 1180 (iPad en vertical) y a 1180 × 820: mesas y ficha de mesa. La maqueta está dibujada a 390 × 844; las pruebas y capturas, a 375 × 812.

### 4.4. C3b — Bodega y bebidas: comandero y mesa del puesto (`0.42.0-c3b`, sin cambio de esquema)

**Entregado el 21-10**: `docs/native/C3b-bodega-y-bebidas.md`, con los pasos de `docs/plans/2026-10-21-c3b-bodega-y-bebidas.md`. Como aquí, con estas diferencias:
- **Defensa contra el doble cargo** (diferida de D6.7): el identificador de cada línea de `add-consumptions` y de `add-consumption` se deriva del servicio, de la clave de la orden y de la línea. La cuenta no admite dos iguales: la misma orden reenviada por otro actor responde lo mismo sin cobrar, y la misma clave con otras líneas da `duplicate_consumption`.
- **Una tabla de casos común** al comandero y al puesto, `dotnet/tests/fixtures/drinks-cases.json` (búsqueda, agrupación y ronda), que leen vitest y ClientChecks. Caja busca con la misma regla.
- **Una ronda sin confirmar sale de la bandeja**, porque ya es la orden pendiente durable. Una rechazada o con `version_conflict` se queda.
- **El catálogo del comandero** se lee también al montar el tablero. El del PC se lee entero cada vez que se abre el selector, sin `ETag`.
- **Las E2E de la ronda** usan la Mesa 1 en los dos proyectos, una detrás de otra.
- **Varios tamaños de copa** («Copa 15 cl») quedan como pregunta para el promotor. Mientras tanto sigue una copa por vino.
- **Quedan para revisar con el promotor** veinticuatro decisiones pequeñas del motor, del comandero y del PC, en el documento del corte. Por ejemplo: las restricciones también arriba en Bebidas, las familias de la A a la Z, la ronda plegada en el PC, «Enviar ronda» en verde claro sobre el botón grande de la ficha y «Añadir consumición» en la cabecera fija de la ficha del PC.
- **Sin vuelta atrás:** como se prevé para E4e, una cuenta guardada por `0.42.0-c3b` ya no la lee `0.41.0-c3a`; bajar de versión exige restaurar la copia previa. Lo dice el documento del corte, y que el instalador nunca baje de versión sigue pendiente.

Lleva también la petición del promotor del 15-10 de añadir una consumición desde la mesa abierta del puesto principal, sin ir a Caja (sección «Origen»). Usa la misma orden de ronda y la misma regla de una orden por gesto que el comandero, así que el motor no añade nada más por ella.

**Motor (solo añade):**
- **Ronda en una orden:** `POST /dining/services/{id}/commands/add-consumptions`, con cuerpo `{expectedVersion, lines:[{productId, presentationId?, quantity, guestPosition?}]}`.
  - La pueden usar sala y principal, con hasta 30 líneas y cantidades de 1 a 99.
  - Va todo o nada, en una transacción. Una línea mala da 422 con su índice (`line`).
  - Respuesta sin dinero: `{version, consumptionIds:[…]}`.
  - **Contrato:** `lines` es un campo nuevo de `DiningCommand` (la ruta rechaza campos desconocidos, `Contracts.cs:12`), la acción se despacha junto a `add-consumption` (`DiningModule.cs:59-62`) y se añade a `SessionActions` (`:20`) y a `Affordances.Allows`, que es a la vez la `ActionPolicy` de la ruta: sala y principal sí, cocina no.
  - **No admite platos de cocina:** una línea cuyo producto tiene estación de cocina o de pase se rechaza con `use_add_dish` («pídelo como plato»). Así lo que se pide en Bebidas nunca se cobra sin que cocina lo vea, y no depende solo de que el comandero filtre por sección. `add-consumption` se queda igual, por compatibilidad.
  - `guestPosition` es opcional y se guarda en la línea de la cuenta como campo nuevo y aditivo, como hizo E3 con `ProductId`. Las cuentas guardadas antes se leen sin él. Por defecto, la línea es de toda la mesa. Es un dato de sala dentro de la cuenta del núcleo: se anota en el documento del corte como decisión (ADR-012).
  - La línea guarda también su **origen** (`Origin`: `consumption`, `dish`, `menu`, `checkout`), otro campo aditivo, para saber después qué se pidió como bebida o extra.
- **Texto de la línea sin «Unidad».** `add-consumption(s)` y `add-product` de Caja quitan la presentación por defecto («Agua con gas», no «Agua con gas · Unidad»), igual que `add-dish` para cocina (`DiningOperations.cs:129`). La línea de cobro de `add-dish` (`:126`) no cambia: la demo llama «Ración» a esa presentación y la prueba `offers_http.py:215` la fija.
- **Lo que lleva la mesa.** Cada entrada de `GET /dining/board` añade `consumptions:[{id, productId, presentationId, name, presentation, quantity, guestPosition, at, voided}]`.
  - Se lee de las líneas de la cuenta por el contrato de la `Unit`, **sin importes**: una proyección propia, nunca `ChargeLine`, que lleva `unitPriceCents`, `totalCents` y `tariffId` (`Core.Domain/SettlementAccount.cs:7-10`) y haría que la PWA rechazara el tablero entero.
  - Lleva las líneas con origen `consumption` o `checkout`: bebidas y extras, también los apuntados en Caja. Las líneas anteriores al corte, sin origen, se clasifican por su producto: fuera el producto-menú y los platos que tienen su elaboración. Un plato pedido por error como consumo antes del corte sí se ve, para que alguien lo note.
  - Se leen todas las cuentas del tablero en una sola consulta, no una por mesa: `Board()` ya lee mesa a mesa (deuda «N+1» de `docs/STATUS.md`) y cada ronda hace releer a todos los equipos.
  - **ADR-007:** mostrar a sala lo que ha bebido una mesa, con nombre, formato, cantidad y hora y **sin importes ni totales**, se añade como apartado del ADR («no es la cuenta provisional»). Sin ese apartado, la lista choca con «no debe mostrar: cuenta provisional» (`ADR-007-service-control-vs-checkout.md:31`).
- **Aviso a todos.** `add-consumption(s)`, y también `add-product` y `void-charge` de Caja, emiten un aviso operativo nuevo, `service.consumptions_changed` (agregado = el servicio), que no empieza por `account.` ni por `payment.` y por eso llega a todos (`OutboxPublisher.cs:24-25,48`). Así una anulación en Caja también llega a los comanderos.
  - La versión del servicio no se mueve, así que no se pisan las órdenes de otros camareros.
- **Fotos:** la CSP pasa a `img-src 'self' blob:` (`PwaHosting.cs:37`).
  - Sigue sin `unsafe-inline`, sin terceros y sin `data:`.
  - **Cambio que hay que revisar en la PR:** `blob:` solo sirve para mostrar los bytes que la propia PWA ha pedido con su token.

**Comandero (PWA):**
- **«Bodega y bebidas».** Solo lleva productos de la sección `bodega`; los platos no aparecen aquí (y el servidor los rechaza igualmente).
  - **Buscador siempre arriba:**
    - no distingue tildes ni mayúsculas;
    - busca por nombre, referencia (código de Verial o de la tienda), familia y región. La uva, la añada o la bodega solo se encuentran si están en el nombre: el catálogo no las guarda;
    - da resultados al teclear, desde 2 letras;
    - trabaja sobre la lista en memoria, unas 2.000 filas: no hace falta servidor ni dependencias nuevas.
  - **Accesos rápidos** antes de buscar:
    - «En esta mesa», para repetir con +1 lo ya pedido;
    - **«Por copa»**, con los vinos que traen `glass: true`;
    - chips de familia (Tintos, Blancos, Espumosos, Cervezas, Refrescos, Cafés, Combinados…) con sus subfamilias.
    - No hay «Más pedidos»: hace falta la estadística de ventas (sección 6).
  - **Una fila por producto**, agrupando las filas del catálogo por `id`:
    - foto, nombre y familia › región;
    - **un botón por presentación**: «Copa» primero, después «Botella 0.75 L»…; con una sola presentación, un único botón «Añadir»;
    - **un toque suma 1 a la ronda, para toda la mesa**: sin hoja intermedia. La cantidad y el comensal se cambian en la línea de la ronda;
    - **sin precios** (ADR-007).
  - **Ronda:**
    - se ve abajo con − / +, quitar y «Para: Toda la mesa ▾» (comensal opcional);
    - es un borrador en memoria (sección 2) y se ve como **«Sin enviar»** hasta que el servidor confirma;
    - «Enviar», en la barra «Ronda · n sin enviar», es **una sola** orden `add-consumptions`;
    - si el servidor contesta `version_conflict` porque la mesa cambió (por ejemplo, cocina marcó un plato), la ronda **sigue en la bandeja**, la pantalla relee y ofrece «La mesa ha cambiado: enviar de nuevo», que es una orden nueva con su propia clave;
    - salir de la mesa con una ronda sin enviar pide confirmación (es un camino excepcional).
  - **Lo que lleva la mesa:** nombre, Copa o Botella, cantidad, comensal, hora y «Anulado en Caja» cuando corresponda. Sin importes; el pie dice «Los importes solo se ven en Caja».
- **Fotos:**
  - se piden con el token (`Authorization`) solo si su `imageSha` no está ya en memoria;
  - se guardan en memoria por `imageSha`, hasta unas 150 o 20 MB; al descartar una se libera su `URL.createObjectURL`;
  - solo se piden las de las filas visibles (`IntersectionObserver`);
  - si no hay foto o falla, sale un recuadro con las iniciales;
  - el service worker sigue sin tocar `/api/`.
- **Catálogo al día:** se revalida con `ETag` al abrir Bebidas, cada 5 minutos y siempre tras reconectar. `api.ts` gana una lectura condicional que manda `If-None-Match` a mano (hoy todo va con `cache:'no-store'`) y acepta el 304 sin cuerpo; hoy un 304 sería un error (`api.ts:53-56`). La guarda de dinero se sigue aplicando a cada cuerpo nuevo.

**Puesto principal (WPF), Comedor › ficha de la mesa** (petición del 15-10):
- **Motor:** nada nuevo. Usa `add-consumptions` y la lista `consumptions` del tablero de este mismo corte.
- **«Añadir consumición»** en la cabecera de «Lo que lleva la mesa» de la ficha de una mesa abierta («Ver cuenta», con su candado, sigue arriba: `ServiceView.xaml:236-241`). Abre dentro de la propia ficha, sin salir del Comedor ni ir a Caja, el selector **«Bodega y bebidas»**, el mismo del comandero:
  - buscador siempre arriba, con la misma regla (sin tildes, por nombre, referencia, familia y región);
  - accesos «En esta mesa» y «Por copa», y chips de familia con sus subfamilias;
  - una fila por producto con foto, nombre y familia › región, y un botón por presentación (Copa primero, después Botella…); con una sola, «Añadir»;
  - un toque suma 1 a la ronda, para toda la mesa; abajo, la ronda con − / +, quitar y «Para: Toda la mesa ▾»;
  - **sin precios**, también para el responsable.
- **Sin contraseña en el nivel Puesto.** El botón sale y se habilita con lo que anuncia el servidor: la acción de sesión `add-consumptions`, que sala y principal tienen (sección 1, «Consumos desde la mesa»). El Puesto es un dispositivo de sala, así que la tiene. No pasa por `RequestElevation`, a diferencia de «Ver cuenta» (`ServiceViewModel.cs:496-505`). Sin la acción anunciada, el botón no sale.
- **La misma orden y la misma regla que el comandero:**
  - «Enviar» es **una sola** orden `add-consumptions` con la versión de la mesa leída. F01: el contexto es la ficha leída en ese momento, no la mesa que se elija después;
  - la ronda es un borrador en memoria (sección 2): nunca va al almacén de la orden incierta del puesto antes de enviarse y nunca es una cola. Mientras la orden anterior siga incierta, se puede llenar pero no enviar;
  - `version_conflict` deja la ronda donde estaba, relee y ofrece «La mesa ha cambiado: enviar de nuevo», con una clave nueva;
  - cambiar de mesa o de pestaña con una ronda sin enviar pide confirmación; cerrar el programa la pierde, y no se recupera como si estuviera enviada (D6.1).
- **Lee `GET /catalog`, sin importes** (`DesktopReadRoutes.cs:53-55`), y no `/checkout/catalog`, que lleva precios y es solo del principal (`:68-70`). Los platos no salen (solo sección `bodega`), y el servidor los rechaza igual con `use_add_dish`. El catálogo se lee al abrir el selector.
- **«Lo que lleva la mesa»** también en la ficha, con la misma lista que el comandero (nombre, Copa o Botella, cantidad, comensal, hora y «Anulado en Caja»), sin importes. `BoardEntry` (`Costina.Client`) gana `Consumptions`, que el Comedor ya relee con el tablero.
- **Un control compartido:** el selector es un `UserControl` propio (`Desktop/Views/DrinksPicker.xaml`), con su lógica sin WPF en `Costina.Client`: búsqueda, agrupación por producto con Copa primero y ronda. Usa la misma tabla de casos que `search.ts` y `round.ts` y que el buscador de Caja de E3.3, así que el puesto y el comandero no se separan, y Caja puede adoptar el control más adelante. Las fotos van por el cargador del Catálogo (`Desktop/ImageLoader.cs`).
- **Caja no cambia:** sigue cobrando, anulando y cerrando, y conserva su «Añadir consumo» con precio para el responsable.

**Envío a estaciones:** ninguno. Una bebida es una línea de la cuenta, como hoy: no pasa por los pases ni por el pase de cocina (pregunta abierta 4).

**Pruebas:**
- vitest:
  - `search.ts`: tildes, referencia y orden de los resultados (tabla de casos compartida con Caja, E3.3);
  - `round.ts`: sumar, quitar, el límite de 30 líneas y que la ronda sigue tras un `version_conflict`;
  - agrupación por producto, con Copa primero;
  - lectura condicional: 304 conserva la lista anterior; 200 pasa por la guarda de dinero.
- `pwa_http.py` y `checkout_http.py` (no existe `dining_http.py`):
  - `add-consumptions` todo o nada e idempotente por clave;
  - 403 para cocina, 422 con el índice de la línea y `use_add_dish` con un plato de la carta;
  - `consumptions` sin claves de dinero según la regla de la PWA, sin la línea del menú y con lo apuntado en Caja;
  - el aviso llega al canal operativo, también tras `add-product` y `void-charge` de Caja;
  - el texto de la línea va sin «Unidad».
- `affordances_http.py`: `add-consumptions` en las acciones de sesión de principal y sala, no en las de cocina. `desktop_reads.py:94` y `checkout_http.py:40-41`, que fijan la lista de acciones de sesión por rol, cambian con ella.
- RealtimeChecks: un comandero relee cuando otro añade una ronda y cuando Caja anula un consumo.
- ClientChecks: la búsqueda, la agrupación y la ronda de `Costina.Client`, con la tabla de casos compartida con `search.ts` y `round.ts`.
- DesktopChecks, para la ficha de la mesa del Comedor:
  - en el nivel Puesto, con `add-consumptions` en las acciones de sesión (la sesión de prueba de hoy solo lleva `open`), una mesa abierta muestra «Añadir consumición» habilitado y abrirlo **no** pide la contraseña (`ElevationRequested` sigue en falso); sin la acción, el botón no sale;
  - buscar «albar» → Copa → «Enviar» manda exactamente **un** POST `add-consumptions`, con la versión de la mesa leída y las líneas de la ronda; ningún GET de `checkout/catalog`;
  - con `version_conflict`, la ronda sigue y se ofrece enviar de nuevo; una relectura del tablero no la pisa;
  - cambiar de mesa con una ronda sin enviar pide confirmación;
  - «Lo que lleva la mesa» pinta la ronda confirmada y «Anulado en Caja»;
  - el selector no pinta importes ni códigos (`RefuseRaw`).
- Playwright, en escritorio y en móvil:
  - buscar «albar» → Copa → «Enviar»;
  - **un solo efecto:** tras una respuesta perdida de verdad (`route.fetch()` + `route.abort()`), la mesa tiene exactamente las líneas de la ronda. Aquí no vale contar la versión del servicio, que no sube;
  - la ronda aparece en «Lo que lleva la mesa» del otro comandero;
  - la foto se ve sin violaciones de CSP;
  - el texto visible no lleva «€» ni palabras de caja (`board.spec.ts:103`).
- Los selectores E2E pasan de `select[name=product]` a un `data-testid` por fila y por botón.

**Capturas** a 375 × 812 y a 820 × 1180: Bebidas (accesos rápidos, búsqueda y ronda) y lo que lleva la mesa. Del puesto, en DesktopChecks, la ficha de una mesa abierta en el nivel Puesto: `comedor-consumicion.png` (selector abierto, una búsqueda y una ronda sin enviar) y `comedor-lo-que-lleva.png` (selector cerrado, «Lo que lleva la mesa» con la ronda confirmada y «Anulado en Caja»).

### 4.5. E4e — Alergias adaptadas antes de enviar y cocina por plato (`0.43.0-e4e`, sin cambio de esquema)

**Este corte cambia una regla de seguridad documentada (D3.5, `dotnet/AGENTS.md:17`) y necesita el sí del promotor** (pregunta abierta 1). La regla nueva se escribe en un ADR propio («revisión de alergias antes de enviar»), que sustituye en ese punto a D3.5.

**Dominio (Dining):**
1. **Toda elaboración a la que aplica una restricción necesita una decisión de cocina antes de cocinarse.**
   - Al declarar una restricción, se marcan «por revisar» las elaboraciones afectadas que no estén servidas, **también las de los pases sin enviar**. Hoy solo se marcan las enviadas (`CourseExecution.cs:141-146`).
   - Una elaboración que nace para un comensal con restricción (al abrir, con `choose` o con `add-dish`) nace por revisar. `choose` sustituye la elaboración (`CourseExecution.cs:67-69`): el plato nuevo vuelve a necesitar revisión.
   - **Ojo con «toda la mesa»:** una elaboración sin comensal (un plato de carta para compartir) recibe cualquier restricción de cualquier comensal (`Restrictions.cs:28-29`), y una restricción de toda la mesa recibe todas las elaboraciones. Las dos nacen por revisar (pregunta abierta 1).
2. **Revisar también lo que no ha salido.**
   - `review-preparation` se admite en pases sin enviar, con «no afecta» o «adaptar»; la nota sigue siendo obligatoria.
   - **También con la mesa «Sentados»:** hoy revisar pasa por `KitchenAllowed`, que exige «En servicio» o «En pausa» (`Restrictions.cs:79`; `DiningService.cs:152-158`). Sin cambiarlo, una alergia declarada al abrir no se podría revisar hasta empezar, y con el punto 6 el primer pase no saldría nunca.
   - El anuncio cambia con ella: hoy `review-preparation` solo se anuncia en un pase activo (`AllowedActions.cs:58`).
   - «Rehacer» sigue siendo solo para lo enviado; en otro caso da `nothing_to_remake`.
   - La nota de «adaptar» es **el plato adaptado**, por ejemplo «tosta de maíz en vez de pan». El nombre del plato no cambia: el comandero y la cocina enseñan el plato del menú y, debajo, «Adaptado: …».
3. **Bloqueo por pase, no global.**
   - `fire-next` se niega con `restrictions_unreviewed` si el pase que va a salir tiene algo por revisar. Hoy se niega ante cualquier pase (`DiningService.cs:44`), y el anuncio igual (`AllowedActions.cs:18-19`): los dos cambian juntos.
   - Validar y servir siguen igual.
   - **Omitir un pase retira sus revisiones pendientes:** lo que no se va a cocinar no se revisa. Si no, el aviso «por revisar» del Comedor (`FloorPlan.cs`, `Alert`) se quedaría encendido para siempre y la mesa no se podría restaurar (punto «Instantáneas»).
4. **Abrir con alergias.** `open` acepta `restrictions:[{guestPosition?, kind, substance, severity}]` (campo nuevo de `OpenRequest`, `DiningOperations.cs:9`) y las declara en la misma transacción: una sola orden. Los valores desconocidos se rechazan como hoy (`invalid_restriction`).
5. **Órdenes por grupo.** Van todo o nada:
   - `preparations-start` y `preparations-ready {courseId, itemIds:[…]}`, para las estaciones;
   - `review-preparations {items:[{courseId, itemId, decision, note}]}`, para el pase y el principal. **Decisión y nota por elaboración**: al sentarse, cocina recorre el menú del comensal 3 y en una sola orden dice «no afecta» a unos platos y «adaptado: …» a otros, cada uno con su texto. El comandero rellena la misma decisión en todas cuando se toca «No afecta a nada de este pase».

   **Matriz de estación, sin huecos.** `StationAllowed` (`DiningOperations.cs:57-72`) solo mira los nombres de las acciones de hoy y deja pasar cualquier otro: las acciones nuevas se añaden ahí y se comprueban elaboración a elaboración, o un dispositivo de una estación podría marcar las de otra. Lo mismo en `Affordances`: `KitchenAllowed` (`Affordances.cs:10`), la exclusión de sala para `review-preparations` (`:15`), `StationKeeps` y `PassKeeps` (`:24-31`). `Allows` es la `ActionPolicy` de la ruta, así que lo que no se añada da 403.

   **Anuncio:** el pase anuncia la orden de grupo si alguna de sus elaboraciones anuncia la individual para ese rol y estación. Se calcula en `Affordances.Filter`, después de filtrar las acciones de las elaboraciones, así que hereda la matriz de estación (es la primera acción que se deduce en el filtro: se documenta). El botón de grupo envía solo las elaboraciones que anuncian la acción individual.

   **Contrato:** `itemIds` e `items` son campos nuevos de `DiningCommand` (la ruta rechaza campos desconocidos).
6. **Propuesta (supuesto: «Sentados» no tiene otro uso que esperar).** `fire-next` se admite también desde «Sentados»: empieza el servicio y envía el primer pase en la misma orden, un toque menos. `start` sigue existiendo.

**Instantáneas (sí cambian sus reglas, aunque no haya campos nuevos):**
- Los campos ya existen (`ReviewPending` y `Review`).
- Al restaurar hoy se rechaza una elaboración por revisar en un pase sin enviar (`Snapshots.cs:80-81`) y un acuse pendiente con la mesa «Sentados» (`Snapshots.cs:55-57`). Las dos reglas se amplían a pases pendientes y a mesas «Sentados»; sin eso, la primera mesa con una alergia declarada al abrir quedaría ilegible (`invalid_snapshot`).
- La compatibilidad con D3.2 (`Snapshots.cs:61-62`) sigue marcando solo los pases activos: no puede usar el marcado nuevo, que incluye los pendientes.
- **Sin vuelta atrás:** un motor anterior a E4e no lee una mesa guardada con revisiones en pases sin enviar. El esquema no cambia, así que nada lo impide al restaurar una copia: el documento del corte lo dice.
- Una mesa abierta en el momento de actualizar conserva su estado: sus pases sin enviar no quedan marcados, igual que con la regla vieja. Se documenta.

**Puesto principal (WPF):**
- **Comedor, abrir mesa:** un apartado «¿Alergias o intolerancias?» con una fila por comensal:
  - comensal explícito; «Toda la mesa» es una opción más, nunca la de por defecto;
  - tipo, con Alergia por defecto;
  - sustancia;
  - gravedad, con Grave por defecto.
- **Ficha de mesa:**
  - platos **agrupados** («Ostra × 7»);
  - debajo, en rojo, la línea del comensal: «Ostra · Comensal 3 · ⚠ ALERGIA marisco (grave) · Adaptado: vieira», o «· Por revisar en cocina»;
  - `FloorPlan` cuenta platos, no líneas por comensal: «3 de 5 platos listos» (`FloorPlan.cs:95-115`).
- **Revisión desde el puesto**, por plato agrupado: el principal puede hacerla.

**PWA, mínimo hasta C3c.** La pantalla del pase lista también lo que está por revisar en pases sin enviar, con los botones que ya existen («No afecta» y «Adaptar»). Así ningún pase queda bloqueado mientras falta el rediseño.

**Envío a estaciones:** sin cambio. Las órdenes de grupo respetan la matriz de estación; solo `pase` y el principal revisan.

**Pruebas:**
- Aceptación de dominio:
  - una alergia al abrir deja todo el menú de ese comensal por revisar;
  - se puede revisar un pase sin enviar;
  - un pase revisado sale aunque otro siga por revisar; uno sin revisar no sale;
  - `remake` en un pase sin enviar da 422;
  - un `choose` para un comensal con alergia nace por revisar;
  - se revisa con la mesa «Sentados», y `fire-next` desde «Sentados» sale cuando el primer pase está revisado;
  - omitir un pase con revisiones pendientes las retira;
  - las órdenes de grupo van todo o nada; `review-preparations` con decisiones distintas por elaboración.
- Instantáneas: una de D3.5 y una de D3.2 se cargan sin cambios; una nueva con revisiones en un pase pendiente y la mesa «Sentados» se guarda y se vuelve a leer.
- `restrictions_http.py`, `station_http.py` y `affordances_http.py` (no existe `dining_http.py`):
  - un grupo con una elaboración de otra estación da `wrong_station` y no aplica nada, probado con el token del propio dispositivo;
  - una estación que no es `pase` recibe `wrong_station` en `review-preparations`;
  - el anuncio del grupo sale filtrado por rol y estación; sala nunca ve `review-preparations`.
- ClientChecks y DesktopChecks, con capturas de la apertura con alergias y de la ficha con la línea adaptada.

### 4.6. C3c — Comandero y cocina: abrir mesa y servir el menú (`0.44.0-c3c`, sin cambio del motor)

**Comandero:**
- **Abrir mesa:** al tocar una mesa libre se abre una hoja con:
  - las **personas** en botones grandes, de 1 a 8 y «Más…», sin pasar de la capacidad;
  - el **menú propuesto ya elegido**, con «Otro menú…» para cambiarlo;
  - «¿Alergias o intolerancias?», con filas por comensal como en el puesto.

  «Abrir mesa» es una sola orden. Al confirmarse, lleva a la ficha de esa mesa.
  - Los menús con elección vigentes salen bajo «Más opciones» de la hoja, no en la lista principal.
- **«Carta y menús»** (la entrada de la ficha de C3a):
  - los pases en una línea de tiempo; los ya servidos, recogidos en una tarjeta corta;
  - en un menú cerrado, los **platos agrupados** («Ostra × 7»), con la línea adaptada aparte, en rojo: el plato del menú, el comensal, la restricción y «Adaptado: …» con el texto de cocina (el nombre del plato no cambia, E4e);
  - **un botón grande** con la acción que anuncia el servidor, en el orden de C2: Servir, Enviar siguiente pase o Empezar;
  - omitir pide el motivo en su propia hoja. Hoy el campo está al final de la página (`ServiceActions.vue:84,151-155`).
  - «Más opciones» solo lleva lo que existe: pausar y omitir un pase. «Plato fuera del menú» y «Otro menú para un comensal» no existen hasta su corte (sección 6).
- **Menú con elección** (no es el caso de Costiña): por pase, una fila por comensal con los platos como botones. Cada toque es una orden hasta E4g.
- **Carta:** por grupo, los platos como botones, con su cantidad. Una orden por línea hasta E4g.
- **Alergias:** la franja roja de la ficha abre la lista por comensal con su estado («Adaptado: …» o «Por revisar en cocina») y el alta con el comensal explícito.

**Cocina:**
- **Tickets agrupados por plato:** «Ostra × 8 (1 adaptada)». La línea adaptada va destacada y nunca atenuada, con tipo, sustancia, gravedad y el texto de cocina.
- **Botones de grupo** «Empezar todas» y «Listas todas» (órdenes de E4e), además de los de cada línea.
- **«Por revisar», siempre visible en el pase:**
  - por mesa y comensal, los platos del menú afectados, también los de pases sin enviar (hoy `kitchen.ts:120-138` solo enseña pases enviados);
  - «No afecta» y «Adaptar», con la nota obligatoria. Atajos como «No lo lleva» rellenan la nota a la vista;
  - el menú entero de un comensal se revisa en una orden, con una decisión por plato.
- La agrupación es solo presentación (`kitchen.ts`). Qué se puede hacer lo sigue diciendo el servidor.

**Envío a estaciones:** sin cambio.

**Pruebas:**
- vitest:
  - `kitchen.ts`: agrupación con las adaptadas aparte;
  - apertura con alergias;
  - navegación a la mesa tras abrir.
- Playwright en móvil y en tableta (1180 × 820 en cocina, 820 × 1180 en el comandero):
  - abrir en 3 toques con el menú propuesto y una alergia;
  - en cocina, «Listas todas» es una orden y la versión sube en uno;
  - el pase decide una adaptación antes de enviar y el comandero la ve;
  - ni el comandero ni la cocina enseñan palabras de caja (`board.spec.ts:103`, `kitchen.spec.ts:151`).
- Los selectores E2E de menú y cocina se reescriben.
- **Guion físico corto** con el móvil Android y el iPad del promotor, con D6.5 como modelo y los criterios de la sección 2.

**Capturas:** abrir mesa, ficha de un menú cerrado con línea adaptada, cocina agrupada y «Por revisar».

### 4.7. E4c — Combinados a precio fijo (`0.46.0-e4c`, esquema **v9**; se entrega después de E4f)

Decisión del promotor: cada combinado tiene un precio fijo («Gin tonic Premium 9,00 €»), y la base y el refresco se eligen sin cambiar el precio.

**Esquema v9:**
- **Migración:** `upgrade-v9.sql`; concesiones SELECT, INSERT y UPDATE en `grants.sql`; `packaging_http.test_06e` llega hasta v9. La tabla nueva entra sola en las copias.
- **Tabla nueva `core.product_option_groups`**, con claves ajenas a productos y categorías. Campos: `tenant`, `company`, `location`, `product_id`, `id`, `name`, `source_category_id`, `default_product_id` (puede ser nulo), `required` (sí por defecto), `sort` y `active`.
- **Opciones de un grupo:** son **los productos activos de una familia** y de sus subfamilias, por ejemplo «Base: Ginebras premium» y «Refresco: Tónicas».
  - Así no hay otra lista que mantener.
  - En E6, cada opción es un producto que se puede descontar del stock.
- **Sin columna de precio:** no hay suplementos, por decisión del promotor. Si llegan algún día, nunca saldrán en `/catalog`.

**Motor:**
- **Órdenes** `option-group-create|update|deactivate|reactivate` en `/erp/catalog/commands`, con código generado.
- **Validación al vender:**
  - cada grupo obligatorio activo lleva una sola opción;
  - los opcionales llevan una como mucho;
  - la opción tiene que ser de la familia;
  - los errores son `option_missing` y `option_invalid`.
- **Dónde se eligen:** `add-consumption`, `add-consumptions` (por línea) y `add-product` de Caja aceptan `options:[{groupId, choiceId}]` (campo nuevo de `DiningCommand` y de `AccountCommand`, `CheckoutOperations.cs:8-9`). `choiceId` es el código del producto elegido.
- **Lo que guarda la cuenta:** la línea congela las opciones (campo aditivo `Options`) y el texto, siempre con el formato «Gin tonic Premium · Hendrick's + Fever-Tree» (sin «Unidad», C3b), con el mismo precio. Ese texto es el concepto de Caja y del ticket.
- **Lo que lee el comandero:** `/catalog` añade a esos productos `optionGroups:[{id, name, required, defaultChoiceId, choices:[{id, name}]}]`. Los resuelve el servidor, porque una opción puede no tener precio propio y por eso no sale en `/catalog`. Un grupo obligatorio sin ninguna opción activa deja el combinado fuera de `/catalog` y de Caja, en vez de ofrecer algo que luego da `option_missing`.
- **Nombres de las claves:** `optionGroups`, `choices`, `defaultChoiceId`, `required` pasan la guarda de la PWA; nada de `surcharge` ni `extraPrice`.

**WPF:**
- **Ficha del producto** en Bodega › Combinados: el apartado «Opciones del combinado», con «＋ Elegir de una familia» (nombre del grupo, familia y opción por defecto).
- **Caja:** al elegir un combinado salen chips por grupo.

**PWA:** al tocar la fila de un combinado se abre una hoja con chips por grupo y la opción por defecto ya marcada; después, «Añadir a la ronda». Con las opciones por defecto son 2 toques. La hoja no habla de precio (nada de «Precio fijo»): como mucho, «Elegir otra ginebra o tónica no cambia nada».

**Envío a estaciones:** ninguno, porque es una línea de la cuenta. Puede cambiar según la respuesta a la pregunta abierta 4.

**CI:** el workflow del instalador no salta solo con una migración (`dotnet/AGENTS.md:55`: solo con `deploy/windows/**`, `dotnet/tests/service/**` o los ficheros del ciclo de vida); en la PR de E4c se lanza a mano, porque el instalador hace `backup` y `upgrade` sobre v8.

**Pruebas:**
- Dominio: validación de los grupos.
- `catalog_http.py`: grupos y familia de origen; un combinado sin opciones activas no sale en los catálogos.
- `checkout_http.py` y `pwa_http.py`: opción que falta, opción que no es de la familia, idempotencia, texto congelado y mismo precio, también dentro de `add-consumptions`.
- `pwa_http.py`: claves sin dinero.
- `packaging_http.test_06e`: de v1 a v9, con copia y restauración; la tabla nueva en `grants.sql` (sin ella, la batería con el rol de ejecución falla).
- vitest y Playwright: un combinado dentro de una ronda.

**Capturas:** ficha del combinado, Caja y hoja del comandero.

### 4.8. E4f — El menú en una pantalla (`0.45.0-e4f`, sin cambio de esquema; se entrega antes que E4c)

**Motor:** una orden nueva, `offer-dishes-set {offerId, expected:[dishId…], courses:[{courseId, dishes:[{name, stationId?, productId?}]}]}`. Es solo para el responsable, auditada, y va en una transacción:
- cada plato se reconoce **por su nombre** dentro del pase, sin tildes ni mayúsculas. Si ya existía, activo o no, se reactiva y se reordena: no se duplica. Es reconocer por nombre **y por padre** (la oferta y el pase), como E3.1; la clave sigue siendo el código generado (E3.1: «nunca por el nombre solo»). Dos nombres iguales en el mismo pase se rechazan (`duplicate_name`);
- los nombres nuevos se crean con código generado;
- los que ya no están se desactivan (nada se borra);
- la estación por defecto es la que tenía ese plato. Si es nuevo, la más usada en ese pase. Si el pase no tiene ninguna, hay que elegirla.
- **Dos puestos a la vez:** los datos maestros no llevan versión («gana la última edición», `CatalogOperations.cs:11`), pero esta orden sustituye el pase entero. `expected` lleva los platos activos que se leyeron; si no coinciden con los de ahora, `409 offer_changed` y la pantalla relee sin perder lo escrito (F01).

**WPF:** una pantalla **«Menú de hoy»**, con acceso desde Inicio y desde Oferta:
- abre el menú que se propone al abrir mesa;
- muestra los pases en orden, con sus platos como líneas de texto, «＋ añadir plato», quitar, y subir o bajar;
- la estación es un chip dentro de «Más opciones»;
- enseña una vista previa, «Así lo verá el camarero», y un único botón «Guardar»;
- sirve igual para un menú del día con elección.

**Maridaje (decisión 15-10: un menú puede llevarlo o no):**
- un plato de un pase puede ser un **vino de la Bodega**: `{productId}` de un producto sin estación de cocina ni de pase, sin `stationId`. La pantalla lo añade con «＋ vino del pase» y lo pinta como «Vino: …».
- No va a ninguna estación de cocina; el camarero lo ve en el pase (C3c lo muestra con el resto del pase, sin precio). No se cobra aparte: el precio del menú lo incluye, y en Caja la mesa de menú no lleva línea por él.
- `offer-dishes-set` y `offer-create` lo admiten solo en un **Menú cerrado** (`pairing_requires_fixed_menu`); en un menú con elección no hay maridaje.
- Deja de valer la regla «un plato del menú siempre tiene estación» en esos productos: la exclusión de la revisión 25 (productos-menú sin estación) se amplía a los vinos de pase. Sin cambio de esquema: es una fila más de `core.offer_dishes`.
- Hasta E4f, un vino del menú se sirve sin anotarlo, como hoy.

**Mesas abiertas:**
- Un menú cerrado guardó sus platos al abrir (`DiningOperations.cs:28-29`), así que no cambia.
- En un menú con elección, un plato retirado ya no se puede elegir. La pantalla lo avisa.
- Los comanderos ven los platos nuevos al releer `/configuration` (C3a: al abrir la hoja de «Abrir mesa» y cada 5 minutos).

**Envío a estaciones:** cada plato va a su estación, como hoy. Solo cambia de dónde sale la estación por defecto.

**Pruebas:**
- `offers_http.py`: sustituir los platos, reactivar por nombre sin duplicar, desactivar, estación por defecto, nombre repetido, `offer_changed` con `expected` viejo, todo o nada e idempotencia.
- DesktopChecks, con captura.

### 4.9. E4g — Carta por familias y comanda de una vez (`0.47.0-e4g`, sin cambio de esquema)

**Motor:**
- **Pase y familia dejan de ser lo mismo.**
  - Los grupos de la carta siguen siendo pases: Primeros, Segundos, Postres.
  - Cada plato lleva en `configuration.offers.items` su `family` de Cocina (Entrantes, Carnes, Pescados y mariscos, Postres) y su `imageSha` (`OfferUnit.cs:17,112,134`).
  - Así Carnes y Pescados van juntos en el pase de segundos.
- **Los grupos vacíos se omiten solos.** Al enviar o al terminar, un grupo opcional sin platos queda omitido con el motivo «Sin platos pedidos», una palabra que ya está en el vocabulario.
  - Hoy da `course_empty` y obliga a omitirlo a mano (`CourseExecution.cs:87`; `DiningService.cs:130`).
  - Queda guardado como omitido.
  - Los anuncios cambian con ello: `fire-next` se anuncia si queda algún grupo pendiente **con platos**, y `complete` cuando lo que queda pendiente son grupos opcionales vacíos (`AllowedActions.cs:18-22`). Si no hay ningún plato pedido, `fire-next` sigue negándose (`course_empty`) en lugar de omitirlo todo.
- **Comanda en una orden:** `add-dishes {lines:[{courseId?, productId, presentationId, quantity, guestPosition?}]}`. El pase por defecto es el grupo que tiene ese plato; si el plato está en varios grupos de la carta, `courseId` es obligatorio (`course_required`). Va todo o nada, en una sola orden. Las listas son campos nuevos de `DiningCommand`.
- **Menú con elección en una orden:** `choose-many {courseId, choices:[{guestPosition, dishId}]}`.
- **Carta desde el catálogo:** `group-items-set {offerId, courseId, expected:[…], items:[…]}` sustituye de una vez los platos de un grupo, con la misma comprobación de `expected` que E4f. En el puesto se usa con «Añadir una familia entera».
- **Solo platos de cocina en la carta:** `item-create` y `group-items-set` exigen una estación de cocina o de pase (hoy vale cualquier estación activa, `OfferOperations.cs:123`), para que una bebida con estación de barra no se pida como plato ni espere al pase de la comida.

**WPF:** en Oferta › carta, los platos se eligen de Cocina por familia, con buscador. El formulario de platos de menú deja de verse en una carta; hoy queda visible y deshabilitado (`OffersView.xaml:83-95`).

**PWA:**
- **Carta:** en «Carta y menús» de una mesa de carta, chips de familia, buscador sobre la carta y una «Comanda» como la ronda (borrador en memoria), que se envía en una orden. El plato se pide para «Toda la mesa» o para un comensal; las fichas enseñan foto y nombre, no alérgenos ni descripción, que el catálogo no tiene (sección 6).
- **Menú con elección:** «Lo mismo para todos» y un toque por comensal, todo en una orden por pase.

**Envío a estaciones:** cada plato va a la estación de su producto, en el pase de su grupo, como hoy.

**Pruebas:**
- Dominio: omisión automática, anuncios de `fire-next` y `complete` con grupos vacíos, y comanda todo o nada.
- `offers_http.py`: `add-dishes`, `course_required`, `choose-many`, `group-items-set` con `expected` viejo y estación de barra rechazada; `affordances_http.py` para las acciones nuevas.
- vitest y Playwright: una comanda de 4 platos es una sola orden.
- Capturas.

## 5. Qué NO cambia

- **Los botones los decide lo que anuncia el servidor** (D3.1). Un botón de grupo existe solo porque el pase lo anuncia, y solo envía las elaboraciones que lo anuncian. El cliente no deduce reglas: tampoco cruza alérgenos con restricciones, ni decide qué es «agotado» o «por copa» (eso lo marca el servidor con `glass`).
- **Cada acción nueva entra en los tres sitios a la vez:** `Affordances.Allows` (que es la `ActionPolicy` de la ruta), el espejo de estación (`StationKeeps`/`PassKeeps`) y `StationAllowed`. Lo que falte en el primero da 403; lo que falte en el último deja pasar a cualquier estación.
- **F01:** el contexto de cada orden del puesto principal es inmutable, y las órdenes nuevas lo usan igual. En las tablas editables («Vinos por copa», «Menú de hoy») lo escrito es un borrador que una relectura no pisa, y la orden solo lleva filas de la última lectura correcta.
- **Sin dinero en comanderos ni en cocina** (ADR-007).
  - Ninguna clave nueva lleva vocabulario de caja; `pwa_http.py` lo comprueba con la regla de la PWA.
  - Los precios de copa y de combinado se ponen en el puesto principal y siempre los fija el servidor.
- **Una sola orden incierta durable**, en la PWA y en el puesto. Rondas, comandas y grupos son **una** orden con varias líneas, nunca una cola.
- **Nada se borra.** Quitar un vino de la copa, un plato del menú o un grupo de opciones los desactiva.
- **Lo desconocido se muestra tal cual.** Una sección, un tipo de oferta, una decisión o una restricción que el cliente no conozca sale en crudo, nunca rebajada.
- **Restricciones estructuradas**, siempre escritas con tipo, sustancia y gravedad, nunca solo con color (D3.2, D6.4).
- **La matriz de estación vive en el servidor**, y `pase` es la única estación que valida y revisa.
- **Tiempo real con avisos finos y relectura autoritativa.** Los avisos de caja (`account.*`, `payment.*`) solo llegan al principal; el aviso nuevo `service.consumptions_changed` no lleva datos y es operativo.
- **El cargo congela su origen**: producto, presentación, tarifa y ahora opciones. Siguen la regla del precio vigente con la General de respaldo y la tarifa de sala de E3.2.
- **Códigos y rutas existentes:** los códigos internos de los tipos de oferta y las rutas de hoy (`add-consumption`, `review-preparation`, el alias `menuId`…).
- **La importación** no borra ni desactiva nada (y desde E3.3 tampoco deshace la familia ni el IVA de un producto existente).
- **Órdenes con cuerpo nuevo:** los campos nuevos (`lines`, `items`, `itemIds`, `options`, `restrictions`, `stop`, `expected`) se añaden a los registros de orden existentes y son opcionales; una orden de hoy sigue valiendo igual.
- **Esquema:** solo E4c lo sube, a v9, con migración encadenada y prueba desde v1. E4e cambia las reglas con que se leen las instantáneas, no el esquema (sección 4.5).

## 6. Diferimientos conscientes

- **Platos fuera del menú que llegan a cocina.** Por ejemplo, un postre de la carta en una mesa de menú cerrado, u «otra de croquetas» después de enviar el grupo.
  - Hoy solo se pueden cobrar como consumo, y la cocina no se entera.
  - Hace falta que `add-dish` acepte platos de cualquier carta vigente y un pase de «Extras». A la vez, `add-consumption` (la orden antigua) debería rechazar los productos con estación de cocina, como ya hace `add-consumptions` desde C3b.
  - Irá en un corte propio después de E4g, cuando el promotor lo pida. Mientras tanto, el comandero no ofrece platos en Bebidas y su menú no ofrece «Plato fuera del menú» ni «Otro menú para un comensal».
- **Alérgenos de cada plato y descripción del plato.** El catálogo no tiene ni alérgenos ni descripción (`Core.Domain/Catalog.cs:9-12`), y cruzar en el comandero un alérgeno con el texto libre de una restricción («lactosa» frente a «lácteos») sería una regla de seguridad inventada en el cliente (D3.1). Si el promotor lo quiere, es un corte propio con esquema: los 14 alérgenos de declaración obligatoria por producto y por plato del menú, y el cruce hecho en el servidor.
- **Uva, añada y bodega como datos del vino.** Hoy solo existen si están en el nombre. Se pueden traer de los atributos de la tienda en el conversor (`tools/catalog/woocommerce-to-csv.py`) cuando se pidan.
- **Comensal que no toma el menú y cambio de personas** con la mesa abierta (pregunta abierta 5; `docs/native/E4a-offers.md`).
- **Menú con fecha:** preparar el de mañana, «copiar del último día» o consultar el historial. Necesita la columna `served_on` en `core.offer_dishes` (esquema nuevo) cuando un restaurante lo necesite.
- **Elección por recuento** en un menú con elección («Sopa 2 · Ensalada 1», sin asignar comensales).
- **Suplementos** en los combinados y **modificadores** de platos («poco hecho», «sin cebolla»).
- **Pantalla o ticket de barra** para bebidas y combinados (pregunta abierta 4).
- **Stock (E6):** la copa que descuenta de la botella abierta, los agotados y el stock en general. Hasta entonces no hay «Agotado»: un vino sin existencias se desactiva y desaparece del comandero.
- **Miniaturas en el servidor.** Las fotos de la tienda son de 450 × 450 y pesan poco. Si alguna foto grande pesa demasiado en el móvil, se añaden miniaturas.
- **Favoritos y más vendidos automáticos:** necesitan estadísticas de venta (informes). En C3b hacen de atajos «En esta mesa» y «Por copa».
- **Precio de la copa en otras tarifas** (terraza): la orden lo admitiría, pero se queda en la General, como decidió el promotor en E3.2.
- **Auditoría de la importación.** `import-catalog` no escribe eventos `catalog.*` (`CatalogImport.cs:140-209`), en contra de la regla de `dotnet/AGENTS.md`. Irá en un corte correctivo aparte.
- **Cobrar, anular o cerrar desde el Comedor del puesto:** sigue en Caja, también tras C3b, hasta el TPV táctil (Hito 7). Añadir consumiciones desde la mesa abierta ya no se difiere: entra en C3b, con «Lo que lleva la mesa» (petición del 15-10).
- **Aviso sonoro** en cocina.
- **Códigos generados en Salas, mesas y estaciones** (`organization-*`): misma función común que E4d, cuando C2b o un corte posterior rehaga esos formularios. Mientras tanto, desde C2b el cliente genera del nombre el código vacío (sección 4.1).

## 7. Pruebas (en todas las entregas)

- **Locales:**
  - `sh tools/agent/local-tests.sh` completo antes de cada subida que toque `dotnet/`;
  - vitest local para la PWA;
  - nunca desde una consola de administrador en el PC con la instalación real, y con el puerto de pruebas 5090;
  - las pruebas nuevas van en las suites que ya existen (sección 1); si hiciera falta una suite nueva, se añade a `tools/agent/local-tests.sh` y a la CI en su orden, y arranca el motor con su propio `COSTINA_DATA` heredado de `native_http` (C2.1).
- **CI:** se leen los registros de D0, D1, desktop, PWA (vitest y Playwright, ahora con proyecto móvil), servicio e instalador. En E4c el workflow del instalador se lanza a mano (sección 4.7).
- **Prueba real** en la base desechable con la bodega completa (1.037 vinos, 1.017 fotos): copas en bloque (E3.3), búsqueda y ronda en el comandero (C3b) y combinado (E4c).
- **Guion físico** con el móvil y el iPad del promotor al cerrar C3c, con la tabla de criterios de la sección 2.
- **Capturas** de cada corte en `docs/native/images/<corte>/`, revisadas una a una antes de cerrarlo.

## 8. Riesgos

- **Cambio de una regla de seguridad (E4e).** Exigir la revisión de cocina antes de enviar puede frenar un pase si el pase no revisa a tiempo. Mitigaciones:
  - «Por revisar» siempre visible en el pase y en el Comedor;
  - un rechazo con una frase clara: «Cocina tiene que revisar la alergia del comensal 3 en este pase»;
  - revisión por plato agrupado y del menú entero de un comensal en una orden, que deja en pocos toques incluso una restricción de toda la mesa, que genera muchas líneas;
  - entre E4e y C3c, una lista mínima «Por revisar» en la pantalla del pase de la PWA.
- **Sin vuelta atrás tras E4e.** Las mesas guardadas con revisiones en pases sin enviar no las lee un motor anterior (sección 4.5). Mitigación: el documento del corte lo dice y el instalador nunca baja de versión.
- **Rondas rechazadas por la cocina.** Cada toque de cocina sube la versión del servicio, y una ronda preparada contra la versión anterior recibe `version_conflict`. Mitigación: la ronda sigue en la bandeja y se reenvía con un toque (C3b); la prueba real mide cuántas veces pasa en un servicio. Si molesta, se estudia que las rondas comprueben solo que la mesa sigue abierta, con su propio ADR.
- **Tablero más pesado.** `Board()` ya lee mesa a mesa, y C3b le añade los consumos; cada ronda o anulación hace releer a todos los equipos. Mitigación: los consumos de todas las mesas en una sola consulta, y medir el tiempo del tablero en la prueba real con 30 mesas.
- **Ráfagas de avisos.** Una orden `glass-sale-set` de 300 vinos deja 600 avisos `catalog.*`. Mitigación: los clientes agrupan ráfagas; se comprueba en la prueba real.
- **Reescritura de las pruebas E2E de la PWA** (C3a, C3b y C3c), más el proyecto móvil en la CI: la CI tardará más. Mitigaciones: cada spec sigue siendo dueño de su mesa (M6, M7, M8) y el proyecto móvil solo cubre las historias principales.
- **Tamaño del catálogo en el móvil:** unas 2.000 filas en memoria y fotos de hasta 2 MB. Mitigaciones:
  - `ETag` y 304;
  - fotos solo de lo visible, con límite de memoria;
  - vigilar el `LIMIT 5000` en la prueba real; si se acerca, se pagina.
- **Secciones por convención.** Un producto sin sección se clasifica por su estación, así que un pan sin estación caería en Bodega. Mitigación: el apartado «Sin colocar» del Catálogo lo enseña y se mueve en un paso.
- **Reimportar la tienda.** Desde E3.3 la reimportación conserva familia e IVA de lo que ya existe: si la tienda corrige una familia, en Costina hay que moverla a mano. Mitigación: el resumen de la importación dice cuántos productos traían otra familia o IVA y no se cambiaron.
- **Códigos generados.** Dos menús con el mismo nombre dan `…-2`. Mitigación: el puesto avisa «Ya existe un menú con ese nombre» antes de crearlo.
- **Coincidir con C2b.** E4d y E3.3 rehacen formularios de Oferta, Catálogo y Caja que C2b está recolocando. Mitigación: C2b solo mueve y arregla el cierre al guardar, con el código vacío generado en el cliente; los campos cambian aquí (secciones 4 y 4.1).
- **Vocabulario.** Cambia una palabra que el personal ya ha visto: «Menú cerrado» cambia de sentido. Unas 30 aserciones del WPF, de ClientChecks y de E2E cambian en la misma PR.
- **Versiones mezcladas.** Un puesto principal antiguo con un motor nuevo ignora las claves nuevas, que son todas aditivas. La PWA siempre llega con su motor.
- **CSP con `blob:`.** Amplía lo que la página puede pintar. Mitigaciones: solo en `img-src`, nunca en `script-src`, y la prueba E2E sigue exigiendo cero violaciones.

## 9. Decisiones ya tomadas por el promotor

- **15-10:** hacer este trabajo **justo después de C2b y antes de E8 reservas**, **incluido el aspecto nuevo de la PWA (C3)**.
- **15-10:** separar la **comida** (menús y carta) de la **bodega y las bebidas**, y poder pedir **copa, botella y combinados** desde el comandero de forma intuitiva, con buscador.
- **15-10:** el **precio de la copa se pone a mano en cada vino**, y hay que poder marcar **varios a la vez**.
- **15-10:** cada **combinado tiene un precio fijo** («Gin tonic Premium 9,00 €»); la base y el refresco se eligen **sin cambiar el precio**.
- **15-10:** en Costiña el **menú es cerrado: todos comen lo mismo en cada pase**, salvo intolerancias y alergias.
  - La elección por comensal se conserva para otros restaurantes, fuera del camino principal.
  - **«Tenemos que hacer el sistema más intuitivo.»**
- **15-10:** en el puesto principal, **añadir una consumición desde la mesa abierta** del Comedor, sin salir a Caja y sin contraseña en el nivel Puesto, con el mismo selector que el comandero; Caja queda para cobrar, anular y cerrar (C3b). Se le contestó así al promotor.
- **15-10 (tarde, al aprobar E4d):** `tasting` sigue siendo **«Menú degustación»** (nombre del menú de Costiña); la sección se llama **«Menús y cartas»** («oferta» suena a rebaja); el menú propuesto al abrir mesa es el **«Menú del día»**; «Más opciones» y «Subir/Bajar»: sí. Permiso de subida y merge de E4d dado («Sube y mergea y adelante»).
- **15-10 (respuestas a las siete preguntas):**
  - cocina decide cada plato de un comensal con alergia o intolerancia **antes de que salga el pase**, y el pase espera (E4e);
  - lo mismo para las **preferencias e intolerancias leves**: cocina las despacha con un toque;
  - el **IVA de los vinos queda pendiente del gestor**, y mientras tanto **configurable** desde el programa en bloque (E3.3, `products-tax-set`); no se cambia el 21 % de la importación;
  - las bebidas y los combinados **van a la cuenta y se ven en la mesa y en Caja**; sin pantalla de barra por ahora;
  - comensal sin menú y cambio de personas con la mesa abierta: **más adelante** (después de E4g);
  - un menú **puede llevar maridaje o no**: el vino incluido es un plato más del pase, sin cocina y sin cargo (E4f);
  - **el programa sirve para distintos tipos de negocio**: hay que poder añadir un **menú del día** pronto, así que **E4f va antes que E4c**.
- **14-10 (E3.2):** los precios importados de la web son los de sala y van en la General. Se pueden crear más tarifas, pero no se rellenan.
- **14-10 (nuevo aspecto):**
  - tema claro con acento `#0E7A5F` y rojo solo para alergias;
  - nombres y no códigos, todo en castellano;
  - el vocabulario se puede cambiar antes de C3.

## 10. Preguntas abiertas (solo las que necesitan al promotor)

**Contestadas el 15-10-2026** (respuesta del promotor → qué hace el plan): 1 «sí» → E4e como está, con preferencias e intolerancias leves incluidas (su 2, también «sí»). 2 «déjalo pendiente y configurable» → E3.3 añade `products-tax-set` y el botón «Cambiar IVA de los marcados»; el tipo lo confirma el gestor, sin fecha. 4 «basta por ahora» → sin pantalla de barra (diferimiento). 5 «más adelante» → diferimiento, después de E4g. 6 «puede ser menú con maridaje o sin él» → E4f admite un vino de la Bodega como plato de un pase en los menús cerrados. 7 «hay que poder añadir menú del día» → E4f antes que E4c (versiones `0.45.0-e4f` y `0.46.0-e4c`). Queda solo una cosa pendiente del promotor, y no bloquea nada: el tipo de IVA que diga su gestor. El texto original de las preguntas se conserva a continuación porque el resto del plan lo cita.

El «Resumen para el promotor» las hace en palabras llanas, para contestar con una palabra o una línea. La 1 se parte en dos (sus 1 y 2); su segundo detalle, «toda la mesa», no se le pregunta, porque es lo seguro. La 2, 4, 5, 6 y 7 son sus 3, 4, 5, 6 y 7. La 3 ya no se pregunta: el resumen la da como algo que se queda igual. La numeración de aquí se conserva porque el resto del plan la cita.

1. **Alergias antes de enviar.** Cuando un comensal tiene una alergia o intolerancia, ¿aceptas que **cocina tenga que decidir cada plato de su menú** («no le afecta» o «adaptado: …») **antes de que ese pase salga**, y que el pase no salga sin esa decisión? Hoy solo se revisa lo ya enviado. Propuesta: sí (E4e). Dos detalles para que decida sabiendo lo que cuesta:
   - ¿también las **preferencias** («no le gusta el cilantro») y las intolerancias **leves**, o solo alergias e intolerancias? Propuesta: todas, porque cocina las despacha con «no afecta» en un toque por comensal;
   - una restricción de **toda la mesa**, y los platos de carta **para toda la mesa**, piden revisar todos los platos afectados. Propuesta: sí; es lo seguro.
2. **IVA de los vinos.** Los 1.037 vinos entraron con **IVA 21 %**, el de la tienda. En sala suele ser el 10 %, y la copa hereda el IVA del vino. ¿Lo confirma tu gestor? Si es el 10 %, E3.3 añade «Cambiar IVA de los marcados», y la reimportación ya no lo devolverá al 21 % (sección 4.2).
3. **Precios en el comandero (ya no se pregunta).** Por seguridad (ADR-007), comanderos y cocina **no ven importes**: el camarero verá «Copa» y «Botella» sin precio, y el selector de la mesa del puesto tampoco los pinta (C3b). Se queda como está. Si el promotor pide verlos algún día, es un cambio de regla con su propio corte.
4. **Bebidas y combinados en la barra.** ¿Basta con que vayan a la cuenta y se vean en «Lo que lleva la mesa», o quieres que salgan en una **pantalla de barra** para que alguien los prepare? Propuesta: solo a la cuenta por ahora.
5. **Comensales y menú.** ¿Necesitas ya que **un comensal no tome el menú** (un niño, o alguien que solo toma un plato) o **cambiar el número de personas** con la mesa abierta? Propuesta: dejarlo para después de E4g.
6. **Maridaje.** ¿El menú de Costiña lleva **vino por pase incluido** en el precio? Si es así, esas copas no se pueden pedir como bebida (se cobrarían aparte): habría que meterlas en el menú. Propuesta: preguntar antes de C3b; si no hay maridaje, nada cambia.
7. **Orden de los últimos cortes.** El menú del día cambia cada día: ¿prefieres «El menú en una pantalla» (E4f) antes que los combinados (E4c)? Propuesta: sí, si el menú del día se usa ya.

## Revisión

Revisión crítica del borrador contra el código de `8bb94a0` (copia de estudio, solo lectura) y contra la maqueta `Comandero.dc.html`. Lo que se cambió en el plan y por qué:

**Errores de hecho corregidos**
1. **Puesto principal y menú propuesto:** no propone «la primera por orden», sino la elegida la vez anterior (`ServiceViewModel.cs:228`). E4d hace que proponga siempre la primera vigente.
2. **Vocabulario:** faltaba `MainWindow.xaml:355`, y el cambio no es «una línea en `Labels.cs` y otra en `labels.ts`». Hay plurales propios en `HomeViewModel.cs:279` y `OffersViewModel.cs:117`, `labels.ts` no tiene tipos de oferta y las aserciones están en `ClientChecks:463` y `DesktopChecks:617`.
3. **«Menú del día» como ejemplo de «Menú con elección»:** contradecía al promotor, que lo pone junto a la degustación como menú cerrado. Se quitó el ejemplo y se añadió la nota.
4. **No existe `dining_http.py`:** las pruebas pasan a las suites reales (`checkout_http`, `pwa_http`, `offers_http`, `restrictions_http`, `station_http`, `affordances_http`).
5. **E4e decía «Instantáneas: no hay campos nuevos» como si no cambiara nada.** La restauración rechaza hoy revisiones en pases sin enviar (`Snapshots.cs:80-81`) y un acuse con la mesa «Sentados» (`:55-57`). Sin ampliar esas reglas, la primera mesa con alergia declarada al abrir quedaría ilegible. Ahora se dice, junto con la compatibilidad D3.2 (`:61-62`) y que no hay vuelta atrás.

**Invariantes que el borrador rompía o dejaba abiertos**
6. **Revisar con la mesa «Sentados»:** `review-preparation` exige servicio activo (`Restrictions.cs:79`; `DiningService.cs:152-158`) y solo se anuncia en un pase activo (`AllowedActions.cs:58`). Con la regla nueva y `fire-next` desde «Sentados», el primer pase no saldría nunca. Se cambian los dos.
7. **Matriz de estación de las órdenes de grupo:** `StationAllowed` (`DiningOperations.cs:57-72`) deja pasar cualquier acción que no conoce. Las acciones nuevas se añaden explícitamente allí y en `Affordances` (`KitchenAllowed`, exclusión de sala, `StationKeeps`/`PassKeeps`). Sin eso, una estación podría marcar elaboraciones de otra, o recibir un 403 que sí se anuncia.
8. **Omitir un pase con revisiones pendientes:** dejaba «por revisar» para siempre y una instantánea inválida. Ahora omitir retira las revisiones de ese pase.
9. **`review-preparations` con una sola decisión y una sola nota** no sirve para revisar el menú de un comensal, donde cada plato lleva su adaptación. Pasa a decisión y nota por elaboración.
10. **Cuerpos de orden:** la ruta rechaza campos desconocidos (`Contracts.cs:12`). Las listas nuevas se declaran como campos de `DiningCommand`, `CatalogCommand`, `OpenRequest` y `AccountCommand`.
11. **ADR-007 prohíbe la «cuenta provisional» en sala:** «Lo que lleva la mesa» necesita un apartado escrito en ese ADR, además de no llevar importes.
12. **Palabras de caja en el comandero:** las E2E fallan con «precio», «cuenta», «pago», «saldo» o «€» en pantalla (`board.spec.ts:103`, `kitchen.spec.ts:151`). La maqueta usaba «Precio fijo» y «la cuenta se ve en caja»; se fijó la regla en el vocabulario.
13. **Bebidas que son platos:** el filtro por sección era solo del cliente. `add-consumptions` rechaza ahora los productos con estación de cocina o de pase (`use_add_dish`); `add-consumption` no cambia.
14. **Avisos de Caja:** `add-product` y `void-charge` solo emiten `account.*` (solo principal). El aviso operativo nuevo (`service.consumptions_changed`) también sale de ellos, para que «Anulado en Caja» llegue a los comanderos.
15. **Clasificar consumos por producto** ocultaba justo el error que se quiere ver (un plato cobrado como consumo). Se añade a la línea de la cuenta un campo aditivo `Origin`; la clasificación por producto queda solo para líneas antiguas.
16. **Concurrencia en las órdenes que sustituyen listas enteras** (`offer-dishes-set`, `group-items-set`): los datos maestros no llevan versión. Se añade `expected` y `409 offer_changed`.
17. **F01 en tablas editables** («Vinos por copa», «Menú de hoy»): lo escrito es un borrador que una relectura no pisa.
18. **«Guardar» de copas eran dos órdenes** (`glass-sale-set` y `glass-sale-stop`). Ahora es una, con `stop`, como pide el criterio de la sección 2.
19. **Ronda como borrador:** se deja escrito que vive solo en memoria y nunca es una cola (D6.1, D6.3), y qué pasa tras un `version_conflict`, frecuente porque cada toque de cocina sube la versión.
20. **Prueba de «un solo efecto» de la ronda:** no puede contar la versión del servicio, que no sube; cuenta las líneas.
21. **304 en la PWA:** `call()` lo trata como error y todo va con `no-store` (`api.ts:42,53-56`). Se añade una lectura condicional.

**Fallos nuevos encontrados en el código**
22. **Código del producto-menú:** `"menu-" + código` (`OfferOperations.cs:48`) no cabe en 32 caracteres con un código generado de un nombre largo. Se deriva aparte.
23. **`offer-create` no audita** el producto-menú ni su precio (`OfferOperations.cs:48-61`). E4d lo corrige.
24. **La reimportación pisa familia e IVA** de los productos existentes (`CatalogImport.cs:172-177`). Desharía «Mover a…» y «Cambiar IVA de los marcados». Se propone que conserve los dos.
25. **Productos-menú sin estación** caerían en Bodega con la regla «sin estación = bebida». Se excluyen.
26. **`EnsureOrganizationAsync` solo corre en `init`, `upgrade`, `restore` e `init-lab`** (`Program.cs:85,107`): las raíces reservadas aparecen con el `upgrade` del instalador, y las pruebas que cuentan categorías cambian.
27. **Carta con productos de barra:** `item-create` acepta cualquier estación activa (`OfferOperations.cs:123`). E4g exige cocina o pase.
28. **`add-dishes`** era ambiguo si un plato está en varios grupos; ahora pide `courseId` (`course_required`). **La omisión automática** de grupos vacíos cambia también los anuncios de `fire-next` y `complete`.

**Cortes, orden y alcance**
29. **C3a era demasiado grande:** aspecto entero, reescritura de E2E, proyecto móvil, cinco cambios del motor y la bodega. Se parte en **C3a** (aspecto y mesas, sin motor) y **C3b** (Bodega y bebidas, con el motor de las rondas). El antiguo C3b pasa a **C3c**, y las versiones se corren una.
30. **`offers-reorder`** sustituye a «reescribir `sort` con `offer-update`», que exigía una orden por oferta porque el orden no baja de 0.
31. **Coordinación con C2b**, que está en curso, para no rehacer dos veces Oferta, Catálogo y Caja.
32. **CI de E4c:** el workflow del instalador no salta con una migración; hay que lanzarlo a mano.
33. **`/configuration` en el comandero** se relee al abrir mesa y cada 5 minutos. Si no, los platos cambiados con E4f no llegan.
34. **Preguntas al promotor:** nuevas la 6 (maridaje) y la 7 (E4f antes que E4c); la 1 se amplía con preferencias, restricciones de toda la mesa y platos para compartir.
35. **Petición del 15-10, «Añadir consumición» desde la mesa del puesto:** entra en C3b, porque usa la misma orden `add-consumptions` y la misma regla de una orden por ronda. Quita el diferimiento «Añadir bebidas desde el Comedor del puesto» de la sección 6, que mandaba a Caja hasta el Hito 7.
36. **Cierre al guardar, arreglado en C2b:** E4d parte de ese arreglo (código vacío generado en el cliente) y lo lleva al motor. Para los nombres largos, C2b recorta a 27 caracteres el código generado de una oferta, porque el producto-menú se llama `menu-<código>` y un código admite 32.
37. **Resumen para el promotor** al principio, en palabras llanas, con las preguntas para contestar en una línea. La 3 (precios en el comandero) deja de preguntarse.

**La maqueta ya sigue el plan** (corregida el 15-10): sin palabras de caja, sin «Más pedidos» ni «Agotado», sin uva ni añada fuera del nombre, carta sin descripción ni alérgenos, «Más opciones» solo con pausar y omitir, plato adaptado con su nombre, «Enviar siguiente pase», «Toda la mesa», un toque suma 1 (la hoja de cantidad y comensal solo sale desde «Para: …» de la ronda; la de opciones, en combinados), sin marca «De la casa», «Hendrick's + Fever-Tree», solo tema claro, tableta 820 × 1180 y la pestaña del PC.

**Comprobado y correcto en el borrador:** las cifras de `/catalog` (`LIMIT 5000`, solo precios de la General, sin padre de la categoría ni estación); la guarda de dinero; `add-consumption` sin elaboración y sin mover la versión; el fallo de la estación al reimportar; el bloqueo global de `fire-next`; la apertura por comensal en la degustación; el tope de 32 KiB; la CSP sin `blob:`; las rutas y roles de las imágenes; que el vocabulario puede cambiar antes de C3 (`nuevo-aspecto…md:154`).
