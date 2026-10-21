# C3b — Bodega y bebidas: comandero y mesa del puesto

Fecha: 2026-10-21. Rama `feat/c3b-bodega-y-bebidas` sobre `develop` (`49e03b3`, `0.41.0-c3a`). Cuarto corte del plan aprobado `docs/plans/2026-10-15-menus-carta-y-bodega.md`, sección 4.4, bajada a pasos en `docs/plans/2026-10-21-c3b-bodega-y-bebidas.md`. El promotor la aprobó el 21-10 con «adelante con c3b», que da también el permiso para subirla y mezclarla. Versión **`0.42.0-c3b`**.

**Sin cambio de esquema** (sigue la v8). El motor **solo añade**:
- una orden nueva, la ronda;
- campos opcionales en la línea de la cuenta;
- la lista «Lo que lleva la mesa» en el tablero;
- un aviso operativo;
- `blob:` en `img-src` de la CSP.

Las cuentas guardadas antes se leen igual. Cambian el comandero (PWA) y la ficha de la mesa del Comedor en el puesto principal (WPF).

Lleva también lo que D6.7 y E3.3.1 dejaron para antes de C3b:
- la **defensa del servidor contra el doble cargo**;
- el orden de guardado de la orden sin confirmar en el puesto;
- tres rechazos que salían en crudo.

## Lo que notará el promotor

### En el comandero (móvil o tableta)

- **«Bodega y bebidas» es nueva**, como en la maqueta (pestañas «2 · Bodega y bebidas» y «2b · La ronda»):
  - **un buscador arriba**, que no se va al desplazarse. No distingue tildes ni mayúsculas y busca en el nombre, la referencia, la familia y la región. Da resultados desde 2 letras: con una sola, dice «Escribe una letra más para buscar.»;
  - **antes de buscar:**
    - «Por copa», con los vinos que el servidor vende por copa;
    - las familias en chips con su número (Blancos 3, Tintos 5…) y, debajo, sus subfamilias (Rías Baixas, Ribeira Sacra…);
    - en la primera ficha, **«En esta mesa»**: lo ya pedido, para repetirlo con «+1»;
  - **una fila por vino o bebida**, con la foto, el nombre, «familia › región» y **un botón por formato**: «Copa» primero, después «Botella 0.75 L». Si solo hay uno, un botón «Añadir»;
  - **sin precios**, como siempre en el comandero.
- **Un toque suma 1 a la ronda**, para toda la mesa, sin hojas intermedias. La fila dice «1 en la ronda».
- **La ronda va abajo**: «Ronda · 3 sin enviar», con lo que lleva y el botón **«Enviar»**.
  - Al desplegarla: «−» y «+» (en 1, la papelera quita la línea) y «Para: Toda la mesa».
  - «Para» abre la hoja de la línea, con la cantidad y el comensal. Los comensales con alergia o restricción llevan un punto rojo, y la hoja la escribe entera.
  - Mientras la ronda tenga algo, también se ve en la ficha de la mesa, encima del botón grande.
- **«Enviar» es una sola orden**, aunque la ronda lleve 4 bebidas. Antes eran 4 órdenes, con la pantalla bloqueada entre una y otra.
  - Al confirmarse, la ronda se vacía y dice «Ronda enviada: ya sale en «Lo que lleva la mesa».».
  - Si no llega la respuesta, la ronda pasa a ser la **orden sin confirmar** de siempre («Ronda para Mesa 4: 2 × Albariño · Copa, 1 × Agua mineral · Botella»): se reintenta igual y se resuelve sola, con un solo efecto.
  - Mientras haya una orden sin confirmar, la ronda se puede seguir llenando, pero no enviar.
- **Si la mesa cambió mientras tanto** (por ejemplo, cocina marcó un plato), la ronda **sigue en la bandeja**, la mesa se relee y el botón dice **«La mesa ha cambiado: enviar de nuevo»**.
- **Si el servidor rechaza una línea**, la ronda sigue entera y la bandeja dice cuál y por qué («Línea 2, …: …»). No se añade nada.
- **La ronda es un borrador**: no se guarda en el aparato. Si la página se recarga, se pierde, y la bandeja ya lo avisa. Salir de la mesa con una ronda sin enviar pregunta antes («¿Salir sin enviar la ronda?»).
- **«Lo que lleva la mesa»** en la ficha, con lo ya enviado de bebida y extras, también lo apuntado en Caja:
  - nombre, Copa o Botella, cantidad, comensal y hora;
  - «Anulado en Caja», tachado, cuando se anula;
  - el pie «Los importes solo se ven en Caja.».

  En una tableta, la ficha va a dos columnas, como la maqueta.
- **Lo que hace otro aparato llega solo:** la ronda de otro camarero y una anulación en Caja aparecen sin tocar nada.
- **Las fotos** de la tienda (E3.2) se ven en las filas. Si un producto no tiene, sale un recuadro con sus iniciales.
- **El catálogo se mantiene al día:** se revisa al abrir Bebidas, cada 5 minutos y al volver la conexión, sin descargarlo entero si no ha cambiado.

### En el PC principal (Comedor › ficha de la mesa)

Es la petición del 15-10 (pestaña «5 · En el PC» de la maqueta):
- **«Lo que lleva la mesa»** en la ficha de una mesa abierta, entre los pases y las elaboraciones, con lo mismo que el comandero y el pie «Los importes solo se ven en Caja.». **Sin importes, también para el responsable.**
- En su cabecera, **«Añadir consumición»**:
  - **no pide la contraseña en el nivel Puesto**. Sale y se habilita solo si el servidor anuncia la ronda: el Puesto es un aparato de sala y la tiene;
  - abre **dentro de la ficha**, sin salir del Comedor, el mismo selector «Bodega y bebidas» del comandero: buscador con el cursor ya puesto, «Por copa», familias y subfamilias, «En esta mesa», filas con foto y un botón por formato. Sin precios;
  - mientras está abierto, la ficha se ensancha y el plano pasa a dos columnas, como en la maqueta. Las restricciones se quedan arriba, escritas.
