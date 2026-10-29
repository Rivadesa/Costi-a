# C3d — Costiña Cocina: comandas por mesa, cada estación con lo suyo y el pase con la mesa entera

Fecha: 2026-10-27. Rama `feat/c3d-costina-cocina` sobre `develop` (`d4f095b`, `0.44.0-e4e`). Es la sección 4.3 del plan `docs/plans/2026-10-22-sala-y-cocina.md`, aprobado el 22-10, con las secciones 5 a 10 y la «Revisión» de ese plan, bajada a pasos en `docs/plans/2026-10-27-c3d-costina-cocina.md`. Objetivo visual: la maqueta aprobada `docs/plans/maquetas/2026-10-22-sala-y-cocina.html`, pestañas «3 · Cocina · estación» y «4 · Cocina · pase». Versión **`0.45.0-c3d`**. Decisión escrita: **ADR-017** (`docs/ADR/ADR-017-la-estacion-ve-solo-lo-suyo.md`), que sustituye la regla de D6.4 «una estación ve el pase entero».

Respuestas del promotor que fija este corte:
- **22-10, pregunta 1:** en las estaciones, **solo «Listo»**, sin «Empezar».
- **22-10, pregunta 2:** **el pase puede marcar el trabajo de una estación como listo**: en «Más opciones» de cada comanda, «Marcar Cocina fría como lista», con la orden de grupo que E4e ya permite al pase de cocina.
- **22-10, pregunta 3:** el tiempo, **neutro al principio, ámbar desde 15 minutos y ámbar oscuro con letra blanca desde 25. Nunca rojo**, y siempre escrito: «Lleva N min».
- **22-10, pregunta 4:** **suena** cuando llega una comanda nueva, solo después de tocar «Empezar turno».
- **27-10, «si, adelante»:** permiso para subir y mezclar C3d cuando la CI esté en verde.

**Sin cambio de esquema (sigue la v8), de motor, de API ni de CSP.** Cambian Costiña Cocina (la PWA), la regla común de agrupar platos de `Costina.Client`, que también usa el Comedor del PC, y las pruebas.

## Antes de instalar

- **No hay nada que hacer con los datos.** C3d no cambia cómo se guardan las mesas ni el esquema.
- **Las tablets ya emparejadas siguen emparejadas.** Después de actualizar el servidor, en cada tablet de cocina: recargar Costiña Cocina (o cerrarla y abrirla), porque la página se pide siempre al servidor primero, y tocar **«Empezar turno»**.
- **Con la conexión sencilla (http), deja la pantalla de cada tablet siempre encendida en sus ajustes** (más abajo, «La pantalla siempre encendida»).
- **La guarda contra bajar de versión de E4e se estrena aquí.** Desde que `0.45.0-c3d` sirve los datos, el instalador de `0.44.0-e4e` se para con `SETUP FAILED`, sin copia y sin tocar nada. Volver atrás es lo de E4e: restaurar, en un equipo limpio y con el instalador anterior (`/RESTORE=`), la copia que el instalador de `0.45.0-c3d` hace justo antes de actualizar. Esa copia sí se restaura con `0.44.0-e4e`. Se pierde lo hecho después de actualizar.
- Siguen valiendo lo de C3b y E4e (las cuentas y las mesas no tienen vuelta atrás instalando encima) y lo de C3c (un dispositivo emparejado con C3c tendría que emparejarse de nuevo en una versión anterior a C3c).

## Lo que notará la cocina

### En cada estación (Cocina caliente, Cocina fría…)

Captura: [la estación](images/c3d/cocina-estacion-1180x820.png).

- **Una comanda por mesa y pase, solo con lo suyo.** Arriba, «Mesa 4 · Principal» (el nombre de la mesa, nunca su código), «4 personas» y el tiempo. Las comandas van de la más antigua a la más nueva.
- **Los platos agrupados:** «Merluza á galega × 4», con la cantidad grande. En cocina la cantidad se escribe siempre, también «× 1».
- **Las alergias, bajo su plato y nunca atenuadas.** Cada comensal con restricción, o con una decisión del pase, tiene su línea: «Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra». Mientras el pase no decide, la línea termina en «Por revisar en el pase» (*C3e.1: en lo que queda por hacer, «… · no lo emplates hasta que decida». Ver `C3e.1-arreglos-rapidos-usabilidad.md`.*). En rojo si la restricción alarma (una alergia, o cualquier restricción grave); si no, en ámbar. El plato lo cuenta: «Merluza á galega × 4 (1 adaptada)».
- **Una restricción de toda la mesa** sale una sola vez, arriba de la comanda, nunca repetida en cada plato.
- **«También en: Cocina fría»:** las otras estaciones del mismo pase, por su nombre entero. Sus platos no se pintan (antes salían atenuados).
- **«Listo», grande, abajo de cada comanda:** **un toque y una orden** con todo lo suyo de esa comanda. Con dos platos o más, cada plato lleva también su «Listo», que marca solo ese plato. No hay «Empezar» (respuesta 1) y no hay nada que escribir.
- **«Hecho · esperando al pase»:** cuando todo lo suyo está listo, la comanda baja a esa franja, al pie de la página, con sus alergias escritas, y desaparece cuando el pase valida ([captura](images/c3d/cocina-estacion-hecho-1180x820.png)). **«Listo» no se deshace:** si se toca por error, hay que decírselo al pase, que no valida sin ver el plato.
- **El tiempo**, siempre escrito: «Lleva 6 min» en gris; **ámbar desde 15 minutos** («Lleva 16 min»); **ámbar oscuro con letra blanca desde 25** («Lleva 27 min»). Nunca rojo, que es de las alergias. Cuenta desde que se envió el pase.
- **Una mesa en pausa** dice «Mesa en pausa» en su comanda.
- **Dos pantallas a la vez en la misma mesa** (por ejemplo, la fría toca «Listo» justo cuando el pase marca un plato caliente): no se aplica nada, la pantalla vuelve a leer y la comanda dice «Otra pantalla cambió esta mesa a la vez. Ya se ha vuelto a leer: toca otra vez si sigue haciendo falta.». Nada se reenvía solo: hay que tocar otra vez.
- **Arriba**, en la fila de la conexión, cuántas comandas hay («4 comandas») y la hora, en grande.

### En el pase

Capturas: [el pase con «Más opciones» abierto](images/c3d/cocina-pase-1180x820.png), [«Por revisar antes de enviar» con una nota](images/c3d/cocina-pase-por-revisar-1180x820.png) y [el pase desplazado, con una revisión de lo enviado en ámbar](images/c3d/cocina-pase-desplazado-1180x820.png).

