# C3a — Comandero nuevo: aspecto y mesas

Fecha: 2026-10-20. Rama `feat/c3a-comandero-nuevo` sobre `develop` (`0fd446e`, `0.40.2-e3.3.1`). Primera parte de C3 en el plan aprobado `docs/plans/2026-10-15-menus-carta-y-bodega.md`, sección 4.3, bajada a pasos en `docs/plans/2026-10-20-c3a-comandero-nuevo.md`. El promotor la aprobó el 20-10 con «merge y sigue», que da también el permiso para subirla y mezclarla. Versión **`0.41.0-c3a`**.

**Sin cambio de esquema** (sigue la v8), **ni del motor, ni de la API, ni de la CSP**. Cambia la PWA (comandero y pantalla de cocina). En el puesto principal, `FloorPlan.cs` gana dos funciones con lo que el Comedor ya calculaba, para que el comandero y el puesto compartan una tabla de casos.

## Lo que notará el promotor

- **El comandero se parece al Comedor del PC.** Fondo gris claro, tarjetas blancas, el verde de la casa, iconos de trazo y todo con tildes. Solo tema claro.
- **Al entrar se ven las mesas**, no un formulario:
  - una tarjeta por mesa, **agrupadas por sala y también las libres**, con el resumen arriba («7 de 8 mesas ocupadas · 20 personas»);
  - cada tarjeta lleva el nombre, los minutos del estado que se ve, las personas y el menú (o las plazas si está libre), el estado en palabras, los pases «1 de 2» y el aviso de alergia, escrito y en rojo;
  - con dos salas o más, pestañas de sala.
- **Tocar una mesa ocupada abre su ficha**, como en la maqueta:
  - arriba, la mesa, su estado y desde cuándo («4 personas · en cocina desde las 21:14 (12 min)»);
  - las restricciones en rojo, escritas con quién, qué y gravedad, y «Anotar alergia o intolerancia» debajo;
  - dos entradas grandes, **«Carta y menús»** y **«Bodega y bebidas»**;
  - los pases en corto, con su estado y su detalle;
  - abajo, **un solo botón grande** con lo siguiente que toca («Enviar siguiente pase · Segundo pase»). Lo demás (pausar, omitir…) está en «Más opciones». Si no toca nada, el botón queda apagado y dice qué se espera («Esperando a cocina»).
- **Tocar una mesa libre abre «Abrir mesa»:** personas en botones del 1 al 6 (empieza en 2) más «−» y «+», el **menú del día ya propuesto** y los demás en «Otro menú…». Con el menú del día son tres toques: mesa, personas y «Abrir Mesa 7». Al confirmarse, se pasa a la ficha de la mesa recién abierta.
- **Nombres, nunca códigos.** «Mesa 7» y «Cocina fría», no `M7` ni `cold`. La empresa y el local ya no se ven. Arriba a la derecha, qué es el aparato y dónde está («Comandero · Sala 1») y su nombre, sin el prefijo `device:`. La versión del servidor está en «Opciones».
- **Palabras del puesto.** Algunas cambian en el comandero:

  | Antes | Ahora |
  |---|---|
  | Mesa abierta | Sentados |
  | EN PAUSA | En pausa |
  | Enviado a cocina | En cocina |
  | LISTO PARA SERVIR | Listo para servir |
  | Enviada (una elaboración) | Por empezar |
  | Mesa · … (una restricción) | Toda la mesa · … |
  | SI se aplico | SÍ se aplicó |
  | PENDIENTE DE REVISION POR COCINA | PENDIENTE DE REVISIÓN POR COCINA |
  | La cuenta de esta mesa esta cerrada… | Esta mesa ya está cerrada en Caja: avisa en el PC principal. |

- **Cocina** con el aspecto nuevo: botones de 64 px, la mesa y la estación por su nombre, y «(otra estación)» en lo ajeno.
- **Un menú cambiado en el PC llega solo** al comandero: la configuración se relee al abrir «Abrir mesa», cada 5 minutos y con «Actualizar».
- **Igual que antes:** la orden sin confirmar y los avisos de D6.7 (otro nombre de dispositivo, otra instalación, ilegible), con los mismos textos y botones, ahora con el aspecto nuevo.