- **La ronda**, abajo: «Ronda · n sin enviar», plegada como en la maqueta. Al desplegarla: «−», «+», la papelera y «Para:», un desplegable con cada comensal y sus restricciones escritas («Comensal 2 · ALERGIA frutos secos (grave)»).
  - «Enviar» es **una sola orden**, con la mesa tal como se leyó en ese momento.
  - Con la ronda sin enviar y el selector cerrado, la bandeja sigue en la ficha, encima del botón grande.
- **Cambiar de mesa o de vista con una ronda sin enviar** pregunta antes, dentro de la propia ventana: «¿Salir sin enviar la ronda?», con «Salir sin enviar» o «Seguir con la ronda». Vale para cambiar de mesa, para «Abrir mesa» y para ir a otra vista desde el menú, la barra lateral o un acceso como «Ver cuenta».
- **Caja busca igual que el selector:** sin tildes, por nombre, referencia, familia y región, y con las palabras en cualquier orden («pazo albar» encuentra «Albariño Pazo de Señoráns»). Sigue buscando desde una letra y con sus precios. Cobrar, anular y cerrar no cambian.

### En Caja y en la cuenta

- **La línea dice «Agua con gas», no «Agua con gas · Unidad»**: la ronda, el consumo suelto y «Añadir consumo» de Caja ya no escriben la presentación por defecto. Las líneas del menú y de los platos de la carta no cambian.
- **Anular un consumo en Caja** se ve al momento como «Anulado en Caja» en los comanderos y en la ficha del Comedor.

### Criterios de la sección 2 del plan

| Tarea | Antes | Ahora |
|---|---|---|
| Copa de un vino conocido | buscarlo a ojo en un desplegable de unas 2.000 filas | «Bodega y bebidas» → 3 o 4 letras → «Copa» → «Enviar»: 3 toques más las letras |
| Ronda de 4 bebidas | 4 órdenes, con la pantalla bloqueada entre ellas | 4 toques y «Enviar»: **1 orden** |
| Copa para una mesa abierta, desde el PC | «Ver cuenta» (en el nivel Puesto, con la contraseña del responsable) y buscar en Caja | en la ficha: «Añadir consumición» → letras → «Copa» → «Enviar»: 1 orden, sin contraseña |

## La defensa contra el doble cargo (diferida de D6.7)

**El riesgo.** El servidor guarda la idempotencia **por actor**: la misma clave enviada por otra identidad es, para él, una orden nueva. La revisión externa del 19-10 lo encontró:
1. una tableta deja un consumo sin confirmar;
2. se vuelve a emparejar con otro nombre de dispositivo;
3. la orden guardada se reenvía y la mesa queda cobrada dos veces.

D6.7 lo cerró en los clientes: una orden de otro actor queda bloqueada y nunca se reenvía. Faltaba la defensa en el servidor, que no depende de que el cliente lo haga bien.

**Lo que hace ahora el motor:**
- **El identificador de cada línea se deriva de la orden** (`SettlementAccount.DerivedLineId`): se calcula con el servicio, la clave de la orden y la posición de la línea (SHA-256; 32 caracteres hexadecimales). Vale para la ronda (`add-consumptions`) y para el consumo suelto (`add-consumption`), que el comandero usaba hasta ahora.
- **La cuenta nunca admite dos líneas con el mismo identificador** (`duplicate_charge`).
- **Si la misma orden vuelve con otro actor**, el motor ve que sus líneas ya están en la cuenta, iguales, y **responde lo mismo sin cobrar nada más**. Lo mira antes que la versión de la mesa, así que responde igual aunque la mesa haya cambiado entretanto; la versión de la respuesta es la actual.
- **Si esa clave trae otras líneas**, se rechaza con `409 duplicate_consumption` y no se apunta nada.

**Lo que ya estaba protegido:** las demás órdenes de sala mueven la versión de la mesa (o de la ocupación, al liberar), así que su reenvío con otro actor recibe `version_conflict`. Abrir otra vez una mesa ya abierta se rechaza porque está ocupada. «Añadir consumo» de Caja exige la versión de la cuenta, que también sube. Los consumos eran el único caso abierto: no mueven la versión de la mesa, para no pisar las órdenes de otros camareros.

**La prueba** (`checkout_http.py`):
- `test_06`: dos comanderos emparejados de verdad, con actores `device:<nombre>` distintos, mandan la misma ronda con la misma clave. Queda **un solo cargo** por línea. Un tercer actor con la misma clave y otras líneas recibe `duplicate_consumption`;
- `test_06b`: lo mismo con `add-consumption`.

Con el motor de C3a, `test_06b` daba dos identificadores distintos, es decir, dos cargos. Con los identificadores derivados cambiados por otros al azar, `test_06` falla con «3 != 5 : the same order charged twice».

## Qué cambia

### Motor

- **La ronda en una orden:** `POST /dining/services/{id}/commands/add-consumptions` con `{expectedVersion, lines:[{productId, presentationId?, quantity, guestPosition?}]}`.
  - `lines` es un campo nuevo y opcional de `DiningCommand` (registro `ConsumptionLine`). Una orden de hoy vale igual.
  - Es **acción de sesión** de principal y sala (`DiningModule.SessionActions`). Cocina recibe 403 de la política de la ruta, que es la misma `Affordances.Allows` que filtra lo anunciado.
  - Admite de 1 a 30 líneas (más: `409 too_many_lines`), cantidades de 1 a 99 y el comensal opcional, dentro de las personas de la mesa.
  - **Todo o nada**, en la transacción de la orden. Una línea mala da `422 {error, line}`, con su índice desde 0 (excepción `LineRejected`, que atiende `Program.cs`), y no se apunta ninguna.
  - **Un producto con estación de cocina o de pase** se rechaza con `use_add_dish` («pídelo como plato»): lo que se pide en Bebidas nunca se cobra sin que cocina lo vea. `add-consumption` no cambia en esto, por compatibilidad.
  - La respuesta no lleva dinero: `{version, consumptionIds}`. **La versión de la mesa no se mueve**, pero se exige la leída (F01).
