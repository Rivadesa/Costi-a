# Plan C3b: bodega y bebidas en el comandero y «Añadir consumición» en la mesa del puesto

Fecha: 2026-10-21. **Estado: APROBADO** por el promotor el 21-10, con «adelante con c3b»: es el permiso para hacer el corte, subirlo y mezclarlo.

**Entregado el 21-10**: `docs/native/C3b-bodega-y-bebidas.md`, con sus capturas en `docs/native/images/c3b/`. Incluye todo el alcance de abajo. Dos detalles más:
- una ronda sin confirmar sale de la bandeja, porque ya es la orden pendiente durable;
- las E2E de la ronda usan la Mesa 1 en los dos proyectos de Playwright, una detrás de otra.

Veinticuatro decisiones pequeñas quedan para revisar con el promotor (las cuatro últimas, de la revisión del 21-10 antes de subir: «Enviar ronda» en verde claro en la ficha, la bandeja que dice cuándo la ronda está sin confirmar, «Añadir consumición» en la cabecera fija de la ficha del PC y «Por copa» discontinuo en el PC); están en «Decisiones y diferencias con la maqueta» del documento del corte. La pregunta de los varios tamaños de copa sigue abierta. No hay vuelta atrás a `0.41.0-c3a` sin la copia previa: está en «Límites y diferido» del documento del corte.

Es el corte 4 del plan aprobado `docs/plans/2026-10-15-menus-carta-y-bodega.md` (sección 4.4). Aquí solo se baja a pasos. Se aplican tal cual las reglas de las secciones 1 («Consumos desde la mesa en el puesto principal»), 2 («más intuitivo»), 3 (vocabulario), 6 (diferimientos), 9 y 10 (decisiones del promotor). Lleva también lo que D6.7 y E3.3.1 dejaron para antes de C3b (traspasos del 19-10 y del 20-10).

- **Base:** `develop` en `49e03b3` (`0.41.0-c3a`, esquema v8).
- **Rama:** `feat/c3b-bodega-y-bebidas`.
- **Versión:** `0.42.0-c3b`.
- **Sin cambio de esquema.** Todo lo nuevo del motor son campos opcionales y aditivos: las cuentas guardadas antes se leen igual.

Objetivo visual: la maqueta `docs/plans/maquetas/2026-10-15-comandero.html`, pestañas «2 · Bodega y bebidas», «2b · La ronda» y «5 · En el PC» (capturas `bodega.png`, `copa.png` y `pc.png`), con el aspecto del comandero de C3a (`docs/native/C3a-comandero-nuevo.md`, `docs/native/images/c3a/`) y el del Comedor del puesto de C2.

## Alcance

### 1. Motor (solo añade)

- **Ronda en una orden:** `POST /dining/services/{id}/commands/add-consumptions` con `{expectedVersion, lines:[{productId, presentationId?, quantity, guestPosition?}]}`.
  - `lines` es un campo nuevo de `DiningCommand`; la acción se despacha junto a `add-consumption`.
  - Es acción de sesión de sala y principal (`SessionActions`); `Affordances.Allows`, que es también la política de la ruta, la niega a cocina (403).
  - De 1 a 30 líneas y cantidades de 1 a 99. Todo o nada, en una transacción.
  - Una línea mala da **422** con `{error, line}` (índice desde 0). Un producto con estación de cocina o de pase da `use_add_dish` («pídelo como plato»).
  - Respuesta sin dinero: `{version, consumptionIds}`. La versión del servicio no se mueve.
- **Defensa en el servidor contra el doble cargo** (diferido de D6.7):
  - el identificador de cada línea **se deriva de la orden**: servicio + clave de la orden + índice de la línea. Vale para `add-consumptions` y para `add-consumption`;
  - la cuenta nunca admite dos líneas con el mismo identificador. Si la misma clave vuelve con otro actor (la idempotencia del servidor es por actor), la orden ya aplicada responde lo mismo **sin cobrar otra vez**; si esa clave trae otras líneas, se rechaza con `409 duplicate_consumption`, sin aplicar nada.