## Qué cambia

### Palabras y lógica sin DOM

- **`labels.ts`** copia el vocabulario de `Costina.Client/Labels.cs` con tildes:
  - estados de mesa, servicio, pase y elaboración;
  - tipos de oferta («Menú degustación», «Menú con elección», «Carta»);
  - tipos de estación, que `/configuration` envía en minúscula;
  - las palabras de las restricciones, gravedades y revisiones de cocina;
  - las acciones operativas.

  Las de caja no tienen palabra: saldrían en crudo, nunca como un rótulo de Caja. Ni mayúsculas ni guiones cuentan, y un valor desconocido sale tal cual, como en el puesto. Ayudantes nuevos: `courseStateLabel`, `guestLabel` («Toda la mesa» / «Comensal n»), `clockLabel`, `minutesLabel` y `plural`.
- **`floor.ts`** es la copia sin DOM de `Costina.Client/FloorPlan.cs`. Calcula, siempre como **presentación de lo leído**:
  - el estado de la tarjeta, traduciendo solo los códigos exactos de tarjeta (lo desconocido, en crudo);
  - los minutos del estado que se ve, nunca inventados;
  - los pases «n de m» y su barra;
  - el aviso de restricciones: «Alergia», en crudo lo desconocido, « · por revisar» mientras cocina no decide; siempre escrito, rojo solo si alarma;
  - el detalle corto de cada pase.
- **Una sola tabla de casos para las dos implementaciones.** `dotnet/tests/fixtures/floorplan-cases.json` (44 casos de tarjeta y 18 de pase) y `vocabulary-cases.json` (10 familias de palabras, más comensales, restricciones, minutos y plurales) los leen vitest y ClientChecks. Si el comandero y el puesto se separan, falla una de las dos. Un campo o una familia que la prueba no conoce es un fallo, nunca se salta. vitest falla además si `labels.ts` tiene una palabra que la tabla no tiene, y ClientChecks si un valor que el dominio puede enviar (un estado, un tipo, una acción operativa) no tiene su fila: una palabra nueva solo en `Labels.cs` no pasa.
- **`FloorPlan.cs`** gana `Elapsed(service, now)` y `Progress(service)`, y el `TableTile` del Comedor las usa: lo que la tabla común prueba es exactamente lo que pinta la tarjeta del puesto. Su comportamiento no cambia.
- **Tildes en todos los textos** de `commands.ts`, `operations.ts`, `startup.ts`, `storage.ts`, `kitchen.ts` y `money-guard.ts`, también los de D6.7. El rechazo `account_closed` ya no dice «cuenta».

### Mesas (`FloorGrid.vue`, `configuration.ts`)

- **`configuration.ts`**:
  - normaliza `/configuration`, también la de un servidor anterior (solo `menus`, sin `stations`);
  - `Names` resuelve mesas y estaciones como el `Names` del puesto: el nombre configurado o, si falta, el código tal cual;
  - `floorRooms` hace lo que `RebuildFloor` en el Comedor: todas las mesas activas por sala, también las libres, en el orden de la configuración. Una mesa ocupada que la configuración ya no lista sale al final, en «Otras mesas ocupadas»: nunca se esconde una mesa con gente;
  - `useConfiguration` lee al montar, cada 5 minutos y con `reload()`. Un fallo conserva lo último leído y lo dice; un 401 avisa a la aplicación;
  - `/configuration` solo trae las ofertas vigentes ahora, y un menú de comida deja de serlo a las 17:00. `useConfiguration` recuerda cada oferta leída en la sesión (`known`) y `offerFor` da la de una mesa ya abierta aunque haya dejado de estar vigente: su nombre y sus platos siguen en la tarjeta, en la cabecera y en «Carta y menús». «Abrir mesa» solo ofrece las vigentes;
  - `floorView`: **el plano solo se pinta tras una lectura de verdad del tablero** (D6.1). Antes, con la configuración leída y el tablero aún no, todas las mesas salían «Libre» un momento, y si esa lectura fallaba se quedaban así, con «Abrir mesa» en mesas ocupadas. Ahora se lee «Leyendo las mesas…» o, si la lectura falló, que las mesas saldrán cuando el servidor responda. «No hay mesas activas» solo se dice con las dos lecturas buenas.