- **La línea de la cuenta guarda más datos**, todos opcionales:
  - el comensal (`GuestPosition`);
  - el origen (`Origin`): `consumption` (sala), `dish` (plato de la carta), `menu` (el menú al abrir) o `checkout` (Caja);
  - la hora (`At`).

  Una cuenta anterior se restaura con esos campos vacíos.
- **Decisión (ADR-012):** el comensal es un dato de sala y se guarda dentro de la línea de la cuenta, que es del núcleo. Se acepta porque es un número opcional, sin regla del módulo: la cuenta solo comprueba que sea positivo, y el módulo comprueba que esté dentro de la mesa. El núcleo sigue sin referencias a tablas del módulo. Si otro módulo necesitara datos propios por línea, irían en su esquema, no en la cuenta.
- **El texto de la línea sin la presentación por defecto** (`Catalog.LineText`): «Tónica», no «Tónica · Unidad», en la ronda, el consumo suelto y «Añadir consumo» de Caja. La línea del cobro de `add-dish` conserva su texto («… · Ración»).
- **Lo que lleva la mesa:** cada entrada de `GET /dining/board` añade `consumptions:[{id, productId, presentationId, name, presentation, quantity, guestPosition, at, voided}]`.
  - El núcleo lee las cuentas de todas las mesas del tablero en **una sola consulta** (`AccountLines`) y solo saca de cada línea los campos sin dinero. Nunca se usa `ChargeLine` ni la vista de la cuenta, que llevan precio, total y tarifa.
  - El módulo decide qué se ve (`TableConsumptions`):
    - origen `consumption`, `checkout` o uno que no conoce: sí;
    - `dish` o `menu`: no.
  - **Líneas anteriores a C3b**, sin origen: se clasifican por su producto.
    - Fuera el producto-menú y los platos que tienen su elaboración en un pase.
    - Un plato apuntado por error como consumo **sí** se ve, para que alguien lo note.
  - `name` es el nombre actual del producto en el catálogo; sin producto, el texto guardado en la línea. `presentation` va vacío para la presentación por defecto.
- **Aviso operativo nuevo `service.consumptions_changed`**, sin datos y con el servicio como agregado. Lo dejan la ronda, el consumo suelto y, en Caja, «Añadir consumo» y «Anular».
  - No empieza por `account.` ni por `payment.`, así que llega a todos los roles. Cada aparato relee el tablero.
  - Los avisos de caja siguen llegando solo al principal.
- **CSP de la PWA:** `img-src 'self' blob:`, solo para pintar las fotos que la propia PWA pide con su token. Sigue sin `unsafe-inline`, sin terceros y sin `data:`.
- **ADR-007, apartado C3b:** «Lo que lleva la mesa» (nombre, formato, cantidad, comensal, hora y «Anulado en Caja», sin importes ni totales) **no es la cuenta provisional**. Está en `docs/adr/ADR-007-service-control-vs-checkout.md`.

### Lógica común del comandero y del puesto (una sola tabla de casos)

- **`dotnet/tests/fixtures/drinks-cases.json`** la leen vitest (`tests/drinks.test.ts`) y ClientChecks. Si el comandero y el puesto se separan, fallan los dos. Un apartado, campo, paso de ronda o resultado que la prueba no conoce es un fallo: nunca se salta.
- **Búsqueda** (`search.ts` y `Costina.Client/CatalogSearch.cs`):
  - el texto se pliega: sin tildes (NFD sin las marcas) y en minúscula;
  - busca en el nombre, la referencia, la familia y la ruta de categorías (la región);
  - empieza en 2 letras, sin contar los espacios de los extremos, y cada palabra escrita tiene que estar en algún campo;
  - orden de los resultados:
    1. el nombre empieza por la primera palabra, o la referencia es exactamente lo escrito;
    2. alguna palabra del nombre empieza por ella;
    3. el resto.

    Dentro de cada grupo, por nombre plegado, nombre e identificador, comparados carácter a carácter. **Sin reglas de idioma**, para que los dos clientes no se separen con versiones distintas de ICU;
  - los nombres de las presentaciones no se buscan.
- **Agrupación** (`drinks.ts` y `Costina.Client/DrinksPicker.cs`):
  - solo las filas de la sección `bodega` con presentación;
  - una fila por producto, de la A a la Z;
  - botones: las copas primero, después el orden del servidor, y «Añadir» si solo hay una presentación;
  - detalle: la ruta con « › » y, si solo hay una presentación que no es la de por defecto, su nombre. Si se buscó por referencia, añade « · Ref. X»;
  - familias de la A a la Z, con «Otros» al final; subfamilias solo cuando hay 2 o más;
  - «En esta mesa», con lo del tablero en el orden en que se pidió:
    - sin anulados ni repetidos;
    - solo lo que se sigue vendiendo en Bodega.