- **A la derecha** (arriba en una pantalla estrecha), dos apartados. En la tablet, esa columna **se queda a la vista** al desplazar las comandas y no pasa del alto de la pantalla: «Por revisar antes de enviar» se desplaza dentro de ella y **«Esperando al camarero» queda siempre al pie**, también al abrir la página (como en la maqueta):
  - **«Por revisar antes de enviar»** (de E4e), siempre arriba: por mesa y comensal, con su alergia escrita, cada plato de los pases sin enviar con su nota obligatoria, «No le afecta» y «Adaptado». **Atajos que rellenan la nota a la vista:** «No lo lleva», «Sin gluten» y «Cambiado por …», que deja el cursor al final para escribir por qué; solo «Cambiado por» no vale como nota. La nota se puede corregir antes de decidir. Con dos platos o más, «No le afecta a nada más», con los atajos «No lo lleva» y «Sin gluten»: una orden para todo lo que queda de ese comensal. *C3e.1: ese bloque va arriba, bajo la alergia del comensal, con los platos que decide escritos encima («No le afecta a ninguno de estos 3 platos»); «Sin gluten» solo se ofrece ante una restricción de gluten, y los atajos van a 10 px. Ver `C3e.1-arreglos-rapidos-usabilidad.md`.*
  - **«Esperando al camarero»:** lo validado y sin servir, «Mesa 7 · Aperitivo · listo hace 3 min», del más antiguo al más nuevo. Se va solo cuando el camarero toca «Servido». Nunca dice «Sala avisada»: el servidor no sabe si el aviso llegó al móvil.
- **A la izquierda, una comanda por mesa y pase con la mesa entera:**
  - **el progreso de cada estación**, con su nombre entero: «Cocina caliente: lista», «Cocina fría: 1 de 2 platos». Cuenta platos agrupados, no comensales;
  - una restricción de toda la mesa, una vez;
  - **los platos agrupados**, con su estado («Por empezar», «Lista», «1 de 4 listas») y, si la comanda tiene dos estaciones o más, el nombre de la estación bajo cada plato. Debajo, sus alergias, como en las estaciones;
  - **lo enviado que queda por revisar** (una alergia anotada con el pase ya en cocina), bajo su plato y en ámbar, como lo de antes de enviar, con la nota, los atajos y «No le afecta», «Adaptado» o «Rehacer». «Rehacer» devuelve el plato a su estación (*C3e.1: va en su propia fila y en dos pasos, «¿Rehacer …? Vuelve a la estación.» con «Cancelar» y «Sí, rehacer»*). El rojo queda en la línea de la alergia, encima, si alarma;
  - **«Siguiente: Postre (sin enviar)»**, solo para leer: el ritmo lo sigue marcando el camarero;
  - **qué falta**, mientras no se puede validar: «Falta Cocina fría», «Falta revisar la alergia» o «Falta Cocina caliente y revisar la alergia»;
  - **«Validar pase: listo para servir»**, grande, solo cuando el servidor deja validar. **Ese toque es el aviso al camarero.** La tarjeta se enmarca en verde;
  - **«Listo» en lo que se emplata en el propio pase** (los platos de la estación Pase), una orden por plato;
  - **«Más opciones» › «Marcar Cocina fría como lista»** (respuesta 2): si se estropea la tablet de una estación, el pase marca por ella. Una orden con lo de esa estación que el servidor deja marcar. Sale solo si queda algo por marcar de esa estación.
- **Un pase validado con algo por revisar** (una alergia anotada después de validar) vuelve a su comanda hasta que se revise; no se puede servir mientras tanto y no sale en «Esperando al camarero».
- **Arriba**, «4 comandas · 1 por revisar» y la hora.
- **Una nota escrita no se pierde:** si otra pantalla cambia la mesa a la vez, la revisión no se aplica, la comanda dice «toca otra vez» y la nota sigue ahí; basta un toque. La nota se borra solo cuando la revisión se aplicó (o quedó como la orden sin confirmar).

### «Empezar turno» y el sonido

Captura: [«Empezar turno» en la estación](images/c3d/cocina-empezar-turno-1180x820.png).

- **Al abrir Costiña Cocina**, encima de las comandas, una franja con **«Empezar turno»**: «Tócalo al empezar: así la tablet suena cada vez que llega una comanda nueva.», y lo que hay que hacer con la pantalla. Las comandas y las alergias se ven también sin tocarlo; lo único que falta sin el toque es el sonido.
- **El toque suena una vez**, para oír cómo sonará. Después dice «Turno empezado: suena con cada comanda nueva.». Si el navegador no puede sonar: «Turno empezado, pero este navegador no puede sonar: mira la pantalla.».
- **Suena con cada comanda nueva:** dos notas cortas, cuando la relectura del servidor trae una comanda que antes no estaba. En una estación, sus comandas por hacer (una que vuelve por un «Rehacer» suena otra vez); en el pase, sus comandas. Lo que estaba al abrir no suena, y una alergia nueva en «Por revisar antes de enviar» tampoco.
- **El sonido es un tono sintetizado** en la propia página (Web Audio), sin ficheros de audio: la CSP no cambia. El volumen es el de la tablet (multimedia).
- **El turno dura lo que la página abierta.** Si se recarga la página, o se cierra y se abre la aplicación, hay que volver a tocar «Empezar turno»: el navegador no deja sonar sin un toque. Si la pantalla se queda un momento sin conexión con el servidor, el turno sigue.

### La pantalla siempre encendida

- **Con https**, «Empezar turno» deja la pantalla encendida (Wake Lock) si el navegador lo permite, y lo vuelve a pedir cada vez que la página vuelve a verse. Dice «La pantalla se queda encendida.».
- **Con la conexión sencilla (http)** el navegador no lo permite. «Empezar turno» lo dice: «Con esta conexión la pantalla no se queda encendida sola: déjala siempre encendida en los ajustes de la tablet.». Por ejemplo, en iPad, Ajustes › Pantalla y brillo › Bloqueo automático › Nunca; en Android, Ajustes › Pantalla › Tiempo de espera de la pantalla, el máximo (el nombre cambia según el fabricante), con la tablet enchufada.
- **Con https, pero con un navegador sin Wake Lock**, dice lo mismo: «Este navegador no mantiene la pantalla encendida: déjala siempre encendida en los ajustes de la tablet.».

### En el PC (Comedor)