- **Cada tarjeta es un botón** (`data-testid="table-<id>"`). **Tocarla solo elige:**
  - una ocupada abre su ficha con la lectura de siempre (F01);
  - una libre abre «Abrir mesa», solo si el servidor anuncia `open`. Sin `open`, la tarjeta libre queda apagada.
- **«Actualizar»** relee el tablero y la configuración, como `RefreshAll` en el puesto. Tras una lectura buena del tablero, una configuración que falló se reintenta sin esperar a los 5 minutos.
- **La sala elegida se conserva** al ir a una ficha o a «Abrir mesa» y volver, como `SelectedZone` en el Comedor (la guarda `BoardView`, no el plano).
- El aviso de la tarjeta se parte por su separador: en un móvil, «Alergia ·» y «por revisar» debajo, nunca «por» y «revisar» separados.

### Ficha de mesa (`TableCard.vue`, `ficha.ts`, `DishPanel.vue`, `DrinksPanel.vue`)

- **`ficha.ts`** decide solo presentación, con el orden del Comedor:
  - `tableActions` lista las acciones **anunciadas**: servir, validar y omitir son de cada pase; liberar, de la ocupación; las demás, del servicio;
  - `primaryAction` elige el botón grande: la primera anunciada de servir, validar, enviar siguiente pase, empezar, reanudar, terminar y liberar;
  - `commandFor` arma la orden con la versión leída de esa mesa y la descripción con su nombre («Pausar servicio · Mesa 7»). Liberar va a `/dining/occupancy/{id}/release` con la versión de la ocupación y un motivo, como el puesto;
  - el campo de motivo nombra solo lo anunciado que lo pide: un camarero no lee «liberar» si no puede liberar.
- **«Carta y menús»** abre lo de hoy con el aspecto nuevo: elegir plato por comensal donde el servidor anuncia `choose` y pedir de la carta donde anuncia `add-dish`. Se rehace en C3c.
- **«Bodega y bebidas»** abre la consumición de hoy (`add-consumption`) con el aspecto nuevo y los productos agrupados por categoría. Sin importes: el servidor los fija. Se rehace en C3b.
- **«Anotar alergia o intolerancia»** pide para quién (comensal 1…n o «Toda la mesa», sin valor por defecto, como en la maqueta), tipo, a qué y gravedad, en botones.
- Mientras una orden está sin confirmar, todos los botones de envío quedan bloqueados, como hasta ahora.

### Abrir mesa (`OpenTable.vue`)

- Los campos de hoy (personas y menú o carta), con el aspecto nuevo. Personas en botones (hasta la capacidad, máximo 6) y «−»/«+» hasta 40; empieza en 2.
- El **menú del día** (la primera oferta vigente, E4d) se propone siempre, con la etiqueta «Menú del día». Los demás, en «Otro menú…».
- Si la configuración releída trae otro menú del día y nadie ha elegido aún, se propone el nuevo. Si lo elegido deja de estar vigente, vuelve el del día.
- Si la última lectura dice que la mesa está ocupada, lo avisa: el servidor no la abrirá otra vez.

### Cocina (`KitchenView.vue`)

- Solo el aspecto nuevo y los nombres de mesa y de estación. Las reglas de D6.4 no cambian: cada estación ve el pase entero, lo ajeno atenuado y sin botones, y el `pase` valida y revisa.
- La estación de cada línea solo se escribe donde dice algo: en lo de otra estación («Cocina caliente (otra estación)»), en el `pase` y en una pantalla sin estación. En lo propio ya la dice la cabecera.
- Botones de acción y nota de revisión de 64 px. Las tres decisiones de una revisión («No afecta», «Adaptar», «Rehacer») van en una sola fila, a partes iguales.
- La agrupación por plato llega en C3c.

### Emparejar, cabecera y orden pendiente (`App.vue`, `PairView.vue`, `HomeView.vue`, `StatusPanel.vue`)