- **Ronda** (`round.ts` y `Costina.Client/Round.cs`), **un borrador en memoria**:
  - al sumar dice si entró, si sumó a una línea, si la ronda está llena (30 líneas), si la línea llegó a 99 o si se está enviando;
  - dos líneas iguales se juntan solo si no pasan de 99;
  - no se envía mientras haya una orden sin confirmar o bloqueada;
  - `begin(versión, mesa)` arma **un solo cuerpo** y la descripción («Ronda para Mesa 4: 2 × … · Copa, 1 × … (Comensal 2)»). Hasta saber el resultado, la ronda no se edita;
  - resultados:
    - **confirmada** o **sin confirmar**: la ronda se vacía. La sin confirmar ya es la orden pendiente durable, y dejarla en la bandeja podría mandarla dos veces;
    - **rechazada**: la ronda sigue, con el código y la línea;
    - **`version_conflict`**: la ronda sigue y el botón ofrece «La mesa ha cambiado: enviar de nuevo», que es **otra orden, con otra clave**;
    - **no enviada**: la ronda sigue igual.
  - **Nunca se guarda:** vitest comprueba que esos módulos no importan almacén, órdenes pendientes ni base de datos, ni usan el almacenamiento del navegador. ClientChecks comprueba que `Round` no guarda ningún almacén, cliente HTTP, flujo ni fichero.
- **Modelos:**
  - PWA: `TableConsumption` y `BoardEntry.consumptions?`;
  - .NET: `ConsumptionDto`, `BoardEntry.Consumptions` (opcional, al final), `CatalogItem` (la fila de `GET /catalog`, sin dinero) y `ProductChoice.CategoryPath` (opcional).

### Comandero (PWA)

- **`DrinksPanel.vue`**, reescrito: sustituye al desplegable de C3a.
  - Buscador fijo arriba.
  - Familias que se desplazan de lado en el móvil, y en columna a la izquierda en una tableta.
  - Filas de 60 en 60, con «Ver más».
  - Un `data-testid` en cada fila y en cada botón.
- **`RoundTray.vue`** (la bandeja) y **`LineSheet.vue`** (la hoja de una línea). La ronda pertenece a la ficha de la mesa (`TableCard.vue`), que también pinta «Lo que lleva la mesa».
- **`bodega.ts`**: los textos y los accesos, sin reglas de negocio, y `settlementOf`, que traduce el resultado de la orden. Sin respuesta, la ronda cuenta como sin confirmar **solo si esta misma orden** quedó guardada como la pendiente.
- **`commands.ts`** lleva el `line` de un 422 hasta la bandeja.
- **`operations.ts`** da por **confirmada** una orden aplicada cuya respuesta traía dinero. Así una ronda nunca se envía dos veces.
- **Fotos** (`photos.ts` y `DrinkPhoto.vue`):
  - se piden con el token y solo para las filas que se ven (`IntersectionObserver`);
  - se guardan en memoria por su huella (`imageSha`), hasta 150 o 20 MB. Al descartar una, se libera su URL `blob:`;
  - una sola petición por foto, aunque la pidan varias filas a la vez;
  - solo se aceptan JPEG, PNG o WebP. Las iniciales se saltan los números («Lalama 2019» da «L»).
- **Catálogo** (`catalog.ts` y `readCatalog` en `api.ts`):
  - se lee al montar el tablero, solo si el servidor anuncia la ronda;
  - se revalida con `If-None-Match` al abrir Bebidas, cada 5 minutos y cuando el tiempo real vuelve. Un 304 conserva la misma lista;
  - cada 200 pasa entero por la guarda de dinero;
  - sigue con `cache: 'no-store'`, sin credenciales del navegador y sin seguir redirecciones.

  `getCatalog` desaparece.
- **Lo que hace otro aparato** llega por la relectura de siempre en cada aviso. La E2E lo prueba con un segundo comandero.

### Puesto principal (WPF)

- **`ServiceView.xaml`**: «Lo que lleva la mesa» (`HadBlock`) con «Añadir consumición» en su cabecera. Sale cuando el tablero trae `consumptions` o el servidor anuncia la ronda. Contra un motor o una sesión de C2 (sin ninguna de las dos), la ficha es exactamente la de antes; la captura `service.png` no cambia.
- **«Añadir consumición»** (`SoftAccentButton`) solo existe y se habilita si `/session` anuncia `add-consumptions`, y nunca pasa por `RequestElevation`.
- **`Views/DrinksPicker.xaml`**, con `ViewModels/DrinksViewModel.cs`, abre dentro de la ficha:
  - la ficha pasa de 400 a 480 px y el plano, de 3 a 2 columnas;
  - se ocultan los pases y la acción grande, que vuelven al cerrar;
  - lee `GET /catalog` cada vez que se abre, nunca `/checkout/catalog`;
  - las fotos van por el `ImageLoader` del Catálogo.
- **`Views/RoundTray.xaml`**: la bandeja. «Enviar» manda **una** orden `add-consumptions` con la versión de la ficha leída en ese momento (F01).
  - **Confirmada:** se vacía, la barra de estado lo dice y la mesa se relee.
  - **Sin confirmar:** pasa a ser la orden pendiente de siempre.
  - **Rechazada:** sigue, con el motivo y la línea.
  - **`version_conflict`:** relee y ofrece enviar de nuevo, con otra clave.
  - **No enviada:** sigue.

  Una relectura del tablero nunca toca la ronda. Si la mesa desaparece del tablero, la ronda se descarta y la barra de estado lo dice.
- **Salir con una ronda sin enviar** pregunta en una capa dentro de la vista, no en otra ventana.
- **`ApiError.Line`**: la línea de un 422 de la ronda. Con cualquier otra forma de respuesta queda vacía; nunca se inventa una línea.
- **Caja** (`CheckoutViewModel`) usa la búsqueda y el orden comunes (`DrinksPicker.SearchFields`). Sigue buscando desde 1 letra, con el orden del servidor cuando el buscador está vacío y como mucho 24 grupos.
- **Tema:**
  - iconos `IconGlass`, `IconMinus`, `IconTrash`, `IconSend`, `IconChevronUp` e `IconChevronDown`;
  - estilos `SoftAccentButton`, `FormatButton`, `ChipButton`, `SubChipButton` y `StepButton`.

