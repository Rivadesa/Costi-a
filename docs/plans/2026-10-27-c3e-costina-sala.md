# Plan C3e: Costiña Sala, mis mesas y abrir y servir el menú

Fecha: 2026-10-27. **Estado: APROBADO.** Aquí solo se baja a pasos lo que ya está aprobado: la sección 4.4 de `docs/plans/2026-10-22-sala-y-cocina.md` (`plan22:717-768`), aprobada el 22-10 con «Si a todo, la 5, va haber android y apple», con las secciones 5 a 10 y la «Revisión» de ese plan (sobre todo los puntos 20 y 21, `plan22:992-998`), y lo que los cortes anteriores dejaron para C3e (`docs/native/C3b-bodega-y-bebidas.md:464-467`, `docs/native/C3c-dos-apps.md:454`, `docs/native/C3d-costina-cocina.md:324,327`). El objetivo visual es la maqueta aprobada `docs/plans/maquetas/2026-10-22-sala-y-cocina.html`, pestañas «1 · Sala · Mis mesas», «2 · Sala · Mesa», «5 · Sala · aviso» y «6 · Sala · servido», con sus capturas `docs/plans/maquetas/2026-10-22-sala-y-cocina/1-mesas.png`, `2-mesa.png`, `5-aviso.png` y `6-servido.png`. La hoja «Abrir mesa» no está dibujada en esa maqueta (no tiene pestaña, `maq:981-982`; `maq:1000` solo dice que una mesa libre la abre): su modelo es la pestaña «0 · Abrir mesa» de la maqueta del 15-10 (`maq15:789-795`, `docs/plans/maquetas/2026-10-15-comandero/abrir.png`), con las personas de 1 a 8 y «Más…», como dice `plan22:738`.

**Respuestas del promotor que fija este corte:**
1. 22-10 (`plan22:878`): **Sala enseña primero «Mis mesas»** (las que abrió y las de su sala; cada móvil va emparejado a una estación de sala) **y después «Todas»**.
2. 22-10 (`plan22:876-877`): el camarero envía cada pase, recibe el aviso con la banda, vibración en Android y su mesa en verde, sirve y toca **«Servido»**. Con la aplicación cerrada no hay aviso (límite del iPhone aceptado).
3. 15-10, que siguen valiendo (`plan22:886-893`): menú cerrado en Costiña; las alergias las decide cocina antes de enviar (E4e); tema claro con el acento `#0E7A5F` y el rojo solo para las alergias; nombres y no códigos; castellano con tildes; sin importes en los móviles.

**27-10, «Adelante»** (a «¿Te propongo el plan y me das permiso para subirlo y mezclarlo?»): permiso para subir y mezclar C3e cuando la CI esté en verde. El permiso de C3d no valía para C3e (`docs/handoff/2026-10-27-c3d-costina-cocina.md:100`).

**27-10, «también la caché»:** se refiere a la caché de la CI, otra sesión con su propio permiso: ya está entregada y mezclada en el PR #97 (`docs/plans/2026-10-27-ci-cache-playwright.md`), que guarda en `actions/cache` los paquetes del sistema de Playwright (`.github/actions/playwright-chromium/action.yml`) y los calienta en `develop` (`.github/workflows/playwright-apt-cache.yml:5-9`). C3e no toca la CI ni la caché del móvil: solo lee en la CI el tiempo de ese paso.

**Entregado el 27-10** como `0.46.0-c3e`, en los seis pasos de abajo: `2c5b7d4` (este plan), `f787145` (la lógica, con vitest), `018bac1` (las pantallas), `d07e97b` (las pruebas de navegador y `captures-c3e.spec.ts`) y los commits de documentos (con las capturas en PNG), ADR y versión (pasos 5 y 6). Quedan `docs/native/C3e-costina-sala.md` (con las trece capturas de `docs/native/images/c3e/`, las decisiones para revisar, las pruebas en rojo y el guion físico corto), `docs/ADR/ADR-018-mis-mesas-por-dispositivo.md` y el traspaso `docs/handoff/2026-10-27-c3e-costina-sala.md`. Concreciones, todas en «Decisiones tomadas al construir» del documento del corte: el menú propuesto puede ser un menú con elección, como el PC; la banda deja fuera la mesa cuya ficha está a la vista, así que en `mesas.spec` A vuelve a «Mis mesas» para ver la banda; abrir con una alergia lleva más de tres toques (tres sin ella); en la hoja de alergias, un pase ya servido dice «Servido a las …»; trece capturas en lugar de diez; y `playwright.config.ts` ya no tiene dirección por defecto sin `E2E_BASE`.