- Barra superior con la marca, el papel y la estación por su nombre, y el nombre del dispositivo.
- **En el móvil, la ficha empieza justo bajo el enlace**, como en la maqueta: con una ficha o «Abrir mesa» abiertas no se ven la marca ni el aparato (siguen en el plano, en cocina y al emparejar; el aviso de DEMOSTRACIÓN se queda), y la antigüedad de la lectura va en corto en la fila del enlace («Leído hace 3 s»), siempre a la vista (D6.2). La mesa empieza a unos 160 px, antes a 236.
- `StatusPanel`, con el aspecto nuevo. Los paneles de D6.7 (pendiente, bloqueada por `actor`, `foreign` o `unreadable`) conservan sus textos y sus botones.
- «Opciones» al pie, con la versión del servidor y desvincular en dos pasos. Con una ficha abierta no se ofrece, para que el botón grande quede al pie; el aviso de almacenamiento (D6.6) sí se ve.
- Los errores de IndexedDB que llegan a la pantalla de arranque («Servidor no listo (…)») tienen tildes, como el resto.
- `ServiceActions.vue` desaparece: su contenido está en la ficha.

### Aspecto (`style.css`, `icons.ts`, `Icon.vue`, `index.html`, manifiesto)

- `style.css` reescrito con variables: fondo `#F3F4F6`, tarjetas blancas, acento `#0E7A5F`.
  - **Rojo solo para alergias y restricciones que alarman.** Lo pendiente y lo bloqueado van en ámbar, y las acciones sin vuelta atrás (liberar, cancelar) llevan borde de aviso, nunca rojo.
  - Objetivos de 48 px en el comandero y de 64 px en cocina.
- Iconos de trazo como rutas SVG en la plantilla: ni imágenes, ni estilos en línea, ni cambios en la CSP.
- `index.html` y el manifiesto, con los colores del tema claro. El icono de la pantalla de inicio es la «C» blanca sobre el verde de la casa, como la marca de la cabecera y del puesto (`public/icon.svg` y los PNG, que genera `scripts/make-icons.mjs`), y el manifiesto se describe como «Comandero y pantalla de cocina de Costina.».
- La ficha ocupa lo que queda de pantalla (la app es una columna), así que el botón grande cae al pie sin dejar debajo un desplazamiento en blanco. En tableta, la barra del botón es blanca de lado a lado, con su línea arriba, como la maqueta.

## Divergencias deliberadas, para revisar

1. **El botón grande dice «Enviar siguiente pase · <pase>»**, como la maqueta y el vocabulario del plan. La orden pendiente y el puesto mantienen «Enviar siguiente pase a cocina».
2. **Una oferta que ya no está vigente** se sigue nombrando, con sus platos en «Carta y menús», si este comandero la leyó en la sesión. Una que nunca leyó (el comandero se encendió después de las 17:00 con una mesa de mediodía abierta) no se nombra: nunca se pinta su código. Los pases ya dicen qué pasa. El puesto muestra su código.
3. **La orden pendiente nombra la mesa** («Pausar servicio · Mesa 7»), no su código. Una orden guardada por una versión anterior conserva su texto.
4. **«¿Para quién?» no tiene valor por defecto** al anotar una restricción, como en la maqueta. Antes era «Toda la mesa».
5. **El aviso de mesa cerrada** dice «Esta mesa ya está cerrada en Caja: avisa en el PC principal.» El promotor no lo ha visto aún.

## Capturas

Las hace `e2e/captures.spec.ts` contra el motor real, con una sala montada por la API:
- Mesa 1, sentados;
- Mesa 3, en cocina, con una alergia por revisar;
- Mesa 4, listo para servir;
- Mesa 5, comiendo, con dos restricciones y el siguiente pase por enviar;
- Mesa 6, en pausa;
- Mesa 7, libre;
- Mesa 8, terminado, por liberar.

Las mesas marcan «0 min» porque se abrieron segundos antes. El motor no deja fecharlas atrás y no se ha falseado nada.