### Diferidos de D6.7 y E3.3.1

- **Orden de guardado en el puesto:** `ApiClient.SendAsync` prepara la orden, la guarda con `store.Save` y **solo después** la deja como pendiente. Si el disco falla, la excepción sale sin pendiente: no se envía nada, ni ahora ni con «Reintentar». Antes la orden quedaba en memoria sin guardar y «Reintentar» la mandaba igual.
- **Textos en castellano** en `Labels.cs`, para los rechazos que salían en crudo y para los nuevos de este corte:
  - `reserved_category`, `nothing_to_do` y `product_ids_required`;
  - `use_add_dish`, `too_many_lines` y `duplicate_consumption`.

  ClientChecks exige una frase para cada uno. En el comandero (`labels.ts`) entran `use_add_dish`, `too_many_lines`, `duplicate_consumption` y `product_unavailable`, sin palabras de caja.
- **La copa con otro código** («Copa 15 cl») no se cambia: necesita una decisión del promotor (última sección).

## Decisiones y diferencias con la maqueta, para revisar

**Motor:**
1. «Lo que lleva la mesa» escribe el **nombre actual** del producto. Si después se renombra en el Catálogo, cambia también en las mesas abiertas; la cuenta conserva el texto congelado. La presentación por defecto no se escribe.
2. Una línea con un **origen que este motor no conoce** se enseña: es mejor verla que esconderla.
3. La misma orden reenviada por otro actor recibe sus identificadores **antes** de mirar la versión, con la versión actual.
4. Una ronda sin líneas da `422 invalid_request` sin índice; una línea vacía (`null`), 422 con su índice.

**Comandero:**

5. Las restricciones siguen arriba también en Bebidas (regla de C3a); la maqueta no las pone ahí. La hoja de la línea las escribe además junto a cada comensal.
6. La ronda sale también en la ficha, encima del botón grande. En el móvil son dos botones verdes, uno encima del otro.
7. «En esta mesa» sale solo en la primera ficha, como en la maqueta.
8. Las familias van de la A a la Z (regla común), no en el orden a mano de la maqueta.
9. La hoja dice «Cantidad», no «¿Cuántas?»: no se sabe el género de cada formato.
10. Las filas salen de 60 en 60, con «Ver más».
11. El catálogo se lee también al montar el tablero, no solo al abrir Bebidas, para que la entrada abra al momento.
12. Un vino con un solo formato no deja un hueco para alinear «Añadir» con la columna de «Botella».

**PC:**

13. La ficha se ensancha a 480 px solo mientras el selector está abierto.
14. La bandeja de la ronda empieza plegada, como en la maqueta.
15. «Para» es un desplegable, con las restricciones escritas de cada comensal.
16. En el selector, las restricciones son una franja compacta sin título. El título está en el aviso del icono y en su nombre accesible.
17. El catálogo se lee entero cada vez que se abre el selector, sin `ETag`, y no se revalida mientras sigue abierto.
18. Una ronda cuya mesa desaparece del tablero se descarta y la barra de estado lo dice. En el comandero se pierde sin aviso.

**Los dos:**

19. Una ronda **sin confirmar** se vacía de la bandeja: ya es la orden pendiente, que se ve en su panel.
20. Caja busca ahora con la regla común. Encuentra todo lo que encontraba antes, y más.

## Qué NO cambia

- **El esquema (v8), `grants.sql`, las copias y el instalador.** Solo cambian las versiones.
- **Sin dinero en el comandero ni en cocina** (ADR-007). La guarda de la PWA (`money-guard.ts`) se aplica a cada respuesta, también al catálogo nuevo y a `consumptions`. Ninguna clave nueva lleva vocabulario de caja, y `pwa_http.py` lo comprueba con la misma regla. En el comandero no se escribe «precio», «cuenta», «pago», «saldo» ni «€». **El selector del PC tampoco pinta precios, ni para el responsable.**
- **Los botones solo existen por lo que anuncia el servidor:** Bebidas y «Añadir consumición», por la acción de sesión `add-consumptions`.
- **Una sola orden incierta durable** por cliente, con los bloqueos de D6.7. La ronda es un borrador en memoria: **nunca una cola** y nunca se guarda antes de enviarse.
- **F01:** la ronda se envía con la versión de la mesa leída en ese momento.
- **Nada se borra en el servidor.** Anular sigue en Caja y deja la línea marcada.
- **Toda escritura**, auditoría y outbox en la misma transacción, todo o nada.
- **Nombres, nunca códigos; lo desconocido, en crudo;** castellano con tildes; solo tema claro.
- **El service worker** sigue sin tocar `/api/`. Las fotos se piden con `no-store` y solo viven en memoria.
- **Envío a estaciones:** ninguno. Una bebida es una línea de la cuenta, sin pase ni pantalla de barra.
- **Caja** sigue cobrando, anulando y cerrando, con su «Añadir consumo» con precio.

## Comprobaciones automáticas

- **vitest:** 302 pruebas en 16 ficheros (antes 198 en 12). `npm run build` (`vue-tsc` y Vite) en verde. Las nuevas:
  - `drinks.test.ts`: la tabla común (plegado, agrupación, familias, «Por copa», «En esta mesa», 22 búsquedas y 12 rondas guionizadas) y que la ronda no se guarda en ningún sitio;
  - `bodega.test.ts`: «Por copa» y las familias, los botones de cada fila, el resultado de cada envío, la línea rechazada, la hoja con el punto Y la restricción escrita, y ninguna palabra de caja;
  - `catalog.test.ts`:
    - se lee al montar y se revalida al abrir Bebidas y cada 5 minutos; un 304 conserva la misma lista;
    - dos lecturas a la vez son una, y sin la ronda anunciada no se lee nada;
    - un fallo conserva lo leído; un 401 avisa; un cuerpo con dinero se rechaza;
    - se relee al volver el tiempo real;
  - `photos.test.ts`:
    - con el token y sin la caché del navegador;
    - una petición por foto;
    - el tope de memoria, con su URL liberada;
    - los tipos admitidos, los fallos pasajeros que se reintentan y las iniciales;
  - casos nuevos en `commands.test.ts` (el `line` de un 422), `operations.test.ts` (aplicada con dinero en la respuesta = confirmada) y `core.test.ts` (`readCatalog`: `If-None-Match`, el 304 y cada 200 por la guarda de dinero);
  - `views.test.ts`: un solo `<img>`, en `DrinkPhoto.vue` y con `src` enlazado.