La ficha de la mesa agrupa los platos con la misma regla que Costiña Cocina (la tabla común, abajo), así que cambia un poco:
- la línea de un comensal ya no repite una restricción de **toda la mesa**: ya sale en la franja de restricciones de la ficha;
- un «No le afecta» bajo una restricción **solo de toda la mesa** deja de ser una línea: no cambia el plato;
- un plato **para compartir** dice de quién es la alergia: «Tabla · Toda la mesa · ⚠ ALERGIA marisco (grave) del Comensal 3»;
- lo adaptado, lo rehecho, una decisión desconocida y lo que queda por revisar siguen teniendo siempre su línea.

### Criterios de «más intuitivo» (sección 2 del plan del 22-10)

| Tarea | Antes (E4e) | Ahora |
|---|---|---|
| Marcar lista «Merluza × 4» en una estación | una línea por comensal: 4 órdenes, con la pantalla bloqueada entre una y otra | «Listo»: **un** toque, **una** orden |
| Saber qué tiene que hacer una estación | sus líneas mezcladas con las atenuadas de las demás estaciones | solo lo suyo, y «También en: Cocina fría» |
| Saber en el pase qué falta | recorrer líneas por comensal de todas las estaciones | una tarjeta por mesa: «Cocina caliente: lista · Cocina fría: 1 de 2 platos» y «Falta Cocina fría» |
| Una estación sin tablet | el pase tenía «Empezar» y «Marcar lista» en cada línea de todas las estaciones | «Más opciones» › «Marcar Cocina fría como lista»: **una** orden |
| Ver qué se retrasa | «enviado hace 27 min», sin aviso | «Lleva 27 min» en ámbar oscuro (ámbar desde 15) |
| Enterarse de una comanda nueva | mirar la pantalla | suena, tras «Empezar turno» |
| Anotar la decisión sobre una alergia | escribir la nota entera | «No lo lleva», «Sin gluten» o «Cambiado por …» y un toque |

## La regla que cambia: D6.4 → ADR-017

D6.4 (21-09) decidió, con el promotor, que **una estación viera el pase entero**, con lo de las demás atenuado, rotulado «(otra estación)» y sin botones. C3d lo sustituye: **una estación ve solo su trabajo, y el pase la mesa entera.**

**Por qué:**
- **Lo pidió el promotor el 22-10:** comandas por mesa, platos agrupados, la alergia en rojo con su adaptación, el tiempo y un botón grande «Listo»; sin líneas por comensal, salvo las de alergias; y el pase ve la mesa entera y valida.
- **Es la práctica del sector:** cada estación de preparación ve sus platos y una pantalla de expedición, el pase, ve el pedido entero (Toast, Square KDS, Lightspeed, Oracle MICROS, Qamarero; fuentes en el plan del 22-10).
- **Es seguro:** las alergias de cada plato siguen en la comanda de la estación que lo cocina, nunca atenuadas, y lo que se pinta nunca da permisos. El servidor sigue siendo la frontera: la prueba de navegador comprueba `wrong_station` con el token del propio dispositivo.

**Cambian también tres frases de los límites de D6.4** (`docs/native/D6.4-pwa-kds.md`):
- «sin alarmas por tiempo» → ámbar a los 15 minutos y ámbar oscuro a los 25, nunca rojo;
- «sin sonido […] al entrar un pase» → suena tras «Empezar turno». Las notificaciones del sistema siguen sin existir;
- «no hay vista […] de "lo siguiente" sin enviar» → el pase ve «Siguiente: Postre (sin enviar)». Las estaciones siguen sin ver nada sin enviar.

## Qué cambia

### Lógica sin DOM: `pwa/cocina.ts` (sustituye a `kitchen.ts`)

Todo es presentación de la lectura del tablero: nada habilita nada. Un botón existe solo porque el servidor anuncia su acción en esa elaboración o en ese pase, ya filtrada por papel y estación (D3.1, D4.3a, ADR-016).

- **`stationTickets`:** por mesa y pase, solo las elaboraciones de la estación, de los pases enviados o en preparación (nunca los sin enviar, los validados ni los de una mesa terminada o cancelada). La clave (`servicio:pase`) y el orden (`firedAt`, de la más antigua a la más nueva) son los de `kitchen.ts`. Cada comanda lleva sus platos, su restricción de toda la mesa, `alsoIn` (las demás estaciones), `readyIds` y `done`.
- **`readyIds` y `dishReadyIds`:** las elaboraciones que lleva un «Listo», solo las que anuncian `preparation-ready` y solo si el pase anuncia `preparations-ready`; como mucho 200 (`MAX_GROUP_PREPARATIONS`). `readyBody` pone la versión leída de esa mesa (F01).
- **`passTickets`:** la mesa entera, con `progress` por estación (platos listos de cada una), `missingStations`, `pendingReviews`, `reviews` (lo enviado por revisar que el servidor deja revisar aquí), `canValidate` (se anuncia `ready`), `marks` (una por estación que no es el pase, con lo que el servidor le deja marcar) y `next` (el primer pase sin enviar). Un pase validado con algo por revisar sigue aquí. `passDishIds` da el «Listo» de lo que se emplata en el propio pase.
- **`waitingForWaiter`:** lo validado, sin servir y sin nada por revisar, por `readyAt` y, a igual hora, por mesa y pase en orden ordinal (como `sala.ts`).
- **Platos:** `dishes` agrupa con `floor.ts dishGroups`, el espejo de `DishGroups.Of`: mismo nombre y misma estación, comparación exacta, en el orden de su primera aparición; la cantidad suma la de todas sus elaboraciones. `isException`, `lineRestrictions`, `exceptionLine` (con `REVIEW_PENDING_AT_PASS`, «Por revisar en el pase», y `reviewOutcome`, «Adaptado: …», como Sala y el PC), `wholeTable`, `dishNote` («(1 adaptada)», «(2 adaptadas · 1 por revisar)»), `dishState` y `dishAlarming`.
- **Palabras:** `progressText`, `missingText` («revisar la alergia» o «revisar las alergias», según cuántas restricciones distintas queden), `markLabel`, `nextText`, `alsoText`, `countText`, `DONE_BAND`, `doneText`.
- **El tiempo:** `AMBER_MINUTES = 15` y `DARK_AMBER_MINUTES = 25`; `timeTone` (neutro, ámbar, ámbar oscuro), `elapsedText` («Lleva N min», «Lleva menos de 1 min», «Hora de envío desconocida») y `readySince` («listo hace 3 min»). Nunca negativo: el reloj de la tablet puede ir por detrás del servidor.
- **El sonido:** `TicketWatch` (como `ReadyWatch` de `sala.ts`) da, tras cada lectura correcta, las claves que no estaban en la anterior; la primera lectura no da ninguna. `ringKeys` son las de la rejilla (en una estación, las comandas por hacer; en el pase, todas sus comandas) y `shouldChime` exige «Empezar turno». Sale de la relectura, nunca del contenido del aviso del canal (D2).
- **`version_conflict`:** `versionConflict` reconoce el rechazo; `CONFLICT_TEXT` lo dice en la comanda. Ya lo cerró `operations.ts`, sin aplicar nada, y la vista nunca reenvía.
- **De E4e, movido tal cual:** `presendReviews`, `restOfGuest`, `restBody`, `PRESEND_DECISIONS`, `MAX_GROUP_REVIEWS` y `reviewNoteOk`.
- **Atajos de la nota:** `NOTE_SHORTCUTS` («No lo lleva», «Sin gluten», «Cambiado por …»), `applyShortcut` (sustituye la nota) y `noteReady`, que no acepta una nota que es solo «Cambiado por». `REVIEW_DECISIONS` de lo enviado dice «No le afecta», «Adaptado» y «Rehacer».
- `pwa/kitchen.ts`, `tests/kitchen.test.ts` y `labels.ts reviewLabel` («PENDIENTE DE REVISIÓN POR COCINA», «Revisada: …») se retiran.