**Comandero en un móvil (375 × 812):**
- [Mesas](images/c3a/mesas-375x812.png)
- [Ficha de la Mesa 5](images/c3a/ficha-375x812.png): restricciones arriba, las dos entradas y «Enviar siguiente pase · Segundo pase» al pie.
- [Abrir Mesa 7](images/c3a/abrir-375x812.png): 2 personas y el menú del día propuesto.

**iPad en vertical (820 × 1180):**
- [Mesas](images/c3a/mesas-820x1180.png)
- [Ficha de la Mesa 5](images/c3a/ficha-820x1180.png)

**Tablet en horizontal (1180 × 820):**
- [Mesas](images/c3a/mesas-1180x820.png)
- [Ficha de la Mesa 5](images/c3a/ficha-1180x820.png)

**Cocina (1180 × 820):**
- [Pase](images/c3a/cocina-pase-1180x820.png): la revisión pendiente con su nota obligatoria, y la Mesa 6 en pausa.
- [Cocina fría](images/c3a/cocina-cold-1180x820.png): lo propio con «Empezar» y «Marcar lista», y lo de Cocina caliente atenuado, «(otra estación)» y sin botones.

**Para repetirlas** hacen falta una base nueva y el motor con el build actual de la PWA:
1. `npm run build` en `frontend/pwa`.
2. `dotnet build` de `src/Costina.Server`, o la batería entera.
3. `C3A_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3a)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http`.

Tarda unos 50 s. En la CI no corre nunca.

## Qué NO cambia

- **El motor, el esquema (v8), `grants.sql`, la API, las copias y el instalador.** Solo cambian las versiones.
- **La CSP de `PwaHosting`**: sin estilos en línea, sin imágenes nuevas y sin orígenes nuevos.
- **Sin dinero:** la guarda (`money-guard.ts`) y ADR-007 siguen igual. El comandero no recibe claves de dinero ni escribe «precio», «cuenta», «pago», «saldo» ni «€».
- **Los botones solo existen por lo que anuncia el servidor** (affordances, ya filtradas por rol y estación). El cliente no deduce reglas.
- **F01:** tocar una tarjeta solo elige; la ficha y las órdenes salen de la lectura de esa mesa.
- **Una sola orden incierta durable**, con los bloqueos de D6.7 a la vista.
- **Restricciones siempre escritas**, nunca solo con color. Lo desconocido sale en crudo.
- **Tiempo real** como en D6.2: el aviso solo provoca una relectura.
- **El Comedor del puesto** se ve y se comporta igual.

## Comprobaciones automáticas

- **vitest:** 198 pruebas en 12 ficheros (antes, 78 en 7). `npm run build` (comprobación de tipos `vue-tsc` y build de Vite) en verde. Las nuevas:
  - `floor.test.ts` y `labels.test.ts`: las dos tablas comunes; también, sin tilde ni palabra de caja, los errores de IndexedDB y la antigüedad corta;
  - `configuration.test.ts`: nombres, nunca códigos; plano por salas, con las mesas libres y la ocupada que ya no está en la configuración; el plano solo tras leer el tablero (`floorView`, `floorEmpty`); la oferta que deja de estar vigente, recordada para las mesas abiertas y nunca ofrecida al abrir; relectura al montar, a petición y cada 5 minutos; fallo, 401 y guarda de dinero;
  - `ficha.test.ts`: solo lo anunciado, el orden del botón grande, liberar desde la ocupación, la orden con la versión leída y el nombre de la mesa, y el rótulo del motivo;
  - `views.test.ts` lee las plantillas `.vue` sin navegador y exige cuatro cosas: ninguna palabra de caja (ni dentro de otra), tildes, ningún código pintado (`tableId`, `stationId`…) y nada que la CSP rechace (estilos en línea, imágenes).