- **ClientChecks:** 263 (antes 201). La tabla común, la búsqueda de Caja, `Round` sin almacén, la orden que no se pudo guardar nunca se envía, los rechazos en castellano y `ApiError.Line`.
- **Dominio (D0):** 86 (antes 83):
  - el identificador derivado es estable y la cuenta no lo admite dos veces;
  - la línea guarda comensal, origen y hora, y una cuenta anterior se restaura sin ellos;
  - el aviso es operativo y no lleva datos.
- **HTTP**, las 14 suites en verde:
  - `checkout_http`, 8 (antes 4): `test_05` (ronda todo o nada, sin dinero, 422 con su índice, misma clave = misma respuesta), `test_06` y `test_06b` (dos actores, un solo cargo) y `test_07` (texto sin «Unidad», aviso desde la mesa y desde Caja, auditado con su clave);
  - `pwa_http`, 7 (antes 5): `test_03` (CSP con `blob:` y sin `data:`), `test_06` («Lo que lleva la mesa» sin dinero, sin menú ni platos, con lo de Caja y «anulado») y `test_07` (líneas anteriores a C3b, clasificadas por su producto);
  - `affordances_http`, 6 (antes 5): la ronda es acción de sesión de principal y sala, y cocina recibe 403;
  - `desktop_reads`: las acciones de sesión por rol, con `add-consumptions`.
- **RealtimeChecks:** 13 (antes 12). B9: un comandero relee tras la ronda de otro y tras una anulación en Caja. A2 fija que el aviso nuevo es operativo.
- **Playwright** contra el motor real: 14 (antes 12), 7 de escritorio y 7 de móvil. Nuevo `e2e/drinks.spec.ts`, en la Mesa 1:
  - el PC crea por la API un albariño con copa, botella y una foto PNG real;
  - buscar «albar» → «Copa» dos veces → «Añadir» un agua → Comensal 2 desde la hoja → «Enviar»;
  - **una respuesta perdida de verdad** (`route.fetch()` y `abort()`) deja exactamente las líneas de la ronda, contadas en la cuenta (la versión de la mesa no sube), con una sola clave en todos los intentos. Entretanto la ronda se llena pero no se envía, y tras recargar la orden sale sola;
  - el otro comandero ve la ronda en vivo;
  - `version_conflict` conserva la ronda y «enviar de nuevo» usa otra clave;
  - una anulación en Caja sale como «Anulado en Caja» en los dos comanderos;
  - salir con la ronda sin enviar pregunta;
  - la foto se pinta desde una URL `blob:` con **cero** violaciones de CSP, y no aparece ninguna clave ni palabra de dinero.

  `commands.spec.ts` usa ahora la ronda en lugar del desplegable de antes.
- **DesktopChecks:** en verde, con un apartado C3b nuevo en la muestra del Comedor. El `FakeServer` sirve `GET /catalog` y `add-consumptions` como el motor: idempotente por clave, 409 con otra versión, líneas en `consumptions` y respuestas perdidas a propósito. Comprueba:
  - sin la acción anunciada, no hay botón; con ella, se habilita sin pedir la contraseña;
  - solo se lee `GET /catalog`, nunca `checkout/*`;
  - el selector en la ficha a 480 px, con familias, filas, «En esta mesa», fotos e iniciales;
  - la búsqueda «albar»: dos copas y «Añadir», con un borrador que no se guarda ni se envía;
  - «Para» con el comensal 3 en el desplegable real;
  - **exactamente un** POST con el cuerpo exacto;
  - el conflicto, que se envía de nuevo con otra clave;
  - la respuesta perdida, con un solo efecto;
  - la confirmación al salir;
  - las anulaciones de Caja;
  - el responsable, sin precios;
  - nada de importes ni códigos pintados, sin desplazamientos anidados ni texto cortado, y que todo cabe a 1280 × 860.
- **Batería completa** (`COSTINA_E2E=1 sh tools/agent/local-tests.sh`, puerto 5090, sin administrador): compilación con 0 advertencias. Cifras por suite:
  - dominio 86/86 y ClientChecks 263;
  - packaging 14 (1 saltada), native 11, security 6/6, desktop_reads 9, affordances 6, restrictions 6, checkout 8, identity 10, pairing 14, station 8, organization 5, catalog 10, offers 7 y pwa_http 7 (con Playwright 14);
  - Realtime 13/13.

### En rojo antes de su arreglo

Cada prueba nueva falló primero. Cada rotura fue temporal y se restauró.
- **Motor:**
  - D0 no compilaba contra el código anterior.
  - Con el motor de C3a, sobre una base nueva, fallaban:
    - `desktop_reads`;
    - `affordances test_06`;
    - `checkout test_02, 05, 06 y 07`;
    - `pwa_http test_03, 06 y 07`;
    - Realtime B9.

    La ronda daba 422 por el campo desconocido `lines`, la CSP no tenía `blob:` y el tablero no traía `consumptions`.
  - `checkout test_06b` daba dos identificadores distintos: dos cargos.
  - Con identificadores al azar en lugar de derivados, `checkout test_06` falló con «3 != 5 : the same order charged twice».