- **Base:** `develop` en `d6d7315` (`0.45.0-c3d`, esquema v8, con el PR #97 de la caché de la CI encima de `31a1fac`).
- **Rama:** `feat/c3e-costina-sala`.
- **Versión:** `0.46.0-c3e`.
- **Sin cambio de esquema, de motor, de API, de CSP ni de la CI.** Cambian la PWA (Costiña Sala y lo que comparte con Costiña Cocina: `StatusPanel` y el `CommandRunner` de `pwa/commands.ts` son comunes, así que la orden de otra instalación, su aviso de «no consta» y los textos nuevos de rechazo valen también en Cocina, `pwa/views/KitchenView.vue:56,176`; de `pwa/cocina.ts`, solo un parámetro aditivo), sus pruebas, las capturas y la versión. El servidor ya da todo lo que hace falta: quién abrió cada mesa y desde qué estación (`openedBy` y `openedStation`, `Dining.Domain/ServiceModels.cs:51-56`, puestos al abrir en `Dining/DiningOperations.cs:48`), el actor y la estación del móvil en `/session` (`Server/DesktopReadRoutes.cs:26-30`; el texto `device:<nombre>` se forma en `Server/Program.cs:372-373`) y las alergias en la misma orden `open` (`DiningOperations.cs:10,12,49`). Los huecos que encontró la lectura del código se resuelven sin tocar el motor y quedan escritos en «Diferido» y en las decisiones del final.
- **ADR nuevo:** `docs/ADR/ADR-018-mis-mesas-por-dispositivo.md`, el siguiente número libre (ADR-017 es el último; ADR-010 nunca existió). Dice que **«Mis mesas» por dispositivo y estación es presentación, no atribución a una persona** (`plan22:178-179,365`; `dotnet/AGENTS.md:103`).

Rutas cortas: `pwa/` = `frontend/pwa/src/`; `e2e/`, `tests/` y `public/` son las de `frontend/pwa/`; `Dining/` = `dotnet/src/Costina.Modules.Dining/`; `Dining.Domain/` = `dotnet/src/Costina.Modules.Dining.Domain/`; `Server/`, `Client/`, `Core.Domain/` y `Core.Persistence/` = `dotnet/src/Costina.<nombre>/`; `plan22:` = `docs/plans/2026-10-22-sala-y-cocina.md`; `plan15:` = `docs/plans/2026-10-15-menus-carta-y-bodega.md`; `maq:` = `docs/plans/maquetas/2026-10-22-sala-y-cocina.html`; `maq15:` = `docs/plans/maquetas/2026-10-15-comandero.html`. Las referencias `fichero:línea` son de `d6d7315`. Fuera de `.github/` y de la línea 65 del traspaso de C3d, son las mismas que en `31a1fac`.

## Alcance (un commit por paso)

### 1. `docs(plans)`: este plan

Solo este fichero. Ningún otro cambio.

### 2. `feat(pwa)`: lógica sin DOM, con vitest

Todo es **presentación** de la lectura autoritativa del tablero: nada habilita nada. Un botón existe solo porque el servidor anuncia su acción, ya filtrada por rol y estación (D3.1, E4e). Los textos van con tildes y sin palabras de caja, también dentro de los ficheros nuevos (una prueba por fichero, como `tests/cocina.test.ts:395-409`).

**«Mis mesas»**
- `pwa/types.ts:17-21`: `Dining` gana `openedBy?: string | null` y `openedStation?: string | null`. Es aditivo: el servidor los omite cuando no los sabe, y la guarda de dinero no los toca (`pwa/money-guard.ts:4`).
- `pwa/sala.ts`, **`isMine(service, who)`**, con un tipo propio `{ actor: string; station: string | null }` como `Who` en `pwa/apps.ts:49`. No importa `Session`: `tests/sala.test.ts:176` fija que `sala.ts` solo importa `./ficha`, `./labels` y `./types`. Una mesa es «mía» si:
  - `openedBy` es igual al `actor` de `/session` («device:Móvil Ana»). Se compara con el texto que da el servidor (`Server/Program.cs:372-373`), nunca con uno rehecho con el nombre guardado en el móvil;
  - **o** `openedStation` no está vacío y es igual a `station` (el código de la estación, que no cambia al renombrarla): los otros móviles de «Sala 1», y el PC si su estación es «Sala 1»;
  - **o** no se sabe quién la abrió: sin `openedBy` (abierta antes de E4e) o sin `openedStation` (abierta por una persona en el PC, que no tiene estación). Mejor avisar de más que de menos (`plan22:725`).
- `sala.ts`, **`mineEntries(entries, who)`**: lo que lee la banda. El comentario de `sala.ts:16` pasa a decir que, desde C3e, la banda solo avisa de «Mis mesas».
- **Sin `rescope` en `ReadyWatch`:** el actor y la estación de una sesión no cambian mientras dure la credencial. El motor no tiene ninguna ruta que cambie el nombre o la estación de un dispositivo emparejado (`Server/Program.cs:510-526`: listar, desvincularse y revocar); el único `UPDATE` de `core.devices` es la revocación (`Core.Persistence/DeviceStore.cs:222`). Volver a emparejar pasa por «unpaired» y monta otra vez `BoardView` con un `ReadyWatch` nuevo (`pwa/views/Shell.vue:138,150`).
- `pwa/mesas.ts` (nuevo) ordena lo que ya da `floorRooms` (`pwa/configuration.ts:57-74`), que no cambia, igual que su prueba (`tests/configuration.test.ts:45-70`):
  - **`mineSections(rooms, who)`**: las ocupadas «mías», agrupadas por la sala de cada mesa, en el orden de la configuración (`Core.Persistence/OrganizationUnit.cs:106-109`). Dentro de cada sala, primero las «Listo para servir» (el mismo `tileState` que pinta la tarjeta en verde, `pwa/floor.ts:28`) y después las demás, por el orden de las mesas. Una mesa ocupada que la configuración ya no lista («Otras mesas ocupadas», `configuration.ts:53`) va al final de las mías, si es mía;
  - **`freeSections(rooms)`**: después, **todas** las libres, agrupadas por la sala de cada mesa. No hay «mi sala» ni «otras salas»: falta unir estación y sala, que es esquema (`plan22:729-730`, `Core.Domain/Organization.cs:11`);
  - los textos de la maqueta: «7 abiertas · 19 personas» (`mineSummary`), «Sala 1 · 6 abiertas» en cada sección, «Mesas libres · 3 libres», y la ayuda **`mineHint(stationName)`** (`maq:691`): «Las que abriste tú y las abiertas desde los otros móviles de Sala 1. Arriba, las listas para servir; las libres, al final.» Sin nombre de estación: «Las que abriste tú y las abiertas desde los otros móviles de tu estación. Arriba, las listas para servir; las libres, al final.»;
  - «Todas» es `floorRooms` tal cual, con el resumen de hoy (`floorSummary`, `configuration.ts:77-83`).
- **vitest** (`tests/mesas.test.ts` y `tests/sala.test.ts`): la regla de «mía» (propia, de la misma estación, sin `openedBy`, sin `openedStation`, de otra estación, un móvil con `station` nula); el orden (listas arriba dentro de su sala, libres al final por sala, la mesa que ya no está en la configuración); la banda filtrada (un pase listo de una mesa de otra estación no sale ni vibra, y «Visto» se poda igual).

**Abrir mesa** (`pwa/abrir.ts`, nuevo)
- **`paxChoices(capacity)`**: botones de 1 a 8 sin pasar de la capacidad, y «Más…» solo si la mesa admite más de 8 (o no se sabe su capacidad). **`paxLimit(capacity)`**: la capacidad, o 40 si no se sabe. Hoy se aceptan de 1 a 40 sin mirar la capacidad (`pwa/views/OpenTable.vue:31,36`), y el servidor rechaza con 422 lo que pasa de ella (`DiningOperations.cs:39`). Fuera de límite: «Esta mesa admite hasta 12 personas.».
- **`offerGroups(offers)`**: la propuesta, que es la primera vigente (`pwa/offers.ts:5-7`, «Menú del día»); las demás detrás de «Otro menú…»; y los menús con elección (`set-menu`) solo dentro de «Más opciones» de la hoja (`plan15:671`, `maq15:792`). En el laboratorio, el menú del día es `LAB-TASTING` («Menú de ensayo»), no `LAB-DAILY` (`e2e/commands.spec.ts:24`).
- **`RestrictionDraft`**: cada fila de «¿Alergias o intolerancias?» es un borrador **en memoria**, como la ronda de Bodega y como el PC (`Costina.Desktop/ViewModels/ServiceViewModel.cs:510`). Lleva quién (sin valor por defecto: «Toda la mesa» es una opción más), tipo (Alergia por defecto), a qué y gravedad (Grave por defecto). `KINDS` y `SEVERITIES` salen de `pwa/views/TableCard.vue:131-132` a este módulo, y los usan la hoja de abrir y la de alergias.
- **`draftProblem(drafts, pax)`** comprueba solo el formato, con los textos del PC (`ServiceViewModel.cs:502-505`): «Elige para quién es cada alergia o intolerancia: un comensal o toda la mesa.», «Escribe a qué es cada alergia o intolerancia (2 caracteres o más).» y «El comensal de una alergia tiene que estar entre 1 y 3.». Añade uno que adelanta el `duplicate_restriction` del servidor (`Dining.Domain/Restrictions.cs:44`): «Esa alergia o intolerancia ya está anotada para ese comensal.». Una fila vacía del todo no cuenta, como en el PC (`ServiceViewModel.cs:499`).
- **`openRequest(table, pax, offer, drafts)`**: **una** orden `POST /dining/services` con `{ tableId, pax, offerId, menuId, restrictions }` (E4e). Sin filas, el cuerpo de hoy (`pwa/views/BoardView.vue:114`), que también entiende un servidor anterior. La descripción de la orden pendiente: «Abrir Mesa 5 para 3 personas con 1 alergia o intolerancia».
- **`openSummary(...)`**, la línea encima de «Abrir»: «3 personas · Menú de ensayo · 1 alergia o intolerancia».
- **vitest** (`tests/abrir.test.ts`): botones y «Más…» según la capacidad, grupos de ofertas, cada problema de formato, el cuerpo con y sin filas, la descripción y el resumen.

**La ficha**
- `pwa/ficha.ts:18`: `BUTTON` gana `serve: 'Servido'`, y el botón grande dice **«Servido · Principal»** (`plan22:747`). La orden pendiente sigue diciendo «Marcar servido · Principal · Mesa 4» (`ficha.ts:89`; `action.serve` en `pwa/labels.ts:43`), igual que el PC. `tests/ficha.test.ts:64` pasa a «Servido · Entrantes».
- `ficha.ts`, **`barNote(service, primary)`**: la línea encima del botón grande (`maq:743-746`), sin depender del género del pase, porque su nombre ya va en el botón:
  - con «Enviar siguiente pase», antes de servir nada: «Cada estación lo recibe en su tableta al momento.»;
  - con «Servido»: «Listo en el pase desde las 21:19.»;
  - con «Enviar siguiente pase», después de servir: «Último pase servido a las 21:21.».
- `pwa/pases.ts` (nuevo): los pases de la ficha, como la maqueta (`maq:723-735`):
  - los servidos y los omitidos, recogidos en una línea: «✓ Aperitivos 20:41», «Postre omitido»;
  - cada pase sin terminar, con sus **platos agrupados por la regla común** de `pwa/cocina.ts:53-72` (`dishes`, `isException`, `lineRestrictions`): «Merluza á galega × 4 (1 adaptada)» (`dishText`, `dishNote`), su estado (`dishState`) y la estación por su nombre;
  - las **excepciones aparte**, bajo su plato y nunca atenuadas (`exceptionLine`, `cocina.ts:110-119`), en rojo solo si alarman: «Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra». Mientras cocina no decide, «Por revisar en cocina», que es la palabra del camarero (`labels.ts:105`, `plan22:319`). `exceptionLine` gana un segundo parámetro con ese texto. Es aditivo: Costiña Cocina sigue con «Por revisar en el pase» (`cocina.ts:98`);
  - las restricciones de toda la mesa no se repiten en cada línea: ya están en la franja roja de arriba (`wholeTable`, `cocina.ts:122-128`);
  - la nota de cada pase: «Revisado por cocina» o «Por revisar en cocina»; «En el pase desde las 21:19» si está listo; y la marca «Siguiente» en el que enviaría `fire-next`.
  - Sustituye la regla propia de Sala (`TableCard.vue:194-199`), que contaba como excepción un «No le afecta» y una restricción de toda la mesa sin decisión, la repetía en cada línea (`:284`) y no agrupaba nada (`docs/native/C3d-costina-cocina.md:324`).
- `pases.ts`, **`chooseRequest`** y **`addDishRequest`**: una orden `choose` o `add-dish` por toque, con la versión leída (F01), hasta E4g (`plan15:678-679`).
- `pases.ts`, **`allergyList(service)`**: para la hoja de alergias, cada restricción (las de toda la mesa primero) con los platos a los que afecta y su estado: «Aperitivos · Ostra: Adaptado: sin almendra», «Segundo pase · Merluza á galega: Por revisar en cocina» (`plan15:680`).
- **vitest** (`tests/pases.test.ts`): lee la tabla común `dotnet/tests/fixtures/kitchen-cases.json`, como `tests/cocina.test.ts:17`. La ficha de Sala agrupa y aparta igual que Costiña Cocina y que el PC (`Client/DishGroups.cs`): **romper un caso rompe las tres pruebas**. Además: la línea de servidos, «Siguiente», las notas, «Por revisar en cocina», `allergyList` y las órdenes de elegir y pedir.

**Órdenes y avisos**
- `pwa/commands.ts:63-80`, `REJECTIONS` gana estos textos (hoy caen en «El servidor rechazó la orden (código)», `commands.ts:223`). `REJECTIONS` es común: también los verá Costiña Cocina, si le llegan. Los del PC, salvo `duplicate_restriction`, que el PC no rotula, están en `Client/Labels.cs:89,129,135,151-152,155-156`:
  - `table_occupied`: «Esa mesa ya está ocupada: otro puesto la abrió antes. No se ha abierto otra vez.»;
  - `offer_unavailable`: «Ese menú o carta ya no se ofrece ahora. Se ha vuelto a leer la lista: elige otro.»;
  - `invalid_guest`: «Ese comensal no está en la mesa. No se ha aplicado nada.»;
  - `duplicate_restriction`: «Esa alergia o intolerancia ya estaba anotada para ese comensal. No se ha aplicado nada.»;
  - `choice_missing`: «Falta que algún comensal elija plato: elígelo en «Carta y menús».»;
  - `course_empty`: «Ese pase no tiene platos pedidos: pide alguno en «Carta y menús» u omítelo.»;
  - `course_not_pending`: «Ese pase ya se envió a cocina.»;
  - `course_not_ready`: «Ese pase aún no está listo.»;
  - `offer_unpriced` (409, `Dining/DiningOperations.cs:65,69`): «Ese menú aún no se puede abrir: avísalo en el PC principal. No se ha abierto la mesa.». No se copia el del PC (`Client/Labels.cs:131`), que dice «precio» (`e2e/support.ts:15`);
  - `table_inactive` (409, `Core.Persistence/OrganizationUnit.cs:36`): «Esa mesa ya no está activa: se ha quitado en el PC principal. No se ha abierto.»;
  - `invalid_request` (422, `Server/Program.cs:393-396`; al abrir, más personas de las que caben, `Dining/DiningOperations.cs:39`): «El servidor no admite esos datos: vuelve a leer la mesa y revísalos. No se ha aplicado nada.».
  - **vitest** en `tests/commands.test.ts`: cada código nuevo tiene su texto, sin palabras de caja (como `tests/commands.test.ts:124`).
- **«Preguntar al servidor» con una orden de otra instalación** (arrastrado de C3b: `docs/native/C3b-bodega-y-bebidas.md:467`; `dotnet/AGENTS.md:99`; `plan22:753`). Hoy el panel no dice qué orden era, y la respuesta «Al servidor no le consta esa orden.» (`commands.ts:156`) invita a descartarla y repetirla. Vale también para Costiña Cocina: monta el mismo `StatusPanel` («Cabecera comun de comandero y cocina», `pwa/views/StatusPanel.vue:7`) y el mismo runner (`useOperations`, `pwa/views/KitchenView.vue:56,176`). Se hace sin prop que lo apague: el aviso honesto vale igual en una tableta.
  - `BlockedCommand` `foreign` guarda también su descripción (`commands.ts:37,99`), que el panel enseña como ya hace con `actor`;
  - `foreignAdvice()` en `labels.ts`, junto a `otherActorAdvice` (`:141`): «Se hizo con otra instalación del servidor (reinstalado o restaurado en otro equipo). Este servidor solo puede decir si a él le consta. Antes de descartarla, comprueba en el PC principal si se aplicó.»;
  - se sigue pudiendo preguntar, porque un servidor restaurado puede tener la clave. Pero, para `foreign`, `reconcile()` (`commands.ts:143-159`) dice «A este servidor no le consta esa orden. Eso no dice nada de la instalación donde se hizo: compruébalo en el PC principal antes de descartarla.»;
  - **vitest** en `tests/commands.test.ts`: la descripción se conserva y el aviso de «no consta» es el de otra instalación. Hoy solo hay una prueba de que no se reenvía (`tests/commands.test.ts:175-179`), y su `toEqual` de `:178` pasa a llevar la descripción.

### 3. `feat(pwa)`: las pantallas de Costiña Sala

Tema claro con los colores de la maqueta (`maq:13-25`): el acento `#0E7A5F` en los botones, el verde de «listo» (`--okText`, `--okSoft`), el ámbar para lo pendiente y el rojo solo para las alergias que alarman. Objetivos táctiles de 48 px (`e2e/support.ts:73`) y nada en línea (CSP, `tests/views.test.ts`). Se conservan los `data-testid` de hoy, salvo los que se dicen abajo.

**«Mis mesas» y «Todas»** (`pwa/views/BoardView.vue`)
- `const scope = ref<'mine' | 'all'>('mine')`, junto a la sala elegida (`BoardView.vue:58`; `plan22:732` cita `:55`, que ya es `view`). Vive en memoria: ir a una ficha y volver la conserva, y al abrir la aplicación sale «Mis mesas».
- Un segmento `role="tablist"` con «Mis mesas» y «Todas» (`maq:715`), botones de 48 px con `aria-selected`: `data-testid="board-scope-mine"` y `board-scope-all`.
- **«Mis mesas»** (`pwa/views/MineList.vue`, nueva):
  - título «Mis mesas» con su resumen (`mine-summary`) y la ayuda (`mine-hint`);
  - una sección por sala (`mine-room-<sala>`) y, al final, «Mesas libres» (`mine-free`), con un subtítulo por sala (`free-room-<sala>`);
  - sin pestañas de sala;
  - sin mesas mías: «Ahora no tienes ninguna mesa abierta. Las de las otras estaciones están en «Todas».» (`mine-empty`), y debajo, las libres.
- **«Todas»**: título «Todas las mesas» y el `FloorGrid` de hoy, con su resumen y sus pestañas de sala (`pwa/views/FloorGrid.vue:17-23`).
- **La tarjeta** sale de `FloorGrid.vue:51-67` a `pwa/views/TableTile.vue`, la misma en las dos pestañas: `table-<id>`, `tile-state`, `tile-alert`, el verde y el borde rojo que manda sobre él. La libre añade «＋ Abrir mesa» si el servidor anuncia `open`. **Cada tarjeta se pinta una sola vez** (`v-if`, nunca `v-show`): un `getByTestId` de las E2E no admite dos.
- **La banda, solo de «Mis mesas»:** `ready.read(mineEntries(state.entries, props.session))` en `BoardView.vue:78`. En «Todas», las demás mesas se siguen pintando en verde, sin banda (`plan22:734`).

**Abrir mesa** (`pwa/views/OpenTable.vue`, rehecha; `maq15:789-795`)
- «Abrir Mesa 5» con «Sala · hasta 12 personas» (como hoy).
- «¿Cuántas personas?»: botones del 1 al 8 (`pax-1` … `pax-8`) y «Más…» (`pax-more`), que enseña el paso a paso y el número (`input[name=pax]`) hasta la capacidad. Empieza en 2, como hoy.
- «¿Qué van a comer?»: el menú del día ya elegido, con su marca «Menú del día» (`offer-<id>`); «Otro menú…» (`other-offers`); y «Más opciones» (`offer-more`), con los menús con elección.
- «¿Alergias o intolerancias?» (`open-allergies`):
  - sin filas, el botón «Anotar alergia o intolerancia» (`open-add-restriction`); con alguna, «Otra alergia o intolerancia» (el mismo `data-testid`);
  - cada fila `open-row-<n>` lleva «¿Quién?» (`open-guest-<n>-<comensal>`, `open-guest-<n>-all` para «Toda la mesa»), «Tipo» (`open-kind-<n>-Allergy`, `-Intolerance`, `-Preference`), «¿A qué?» (`name="open-substance-<n>"`, ejemplo «Por ejemplo, gluten»), «Gravedad» (`open-severity-<n>-Severe`, `-Moderate`, `-Mild`) y «Quitar» (`open-remove-<n>`).
- Abajo: el resumen (`open-summary`), el primer problema de formato (`open-problem`) y el botón grande «Abrir Mesa 5» (`do-open`), **apagado mientras una fila esté a medias**.
- **Tres toques:** la mesa, las personas y «Abrir Mesa 5» (`maq15:790`). Los de una alergia van aparte: anotar, quién y a qué.
- `BoardView.open` (`BoardView.vue:109-119`), según el resultado:
  - confirmada: la ficha de la mesa, como hoy;
  - rechazada: la hoja se queda, con sus filas y el aviso. Con `offer_unavailable`, además, relee la configuración;
  - sin confirmar: al plano, con la orden pendiente arriba, que ya lleva las filas.

**La ficha** (`pwa/views/TableCard.vue`)
- **La tarjeta «Pases»** (`TableCard.vue:271-293`) pasa a `pwa/views/CoursesTimeline.vue`, con `pases.ts` (maqueta, pestaña 2):
  - la línea de servidos (`courses-served`, cada uno `served-<id>`);
  - cada pase sin terminar (`course-<id>`), con su nota (`course-note-<id>`), sus platos agrupados (`dish-<pase>-<n>`) y las líneas de excepción (`prep-<id>`), las adaptadas en rojo;
  - el pase «Siguiente», con el borde del acento, y el listo, en verde (`maq:191-192`).
- **El botón grande**, con `barNote` encima (`bar-note`): «Enviar siguiente pase · Aperitivos», apagado con «Esperando la revisión de cocina» (`do-wait`, `ficha.ts:65`), y «Servido · Principal» (`do-serve-<pase>`).
- **Omitir, con su propia hoja** (`pwa/views/SkipSheet.vue`; hoy el motivo es un campo compartido en «Más opciones», `TableCard.vue:318-321`):
  - en «Más opciones», «Omitir pase · Postre» (`do-skip-<pase>`) abre la hoja, sin el «(con motivo)»;
  - la hoja (`skip-sheet`) dice «¿Omitir Postre?» y «Ese pase no se envía a cocina y queda omitido en la mesa.»;
  - lleva el campo «Motivo» (`name="skip-reason"`, 3 caracteres o más, `ficha.ts:80`) y los botones «Volver» (`skip-cancel`) y «Omitir Postre» (`do-skip-confirm`), con borde de aviso y nunca en rojo;
  - es una orden `skip`, con la versión leída.
- **«Más opciones»** (`TableCard.vue:315-329`) se queda con lo anunciado que no va en grande: pausar (con su campo «Motivo (para pausar)», `name="reason"`, que usa `e2e/commands.spec.ts:67`), omitir y lo que el servidor añada (por ejemplo «Iniciar servicio» mientras se espera la revisión, `ficha.ts:47-58`).
- **Alergias desde la franja roja** (`plan15:680`):
  - la cabecera de la franja lleva «Cambiar» (`restrictions-open`), o «Anotar alergia o intolerancia» si no hay ninguna, **solo si el servidor lo anuncia**: «Cambiar» con `declare-restriction` o `remove-restriction`, «Anotar…» con `declare-restriction`, como hoy el alta (`TableCard.vue:229`) y «Retirar» (`:146`). Si no anuncia ninguna (una mesa terminada), la franja lleva «Ver alergias», que abre la hoja solo con `allergyList`, o nada si no hay ninguna;
  - abre `pwa/views/AllergySheet.vue` (`allergy-sheet`), con `allergyList`: cada restricción, escrita entera, con sus platos y su estado;
  - «Retirar» de cada una (`do-remove-restriction-<id>`, con «Motivo (para retirar)», `name="allergy-reason"`) sale de «Más opciones» (`TableCard.vue:324-327`);
  - el alta «Anotar alergia o intolerancia» sale del `<details>` de la franja (`:229-254`), con los mismos campos y `do-declare-restriction`.
- **«Carta y menús»** (`pwa/views/DishPanel.vue`; `plan15:678-679`), sin desplegables:
  - en un menú con elección, una fila por comensal con los platos como botones (`do-choose-<pase>-<comensal>-<n>`). Lo elegido sale marcado, y cada toque es una orden;
  - en la carta, «Para quién» y «Cantidad» arriba, y los platos como botones (`do-add-dish-<pase>-<n>`). Cada toque es una orden, y la cantidad vuelve a 1;
  - si el servidor anuncia elegir o pedir y el móvil no tiene la oferta de esa mesa: «Los platos de esta mesa no están en este móvil: su menú o carta ya no se ofrece ahora. Elige o pide en el PC principal.» (`carta-unknown-offer`).

**La orden de otra instalación** (`pwa/views/StatusPanel.vue:39-51`): la descripción (`blocked-description`) y `foreignAdvice` (`blocked-advice`). Siguen «Preguntar al servidor si la aplicó» y «Descartarla». Es el panel común: Costiña Cocina lo enseña igual.

### 4. `test(pwa)`: pruebas de navegador y capturas

- **Proyecto nuevo `sala-vertical`** en `frontend/pwa/playwright.config.ts`: 820 × 1180, táctil, Chromium, con `testMatch: SALA_SPECS` (`/mesas\.spec\.ts$/`). Esa historia sale del proyecto `desktop` (`testIgnore`, `playwright.config.ts:48`) y se queda en `mobile`: corre en el móvil y en 820 × 1180, como pide `plan22:760`, y no en el escritorio.
- **`e2e/support.ts`:**
  - `TAGS` gana `'sala-vertical': 'vertical'` (`support.ts:106`);
  - un carril `salaTable()` con **M5**, la mesa libre desde C3d (`support.ts:101-104`), para `mobile` y `sala-vertical`, que corren uno detrás de otro;
  - `recordVibrations` y `vibrations` (`e2e/banner.spec.ts:15-22`) y `finish` (`banner.spec.ts:149-161`, como `finishAndRelease`) pasan a `support.ts`;
  - y tres ayudas por API: crear una estación de sala (`/organization/commands/station-create`), desactivarla (`station-deactivate`) y revocar un dispositivo (`/auth/devices/{id}/revoke`, `Server/Program.cs:522-526`).
- **`e2e/mesas.spec.ts`** (nueva), contra el motor real, con `test.setTimeout(240_000)` como `e2e/banner.spec.ts:39` (el de por defecto es 60 s, `playwright.config.ts:32`, y aquí hay dos emparejamientos por la pantalla, de hasta 15 s cada espera, `e2e/support.ts:148,164`):
  1. Se crea por API una segunda estación de sala, con un código de la ejecución (`deviceName('sala2')`, como mucho 32 caracteres) y el nombre «Sala 2 <ejecución>» (`plan22:765`, revisión 21).
  2. Se empareja el móvil A en `sala-1` por la pantalla (`pairThroughTheUi`). El móvil B, en otro contexto (`deviceContext`, `support.ts:128-132`), se empareja en la estación nueva. Los dos cuentan la vibración. El pase se empareja por API (`pairByApi`, `'kitchen'`, `'pase'`).
  3. **Desde «Mis mesas»** de A, en «Mesas libres», se abre M5 en tres toques (la mesa, `pax-3` y «Abrir Mesa 5») con el menú del día ya elegido y una alergia del comensal 2 a frutos secos (Alergia y Grave, las de por defecto). Se comprueba:
     - la ficha se abre sola, con «Sentados» y «Comensal 2 · ALERGIA frutos secos (grave)»;
     - el botón grande está apagado con «Esperando la revisión de cocina», y los platos dicen «Por revisar en cocina»;
     - por API, la mesa tiene `openedBy` = el actor de A y `openedStation` = `sala-1`, y su versión es 1: **una sola orden**. La abre el comandero y no el token principal, que es una persona sin estación y haría la mesa «mía» para todos (`plan22:765`).
  4. En B, M5 no está en «Mis mesas», y en «Todas» sí, con «Sentados». B pasa a «Todas» con un toque de verdad (`board-scope-all`), antes de que cocina valide: sin él, `buzz` no vibra nunca (`pwa/sala.ts:81`).
  5. **El pase la revisa** con su token: un plato «Adaptado: sin almendra» y los demás «No le afecta». En A, la línea adaptada sale en rojo y el botón dice «Enviar siguiente pase · Aperitivos». Se toca, y la versión sube en uno.
  6. **Cocina valida**: las estaciones, por API como el PC, y `ready` con el token del pase (`kitchenValidates`, `banner.spec.ts:31-36`).
  7. **La banda sale solo en A**: «Mesa 5 · Aperitivos», una vibración, el botón grande «Servido · Aperitivos» y «Listo en el pase desde las…».
     - B relee primero: en «Todas», M5 está en verde (`ready`). Solo entonces se comprueba que B no tiene ninguna banda con «Mesa 5», que `navigator.userActivation.hasBeenActive` es `true` en B (el toque del paso 4) y que no ha vibrado: 0 vibraciones. Sin la activación, el 0 no probaría nada, porque el doble de `vibrate` solo cuenta llamadas (`e2e/banner.spec.ts:15-21`).
     - Lo que se prueba es el filtro del cliente, no el servidor: el servidor no filtra `serve` por la estación de sala (`Dining/Affordances.cs:50-51`).
  8. **«Servido»** desde el botón grande: la versión sube en uno, el pase sale servido y la banda desaparece. Al volver, M5 sale en «Mis mesas» de A, en su sala.
  9. En cada pantalla: `checkScreen` (sin palabras de caja, sin desplazamiento lateral y con 48 px) y `checkDevice`, sin datos de dinero en las respuestas y sin violaciones de la CSP.
  10. En `finally`, también si algo falló a medias: se cierra el contexto de B, se termina y se libera M5 (`finishAndRelease`), se desactiva la estación nueva y se revoca B. Así M1, M5 y M8 quedan libres para `pwa_http` 06-08 (`dotnet/tests/http/pwa_http.py:32-34`).
- **Historias que cambian:**
  - `e2e/board.spec.ts`:
    - pasos 4 y 5 (`:49-53,63`): miran la línea del plato agrupado de Cocina caliente y de Cocina fría (`dish-<pase>-<n>`), que pasa de «Por empezar» a «0 de 3 listas»;
    - paso 7 (`:71-92`): toca antes «Todas», porque las pestañas de sala solo están allí y «Mis mesas» enseña todas las libres, también M1.
  - `e2e/commands.spec.ts`:
    - `:28`: el número de personas solo se ve tras «Más…»; se comprueba `pax-2` y el resumen;
    - `:62`: el pase servido está en la línea de servidos (`courses-served`).
  - `e2e/banner.spec.ts`: solo importa las ayudas que pasan a `support.ts`. Su mesa la abre el token principal (`:48`), así que es «mía».
  - `drinks.spec`, `kitchen.spec`, `install.spec` y `pairing.spec` no cambian: sus mesas las abre el token principal, y las tarjetas siguen siendo `table-<id>`.
- **Capturas:** `C3E_CAPTURES=<carpeta>` y `e2e/captures-c3e.spec.ts`, fuera de la CI. Se añaden `-c3e` a `CAPTURE_SPECS` (`playwright.config.ts:27`) y su rama en la cadena de proyectos (`:37-46`). Detalles en «Pruebas».
- **La CI no se toca.** La caché de Playwright ya la entregó el PR #97: el paso «Playwright Chromium and its system packages (apt .deb files cached)» (`.github/workflows/dotnet-d1.yml:94-96`) usa la acción `.github/actions/playwright-chromium` (un solo `npx playwright install --with-deps chromium`, `action.yml:34`; clave por `ImageOS`, Playwright e `ImageVersion` con `restore-keys`, `:14-25`), y `playwright-apt-cache.yml` la calienta en cada push a `develop` (`:5-9`). El navegador no se cachea, por decisión (`docs/plans/2026-10-27-ci-cache-playwright.md:36`). En la ejecución de C3e solo se lee el tiempo de ese paso y si dice «Need to get 0 B» (caché caliente). Si el job se acerca a los 20 minutos (`dotnet-d1.yml:16`), se mira ese paso antes de subir el límite (`docs/handoff/2026-10-27-c3d-costina-cocina.md:65`).

### 5. `docs(adr)` y `docs(native)`: el documento, el ADR y la versión

- **`docs/native/C3e-costina-sala.md`**, con la forma de `docs/native/C3d-costina-cocina.md`:
  - «Antes de instalar», con una comprobación nueva: en Dispositivos, el puesto del PC y todos los móviles de Costiña Sala están en la **misma estación de sala** (en Costiña, una sola). El puesto se aprueba en la estación elegida (`Costina.Desktop/ViewModels/DevicesViewModel.cs:466`), y una mesa abierta desde él guarda esa estación (`Dining/DiningOperations.cs:48`): con estaciones distintas (la «Sala» creada sola, `DevicesViewModel.cs:497`, y otra), lo abierto en el PC no sale en «Mis mesas» de los móviles ni les avisa. El que esté en otra se vuelve a emparejar, porque la estación de un dispositivo no se cambia;
  - lo que notará la sala;
  - la regla nueva y su ADR;
  - qué cambia (también en Costiña Cocina: el panel de la orden de otra instalación, su aviso de «no consta» y los textos nuevos de rechazo);
  - las decisiones tomadas al construir, para revisar, y las diferencias con la maqueta;
  - qué no cambia;
  - las comprobaciones, con las que estuvieron en rojo antes de su arreglo;
  - las capturas, en `docs/native/images/c3e/`;
  - el guion físico corto (`plan22:831-842`), con un Android y un iPhone o un iPad, por la conexión sencilla:
    - dos móviles en dos estaciones de sala, y «Mis mesas» de cada uno;
    - abrir con una alergia en tres toques;
    - la banda solo en el móvil de esa estación, con la vibración en Android;
    - una mesa abierta en el PC como puesto (sin el modo responsable): sale en «Mis mesas», con la banda, en el móvil de la estación del PC, y en el otro solo en «Todas». Al terminar, el móvil de la segunda estación se vuelve a emparejar en la de todos («Antes de instalar»);
    - la banda no tapa volver ni «Enviar» (punto 7);
    - ninguna palabra de caja (punto 11);
  - la vuelta atrás y lo diferido.
- **`docs/ADR/ADR-018-mis-mesas-por-dispositivo.md`:**
  - una mesa es «mía» si la abrió este dispositivo, si se abrió desde su misma estación o si no se sabe quién la abrió;
  - es presentación, nunca permiso: en «Todas» cualquier móvil de sala ve todas las mesas y hace lo que el servidor le anuncie;
  - **no es atribución a una persona**: el nombre del dispositivo no es único (F12), y la auditoría sigue siendo el actor `device:<nombre>`. Atribuir a un camarero es E5 y el Hito 7, con su propio ADR (`plan22:178,817`);
  - la banda avisa solo de «Mis mesas»;
  - un requisito de uso: en un local con una sola sala, el puesto del PC y todos los móviles de sala van en la misma estación de sala; si no, lo abierto en el PC como puesto no sale en «Mis mesas» ni avisa;
  - no hay «mi sala» mientras la estación no tenga sala, que es esquema (`plan22:810`).
- **`dotnet/AGENTS.md`:** un párrafo C3e, en inglés como los demás, después del de C3d (`dotnet/AGENTS.md:105`). Se ponen al día las frases que dejan cosas «para C3e» (`:97,99,101,103,105`).
- **`docs/STATUS.md`:** una entrada C3e después de la de C3d (`docs/STATUS.md:142-152`).
- **Planes entregados:** `plan22` §4.4 y la mitad del camarero de `plan15` §4.6 (`plan15:662-680`), cada uno con «Entregado», y la línea «Entregado» de este plan, con sus concreciones.
- **Versión `0.46.0-c3e`:**
  - `dotnet/src/Costina.Server/Costina.Server.csproj:2`;
  - `dotnet/src/Costina.Desktop/Costina.Desktop.csproj:4`;
  - `frontend/pwa/package.json:4` y su lock (`package-lock.json:3,9`), con `0.46.0`;
  - y las tres veces que la fija `dotnet/tests/Costina.DesktopChecks/Program.cs` (`:625,1177,1378`), como hizo C3d.

### 6. `docs(handoff)`: el traspaso

`docs/handoff/2026-10-27-c3e-costina-sala.md` es el nuevo punto de entrada y sustituye al de C3d. Lleva:
- el estado de `develop` y los commits;
- la CI leída en los registros, con el tiempo del paso «Playwright Chromium and its system packages» y si dijo «Need to get 0 B» (con el job de `develop`, la primera ejecución de la PR ya va en caliente);
- el instalador;
- las trampas nuevas:
  - M5 es de `mesas.spec` (móvil y `sala-vertical`) y queda libre en `finally`;
  - las estaciones `e2e-sala2-*` quedan desactivadas en la base de pruebas;
  - el comando de las capturas;
  - se arrastra la de la caché de Playwright de la CI (`docs/handoff/2026-10-27-c3d-costina-cocina.md:65`): si `postgres-http` se acerca a los 20 minutos, mirar ese paso antes de subir el límite;
- lo que tiene que hacer el promotor: antes de instalar, el guion corto y las decisiones para revisar;
- el siguiente trabajo, E4f (`0.47.0-e4f`), **con su propio permiso** de subida y mezcla.

## Qué no cambia

- **El motor, el esquema (v8), la API y la CSP.** `openedBy` y `openedStation` solo se leen. Las tablas comunes `floorplan-cases.json`, `vocabulary-cases.json` y `kitchen-cases.json` no cambian: una prueba más lee la última.
- **El servidor decide los botones** (D3.1). «Mis mesas», los grupos y los colores son presentación, nunca permiso. Un móvil de sala sigue pudiendo servir cualquier mesa que vea en «Todas», si el servidor se lo anuncia.
- **Una sola orden incierta durable, una por gesto** (D6.3, D6.7):
  - «Abrir mesa» con sus alergias es **una** orden `open`;
  - «Servido», omitir, retirar y anotar son una orden cada uno;
  - elegir y pedir, una por toque, hasta E4g.
- **F01:** cada orden sale con la versión leída de esa mesa.
- **El aviso es un estado leído y no un mensaje** (D2). Vibra solo en Android, solo con una clave nueva, con la página a la vista y tras un toque (`pwa/sala.ts:77-84`). En la sala no suena nada.
- **Restricciones siempre escritas**, con tipo, sustancia y gravedad, y rojo solo si alarman. Una restricción de toda la mesa sale una vez, arriba.
- **Sin dinero** (ADR-007). Nombres y nunca códigos. Castellano con tildes.
- **El PC no cambia, y Costiña Cocina solo en lo común.** De `cocina.ts` solo cambia un parámetro aditivo, con su prueba. Pero `StatusPanel` y el `CommandRunner` son de las dos aplicaciones (`pwa/views/KitchenView.vue:56,176`): en Cocina también salen la orden de otra instalación con su descripción y `foreignAdvice`, su aviso de «no consta» y los textos nuevos de rechazo. Lo cubre `tests/commands.test.ts`, que prueba el runner común.
- **La CI** (`.github/`), que ya trae la caché de Playwright desde el PR #97.
- **Las cachés:** los datos del servidor siguen sin caché (`pwa/api.ts:45`, `Server/Program.cs:319`); `sw.js`, IndexedDB (versión 2) y lo que la pantalla guarda no cambian. La pestaña «Mis mesas» o «Todas» y las filas de alergias de la hoja van en memoria, como la sala elegida. Volver a `0.45.0-c3d` no deja ningún móvil sin abrir.

## Diferido

- **La sala de cada estación** (`core.stations.zone_id`, esquema; `plan22:810`): «mi sala», ver solo las libres de mi sala y contar como mías las de mi sala abiertas desde otra estación. Irá en un corte de esquema propio, cuando un local con varias salas lo pida.
- **Atribución a la persona**, el camarero con PIN: E5 y el Hito 7, con su ADR (`plan22:817`).
- **El aviso con la aplicación cerrada o el móvil bloqueado**: el corte del certificado público, con su ADR (`plan22:806-809`).
- **Volver a avisar** si un pase listo no se sirve en N minutos, y **el aviso en el PC** (`plan22:814,816`): sin corte todavía. Se deciden con lo que salga del guion físico de C3e.
- **`fire-next` anunciado aunque falte elegir** o con un pase vacío (`Dining.Domain/AllowedActions.cs:19-20`): se arregla en el motor en E4g (`0.49.0-e4g`), que rehace la carta y la comanda de una vez. Hasta entonces, los avisos `choice_missing` y `course_empty`. Costiña tiene menú cerrado (`plan15:678`).
- **Elegir y pedir en una sola orden**: E4g (`plan15:678-679`).
- **La vista previa «Así lo verá el camarero»**: E4f (`0.47.0-e4f`), con `pases.ts` (`plan22:772`).
- **La mesa abierta con una oferta que ya no está vigente** (`docs/native/C3b-bodega-y-bebidas.md:466`): el móvil solo conoce las ofertas que leyó en esta sesión (`configuration.ts:16-18`), así que tras recargar pierde los platos para elegir o pedir de esa mesa. C3e solo lo dice en «Carta y menús» (`carta-unknown-offer`) y manda al PC. Recordar esas ofertas en el móvil cambia lo que se guarda en el dispositivo y va con la carta de E4g (`0.49.0-e4g`).

## Riesgos

- **Avisar de más.** Las mesas sin dato de quién las abrió avisan a todos los móviles. Es lo seguro, y se mide en el guion (`plan22:861`).
- **Nombres de dispositivo repetidos.** Dos móviles con el mismo nombre comparten «Mis mesas» (`plan22:863`). El PC avisa de los nombres repetidos (F12), y el ADR deja escrito que esto no es atribución.
- **El puesto del PC en otra estación de sala.** Lo que abre no sale en «Mis mesas» de los móviles ni les avisa. Lo cubren «Antes de instalar», el guion corto y el ADR.
- **La CI.** Hay dos E2E más, una por proyecto (unos 20 a 40 s cada una, con su `test.setTimeout`), en un job de 20 minutos. Desde el PR #97 el job `postgres-http` tarda unos 6 minutos (6 min 41 s con la caché vacía y 5 min 56 s con la caché llena, en la CI del PR #97, run 40497379143), así que hay margen. Si aun así se acercara al límite, primero se mira el paso de Playwright («Need to get 0 B») y después se sube `timeout-minutes` con su comentario (la misma regla de C3d).
- **Una afirmación negativa en la E2E** (B sin banda). Se espera antes a que B haya leído la mesa en verde en «Todas»; si no, la prueba pasaría sin probar nada.
- **El cambio de la ficha rompe historias que ya pasan.** Las que cambian están listadas en el paso 4. La batería entera se pasa antes de subir.
- **Un móvil sin red con una versión anterior en caché** no tiene las pestañas. Como no cambia la API, sigue funcionando.
- **Estaciones de prueba que se quedan.** Cada ejecución deja dos estaciones desactivadas, y `/configuration.stations` llega como mucho a 500 (`Core.Persistence/OrganizationUnit.cs:110-113`). La base de la CI es nueva en cada ejecución.

## Pruebas

Cada prueba nueva falla antes de su arreglo, y se apunta en el documento del corte.

- **vitest:**
  - nuevos: `mesas.test.ts`, `abrir.test.ts` y `pases.test.ts`, este con la tabla común `kitchen-cases.json`;
  - cambian: `sala.test.ts` (la banda filtrada), `ficha.test.ts` («Servido · …» y `barNote`), `commands.test.ts` (los avisos nuevos, también `offer_unpriced`, `table_inactive` e `invalid_request` sin palabras de caja, y la orden de otra instalación, que vale para Sala y para Cocina) y `views.test.ts` (las vistas nuevas pasan sus reglas).
- **ClientChecks:** no cambian. La tabla común se rompe a la vez en ClientChecks, en `cocina.test.ts` y en `pases.test.ts`.
- **DesktopChecks**, porque cambia `dotnet/tests` (la versión).
- **Playwright:** 31 historias en lugar de 29: 14 de escritorio, 15 de móvil, 1 de `tableta` y 1 de `sala-vertical`.
- **Batería local**, una sola a la vez, sin administrador y nunca contra el puerto 5088:
  - `npm run build` antes de compilar el servidor;
  - `COSTINA_E2E=1 sh tools/agent/local-tests.sh`;
  - `sh tools/agent/local-tests.sh desktop`;
  - se apunta la duración de `pwa_http`.
- **CI leída en los registros:** las cifras de cada job, `[mobile]` y `[sala-vertical] mesas.spec` con su tiempo (también apuntado en el documento del corte), y solo el tiempo del paso «Playwright Chromium and its system packages», con «Need to get 0 B» si fue en caliente.
- **Capturas** en `docs/native/images/c3e/`, sobre una base nueva y con el motor compilado con el build actual de la PWA:
  - el comando, como el de C3d (`e2e/captures-c3d.spec.ts:13-14`): `C3E_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3e)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http`;
  - usan las mesas M3 a M8 y dejan M1 y M2 para `pwa_http`;
  - una mesa la abre por API un dispositivo de una segunda estación, para que «Mis mesas» y «Todas» se distingan. Para las pestañas de sala, `/configuration` se reescribe en el navegador, como `e2e/board.spec.ts:73-79`;
  - en 375 × 812: `sala-mis-mesas`, `sala-mis-mesas-libres`, `sala-todas`, `sala-abrir-mesa`, `sala-mesa` (platos agrupados, la línea adaptada y «Enviar siguiente pase»), `sala-mesa-servido`, `sala-aviso` y `sala-alergias`;
  - en 820 × 1180: `sala-mis-mesas` y `sala-mesa`;
  - se revisan una a una antes de cerrar.

## Decisiones pequeñas para confirmar con el promotor más adelante

No se le pregunta ahora: se construyen así, quedan escritas en el documento del corte y se revisan con el guion.

1. La ayuda de «Mis mesas» es la de la maqueta. No nombra las mesas abiertas en el PC con la contraseña del responsable, que también cuentan como «mías». Las abiertas en el PC como puesto cuentan solo si el puesto está en la misma estación que el móvil («Antes de instalar»).
2. Las listas para servir van arriba **dentro de cada sala**, como la maqueta, y no arriba de todo. En Costiña, con una sola sala, es lo mismo.
3. «Mis mesas» dice «abiertas», como la maqueta. «Todas» conserva las palabras del Comedor del PC: «7 de 12 mesas ocupadas».
4. Personas: botones del 1 al 8 y «Más…» (`plan22:738`), no del 1 al 6 de la maqueta del 15-10. Nunca más que la capacidad de la mesa.
5. En la hoja de abrir, cada fila es **una alergia con su comensal**, como el PC y la maqueta del 15-10. No hay una fila fija por comensal: con 8 personas serían 8 filas vacías.
6. «Abrir mesa» queda apagado mientras una fila esté a medias. Una fila vacía del todo no cuenta.
7. Si el servidor rechaza la apertura, la hoja se queda con sus filas, para no escribir otra vez las alergias.
8. Los platos agrupados van en la tarjeta «Pases» de la ficha, como dibuja la maqueta del 22-10. «Carta y menús» se queda para elegir y pedir.
9. Todo pase sin enviar enseña sus platos con sus líneas de alergia. La maqueta resume los que van después del siguiente.
10. El título sigue siendo «Pases · 2 de 4 hechos», que cuenta también los omitidos, y no «servidos».
11. Cada plato agrupado lleva el nombre de su estación, que la maqueta no pinta: el mismo nombre en dos estaciones son dos platos.
12. Con una orden de otra instalación se sigue pudiendo preguntar, con el aviso honesto, en lugar de quitar el botón.