- **ClientChecks:** 201 (antes 126). Las 75 nuevas pasan `FloorPlan` y `Labels` por las dos tablas comunes, y exigen una fila para cada valor que el dominio puede enviar en una familia común.
- **DesktopChecks** (`sh tools/agent/local-tests.sh desktop`): «WPF startup and viewmodel checks passed». `TableTile` usa ahora `FloorPlan.Elapsed` y `Progress`.
- **Playwright contra el motor real**, en dos proyectos con las mismas historias:
  - `desktop` (Chrome de escritorio);
  - `mobile` (Chromium con el perfil de un Pixel 7, a 375 × 812, táctil).

  Cada proyecto tiene sus mesas: el de escritorio M6, M7 y M8; el móvil M3, M4 y M5. M2 es de `pwa_http.py`. Cada uno tiene también sus nombres de dispositivo, porque los dos comparten motor y base.
  - **Selectores por `data-testid`** (`table-M7`, `pax-4`, `offer-…`, `do-open`, `detail-state`, `do-fire-next`, `more-options`, `entry-bodega`…) en lugar de `select[name=table|menu]`.
  - **commands:** tarjeta libre → `pax` → menú → `do-open` → «Sentados» → empezar → enviar siguiente pase → `do-wait` apagado → servir → «Comiendo». Después:
    - la pausa con la respuesta perdida, que da un solo efecto;
    - recargar con la misma clave;
    - la reanudación perdida, resuelta preguntando al servidor;
    - el corte de red con la orden anunciada;
    - Carta y menús;
    - una consumición, que da un solo cargo.
  - **board:** la tarjeta libre, la mesa abierta por otro, «En cocina» y «0 de 2», «Alergia · por revisar», la ficha («SIN REVISAR POR COCINA», «Cocina fría», ningún código), las elaboraciones en vivo y la pérdida de red. Al final, dos salas: `/configuration` se reescribe en el navegador (la mesa del carril pasa a una «Terraza»; el motor y la base no cambian), se elige esa sala, se abre la ficha y al volver la sala sigue elegida.
  - **commands:** con la orden sin confirmar, ningún botón `do-…` de toda la ficha está habilitado (también anotar y retirar restricciones, que van arriba).
  - **kitchen:** el nombre de la mesa y «Cocina caliente (otra estación)»; lo demás como en D6.4.
  - **pairing:** igual que antes, con los nuevos selectores.
  - **En cada pantalla visitada**, `checkScreen` comprueba que:
    - no hay palabras de caja: palabras enteras en el comandero, también dentro de otra palabra en cocina;
    - no hay desplazamiento lateral;
    - todo botón, input, select o summary visible mide al menos 48 × 48 px, y en cocina los botones de acción y la nota, 64 px.

    `checkDevice` comprueba que cada proyecto tiene de verdad su ancho y su pantalla táctil. Cada JSON se pasa por la guarda de dinero y se cuentan las violaciones de la CSP: cero.
- **Batería completa** (`COSTINA_E2E=1 sh tools/agent/local-tests.sh`, puerto 5090, binarios de `.lab/pgsql/bin`, sin administrador), en verde:
  - compilación con 0 advertencias y 0 errores;
  - dominio 83/83 y ClientChecks 201;
  - las 14 suites HTTP: packaging 14 (1 saltada), native 11, security 6/6, desktop_reads 9, affordances 5, restrictions 6, checkout 4, identity 10, pairing 14, station 8, organization 5, catalog 10, offers 7 y pwa_http 5, esta vez con la E2E;
  - Realtime 12/12;
  - en `pwa_http`, Playwright con **12 pasadas**: 6 de escritorio y 6 de móvil (board, commands, kitchen y 3 de pairing en cada uno).