- **La línea de la cuenta guarda más datos**, todos opcionales: el comensal (`guestPosition`), el origen (`origin`: `consumption`, `dish`, `menu` o `checkout`) y la hora (`at`). Es un dato de sala dentro de la cuenta del núcleo: se anota como decisión en el documento del corte (ADR-012).
- **Texto de la línea sin «Unidad»:** `add-consumption(s)` y `add-product` de Caja quitan la presentación por defecto, como ya hace `add-dish` para cocina. La línea del cobro de `add-dish` no cambia.
- **Lo que lleva la mesa:** cada entrada de `GET /dining/board` añade `consumptions:[{id, productId, presentationId, name, presentation, quantity, guestPosition, at, voided}]`.
  - Una proyección propia, **sin importes**: nunca `ChargeLine`.
  - Las cuentas de todas las mesas del tablero se leen en **una sola consulta**.
  - Entran las líneas con origen `consumption` o `checkout`: bebidas y extras, también los de Caja. Las líneas anteriores al corte, sin origen, se clasifican por su producto: fuera el producto-menú y los platos que tienen su elaboración. Un plato pedido por error como consumo sí se ve.
- **Aviso operativo nuevo** `service.consumptions_changed` (agregado = el servicio), desde `add-consumption(s)` y desde `add-product` y `void-charge` de Caja. No lleva datos y llega a todos los roles, así que una anulación en Caja también llega a los comanderos.
- **Fotos:** la CSP pasa a `img-src 'self' blob:`. Sigue sin `unsafe-inline`, sin terceros y sin `data:`.
- **ADR-007:** apartado nuevo. «Lo que lleva la mesa» (nombre, formato, cantidad, comensal, hora y «Anulado en Caja», sin importes ni totales) **no es la cuenta provisional**.

### 2. Comandero (PWA)

Como la sección 4.4 del plan:
- **«Bodega y bebidas»**, solo con la sección `bodega`: buscador siempre arriba (sin tildes ni mayúsculas; nombre, referencia, familia y región; desde 2 letras), accesos «En esta mesa» y «Por copa», chips de familia con sus subfamilias y una fila por producto con foto, nombre, familia › región y un botón por presentación (Copa primero). Sin precios.
- **Un toque suma 1 a la ronda**, para toda la mesa. La ronda es un **borrador en memoria**: nunca va a IndexedDB, se ve como «Sin enviar», se pierde al recargar (y la pantalla lo dice) y no se envía mientras haya una orden incierta. «Enviar» es **una sola** orden `add-consumptions`.
- **`version_conflict`:** la ronda sigue en la bandeja, la pantalla relee y ofrece «La mesa ha cambiado: enviar de nuevo», con una clave nueva.
- **Lo que lleva la mesa**, con «Anulado en Caja» y el pie «Los importes solo se ven en Caja».
- **Fotos** pedidas con el token solo para las filas visibles, guardadas en memoria por huella, con un recuadro de iniciales si no hay foto.
- **Catálogo al día** con `ETag` (al abrir Bebidas, cada 5 minutos y al reconectar); el 304 conserva la lista anterior y cada cuerpo nuevo pasa por la guarda de dinero.

### 3. Puesto principal (WPF)

- **«Añadir consumición»** en la cabecera de «Lo que lleva la mesa» de la ficha de una mesa abierta del Comedor. Abre dentro de la ficha el mismo selector «Bodega y bebidas» (`Desktop/Views/DrinksPicker.xaml`, con su lógica sin WPF en `Costina.Client` y la misma tabla de casos que el comandero).
  - Sale y se habilita **solo si el servidor anuncia** `add-consumptions`. No pide la contraseña en el nivel Puesto.
  - **Sin precios, también para el responsable.** Lee `GET /catalog`, nunca `/checkout/catalog`.
  - «Enviar» es una sola orden con la versión de la ficha leída (F01). La ronda es un borrador en memoria, nunca una cola; cambiar de mesa o de pestaña con ella pide confirmación.
- **«Lo que lleva la mesa»** en la ficha, con la lista `consumptions` del tablero (`BoardEntry.Consumptions`).
- **Diferido de D6.7:** `ApiClient.SendAsync` asigna `Pending` **solo después** de que `store.Save` haya guardado la orden: nunca se envía lo que no se pudo guardar.
- **Diferido de E3.3.1:** textos en castellano en `Costina.Client/Labels.cs` para `reserved_category`, `nothing_to_do` y `product_ids_required`, con su comprobación en ClientChecks. También los códigos nuevos de este corte: `use_add_dish`, `duplicate_consumption` y `too_many_lines`.
- **Caja no cambia:** sigue cobrando, anulando y cerrando, con su «Añadir consumo» con precio.

## Qué no cambia