- **Tabla común:** cambiar tres casos de `drinks-cases.json` (el orden de «albar», una tilde del detalle y el resumen de la ronda) hizo fallar esos 3 en vitest y los mismos 3 en ClientChecks.
- **Diferidos:** con `ApiClient.cs` y `Labels.cs` de antes, ClientChecks falló en «a command the store could not save is never sent, kept or retried» y en «server rejections in clear Spanish».
- **Caja:** con `CheckoutViewModel.cs` de antes, DesktopChecks falló en «Caja ranks like the table picker».
- **Comandero:**
  - con el índice de línea quitado de `commands.ts`, el resultado de dinero de `operations.ts` como antes y sin la comprobación de la orden pendiente en `bodega.settlementOf`, fallaron exactamente las tres pruebas nuevas (3 de 300);
  - con una ronda sin confirmar tratada como «no enviada», `drinks.spec` falló en los dos proyectos justo tras la respuesta perdida, porque la ronda no se vaciaba.

  La E2E encontró además un fallo de la propia prueba: su expresión de precios tomaba por precio «Botella 0.75 L». Ahora ignora las unidades de volumen.
- **Puesto:**
  - sin leer `line` en `ApiError`, fallaron los dos casos que la traen;
  - en DesktopChecks, cada rotura falló en su comprobación:
    - el botón sin mirar la acción anunciada;
    - abrir pidiendo la contraseña;
    - leer también `checkout/catalog`;
    - enviar con la versión + 1;
    - un `version_conflict` tratado como confirmado;
    - enviar con una orden sin confirmar;
    - dejar en la bandeja la ronda sin confirmar;
    - cambiar de mesa sin preguntar;
    - perder el «anulado».

## Capturas

Las del comandero las hace `e2e/captures-c3b.spec.ts` contra el motor real, con una bodega parecida a la de la maqueta montada por la API: familias bajo «Bodega y bebidas», copas, botellas y fotos dibujadas en la prueba. La Mesa 4 tiene 4 personas, dos restricciones y lo que lleva.

**Comandero en un móvil (375 × 812):**
- [Bebidas](images/c3b/bebidas-375x812.png): «Por copa», familias, «En esta mesa» y la ronda abajo.
- [Búsqueda «albar»](images/c3b/busqueda-375x812.png): 2 resultados, «Copa» y «Botella 0.75 L» o «Añadir», «1 en la ronda».
- [La hoja de una línea](images/c3b/ronda-375x812.png): cantidad y para quién, con las restricciones.
- [La ficha con la ronda sin enviar](images/c3b/ficha-ronda-375x812.png)
- [Lo que lleva la mesa](images/c3b/lo-que-lleva-375x812.png), con «Anulado en Caja».

**iPad en vertical (820 × 1180):**
- [Bebidas](images/c3b/bebidas-820x1180.png), con las familias en columna;
- [Búsqueda](images/c3b/busqueda-820x1180.png);
- [La hoja de una línea](images/c3b/ronda-820x1180.png);
- [La ficha con la ronda](images/c3b/ficha-ronda-820x1180.png);
- [Lo que lleva la mesa](images/c3b/lo-que-lleva-820x1180.png), a dos columnas.

**Puesto principal**, de DesktopChecks, en el nivel Puesto:
- [«Añadir consumición»](images/c3b/comedor-consumicion.png): el selector abierto en la ficha de la Mesa 4, la búsqueda «albar» y una ronda sin enviar.
- [Lo que lleva la mesa](images/c3b/comedor-lo-que-lleva.png): el selector cerrado y la ronda ya confirmada, junto a dos líneas «Anulado en Caja».

**Para repetir las del comandero** hacen falta una base nueva y el motor con el build actual de la PWA:
1. `npm run build` en `frontend/pwa`.
2. La batería entera, o `dotnet build` de `src/Costina.Server`.
3. `C3B_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3b)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http`.

En la CI no corre nunca. Las del puesto salen de `sh tools/agent/local-tests.sh desktop`, en `dotnet/artifacts/desktop/`.

## Guion manual (solo en un PC de laboratorio)

**Nunca en un PC con una instalación real**, ni sobre sus servicios, puertos o carpetas. Hace falta un PC de laboratorio con una instalación de prueba en `0.42.0-c3b`: la demostración (`/DEMO=1`, D5.6) o el laboratorio de `docs/native/lab-engineering.md`. Además, un móvil en su Wi-Fi, emparejado como comandero de sala (D6.5).

La demostración trae «Vino de ensayo» (Copa y Botella) y «Agua mineral» (solo Botella). Para probar la búsqueda de verdad, se puede importar en esa misma instalación de prueba la bodega de la tienda con sus fotos (E3.1 y E3.2).

1. **Entrada.** En el móvil, abre una mesa con 4 personas y anota «Comensal 2 · Alergia · frutos secos · Grave». En la ficha, toca «Bodega y bebidas». Sale arriba la alergia; debajo, el buscador, «Por copa» y las familias. Ningún precio.
2. **Buscar.** Escribe «e»: «Escribe una letra más para buscar.». Escribe «ensa»: sale «Vino de ensayo» con «Copa» y «Botella». Prueba también «ENSÁ», con mayúsculas y tilde: lo mismo.
3. **La ronda.** Toca «Copa» dos veces y, buscando «agua», «Añadir». Abajo: «Ronda · 3 sin enviar». Despliégala, toca «Para» en la copa y elige el comensal 2: el 2 lleva un punto rojo y la hoja escribe la alergia. Toca «Hecho».
4. **Enviar.** Toca «Enviar». Sale «Ronda enviada…». En la ficha, «Lo que lleva la mesa» tiene dos líneas, con su hora:
   - «2× Vino de ensayo», con «Copa · Comensal 2» debajo;
   - «1× Agua mineral», con «Botella · Toda la mesa».

   En el PC, Caja de esa mesa tiene las dos líneas con su importe.