- **En rojo al romperlo a propósito** (cada rotura fue temporal y se restauró):
  - **Tablas comunes:**
    - cambiar un caso de la tabla («12 min» por «13 min») hizo fallar vitest y ClientChecks;
    - quitar una cláusula de `floor.ts` hizo fallar vitest;
    - devolver «Sentados» para una mesa en pausa en `FloorPlan.cs` hizo fallar 4 comprobaciones de ClientChecks;
    - renombrar una palabra en `labels.ts` o en `Labels.cs` hizo fallar su lado;
    - quitar de la tabla las filas `course.Skipped` y `action.release` hizo fallar ClientChecks («values without a row in vocabulary-cases.json: course.Skipped, action.release»).
  - **Tildes en lo que llega a la pantalla:** con «Transaccion abortada.» en `db.ts`, `labels.test` falla.
  - **Plantillas:** con las vistas `BoardView.vue` y `KitchenView.vue` de `0fd446e`, `views.test` falla por las palabras sin tilde y por los códigos pintados. Con «cuenta» y un `style="…"` metidos en `DrinksPanel.vue`, falla por las dos cosas.
  - **Ficha:** con el botón grande en otro orden, `ficha.test` falla. Con liberar leído del servicio en lugar de la ocupación, fallan 4 pruebas.
  - **Configuración:** sin la relectura de 5 minutos, falla una prueba. Si se esconden las mesas ocupadas que faltan en la configuración, fallan 2.
  - **E2E:** un build con los pasos de personas a 40 px, el título de la ficha de 30rem de ancho mínimo y los botones de cocina a 56 px dio «7 failed, 5 passed»:
    - objetivos de menos de 48 px en «Abrir mesa»;
    - desplazamiento lateral en la ficha del móvil (550 > 375);
    - objetivos de menos de 64 px en cocina y en el inicio de un dispositivo de cocina, en los dos proyectos.

    Un build que volvía a «Todas las salas» al abrir una ficha y dejaba «Anotar restricción» sin el bloqueo de la orden sin confirmar dio «4 failed, 8 passed»: `board` (la sala ya no seguía elegida al volver) y `commands` (un botón `do-…` habilitado en la ficha), en los dos proyectos.

## Guion para el promotor (un móvil y un iPad)

**Nunca en un PC con una instalación real**, ni sobre sus servicios, puertos o carpetas. Hace falta un PC de laboratorio con una instalación de prueba en `0.41.0-c3a`: la demostración (`/DEMO=1`, D5.6) o el laboratorio de `docs/native/lab-engineering.md`. Además, un móvil (Android o iPhone) y un iPad en su Wi-Fi.

1. **Emparejar.** Desde Dispositivos, con el QR (D6.5):
   - el móvil, como comandero de sala, con el nombre `movil-sala-1`;
   - el iPad, como pantalla de cocina del `pase`.

   Arriba a la derecha se lee «Comandero · <sala>» y «movil-sala-1», sin `device:`. En el iPad, «Pantalla de cocina · Pase».
2. **Mesas.** En el móvil salen todas las mesas por sala, las libres con borde discontinuo y «n plazas». No se ve ningún código (`M1`, `cold`…) ni hace falta desplazarse de lado. Gira el móvil: la rejilla se recoloca.
3. **Abrir mesa en tres toques.** Toca una mesa libre: «Abrir Mesa N», 2 personas y el menú del día marcado. Toca «4» y «Abrir Mesa N». Se abre su ficha con «Sentados». En el PC, Comedor: la misma mesa, con 4 personas y ese menú.
4. **Alergia.** En la ficha, «Anotar alergia o intolerancia»: comensal 2, Alergia, «frutos secos», Grave, «Anotar restricción». Sale arriba en rojo: «Comensal 2 · ALERGIA frutos secos (grave)». Vuelve a Mesas: la tarjeta dice «Alergia».
5. **El botón grande.** En la ficha, el botón de abajo dice «Iniciar servicio». Tócalo y pasa a «Enviar siguiente pase · <primer pase>». Tócalo: queda apagado con «Esperando a cocina». La tarjeta de Mesas dice «En cocina», con minutos, y «0 de n».
6. **Cocina en el iPad.** Sale «Mesa N · <pase>» con las elaboraciones, la estación por su nombre y la alergia escrita, sin códigos. Las elaboraciones las marca cada estación: sin pantallas de estación, márcalas en el PC, como responsable, en Comedor y cocina («Marcar elaboración lista»). Cuando todo está listo, el iPad ofrece «Validar pase: listo para servir», con un botón de 64 px. Tócalo. En el móvil la ficha pasa sola a «Listo para servir», con el botón «Marcar servido · <pase>». Tócalo: «Comiendo».
7. **Más opciones.** Ábrelo, escribe un motivo y toca «Pausar servicio (con motivo)». La tarjeta dice «En pausa» y el botón grande, «Reanudar servicio». En el PC, una sola pausa.
8. **Carta y menús, y Bodega y bebidas.** Las dos abren lo de hoy con el aspecto nuevo. En Bodega, añade un agua. En Caja del PC, un agua en esa mesa. En el móvil, ningún importe.
9. **Menú cambiado en el PC.** En Menús y cartas, «Hacerlo menú del día» en otro menú. En el móvil, toca otra mesa libre: «Abrir mesa» propone ya el menú nuevo, sin recargar.
10. **Orden sin confirmar.** En el móvil, quita la Wi-Fi y pulsa el botón grande («Reanudar servicio»): sale «ORDEN SIN CONFIRMAR» y los botones se bloquean. Devuelve la Wi-Fi: se resuelve sola. En el PC, la mesa se reanudó una sola vez.
11. **iPad como comandero** (opcional). Desvincula el iPad desde «Opciones» y emparéjalo como comandero. Con un iPad de 820 × 1180 (como el de las capturas), las mesas salen en 4 columnas en vertical y en 5 en horizontal; con otro tamaño puede variar en una. En horizontal la ficha va centrada, y en las dos el botón grande queda abajo a la derecha.
12. **Repaso.** En ninguna pantalla aparecen «precio», «cuenta», «pago», «saldo» ni «€».