### La tabla común `kitchen-cases.json` y `DishGroups`

- **`dotnet/tests/fixtures/kitchen-cases.json`**, nueve casos de platos agrupados, leída por vitest (`tests/cocina.test.ts`, contra `cocina.ts`) y por ClientChecks (contra `Costina.Client/DishGroups.cs`), como `floorplan-cases.json`:
  - mismo nombre y misma estación juntos, en el orden de la primera aparición; el mismo nombre en otra estación es otro plato;
  - la cantidad suma la de todas sus elaboraciones, también la de un plato para toda la mesa;
  - comparación exacta: otra mayúscula u otro espacio es otro plato;
  - toda la mesa: su restricción nunca se repite en la línea; un «No le afecta» bajo ella no es una excepción, lo adaptado sí, y la restricción de un comensal se escribe;
  - lo que está por revisar va siempre aparte, también sin ninguna restricción (una restricción retirada vuelve a pedir decisión);
  - un plato para compartir recibe la restricción de cualquiera: va aparte, con la del comensal escrita y sin la de toda la mesa;
  - «Rehecho» y una decisión desconocida van aparte;
  - el estado del plato;
  - un pase sin elaboraciones no tiene platos.
- Un apartado o un campo que un lado no conoce es un fallo **en los dos**. Para cambiar una regla, se cambia en la tabla y en las dos implementaciones.
- **`DishGroups`** se alinea con la tabla: `IsException` (lo pendiente, toda decisión que no sea «No le afecta», también una desconocida, o una restricción de un comensal), `LineRestrictions` (solo las de un comensal) y `ExceptionLine`, que dice «del Comensal N» en un plato para compartir.

### Pantalla: `pwa/views/KitchenView.vue`, reescrita

- **Estación:** rejilla de comandas para una tablet en horizontal (1180 × 820), leída a uno o dos metros: «Mesa 4 · Principal» de 24 px, platos de 22 px con la cantidad de 28 px, las líneas de alergia de 18 px en negrita, el tiempo de 18 px y botones de 64 px. Nada operativo por debajo de 16 px. La franja «Hecho · esperando al pase», al pie.
- **Pase:** la columna lateral de 400 px a la derecha desde 1000 px de ancho (arriba en una pantalla más estrecha), y las comandas a la izquierda. Desde 1000 px la columna es `sticky` y como mucho del alto de la pantalla: «Por revisar antes de enviar» se desplaza dentro y «Esperando al camarero» queda al pie, `sticky` también abajo para verse al abrir la página, cuando la columna aún empieza bajo la cabecera.
- **Las notas de revisión** (lo enviado, lo de antes de enviar y «No le afecta a nada más») se borran solo después del resultado y si la orden se aplicó o quedó pendiente (`cocina.ts`, `noteSpent`). Con un rechazo, también un `version_conflict`, la nota sigue escrita.
- **Cada toque es una orden** (D6.3, D6.7), con la versión leída: «Listo» y «Marcar … como lista», `preparations-ready`; «Validar pase», `ready`; cada revisión, `review-preparation`; «No le afecta a nada más», `review-preparations`. Todos los botones se apagan mientras hay una orden en curso o sin confirmar (`operations.ts`).
- **`pwa/turno.ts`**, nuevo, sin globales del navegador (el audio y la pantalla llegan como dependencias): `Shift` crea el audio dentro del toque, suena una vez y pide la pantalla encendida; `chime` (dos notas, 880 Hz y 1318,5 Hz, triangulares, menos de medio segundo); `screenSupport` (`available`, `http` o `unsupported`); las palabras de la franja. El turno es uno por página cargada, fuera del componente: sobrevive a que la pantalla se desmonte sin conexión. El audio se guarda fuera de lo reactivo, porque un `AudioContext` envuelto rompe sus métodos.
- **`style.css`:** un bloque propio de cocina con esos tamaños y los tonos del tiempo (`--time-amber-*`, `--time-late-bg`), nunca rojos; las filas de «Hecho» alineadas arriba. También de 64 px los atajos de la nota, «Más opciones», «Actualizar» y los botones de la orden sin confirmar. Lo que queda por revisar, en ámbar antes y después de enviar. Tema claro, acento `#0E7A5F` en los botones, rojo solo para las alergias que alarman y ámbar para lo pendiente y el tiempo. Sin estilos en línea: la CSP no cambia.
- **`StatusPanel.vue`** gana un hueco en la fila de la conexión, donde Costiña Cocina pone las comandas y la hora. Sala no pone nada.
- **`data-testid` de la pantalla:** `ticket-<mesa>-<pase>`, `elapsed`, `dish-<mesa>-<pase>-<n>`, `do-listo-<mesa>-<pase>` y `do-listo-<mesa>-<pase>-<n>`, `also-…`, `conflict-…`, `done-band` y `done-…`, `progress-…`, `missing-…`, `next-…`, `more-…`, `do-mark-<mesa>-<pase>-<estación>`, `do-ready-…`, `review-…` y `do-review-<decisión>-…`, `waiting` y `waiting-…`, `shift`, `do-shift-start`, `shift-on`, `kitchen-count`, `kitchen-clock` y `presend-empty`. Siguen los de E4e (`presend…`, `do-presend-…`). Desaparecen los de cada elaboración (`prep-…` y `do-preparation-…`).