5. **Repetir.** Vuelve a «Bodega y bebidas»: «En esta mesa» ofrece las dos con «+1». Toca el agua y «Enviar».
6. **Sin Wi-Fi.** Toca «Copa», quita la Wi-Fi y toca «Enviar». Sale «ORDEN SIN CONFIRMAR» con «Ronda para Mesa N: 1 × Vino de ensayo · Copa», y la bandeja queda vacía. Toca «Copa» otra vez: entra en la ronda, pero «Enviar» no se puede tocar. Devuelve la Wi-Fi: la orden se resuelve sola. En Caja, esa copa está **una sola vez**. Envía después la copa que quedó en la ronda.
7. **Anular en Caja.** En el PC, como responsable, anula en Caja una de las líneas. En el móvil, sin tocar nada, sale tachada con «Anulado en Caja».
8. **Salir sin enviar.** Toca «Copa» y vuelve a Mesas: pregunta «¿Salir sin enviar la ronda?». Toca «Salir sin enviar»: en Caja no aparece.
9. **En el PC, nivel Puesto** (sin entrar como responsable). En Comedor, elige la mesa: la ficha tiene «Lo que lleva la mesa» con lo de antes y «Añadir consumición». Tócalo: **no pide contraseña**. Busca «ensa», toca «Copa» y «Enviar». En el móvil, la copa aparece sola en «Lo que lleva la mesa».
10. **Cambiar de mesa con una ronda.** En el PC, abre otra vez «Añadir consumición», toca «Copa» y elige otra mesa: pregunta «¿Salir sin enviar la ronda?». «Seguir con la ronda» la conserva.
11. **El responsable, sin precios.** Entra como responsable y abre «Añadir consumición»: tampoco hay precios. Los importes solo están en Caja.
12. **Caja.** En Caja, el buscador encuentra «ensayo vino», con las palabras al revés.
13. **Repaso.** En ninguna pantalla del móvil aparecen «precio», «cuenta», «pago», «saldo» ni «€».

| Paso | Móvil | PC | Notas |
|---|---|---|---|
| 1 a 13 | | | |

## Límites y diferido

- **Una ronda se pierde sin aviso** en el comandero si su mesa desaparece mientras está sin enviar (liberada o cerrada en Caja): el tablero vuelve a Mesas. En el PC, la barra de estado lo dice.
- **Volver atrás mientras la ronda se está enviando** no pregunta. Un rechazo posterior solo se ve en el aviso de arriba, y el borrador ya no está.
- **Una foto se guarda en memoria con la huella que pidió su fila.** Si la foto cambia en el servidor entretanto, la fila enseña la nueva hasta que el catálogo se revalida.
- **En el nivel Puesto del PC, «Ver cuenta» con una ronda sin enviar** pide primero la contraseña del responsable. La confirmación de salida aparece después. Si se entra, la ronda sobrevive al cambio de nivel y se enviaría como responsable. No hay riesgo de doble efecto: en ese momento no había nada pendiente.
- **Desconectar el PC o un 401** pierden la ronda sin preguntar, como cerrar el programa (D6.1).
- **Las iniciales del PC** copian la regla de `photos.ts` sin tabla de casos común.
- **El selector del PC** no usa `ETag`: lee el catálogo entero en cada apertura.
- **Las E2E de la ronda** comparten la Mesa 1 entre los dos proyectos y la dejan libre al terminar: `pwa_http.py` (`test_06` y `test_07`) la necesita después.
- **`version_conflict` y la respuesta perdida en el PC** solo los cubren las pruebas automáticas: a mano no se reproducen con fiabilidad.
- **El riesgo del plan** (sección 8), rondas rechazadas por la cocina, queda para la prueba real con la bodega completa: cuántas veces sale «La mesa ha cambiado» en un servicio.
- **A C3c:**
  - «Carta y menús»: abrir mesa en tres toques con las alergias en la misma hoja, el menú agrupado y la cocina por plato;
  - la mesa abierta con una oferta que ya no está vigente;
  - el «Preguntar al servidor» que el comandero aún ofrece con una orden de otra instalación (D6.7).
- **A E4c:** los combinados (base y refresco a elegir, a precio fijo) y la ficha de opciones de la ronda.
- **A E4g:** la carta por familias y la comanda entera de una vez.
- **Sin fecha** (sección 6 del plan):
  - platos fuera del menú que llegan a cocina, y que `add-consumption`, la orden antigua, rechace los platos de cocina como ya hace la ronda;
  - favoritos y más vendidos;
  - uva y añada como datos;
  - miniaturas en el servidor;
  - pantalla o ticket de barra;
  - el stock.

## Pregunta abierta para el promotor: varios tamaños de copa

Una presentación hecha a mano como **«Copa 15 cl»** (código `copa-15-cl`) hoy **no cuenta como la copa** del vino:
- no sale en «Por copa» ni en «Vinos por copa»;
- marcar ese vino en «Vinos por copa» crea otra «Copa» al lado.

En el comandero y en el PC sale como un formato más, con su nombre.

Antes de cambiarlo hace falta saber:
1. ¿Algún vino se sirve en **dos tamaños de copa** (por ejemplo, 10 cl y 15 cl)?
2. Si es así:
   - ¿cuál pone «Vinos por copa»?
   - ¿cuál sale primero en el comandero?
   - ¿quitar la copa quita las dos?

Mientras no conteste, sigue la regla de E3.3.1: **una sola copa por vino** (`copa`, o `glass` en los datos de ensayo). Ampliar la regla a todo lo que empiece por «copa» queda descartado: marcaría como copa de vino lo que en E4c será un combinado («copa balón»).