- **Sin dinero en el comandero ni en cocina** (ADR-007): ninguna clave nueva que la guarda de la PWA rechace (`price|cents|amount|balance|subtotal|total|paid|payment|refund|charge|credit|tariff`) y ninguna palabra de caja a la vista («precio», «cuenta», «pago», «saldo», «€»).
- **Los botones solo existen por lo que anuncia el servidor.**
- **Una sola orden incierta durable** por cliente, con los bloqueos de D6.7. La ronda nunca se guarda antes de enviarse.
- **Nada se borra en el servidor**; cada escritura deja auditoría y outbox en su transacción.
- **Nombres, nunca códigos**; lo desconocido sale en crudo; castellano con tildes; solo tema claro.
- **El service worker nunca toca `/api/`.**
- **Envío a estaciones:** ninguno. Una bebida es una línea de la cuenta, sin pase ni pantalla de barra.

## Pruebas

Cada prueba nueva falla antes de su arreglo.

- **HTTP** (`pwa_http`, `checkout_http`, `affordances_http`, `desktop_reads`):
  - `add-consumptions` todo o nada, idempotente por clave, 403 para cocina, 422 con el índice de la línea, `use_add_dish` con un plato de la carta;
  - **la misma clave desde dos actores (dos dispositivos de sala) deja un solo cargo**, el escenario de la revisión externa del 19-10; también con `add-consumption`;
  - `consumptions` sin claves de dinero (regla de la PWA), sin la línea del menú, con lo apuntado en Caja y con «anulado»;
  - el aviso llega al canal operativo, también tras `add-product` y `void-charge` de Caja;
  - el texto de la línea va sin «Unidad»;
  - las acciones de sesión por rol, con `add-consumptions`;
  - la CSP con `img-src 'self' blob:` y sin `data:`.
- **RealtimeChecks:** un comandero relee cuando otro añade una ronda y cuando Caja anula un consumo.
- **Dominio (D0):** identificador derivado de la orden; la línea guarda comensal, origen y hora; una cuenta anterior, sin esos campos, se restaura igual.
- **vitest, ClientChecks y DesktopChecks:** como en la sección 4.4 del plan (búsqueda, agrupación, ronda, lectura condicional, ficha del Comedor).
- **Playwright**, en escritorio y en móvil: buscar «albar» → Copa → «Enviar»; un solo efecto tras una respuesta perdida de verdad (se cuentan las líneas, no la versión); la ronda en el otro comandero; la foto sin violaciones de CSP; sin palabras de caja.
- **Batería completa** con `COSTINA_E2E=1` y DesktopChecks.

## Capturas

En `docs/native/images/c3b/`: Bebidas (accesos rápidos, búsqueda y ronda) y lo que lleva la mesa, a 375 × 812 y a 820 × 1180; del puesto, `comedor-consumicion.png` y `comedor-lo-que-lleva.png`.

## Pregunta abierta para el promotor

**Varios tamaños de copa por vino.** Una presentación hecha a mano como «Copa 15 cl» (código `copa-15-cl`) hoy **no cuenta como la copa** del vino: no sale en «Por copa» ni en «Vinos por copa», y marcar ese vino crea una «Copa» al lado. Antes de cambiarlo hace falta saber:
- ¿algún vino se sirve en dos tamaños de copa (por ejemplo, 10 cl y 15 cl)?
- si es así, ¿cuál de las dos pone «Vinos por copa» y cuál sale primero en el comandero, y quitar la copa quita las dos?

Mientras no conteste, en C3b sigue la regla de E3.3.1: una sola copa por vino (`copa`, o la `glass` de los datos de ensayo); un tamaño distinto sale en el comandero como otra presentación más, con su nombre. Ampliar la regla por el prefijo «copa» queda descartado: marcaría como copa de vino lo que en E4c será un combinado («copa balón»).

## Diferido

- **A C3c:** «Carta y menús» (abrir mesa en tres toques con las alergias, menú agrupado, cocina por plato) y la mesa abierta con una oferta que ya no está vigente. También el «Preguntar al servidor» que el comandero aún ofrece con una orden de otra instalación, que no entró en el traspaso del 20-10.
- **A E4c:** los combinados (base y refresco a elegir, a precio fijo) y la ficha de opciones de la ronda.
- **A E4g:** la carta por familias y la comanda entera de una vez.
- **Sin fecha (sección 6 del plan):** platos fuera del menú que llegan a cocina (y que `add-consumption`, la orden antigua, rechace los productos con estación de cocina), favoritos y más vendidos, uva y añada como datos, miniaturas en el servidor, pantalla o ticket de barra, y el stock.