### Pruebas de navegador: el proyecto `tableta`

- **`playwright.config.ts`:** proyecto `tableta`, 1180 × 820, táctil, Chromium (el único de la CI). Corre **solo** `kitchen.spec.ts` (`testMatch`), que sale de los proyectos `desktop` y `mobile` (`testIgnore`). Así la historia de cocina corre una vez por ejecución en lugar de dos: Playwright pasa de 30 a 29 pruebas.
- **`e2e/support.ts`:** `kitchenTable()` da M8, la mesa de cocina que el escritorio ya no usa; M5 queda libre en el móvil. `deviceName` lleva la etiqueta de cada proyecto (`pc`, `movil`, `tableta`).
- **`kitchen.spec.ts`, reescrito:** abre M8 al empezar y la deja libre en `finally` (revisa, marca, valida y sirve lo que quede como el PC, y la libera), también si la historia falla a medias, como `drinks.spec` con M1. El camarero va en un contexto de móvil (375 × 812).

## Decisiones tomadas al construir, para revisar

Pequeñas. Cualquiera se cambia si el promotor lo prefiere:
1. **«Empezar turno» es una franja encima de las comandas**, no una pantalla que las tape: una tablet que se reinicia en pleno servicio enseña las comandas y las alergias al momento. Sin el toque, solo falta el sonido.
2. **Un turno por página cargada:** sigue si la pantalla se queda un momento sin conexión; una recarga lo olvida.
3. **El toque de «Empezar turno» suena una vez**, como prueba.
4. **Suenan solo las comandas nuevas de la rejilla:** en una estación, las que tiene por hacer; en el pase, sus comandas. Un comensal nuevo en «Por revisar antes de enviar» no suena.
5. **Suena aunque Costiña Cocina no esté delante**, si el navegador lo permite (la vibración de Sala, en cambio, exige la página a la vista).
6. **En cocina la cantidad se escribe siempre**, también «× 1».
7. **Los platos se agrupan por el nombre exacto:** otra mayúscula u otro espacio son otro plato, como en E4e.
8. **«Falta revisar la alergia» o «revisar las alergias»**, según cuántas restricciones distintas queden por revisar en ese pase.
9. **Un pase validado con algo por revisar** sigue en las comandas del pase, no en «Esperando al camarero».
10. **Las comandas del pase van en filas**, de la más antigua a la más nueva y de izquierda a derecha, no en dos columnas independientes como en la maqueta: una comanda corta puede dejar un hueco.
11. **La franja «Hecho» enseña todas las comandas terminadas** (sin «y N más»), con sus alergias, al pie de la página y no fija.
12. **En el pase, la estación bajo cada plato** sale solo si la comanda tiene dos estaciones o más, como en la maqueta.
13. **Un atajo sustituye la nota**, no la completa. «Cambiado por …» deja el cursor al final. «No le afecta a nada más» ofrece solo «No lo lleva» y «Sin gluten».
14. **Un `version_conflict`** se dice en la comanda, además del aviso general de arriba.
15. **La tarjeta de un comensal** en «Por revisar antes de enviar» va con borde ámbar si ninguna de sus restricciones alarma (con una alergia, rojo).
16. **El estado de la conexión y la antigüedad de la lectura** van a 16 px en Costiña Cocina.
17. **«listo hace N min»** va en minúscula, detrás de «·».
18. **El PC cambia un poco** con la tabla común (arriba, «En el PC»).

**Diferencias con la maqueta que se dejan así:**
- «Validar pase: listo para servir» ocupa dos líneas en una columna de 349 px (20 px, negrita).
- En «Siguiente: Postre (sin enviar)», el nombre del pase no va en negrita.
- Cada plato de «Por revisar» es más alto que en la maqueta (etiqueta, nota, tres atajos y dos botones), porque la nota es obligatoria.
- Las comandas y la hora van en la fila de la conexión, no en la del título.
- El aviso de la conexión sin cifrar y «Opciones» quedan al pie de la página, como en el resto de Costiña Cocina.

**Revisión (27-10), lo que cambió tras las revisiones de seguridad, aspecto y pruebas:**
- **Lo enviado por revisar va en ámbar**, como lo de antes de enviar, y alineado con su línea. Antes era siempre rojo, también con una intolerancia leve; el rojo queda solo en la línea de la alergia que alarma.
- **La nota de una revisión ya no se borra en el mismo toque.** Tras un `version_conflict` sigue escrita y basta otro toque.
- **«Esperando al camarero» siempre a la vista en el pase.** Con algo por revisar caía bajo la pantalla de 1180 × 820, y verlo desplazaba las comandas. Ahora la columna lateral se queda y «Esperando al camarero» va al pie. Al abrir la página, si hay mucho por revisar, tapa el final de «Por revisar» hasta que se desplaza la página.
- **64 px también para** los atajos de la nota (eran 52), «Más opciones» (56), «Actualizar» y los botones de la orden sin confirmar (48, con letra de 15,5 px; ahora 17).
- La captura de la página entera del pase se cambia por la del pase desplazado: con la columna fija, una captura de página entera la pinta a medias.

## Qué NO cambia

- **El motor, el esquema (v8), la API, `grants.sql` y la CSP.** El envío a estaciones, igual.
- **El servidor decide los botones** (D3.1): lo que pinta cada pantalla (solo lo suyo, los grupos, los colores) es presentación, nunca permiso. La matriz de estación sigue en el servidor (`StationAllowed`, `Affordances`), y `wrong_station` se prueba con el token del propio dispositivo.
- **Una sola orden incierta durable, una por gesto** (D6.3, D6.7): «Listo», «Marcar … como lista», «Validar pase», una revisión o «No le afecta a nada más». Nunca una cola.
- **Un `version_conflict` relee y pide tocar otra vez**; nunca se reenvía solo con otra clave.
- **Tiempo real con avisos finos y relectura autoritativa** (D2): el sonido sale de la relectura, nunca del contenido del aviso.
- **Alergias y restricciones** nunca escondidas ni atenuadas, siempre con tipo, sustancia y gravedad; el rojo, solo para las que alarman.
- **Sin dinero en cocina** (ADR-007): ni claves de dinero en lo que llega ni palabras de caja en lo que se ve, ni siquiera dentro de otra palabra. Nombres, nunca códigos. Castellano con tildes.
- **El pase sigue siendo la única estación que valida y revisa**; «Rehacer», solo para lo enviado.

## Comprobaciones automáticas