| Paso | Móvil | iPad | Notas |
|---|---|---|---|
| 1 a 12 | | | |

## Límites y diferido

- **A C3b:**
  - «Bodega y bebidas» nueva: buscador, Copa y Botella, rondas en una orden y «Lo que lleva la mesa»;
  - el catálogo, que hoy se lee una vez al montar;
  - «Añadir consumición» en la mesa del puesto;
  - lo pendiente de D6.7 y E3.3.1: la defensa en el servidor (el id del cargo derivado de la clave), el orden de guardado en el puesto, la copa con otro código, los textos de `reserved_category`, `nothing_to_do` y `product_ids_required`, y el «Preguntar al servidor» que aún se ofrece con una orden de otra instalación.
- **A C3c:**
  - las alergias en la misma hoja y la misma orden que «Abrir mesa»;
  - el menú agrupado («Ostra × 7»);
  - la cocina por plato.
  - la oferta de una mesa abierta que el comandero nunca leyó vigente (encendido después de las 17:00 con una mesa de mediodía): sin nombre y sin platos en «Carta y menús», también en el puesto. El servidor sigue aceptando elegir y pedir, pero `/configuration` solo trae las ofertas vigentes; hace falta que el motor dé la oferta de cada mesa abierta.
- **Sin fecha:** la hora de apertura de la mesa («sentados hace N minutos»). Necesita un cambio pequeño del motor. Hasta entonces, una mesa sentada no marca minutos.
- **La cabecera del móvil** sigue encima de la ficha con el enlace y la antigüedad (unos 50 px). La maqueta lleva «En línea» dentro de la barra de la mesa; moverlo obliga a reorganizar `App` y `StatusPanel`, y queda para un corte de comandero.
- **Al recargar** se vuelve al plano: la mesa elegida no se recuerda, como antes.
- **Terminar, validar sin estación, cancelar y liberar** solo se pintan si el servidor los anuncia. Solo los recibe un dispositivo antiguo con papel principal; validar llega a un comandero solo si no tiene estación. vitest los cubre, pero ninguna E2E, porque los comanderos del laboratorio nunca los reciben.
- **Playwright solo prueba Chromium**, el único navegador que instala la CI. Safari (iPhone, iPad) se comprueba con el guion.
- **El proyecto móvil pasa también la cocina a 375 px.** Aguanta (sin desplazamiento lateral y con botones de 64 px), aunque en cocina se usan tablets.
- **La captura de la estación fría** se llama `cocina-cold-1180x820.png` porque el nombre del fichero sale del código de la estación, que no se ve en pantalla.
- **La hora local exacta** del detalle de los pases no se fija a una zona horaria en la tabla común. Cada lado la sustituye con su propio reloj (`{firedAt}`, `{readyAt}`, `{servedAt}`).