Cifras en la punta del corte, con la versión `0.45.0-c3d` y la revisión, todas en este PC, una ejecución cada vez, en primer plano y sin administrador (registros en `.lab-test/`, los de este corte `c3d-*.log`):
- **vitest:** 467 en 20 ficheros (antes 401 en 19). `cocina.test.ts` (59, nuevo) y `turno.test.ts` (10, nuevo); `kitchen.test.ts` se retira. `npm run build` en verde.
  - `cocina.test.ts`: la tabla común; comandas de estación (solo lo suyo, el orden, «También en», «Listo» con solo lo anunciado, la franja «Hecho»); comandas del pase (progreso por estación, qué falta, «Siguiente», marcar por una estación, el «Listo» de lo emplatado en el pase); «Esperando al camarero»; los umbrales del tiempo, nunca rojos y siempre escritos; la comanda nueva para el sonido; los atajos de la nota; el `version_conflict` y la nota que solo se gasta con una orden aplicada o pendiente (`noteSpent`); sin palabras de caja, ni dentro de otra palabra.
  - `turno.test.ts`: el tono (dos notas, menos de medio segundo, sin ficheros de audio); nada suena antes del toque, y el toque empieza el turno, suena una vez y deja la pantalla encendida con https; la pantalla se pide otra vez al volver a verse la página; el texto de http, el de un navegador sin Wake Lock y el de uno sin Web Audio; sin globales del navegador y sin palabras de caja.
  - `views.test.ts`: ni «Empezar» ni `preparation(s)-start` en la pantalla de cocina, `kitchen.ts` retirado, ninguna nota borrada antes de su resultado, y el bloque de cocina de `style.css` (24, 22, 28 y 18 px, botones de 64 con los atajos, «Más opciones», «Actualizar» y los de la orden sin confirmar, nada por debajo de 16, un tiempo que nunca es rojo, las filas de «Hecho» alineadas arriba, lo que queda por revisar en ámbar antes y después de enviar, y la columna lateral del pase fija con «Esperando al camarero» al pie).
- **ClientChecks:** 288 (antes 277): la tabla común contra `DishGroups` y la línea del comensal del PC.
- **DesktopChecks:** en verde (cambian `Costina.Client` y la versión).
- **Batería completa** (`COSTINA_E2E=1 sh tools/agent/local-tests.sh`, puerto 5090), tras la revisión: `rc=0` en 312 s. Compilación con 0 advertencias y 0 errores; dominio 97/97; ClientChecks 288; las 14 suites HTTP (packaging 15 con 1 saltada, native 11, security 6/6, desktop_reads 9, affordances 8, restrictions 9, checkout 8, identity 10, pairing 18, station 11, organization 5, catalog 10, offers 8 y pwa_http 8); Realtime 13.
  - **Playwright, 29** (14 de escritorio, 14 de móvil y 1 de `tableta`) en 2,7 min. `kitchen.spec` tarda unos 11 s, ahora una vez y no dos (9 + 10 s antes).
  - **`pwa_http` tardó 165 s en local** (173 s antes de C3d). El job `postgres-http` de la CI tiene 20 minutos y tardaba unos 17: con una prueba de navegador menos no hace falta subir `timeout-minutes`.
- **`kitchen.spec`** (la historia de cocina, contra el motor real; en cocina, `checkScreen` mide a 64 px también los atajos, «Más opciones», «Actualizar» y los botones de la orden sin confirmar):
  - **antes de enviar:** Sala espera la revisión de cocina; sin nota no se puede decidir («No le afecta» ni «Adaptado»); el pase usa «Cambiado por …» (el cursor al final, y eso solo no vale) y después «No lo lleva» con «No le afecta a nada más», en una orden; Sala ve «Adaptado: …» y envía;
  - **el sonido:** la estación fría empieza el turno antes de que llegue la comanda: exactamente 2 notas al tocar, 2 con la comanda nueva y ninguna más (las notas se cuentan envolviendo `createOscillator`);
  - **solo lo suyo:** un plato, «También en: Cocina caliente» y ningún botón de validar, revisar ni marcar por otra estación;
  - **«Listo» y el `version_conflict`:** el PC empieza el plato caliente en el mismo momento; no se aplica nada, la comanda dice «toca otra vez» y sigue habiendo 1 POST hasta el siguiente toque, que va con otra clave y la versión releída: la versión sube en uno, las dos elaboraciones frías van en una `preparations-ready` y la comanda baja a «Hecho»;
  - **la frontera es el servidor:** con el token de la tablet fría, `wrong_station` en `preparation-start`, en la `preparations-ready` de grupo y en `ready`;
  - **una alergia con el pase enviado** sale escrita en «Hecho»;
  - **el pase:** «Cocina fría: lista», «Falta Cocina caliente y revisar la alergia» y «Siguiente: Segundo pase (sin enviar)»; revisa con nota, y un `version_conflict` en esa revisión (el PC empieza un plato caliente a la vez) no aplica nada, deja la nota escrita y un solo toque más la envía con otra clave; «Marcar Cocina caliente como lista» desde «Más opciones» es una orden; valida; «Esperando al camarero» hasta que se sirve;
  - ninguna palabra de caja ni clave de dinero, y ninguna violación de la CSP.
- **Lo que no se prueba en el navegador:** que se oiga de verdad, Wake Lock en un Chromium sin pantalla (solo se comprueba «Turno empezado») y la lectura a uno o dos metros. Van al guion de abajo.

### En rojo antes de su arreglo

Cada prueba nueva falló primero. Las roturas a propósito se restauraron desde una copia en el `scratchpad`, nunca con `git`.
- **La tabla común:**
  - ClientChecks con las comprobaciones nuevas y la regla de E4e todavía en `DishGroups`: 283 bien y 5 fallos (los casos de toda la mesa, de lo pendiente sin restricción, del plato para compartir y de «Rehecho» o una decisión desconocida, y la línea del comensal del PC);
  - vitest antes de existir `cocina.ts`: el fichero no cargaba; con `cocina.ts` devuelto a la regla de E4e, 4 fallos de 51;
  - romper un caso rompe los dos lados: con la cantidad de «Ostra» cambiada de 3 a 4, vitest 1 fallo y ClientChecks 287 bien y 1 fallo, el mismo caso; con un campo desconocido (`note`) en un plato, los dos fallan con «unknown field in the shared cases».
- **La pantalla:**
  - vitest con las pruebas nuevas escritas antes del código: 14 fallos (`passDishIds`, `countText`, `dishAlarming`, `ringKeys`, `versionConflict`, los atajos, `noteReady`, las palabras de lo enviado, «Empezar» y `kitchen.ts` en la pantalla, y los tamaños de `style.css`), y `turno.test.ts` no cargaba;
  - la franja «Hecho» contra el `style.css` anterior: 1 fallo;
  - `kitchen.spec` reescrito contra la pantalla anterior: fallaba en su primer paso nuevo, el atajo «No lo lleva».
- **Tres roturas de la pantalla, cada una con su fallo en `kitchen.spec`** (con el proyecto `tableta` solo):
  - la pantalla reenvía una vez tras un `version_conflict`: 2 POST con claves distintas, en la comprobación «nada se reenvía sin otro toque» (`kitchen.spec.ts:174`);
  - la estación suena con cada lectura: 6 notas en lugar de 4;
  - la estación vuelve a ver el pase entero (la regla de D6.4): 2 tarjetas de plato en lugar de 1.
- **`wrong_station`** no tiene prueba en rojo propia: es una regla del servidor que ya cubre `station_http`, y solo un cambio en el motor la haría fallar.
- **La revisión (27-10):**
  - vitest con las comprobaciones nuevas escritas antes del arreglo: 5 fallos (`noteSpent` no existía; las tres notas se borraban en el mismo toque; los atajos a 52 px; lo enviado por revisar sin ámbar; la columna lateral sin fijar);
  - `kitchen.spec` nuevo contra el motor con la pantalla anterior (proyecto `tableta` solo): con los objetivos de 64 px, fallo en «pass before sending» («Actualizar» de 48 px y los atajos de 52); sin esa medida, fallo en la nota tras el `version_conflict` de la revisión, que volvía vacía (`kitchen.spec.ts:248`);
  - la comprobación de que «No le afecta» también está apagado sin nota no tiene rojo: la pantalla ya lo hacía bien, y la prueba solo recupera lo que comprobaba la de antes.

## Capturas

En `docs/native/images/c3d/`, de `e2e/captures-c3d.spec.ts` contra el motor real, a 1180 × 820:
- [«Empezar turno» en la estación](images/c3d/cocina-empezar-turno-1180x820.png): Cocina caliente con la franja de «Empezar turno» y el texto de la conexión sencilla.
- [La estación](images/c3d/cocina-estacion-1180x820.png): los tres tonos del tiempo (27, 16 y 6 y 2 min), «Merluza á galega × 4 (1 adaptada)» con la alergia adaptada en rojo, «También en: Cocina fría», un plato ya listo y la franja «Hecho».
- [La franja «Hecho · esperando al pase»](images/c3d/cocina-estacion-hecho-1180x820.png), con una comanda con alergia.
- [El pase](images/c3d/cocina-pase-1180x820.png): el progreso de cada estación, «Falta …», «Más opciones» abierto con «Marcar Cocina caliente como lista» y «Por revisar antes de enviar».
- [«Por revisar antes de enviar»](images/c3d/cocina-pase-por-revisar-1180x820.png), con una nota hecha con «Cambiado por …».
- [El pase desplazado](images/c3d/cocina-pase-desplazado-1180x820.png): una intolerancia leve anotada con el pase ya enviado, con su revisión en ámbar bajo la alergia en rojo, y la columna lateral a la vista, con «Esperando al camarero» al pie.

**Cómo se hicieron:** tres menús creados por API dan los platos de la maqueta. El laboratorio envía todo en un minuto, así que, solo para las capturas, se atrasan `firedAt` y `readyAt` en la instantánea guardada con `psql` (el motor no se toca). A la tablet se le dice que la conexión no es segura, para que enseñe el texto que verán las tablets reales por http.

**Para repetirlas** hacen falta una base nueva y el motor compilado con el build actual de la PWA, y `psql` en el `PATH` (lo pone `local-tests.sh`):
1. `npm run build` en `frontend/pwa`.
2. La batería entera, o `dotnet build` de `src/Costina.Server`.
3. `C3D_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3d)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http`.

En la CI no corre nunca.

## Guion manual (solo en una instalación de LABORATORIO; 10 pasos)

**Nunca en un PC con una instalación real**, ni sobre sus servicios, puertos o carpetas. Hace falta:
- un PC de laboratorio con una instalación de prueba en `0.45.0-c3d`: la demostración (`/DEMO=1`, D5.6) o el laboratorio de `docs/native/lab-engineering.md`, con la **conexión sencilla** activa (B.2) y, si lo hay, también por https;
- **tres tablets con Costiña Cocina**, emparejadas como dice el documento de C3c: una en **Cocina caliente**, otra en **Cocina fría** y otra en el **Pase**, si se puede las que se vayan a usar (anota modelo, sistema y navegador);
- **un móvil con Costiña Sala** y el PC con el Comedor;
- un menú con platos de Cocina caliente y de Cocina fría en el mismo pase (en el laboratorio, el de degustación).

Pon cada tablet donde va a estar, a uno o dos metros de quien cocina.

1. **«Empezar turno».** En las tres tablets, recarga Costiña Cocina: arriba sale la franja con «Empezar turno». Tócalo: suena una vez y dice «Turno empezado: suena con cada comanda nueva.» y, por http, que se deje la pantalla encendida en los ajustes. **Anota** si se oye desde el sitio de quien cocina, con la cocina en marcha, y con qué volumen. En un iPad, prueba también con el modo silencio puesto y anota si suena.
2. **La pantalla encendida.** Por http, pon en los ajustes de cada tablet que la pantalla no se apague (por ejemplo, iPad: Pantalla y brillo › Bloqueo automático › Nunca). Por https, sin tocar los ajustes, debe decir «La pantalla se queda encendida.». Deja las tablets 10 minutos sin tocar: siguen encendidas.
3. **Antes de enviar.** Desde el PC, abre una mesa de 4 personas con el menú de degustación y una alergia: Comensal 2 · Alergia · frutos secos · Grave. En el pase, en «Por revisar antes de enviar», en el primer plato del Comensal 2, toca «Cambiado por …»: la nota dice «Cambiado por » con el cursor al final y «Adaptado» sigue apagado. Escribe «sin almendra» › «Adaptado». En «Nota para todo lo que queda», «No lo lleva» › «No le afecta a nada más». Las estaciones no enseñan nada de esa mesa.
4. **Llega la comanda.** En Sala, «Enviar siguiente pase», **una vez**. Las tres tablets suenan **una vez**. Desde uno o dos metros se lee: «Mesa N · <pase>», «Lleva menos de 1 min», los platos con su cantidad y, bajo el plato del Comensal 2, «Comensal 2 · ALERGIA frutos secos (grave) · Adaptado: sin almendra» en rojo. Cocina caliente ve solo lo caliente y «También en: Cocina fría»; Cocina fría, al revés. El pase ve la mesa entera, el progreso de cada estación y «Siguiente: <el pase que viene> (sin enviar)».
5. **«Listo».** En Cocina fría, «Listo», **un toque**: la comanda baja a «Hecho · esperando al pase». En el pase, «Cocina fría: lista» y «Falta Cocina caliente». En el PC, los platos fríos están listos una vez.
6. **El pase marca por una estación.** Deja apartada la tablet de Cocina caliente. En el pase, en esa comanda, «Más opciones» › «Marcar Cocina caliente como lista»: en la tablet de Cocina caliente, la comanda baja a «Hecho». En el pase sale «Validar pase: listo para servir»: tócalo. En Sala sale «Mesa N · <pase> listo para servir». En el pase, la mesa pasa a «Esperando al camarero · listo hace menos de 1 min», y la franja «Hecho» de las dos estaciones se vacía. En Sala, «Servido»: en el pase, «Esperando al camarero» se vacía.
7. **Una alergia con el pase ya en cocina.** Envía el siguiente pase desde Sala (las tablets suenan). Desde Sala, en la ficha, «Anotar alergia o intolerancia»: Comensal 1 · Intolerancia · lactosa · Leve. En las estaciones, bajo los platos del Comensal 1, «Comensal 1 · Intolerancia lactosa (leve) · Por revisar en el pase», en ámbar. En Cocina fría, «Listo»: la comanda baja a «Hecho» con esa línea. En el pase, bajo el plato frío del Comensal 1, la nota con sus atajos y «No le afecta», «Adaptado» y «Rehacer»: escribe «sin nata» › «Rehacer». La comanda vuelve a la rejilla de Cocina fría, que **suena otra vez**. Decide los demás platos del Comensal 1 con «No lo lleva» › «No le afecta».
8. **El tiempo.** Deja una comanda sin marcar 25 minutos (es lo único que pide esperar): «Lleva N min» en gris hasta 14, en ámbar desde 15 y en ámbar oscuro con letra blanca desde 25. Nunca en rojo.
9. **Recargar.** Recarga la tablet de Cocina caliente y **no** toques «Empezar turno». Envía otro pase: la comanda sale, pero no suena. Toca «Empezar turno»: suena una vez, y la siguiente comanda ya suena.
10. **Repaso.** En ninguna pantalla de cocina aparecen «precio», «cuenta», «pago», «saldo» ni «€», ni códigos de mesa o de estación: solo nombres.

| Paso | Cocina caliente | Cocina fría | Pase | Sala y PC | Notas |
|---|---|---|---|---|---|
| 1. «Empezar turno» | | | | | ¿se oye desde el sitio?: · volumen: · iPad en silencio: |
| 2. La pantalla encendida | | | | | http o https: · ajuste usado: |
| 3. Antes de enviar | | | | | |
| 4. Llega la comanda | | | | | ¿se lee a 1–2 m?: |
| 5. «Listo» | | | | | |
| 6. El pase marca por una estación | | | | | |
| 7. Alergia con el pase en cocina | | | | | |
| 8. El tiempo | | | | | |
| 9. Recargar | | | | | |
| 10. Repaso | | | | | |

**Opcional, la guarda contra bajar de versión** (solo en el laboratorio): con los datos ya servidos por `0.45.0-c3d`, ejecuta el instalador de `0.44.0-e4e`. Se para con `SETUP FAILED`, sin hacer copia, y el servicio queda parado (los programas viejos quedan en `C:\Program Files\Costina`: es el agujero que explica E4e). Ejecuta de nuevo el instalador de `0.45.0-c3d`: todo vuelve, sin perder nada.

## Vuelta atrás

- **El servidor:** C3d no cambia nada de lo guardado, pero desde E4e el instalador no deja bajar de versión. Volver a `0.44.0-e4e` es restaurar la copia previa a la actualización, en un equipo limpio y con el instalador anterior (ver «Antes de instalar»).
- **Las tablets:** no guardan nada nuevo. Con el servidor anterior, Costiña Cocina vuelve a la pantalla de E4e al recargar, sin emparejar de nuevo.

## Límites y diferido

- **«Listo» no se deshace** en el motor: sería una acción nueva y aditiva. Hasta entonces, la comanda sigue en «Hecho» y el pase no valida sin ver el plato.
- **La franja de totales** del turno («Merluza: 11 en marcha»): diferida.
- **La hora por estación** («Cocina caliente: lista hace 3 min»): `PreparationView` no guarda horas. Por eso tampoco «Hecho» lleva hora.
- **El modo quiosco** y la reapertura tras reiniciar la tablet: Hito 7.
- **Los umbrales del tiempo en la configuración:** por ahora son constantes de `cocina.ts` (15 y 25).
- **El tiempo usa el reloj de la tablet** frente a la hora de envío del servidor: un reloj desajustado lo desplaza (nunca se pinta negativo).
- **El sonido exige el toque tras cada recarga**, y no hay notificaciones del sistema: con la tablet apagada o la página cerrada no suena nada.
- **Por http no hay Wake Lock:** la pantalla encendida depende de los ajustes de la tablet hasta el certificado público.
- **Que se oiga en un iPad con el modo silencio** no está comprobado: lo dice el guion.
- **La Sala** (`TableCard.vue`) tiene todavía su propia regla de E4e para las líneas de excepción, no alineada con `kitchen-cases.json`: va a C3e.
- **Las capturas de cortes anteriores** (`captures.spec.ts`, `captures-c3c.spec.ts`, `captures-e4e.spec.ts`) no se han vuelto a ejecutar contra la pantalla nueva; si se repiten, sacarán la cocina de C3d.
- **Problemas de antes, vistos y sin tocar:** `npm run build` no comprueba los tipos de `e2e/` (un `tsc` a mano encuentra un error antiguo en `banner.spec.ts:19`, el doble de `navigator.vibrate`), y la prueba de `pairing.spec` del móvil que sobrevive a una recarga tarda entre 3 y 18 s, lo que mueve `pwa_http` unos 15 s de una ejecución a otra.
- **A C3e** (`0.46.0-c3e`, Costiña Sala): «Mis mesas» con `openedBy` y `openedStation`; abrir con alergias desde Sala en tres toques; «Servido · Principal» en el botón grande; «Carta y menús»; el «Preguntar al servidor» de una orden de otra instalación; y la Sala con la misma regla de excepciones que la tabla común.
