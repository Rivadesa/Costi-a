# E4e — Alergias antes de enviar, órdenes de grupo y quién abrió la mesa

Fecha: 2026-10-26. Rama `feat/e4e-alergias-antes-de-enviar` sobre `develop` (`9e6befe`, `0.43.0-c3c`). Es la sección 4.2 del plan `docs/plans/2026-10-22-sala-y-cocina.md`, que conserva todo lo aprobado en la sección 4.5 de `docs/plans/2026-10-15-menus-carta-y-bodega.md`, bajada a pasos en `docs/plans/2026-10-26-e4e-alergias-antes-de-enviar.md`. Versión **`0.44.0-e4e`**. Decisión escrita: **ADR-016** (`docs/ADR/ADR-016-revision-de-alergias-antes-de-enviar.md`), que sustituye a D3.5 en lo que decía de «solo lo enviado» y del bloqueo global, y cambia D4.3a solo para el pase.

Respuestas del promotor que fija este corte:
- **15-10, confirmado el 22-10:** cocina decide cada plato de un comensal con alergia o intolerancia **antes** de que salga el pase, y el pase espera. También las intolerancias leves y las preferencias, que cocina despacha con un toque.
- **22-10, pregunta 2:** el pase puede marcar «Listo» por una estación con la tablet estropeada.
- **26-10, «sube y mezcla»:** permiso para subir y mezclar E4e cuando la CI esté en verde.

**Sin cambio de esquema** (sigue la v8) y **la CSP no cambia**. Pero **sí cambian las reglas con que se leen las mesas guardadas, y no hay vuelta atrás** (abajo).

## Antes de instalar: sin vuelta atrás

**Léelo antes de poner `0.44.0-e4e` en la instalación de Costiña.**

- **Una vez que `0.44.0-e4e` guarda una mesa, una versión anterior ya no la lee.** Las mesas llevan ahora quién las abrió, y pueden tener platos por revisar en pases sin enviar o con la mesa «Sentados». El motor de `0.43.0-c3c` rechaza todo eso. Por eso no se puede volver a `0.43.0-c3c` instalándola encima.
- **Desde `0.44.0` el instalador no deja bajar de versión.** Por ejemplo, cuando los datos ya los haya usado `0.45.0`, el instalador de `0.44.0-e4e` se parará con `SETUP FAILED`, sin hacer copia ni tocar nada, y el servicio no arrancará con los programas viejos.
- **Volver atrás de verdad** es restaurar, en un equipo limpio y con el instalador anterior (`/RESTORE=`), la copia que el instalador de `0.44.0-e4e` hace justo antes de actualizar. Se pierde todo lo hecho después de actualizar.
- **Nunca instales un instalador anterior a `0.44.0` encima de datos de `0.44.0`.** Esos instaladores no tienen la guarda, y su motor no leería las mesas.
- **Actualiza con el comedor cerrado, sin mesas abiertas.** Una mesa abierta al actualizar conserva su estado: sus pases sin enviar no quedan «por revisar» hasta que se anote o se quite otra alergia en esa mesa.
- Sigue valiendo lo de antes: desde C3b las cuentas tampoco tienen vuelta atrás, y un dispositivo emparejado con C3c tendría que emparejarse de nuevo en una versión anterior.

## Lo que notarán el promotor y la cocina

### La regla nueva, en palabras llanas

**Ningún plato de un comensal con alergia, intolerancia o preferencia sale de cocina sin que el pase haya decidido qué se hace con él.**
- La decisión se toma **antes** de enviar el pase, también con la mesa recién sentada. Antes, solo se pedía para lo ya enviado, y lo que no había salido iba a cocina con el aviso escrito pero sin ninguna decisión.
- Cada plato afectado queda **«Por revisar en cocina»** hasta que el pase dice «No le afecta» o «Adaptado: …», siempre con una nota. La nota de «Adaptado» es el plato adaptado («vieira en lugar de ostra»); el nombre del plato no cambia.
- **Solo espera el pase afectado.** Antes, cualquier plato por revisar, de cualquier pase, paraba «Enviar siguiente pase». Ahora un pase ya revisado sale aunque el postre siga por revisar.
- **Afectados son todos los platos de ese comensal**, no solo los que llevan la sustancia: Costiña no sabe qué alérgenos lleva cada plato. Si la alergia es de toda la mesa, todos los platos; un plato para compartir recibe la alergia de cualquiera. Con un menú degustación de 7 pases, son 7 platos por comensal, y el pase los despacha de una vez con «No le afecta a nada más».
- **Lo que no se va a cocinar no se revisa:** al omitir un pase, o al cancelar una mesa sin empezar, lo que quedaba por revisar de ese pase desaparece. Las decisiones ya tomadas se guardan.
- Si se **quita** una alergia, sus platos sin servir vuelven a quedar por revisar y el pase decide otra vez: lo adaptado puede volver a ser el plato de siempre.
- **«Rehacer»** sigue siendo solo para lo que ya salió.

### En el PC (Comedor)

- **«Abrir mesa» pregunta las alergias:** apartado «¿Alergias o intolerancias?», con una fila por comensal y «Añadir alergia o intolerancia». En cada fila: «¿Para quién?» (un comensal o «Toda la mesa»), «Tipo» (Alergia por defecto), «¿A qué?» y «Gravedad» (Grave por defecto). «¿Para quién?» empieza vacío: «Toda la mesa» nunca va por defecto, y sin elegir la mesa no se abre. Todo va en **la misma orden** que abre la mesa.
- **La ficha agrupa los platos del pase elegido:** «Ostra × 4», con su estación y su estado. Debajo, una línea por cada comensal afectado: «Ostra · Comensal 3 · ⚠ ALERGIA marisco (grave) · Adaptado: vieira en lugar de ostra», o «· Por revisar en cocina». En rojo si alarma o está por revisar; si no, en ámbar.
- **Cada pase dice cuántos platos quedan por revisar:** «⚠ 2 platos por revisar en cocina», también en los pases sin enviar.
- **La cuenta es de platos:** «0 de 2 platos listos», no de líneas por comensal.
- **El responsable también revisa desde el PC**, por plato agrupado: las líneas por revisar salen marcadas; se escribe la nota y se elige «No le afecta» o «Adaptado». Va en una orden. Para decidir distinto a dos comensales, se desmarca uno y se decide en dos toques.
- **«Empezar plato» y «Marcar plato listo»** actúan sobre el plato agrupado entero, en una orden.
- **El botón grande:** con la mesa «Sentados», «Enviar siguiente pase» empieza el servicio y envía el primer pase **en un toque**; «Iniciar servicio» queda debajo. Mientras el pase que va a salir espera la revisión, el botón grande se apaga y dice **«Esperando la revisión de cocina»**.

### En Costiña Cocina (la tablet del pase)

- Arriba del todo, siempre: **«Por revisar antes de enviar»**. Por mesa y comensal, con su alergia escrita (tipo, sustancia y gravedad), los platos de los pases **sin enviar** que esperan una decisión. Las estaciones no lo ven: es trabajo del pase.
- En cada plato, la nota obligatoria y dos botones: **«No le afecta»** y **«Adaptado»**. Cada toque es una orden.
- Con dos platos o más del mismo comensal, **«No le afecta a nada más · N platos»**, con su nota: decide todo lo que queda de ese comensal **en una sola orden**. El plan lo dejaba para C3d; se adelanta aquí porque, con un menú de 7 pases, son 7 decisiones por comensal.
- Lo enviado sigue en su comanda, como siempre: «PENDIENTE DE REVISIÓN POR COCINA», con «No afecta», «Adaptar» y «Rehacer».
- **El pase puede marcar «Empezar» y «Marcar lista» en los platos de cualquier estación** (respuesta 2). Si se estropea la tablet de la cocina caliente, el pase marca por ella. Hasta C3d, esos botones salen en todo lo que falta de todas las estaciones; C3d los llevará a «Más opciones» de cada comanda.
- Las demás estaciones siguen igual: marcan solo lo suyo.

### En Costiña Sala (el móvil del camarero)

- **Desde que se sientan**, la ficha enseña bajo cada plato de los pases sin enviar la línea del comensal afectado: «Por revisar en cocina» o «Adaptado: …».
- **El botón grande** es «Enviar siguiente pase», también con la mesa recién sentada: un toque empieza el servicio y envía el primer pase. «Iniciar servicio» pasa a «Más opciones».
- Mientras el pase que va a salir espera a cocina, el botón grande se apaga con **«Esperando la revisión de cocina»**. El aviso rojo de la ficha dice la regla nueva: «SIN REVISAR POR COCINA: cocina decide cada plato «Por revisar en cocina» antes de que salga su pase; mientras tanto, ese pase espera y no se valida ni se sirve.».
- **Abrir la mesa con las alergias desde Sala** llega en C3e. Hoy, desde Sala, se abre y después se anotan en la ficha («Anotar alergia o intolerancia»); el efecto es el mismo, en dos órdenes.

### Quién abrió la mesa

Cada mesa nueva guarda **desde qué dispositivo, o qué persona, se abrió** y su estación. Todavía no se ve en ninguna pantalla: lo usará «Mis mesas» de Costiña Sala (C3e). Las mesas abiertas antes de actualizar no lo saben.

### Criterios de la sección 2 del plan del 15-10

| Tarea | Antes | Ahora |
|---|---|---|
| Sentar una mesa con una alergia | abrir mesa y, después, anotar la alergia: dos órdenes | en el PC, una sola orden («Abrir mesa» con «¿Alergias o intolerancias?»). En Sala, en C3e |
| Que cocina decida el plato del comensal alérgico | solo después de enviar; antes, el plato salía con el aviso y sin decisión | el pase decide antes de enviar, plato a plato o «No le afecta a nada más» en **un** toque |
| Enviar el primer pase | «Iniciar servicio» y después «Enviar siguiente pase»: dos toques | «Enviar siguiente pase»: **un** toque |
| Una alergia en el postre con los aperitivos ya revisados | nada salía hasta revisarlo todo | salen los aperitivos; solo espera el postre |
| Una estación sin tablet | solo el PC con la contraseña del responsable podía marcar por ella | el pase marca por ella |

## Nunca bajar de versión: las guardas del instalador y de `restore`

Estaban abiertas desde C3b, y E4e las necesitaba porque ni C3b ni E4e tienen vuelta atrás.

- **La señal es una marca en los propios datos**, no la última copia. Cada motor en una instalación deja al arrancar una marca «servido por `0.44.0-e4e`». Viaja con las copias y no cambia el esquema, como la marca de demostración de D5.6. El laboratorio no la deja.
- **Unos programas más antiguos que la marca más nueva se niegan antes de escribir nada**, con el mensaje en castellano y en inglés:
  - al **arrancar** el servicio;
  - al hacer la **copia**, que es el primer paso del instalador sobre una instalación que ya existe. Ahí el instalador para con `SETUP FAILED`, sin copia y sin `upgrade`;
  - en `upgrade`, `load-demo`, `init-lab`, `create-user`, `first-user` e `import-catalog`.
- **`restore` rechaza la copia de unos datos que ya sirvió un motor más nuevo**, antes de tocar la base. Lo dice el manifiesto de cada copia, que ahora lleva `servedBy`: los motores que habían servido esos datos. Por eso la copia que el instalador nuevo hace justo antes de actualizar **sí** se restaura con los programas anteriores: es la vuelta atrás. La copia diaria de un motor más nuevo, no.
- Solo cuenta la parte numérica de la versión, comparada como números (`0.10` es más que `0.9`). Una versión ilegible cuenta como más nueva: ante la duda, no se toca nada.
- **Un agujero que queda:** el instalador copia los programas antes de que el motor se niegue. Tras un intento de bajar de versión, los datos están intactos y el servicio parado (y no arranca), pero en `C:\Program Files\Costina` quedan los programas viejos hasta que se vuelva a ejecutar un instalador de la versión buena o una más nueva. El mensaje del instalador y el LEEME lo dicen.
- **Solo protege desde `0.44.0`.** Un instalador anterior no tiene la guarda.

## Qué cambia

### Motor: la revisión antes de enviar (dominio)

- **Marcar.** Al declarar o retirar una restricción se marcan `reviewPending` todas las elaboraciones a las que aplica de cualquier pase que aún se vaya a cocinar: pendiente, enviado, en preparación o listo. Nunca uno servido ni omitido. La regla de a quién aplica no cambia (`Restrictions.Applies`): toda la mesa recibe todas, y una elaboración sin comensal recibe cualquiera.
- **Nacer por revisar.** Una elaboración que nace para un comensal con restricción nace por revisar:
  - al abrir, con las restricciones de la misma orden;
  - con `choose`, que sustituye la elaboración (también si el comensal cambia de idea);
  - con `add-dish`, también un plato sin comensal.
- **Revisar.** `review-preparation` se admite, y se anuncia, en pases sin enviar y con la mesa «Sentados». `remake` en un pase sin enviar da `409 nothing_to_remake`: todas las reglas del dominio salen como 409 (`Server/Program.cs`), y el plan del 15-10 decía 422.
- **Bloqueo por pase.** `fire-next` se niega con `restrictions_unreviewed`, y deja de anunciarse, solo si el pase que va a salir tiene algo por revisar. Con un pase activo que tiene algo por revisar sigue diciendo `restrictions_unreviewed`, como antes. Validar y servir no cambian.
- **Retirar.** `skip` y `cancel-unstarted` retiran las revisiones pendientes de lo que ya no se cocina. El evento (`course.skipped`, `service.cancelled`) lleva `reviews_withdrawn` solo si retiró alguna; si no, es el de siempre. Lo de `cancel-unstarted` es una concreción del corte: sin ella, una mesa cancelada se quedaría «por revisar» para siempre y su instantánea no se podría leer.

### Motor: abrir con alergias y enviar desde «Sentados»

- **`open` acepta `restrictions`** (`[{guestPosition?, kind, substance, severity}]`, campo nuevo de `OpenRequest`) y las declara en la misma orden y transacción: una sola orden incierta.
  - Un tipo o una gravedad desconocidos, o vacíos, dan `409 invalid_restriction`; una restricción mala no abre nada. Lo demás, como `declare-restriction`: comensal dentro de la mesa (`invalid_guest`), sustancia obligatoria y sin duplicados (`duplicate_restriction`).
  - `declare-restriction` no cambia: un valor desconocido sigue dando `422 invalid_request`.
  - Auditoría: un `restriction.declared` por restricción, justo después de `service.created`. Una apertura sin restricciones deja los mismos eventos, en el mismo orden, que antes.
- **`fire-next` desde «Sentados»** empieza el servicio y envía el primer pase en una orden (`service.started` y `course.fired`), todo o nada: si el pase no puede salir (`restrictions_unreviewed`, `choice_missing`, `course_empty`), la mesa sigue «Sentados». `start` sigue existiendo; con la mesa «Sentados», el servidor anuncia los dos.

### Motor: órdenes de grupo

- **`DiningCommand`** gana `itemIds` e `items` (`ReviewItem`: `courseId`, `itemId`, `decision`, `note`). Son campos nuevos y opcionales: las órdenes de antes valen igual.
- **`preparations-start` y `preparations-ready`** `{expectedVersion, courseId, itemIds}`: de 1 a 200 elaboraciones de ese pase.
- **`review-preparations`** `{expectedVersion, items}`: de 1 a 60 decisiones, cada una con su nota. El menú entero de un comensal, de todos sus pases, en una orden.
- **Todo o nada, con una sola subida de versión** y un evento por elaboración, con los tipos de siempre (`course_item.started|ready`, `restriction.reviewed`). El dominio comprueba todas las elaboraciones antes de tocar ninguna. Las órdenes individuales son ahora el grupo de una, con las mismas reglas, errores y eventos.
- **Rechazos:** más de 200 o de 60 da `409 too_many_items`. Una lista vacía, una elaboración repetida en la misma orden o una decisión incompleta es una petición mal formada: `422 invalid_request`. Los identificadores van tal cual, sin recortar: un espacio de más no puede esconder la estación de una elaboración.
- **Anuncio, en el pase de la mesa:** cada orden de grupo sale si alguna elaboración anuncia su acción individual **después** de filtrar por papel y estación (`Affordances.Filter`). Es la primera acción que se deduce dentro del filtro, y el código lo dice.

### Motor: la matriz de estación, sin huecos

- `DiningOperations.StationAllowed` comprueba `preparations-*` elaboración por elaboración: una sola de otra estación rechaza el grupo entero con `wrong_station`, sin aplicar nada. `review-preparations` es solo del pase, como `review-preparation`.
- `KitchenAllowed` gana las tres acciones. **Por papel, cada orden de grupo vale lo mismo que su individual:** la sala conserva `preparations-start|ready` y nunca tiene `review-preparations`. Lo que impide a un comandero de sala marcar platos de cocina es la estación. `StationKeeps` y `PassKeeps` lo reflejan.
- **El pase marca por cualquier estación:** puede hacer `preparation(s)-start|ready` de cualquiera. Solo para el pase cambia D4.3a; las demás estaciones siguen marcando solo lo suyo. Validar y revisar siguen siendo solo del pase y de una persona con sesión (el responsable en el PC), que no tiene estación.

### Motor: quién abrió la mesa

- `DiningService` guarda `OpenedBy` (`device:<nombre>` o `user:<nombre>`, el actor de la orden `open`) y `OpenedStation` (la estación del dispositivo; vacía para una persona).
- Van en el JSON de la instantánea (`dining.services.payload`), sin columna nueva. Las dos claves se escriben siempre, también a `null`, como `offerId`. Un valor en blanco cuenta como desconocido.
- En `DiningView` salen como `openedBy` y `openedStation`, y se omiten si están vacíos. Sin evento nuevo: `service.created` ya lleva el actor. Sin palabras de dinero: pasan la guarda.
- El nombre de un dispositivo no es único (F12): «Mis mesas» será presentación, no atribución a una persona.

### Motor: instantáneas (sin vuelta atrás)

- Una revisión pendiente se admite ahora en un pase pendiente y con la mesa «Sentados». En un pase servido u omitido, o en una mesa terminada o cancelada, se sigue rechazando.
- La compatibilidad con D3.2 sigue marcando **solo los pases activos**, y un acuse global sin marcas sigue exigiendo una mesa en servicio o en pausa.
- Las instantáneas de antes se leen igual: las de D3.2, las de D3.5 y una mesa guardada por `0.43.0-c3c`, cuyo pase sin enviar no queda marcado.
- **Sin vuelta atrás:** un motor anterior rechaza una mesa con revisiones en pases sin enviar, con la mesa «Sentados» o con `openedBy`/`openedStation` (su decodificador estricto rechaza el campo aunque vaya a `null`).

### Motor: las guardas de versión

- **`Core.Persistence/EngineVersions.cs`:** en modo instalación, el motor deja al arrancar **una** fila idempotente en `core.commands`, con actor `engine`, clave `served-by:<versión>`, la versión en `fingerprint` y `{"version":…}` como respuesta. No es una orden de dominio: no lleva evento, auditoría ni aviso, y nadie la puede pedir por `GET /commands`, porque ninguna sesión tiene ese actor.
- La comparación y los mensajes viven en `EngineVersions`. Se niegan el arranque, `backup` (dentro de su instantánea), `upgrade`, `load-demo`, `init-lab`, `create-user`, `first-user` e `import-catalog`.
- **El manifiesto gana `servedBy`** (aditivo y siempre escrito), leído en la misma instantánea que la copia. `restore` compara `servedBy`, y `serverVersion` solo en un manifiesto anterior a E4e, que no lo trae.
- **¿Por qué no `Backup.Latest()`, como decía el plan?** `serverVersion` es la versión de los programas que **hicieron** la copia, no la del motor que **sirvió** los datos. Las dos difieren justo en la copia previa a una actualización, que hacen los programas nuevos. Con esa señal, tras un `upgrade` fallido no se podría reinstalar la versión anterior, y la vuelta atrás documentada quedaría bloqueada.
- `installer.nsi` solo cambia el texto del fallo. El NSIS sigue sin decidir nada. `D5.4-backup-restore.md`, `D5.5-installer.md` y `D5.5-LEEME.txt` están al día: la frase de D5.5 «rollback = reinstalar la versión anterior» ya no era verdad.

### Puesto principal (WPF)

- **Abrir mesa:** el apartado «¿Alergias o intolerancias?» (`AllergyDraft`, filas en memoria). Las filas viajan en la orden `open` como `restrictions`. Sin filas, el cuerpo de siempre: un servidor anterior nunca ve el campo nuevo. El puesto solo comprueba el formato (quién y a qué); el servidor valida el resto.
- **Ficha:** los platos agrupados con `Costina.Client/DishGroups` (mismo nombre y estación, en el orden en que aparecen; solo presentación, porque `PreparationView` no lleva el identificador del plato). `FloorPlan.CourseDetail` cuenta platos y `FloorPlan.WaitsForReview` dice si el pase que importa ahora espera la revisión.
- **Cocina desde el puesto, por plato agrupado:** «Empezar plato» y «Marcar plato listo» son una orden de grupo con las elaboraciones del plato que anuncian la individual. La revisión (`review-preparations`) lleva la decisión y la nota a las líneas marcadas, como mucho 60. Una relectura nunca vuelve a marcar una línea desmarcada.
- **Botón grande:** mientras el pase que va a salir espera la revisión, «Iniciar servicio» no va en grande aunque el servidor lo anuncie (`PrimaryAction`): el botón grande se apaga con «Esperando la revisión de cocina».
- **«Rehacer» sigue a la vista en un pase sin enviar.** El servidor contesta `nothing_to_remake`, ahora en castellano: el puesto no adivina reglas.
- **Palabras** (`Labels`): `ReviewOutcome` («Por revisar en cocina», «Adaptado: …»), las tres órdenes de grupo («Empezar plato», «Marcar plato listo», «Revisar platos») y los rechazos `nothing_to_remake` e `invalid_restriction`.
- **Tablas de casos comunes:** `floorplan-cases.json` cuenta platos y `vocabulary-cases.json` gana la familia `reviews`. Sus espejos en la PWA (`floor.ts`, `labels.ts`) cambian a la vez.
- El Comedor todavía no usa `openedBy`/`openedStation`.

### PWA (lo mínimo hasta C3d)

- **Costiña Cocina, pase** (`kitchen.ts`, `KitchenView.vue`):
  - `presendReviews` lista, por mesa y comensal, las elaboraciones de los pases **pendientes** con `reviewPending` y con `review-preparation` anunciada a este dispositivo. Nada se deduce: una estación no ve nada.
  - «No le afecta» y «Adaptado» envían `review-preparation`. «Rehacer» no se ofrece antes de enviar.
  - «No le afecta a nada más» (`restOfGuest`, `restBody`) envía en una orden `review-preparations` lo que queda del comensal: solo los platos cuyo pase anuncia la orden de grupo, como mucho 60 y la misma nota en cada uno.
- **Costiña Sala** (`TableCard.vue`, `ficha.ts`): las líneas de excepción de los pases sin enviar, con `reviewOutcome`, y `primaryAction(actions, service)`, que no pone «Iniciar servicio» en grande mientras el pase que va a salir espera.
- **Rechazos en castellano:** `nothing_to_remake`, `invalid_restriction` y `too_many_items`. `restrictions_unreviewed` dice la regla nueva.
- **`data-testid` nuevos:** `presend`, `presend-<mesa>-<comensal|all>`, `presend-item-…`, `do-presend-unaffected-…`, `do-presend-adapt-…`, `presend-rest-…`, `do-presend-rest-…` y `prep-review`.

## Decisiones tomadas al construir, para revisar

Pequeñas. Cualquiera se cambia si el promotor lo prefiere:
1. **Cancelar una mesa sin empezar retira lo que quedaba por revisar**, como omitir un pase.
2. **Con la mesa «Sentados», el botón grande es «Enviar siguiente pase»**, porque el servidor anuncia ahora `fire-next` y `start`. «Iniciar servicio» va debajo en el PC y en «Más opciones» en Sala.
3. **Mientras el primer pase espera la revisión, «Iniciar servicio» tampoco va en grande:** el botón grande se apaga y dice «Esperando la revisión de cocina».
4. **La revisión en el PC es por plato agrupado, con casillas** (todas marcadas). Para decidir distinto a dos comensales: desmarcar uno y decidir en dos toques.
5. **Los botones de cocina del PC son por plato** («Empezar plato», «Marcar plato listo»), no por línea.
6. **La ficha cuenta platos** («2 platos», «1 de 3 platos listos»), en el PC y en Sala.
7. **«No le afecta a nada más» en el pase, adelantado de C3d.** Solo sale con dos platos o más por decidir de ese comensal.
8. **El pase ve «Empezar» y «Marcar lista» en lo que falta de todas las estaciones**, hasta que C3d lo lleve a «Más opciones».
9. **Las palabras aún difieren entre aplicaciones:** Cocina sigue diciendo «PENDIENTE DE REVISIÓN POR COCINA» y «Revisada: …» en lo enviado (hasta C3d); Sala y el PC dicen «Por revisar en cocina» y «Adaptado: …».
10. **El PC ofrece «Rehacer» en un pase sin enviar** y el servidor contesta, en castellano, que no hay nada que rehacer.
11. **`invalid_restriction` es un 409 al abrir**, mientras que `declare-restriction` sigue dando 422 con un valor desconocido.

## Qué NO cambia

- **Seguridad alimentaria primero:** un plato afectado por una restricción nunca llega al comensal sin una decisión de cocina registrada.
- **El servidor decide los botones** (D3.1). Cada acción nueva entra a la vez en `Affordances.Allows` (la política de la ruta), en el espejo de estación y en `StationAllowed`.
- **F01 y una sola orden incierta durable por gesto:** una orden de grupo es **una** orden. Nunca una cola.
- **Todo o nada** en cada orden; cada escritura deja auditoría y outbox; nada se borra (una revisión retirada al omitir deja su evento).
- **Campos aditivos:** las órdenes de antes valen igual.
- **Sin dinero** en las respuestas ni en las claves nuevas (`restrictions`, `itemIds`, `items`, `openedBy`, `openedStation`). Lo desconocido sale tal cual. Castellano con tildes.
- **El esquema (v8), `grants.sql`, la CSP y el envío a estaciones.**
- **Validar y servir:** nada por revisar en ese pase, como en D3.5. **Validar y revisar** siguen siendo solo del pase.
- **Restricciones siempre escritas** con tipo, sustancia y gravedad, nunca solo con color. El rojo, solo para las alergias.

## Comprobaciones automáticas

Cifras en la punta del corte, todas en este PC, una ejecución cada vez, en primer plano y sin administrador (registros en `.lab-test/`):
- **vitest:** 400 en 19 ficheros (antes 388). `npm run build` en verde. Nuevas: `presendReviews`, `restOfGuest`, `restBody`, el botón grande que espera, la familia `reviews` y la cuenta de platos de la tabla común.
- **ClientChecks:** 277 (antes 273): la cuenta de platos, la familia `reviews`, `DishGroups`, las palabras de las órdenes de grupo y de los rechazos nuevos.
- **Dominio (`Costina.Acceptance`):** 97 (antes 86). Entre otras:
  - una alergia al abrir deja por revisar todo el menú de ese comensal, y todo si es de toda la mesa;
  - se revisa un pase sin enviar con la mesa «Sentados»;
  - un pase revisado sale aunque otro siga por revisar, y uno sin revisar no;
  - `fire-next` desde «Sentados» empieza y envía en una orden, y no cambia nada si no puede;
  - `remake` en un pase sin enviar da `nothing_to_remake`;
  - `choose` y `add-dish` nacen por revisar;
  - omitir y cancelar retiran lo pendiente;
  - las órdenes de grupo, todo o nada, con un evento por elaboración y decisiones distintas;
  - quién abrió la mesa;
  - el barrido «lo anunciado se puede ejecutar», con cinco etapas nuevas y las órdenes de grupo.
- **PersistenceChecks** (las comprobaciones de instantáneas de la CI, también aquí): 88 (antes 62). Las de D3.2, D3.5 y `0.43.0-c3c` se leen igual; una revisión en un pase pendiente con la mesa «Sentados» se guarda y se relee; en lo servido, lo omitido o una mesa cancelada se rechaza; quién abrió la mesa sobrevive a un reinicio y una mesa anterior lo lee vacío; la comparación de versiones (numérica, sin la etiqueta, lo ilegible como más nuevo) y los mensajes en los dos idiomas.
- **HTTP**, las 14 suites en verde:
  - `restrictions_http`, 9 (antes 6): `test_07` (abrir con alergias e `invalid_restriction`), `test_08` (revisar con la mesa «Sentados», bloqueo por pase, omitir retira) y `test_09` (contrato de `review-preparations`: rechazos, todo o nada, una subida de versión y tres eventos);
  - `station_http`, 10 (antes 8): `test_07` (un grupo con una elaboración de otra estación da `wrong_station` con el token del propio dispositivo y no aplica nada; `too_many_items`; una orden y N eventos) y `test_08` (el pase marca por otra estación);
  - `affordances_http`, 8 (antes 6): `test_07` (anuncios filtrados por papel y estación con dispositivos emparejados de verdad) y `test_08` (cada orden de grupo, permitida igual que su individual);
  - `offers_http`, 8 (antes 7): `test_07`, `choose` y `add-dish` nacen por revisar, en una mesa propia que se desactiva al terminar;
  - `pwa_http`, 8 (antes 7): `test_08`, `openedBy` y `openedStation` desde un dispositivo y desde una persona, una mesa anterior legible y sin claves de dinero;
  - `packaging_http`, 15 con 1 saltada (antes 14): `test_06h`, las guardas con la CLI real y una marca más nueva puesta con el papel propietario. `backup` no escribe copia; `upgrade`, `create-user` y el arranque se niegan; `restore` rechaza la copia diaria de un motor más nuevo y un manifiesto antiguo que lo nombra; la copia previa a actualizar se restaura;
  - las demás, sin cambios: native 11, security 6/6, desktop_reads 9, checkout 8, identity 10, pairing 18, organization 5 y catalog 10.
- **RealtimeChecks:** 13.
- **Playwright** contra el motor real: 30 (15 de escritorio y 15 de móvil). `kitchen.spec` empieza con la historia nueva: la mesa se abre con una intolerancia en la misma orden y Sala espera; el pase adapta un plato y despacha los otros dos con «No le afecta a nada más», y la versión sube exactamente en uno; Sala ve «Adaptado: …» y envía desde «Sentados» en una orden. `banner.spec` revisa antes el pase sin enviar, `commands.spec` envía directamente desde «Sentados» y `board.spec` usa las palabras de la sala.
- **DesktopChecks:** en verde. Abrir con alergias en una orden, el botón grande que espera, la revisión que solo lleva las líneas marcadas, y las capturas.
- **Batería completa** (`COSTINA_E2E=1 sh tools/agent/local-tests.sh`, puerto 5090): compilación con 0 advertencias y 0 errores; dominio 97/97, ClientChecks 277, las 14 suites HTTP, Playwright 30 y Realtime 13/13.
- **No corre en este PC:** `installer_checks.ps1`, que necesita el instalador real y administrador. Una actualización encima de datos con la marca de un motor más nuevo debe pararse sin copia nueva y con el servicio parado; al quitar la marca, el mismo instalador la completa. Su primera prueba será el workflow «Native Windows installer» de la PR.

### En rojo antes de su arreglo

Cada prueba nueva falló primero. Las roturas a propósito se restauraron desde una copia en el `scratchpad`, nunca con `git`.
- **Revisión antes de enviar** (contra el motor sin cambiar):
  - dominio, 80 de 94: fallaron las 14 pruebas nuevas o cambiadas;
  - PersistenceChecks se paró con una regla del dominio: no se podía revisar con la mesa «Sentados»;
  - `restrictions_http`, 7 fallos y 1 error: `open` con alergias daba `422 invalid_request`, `fire-next` desde «Sentados» daba `service_not_running` y nada se marcaba antes de enviar;
  - `offers_http.test_07`: el motor anterior contestaba 422 a `open` con alergias.
- **Órdenes de grupo, matriz y quién abrió** (contra el motor del paso anterior):
  - `station_http.test_07` recibía 403 en vez de `wrong_station`, y en `test_08` el pase no tenía `preparations-ready` anunciado;
  - `affordances_http.test_07` falló en quién ve `review-preparations`, y `test_08` recibía 403 donde esperaba 409;
  - `restrictions_http.test_09` recibía 403 en vez de 422;
  - `pwa_http.test_08`: faltaba `openedBy`;
  - `kitchen.spec`, en los dos proyectos, en su comprobación nueva (las otras 28 pasaron);
  - con la API nueva en esqueleto: dominio 93 de 97 y PersistenceChecks parado en «quién abrió la mesa sobrevive a un reinicio».
- **Guardas de versión:**
  - PersistenceChecks se paró en la primera comprobación nueva, con `EngineVersions` en esqueleto;
  - `packaging_http.test_06h` falló en su primera comprobación: ni marca ni `servedBy`;
  - cinco roturas, cada una en su comprobación: `backup` sin guarda (escribía la copia), los comandos administrativos sin guarda (`upgrade` pasaba), el arranque sin guarda, `restore` sin guarda y `restore` comparando solo `serverVersion` (rechazaba la copia previa a actualizar, que es la vuelta atrás).
- **Clientes:**
  - vitest contra la PWA anterior: 13 fallos (5 de «por revisar antes de enviar», 6 de la cuenta de platos y 2 del botón que espera) y el fichero de palabras, que no encontraba `reviewOutcome`;
  - Playwright con la PWA anterior y el motor nuevo: 4 de 30, `board.spec` y `kitchen.spec` en los dos proyectos;
  - ClientChecks con las palabras y el plano anteriores: 12 fallos;
  - DesktopChecks, tres roturas, cada una en su comprobación: abrir sin las alergias, «Iniciar servicio» en grande y la revisión que enviaba también las líneas desmarcadas.

## Capturas

En `docs/native/images/e4e/`.

**Puesto principal**, de DesktopChecks, con la versión `0.44.0-e4e`:
- [Abrir mesa con alergias](images/e4e/comedor-abrir-alergias.png): «¿Alergias o intolerancias?» con el Comensal 3 (Alergia · marisco · Grave) y el Comensal 2 (Intolerancia · lactosa · Leve).
- [La ficha con la línea adaptada](images/e4e/comedor-linea-adaptada.png): la mesa «Sentados», «Ostra × 4» y «Pan de la casa × 4» con la línea de cada comensal («Adaptado: vieira en lugar de ostra», «No le afecta: …»), el postre con «⚠ 2 platos por revisar en cocina» y «Enviar siguiente pase a cocina» en grande.

**Costiña Sala y Costiña Cocina**, de `e2e/captures-e4e.spec.ts` contra el motor real:
- [Sala esperando la revisión](images/e4e/sala-esperando-revision-375x812.png): la mesa «Sentados», las dos restricciones, el aviso rojo con la regla nueva y el botón grande apagado: «Esperando la revisión de cocina».
- [El pase: «Por revisar antes de enviar»](images/e4e/cocina-por-revisar-antes-de-enviar-1180x820.png): por mesa y comensal, cada plato con su nota, «No le afecta» y «Adaptado», y «No le afecta a nada más · 3 platos».
- [Sala con lo adaptado](images/e4e/sala-adaptado-375x812.png): «Adaptado: …» bajo el plato y «Enviar siguiente pase» de nuevo en grande.

**Para repetir las de las aplicaciones** hacen falta una base nueva y el motor con el build actual de la PWA:
1. `npm run build` en `frontend/pwa`.
2. La batería entera, o `dotnet build` de `src/Costina.Server`.
3. `E4E_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/e4e)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http`.

En la CI no corre nunca. Las del PC salen de `sh tools/agent/local-tests.sh desktop` en `dotnet/artifacts/desktop/` con el mismo nombre.

## Guion manual (solo en una instalación de LABORATORIO; 10 pasos)

**Nunca en un PC con una instalación real**, ni sobre sus servicios, puertos o carpetas. Hace falta:
- un PC de laboratorio con una instalación de prueba en `0.44.0-e4e`: la demostración (`/DEMO=1`, D5.6) o el laboratorio de `docs/native/lab-engineering.md`;
- una tablet (o un navegador) con **Costiña Cocina · Pase**, otra con **Costiña Cocina · Cocina caliente** y un móvil con **Costiña Sala**, emparejados como dice el documento de C3c.

1. **PC, abrir con alergias.** Comedor › «Abrir mesa», 4 personas, un menú degustación. En «¿Alergias o intolerancias?», «Añadir alergia o intolerancia»: comprueba que «¿Para quién?» empieza vacío y que así no abre. Comensal 3 · Alergia · marisco · Grave y, en otra fila, Comensal 2 · Intolerancia · lactosa · Leve. «Abrir». La mesa sale «Sentados» con «Alergia · por revisar»; la ficha agrupa los platos con «· Por revisar en cocina» en rojo bajo los dos comensales, y el botón grande está apagado: «Esperando la revisión de cocina».
2. **Sala.** La misma mesa: las dos restricciones, «Por revisar en cocina» bajo los platos de los pases sin enviar, el botón grande apagado e «Iniciar servicio» en «Más opciones».
3. **Cocina.** La tablet de Cocina caliente no enseña nada de esa mesa. La del pase, arriba, «Por revisar antes de enviar»: la mesa con el Comensal 2 y el Comensal 3, su restricción escrita y **todos** sus platos de todos los pases.
4. **El pase decide.** Comensal 3: en el primer plato, nota «vieira en lugar de ostra» › «Adaptado»; en «Nota para todo lo que queda», «no lleva marisco» › «No le afecta a nada más». Lo mismo con el Comensal 2. La lista se vacía.
5. **Un toque para empezar.** En Sala, bajo el plato, «Adaptado: vieira en lugar de ostra», y el botón grande es «Enviar siguiente pase». Tócalo **una vez**: la mesa pasa a «En cocina». En el PC, el primer pase está enviado una vez.
6. **El pase marca por una estación.** Deja apartada la tablet de Cocina caliente. En el pase, en la comanda de esa mesa, «Marcar lista» en un plato de Cocina caliente. Al mirar la tablet de Cocina caliente, el plato sale listo. Valida el pase y sírvelo desde Sala.
7. **Una alergia a mitad de servicio.** Desde Sala, en la ficha, «Anotar alergia o intolerancia»: Comensal 1 · Preferencia · cerdo · Leve. En el pase, sus platos de los pases sin enviar salen en «Por revisar antes de enviar»; en Sala, el botón grande vuelve a «Esperando la revisión de cocina». Decide en el pase («No le afecta a nada más»): «Enviar siguiente pase» vuelve.
8. **Omitir retira.** Abre otra mesa desde el PC con una alergia para el Comensal 1. En el PC, escribe un motivo y pulsa «Omitir pase» del primer pase: sus platos desaparecen de «Por revisar antes de enviar» y los de los demás pases siguen.
9. **Revisión desde el PC.** En esa mesa, elige un plato con la línea por revisar y decide «No le afecta» con una nota: la línea pasa a «No le afecta: …». En un pase sin enviar, «Rehacer» contesta en castellano que no hay nada que rehacer.
10. **Repaso.** En ninguna pantalla de Sala ni de Cocina aparecen «precio», «cuenta», «pago», «saldo» ni «€».

| Paso | PC | Sala | Cocina (pase) | Cocina caliente | Notas |
|---|---|---|---|---|---|
| 1. Abrir con alergias | | | | | |
| 2. Sala | | | | | |
| 3. Cocina | | | | | |
| 4. El pase decide | | | | | |
| 5. Un toque para empezar | | | | | |
| 6. El pase marca por una estación | | | | | |
| 7. Alergia a mitad de servicio | | | | | |
| 8. Omitir retira | | | | | |
| 9. Revisión desde el PC | | | | | |
| 10. Repaso | | | | | |

**La guarda contra bajar de versión** la prueba la CI con el instalador real (`dotnet/tests/service/installer_checks.ps1`). En un laboratorio no hay todavía una versión más nueva que `0.44.0-e4e` con la que verla de verdad: se verá al pasar a C3d (`0.45.0-c3d`), si alguien intenta reinstalar `0.44.0-e4e` encima.

## Vuelta atrás

- **No la hay instalando encima.** Ver «Antes de instalar», al principio.
- **Se vuelve restaurando** la copia previa a la actualización, en un equipo limpio y con el instalador anterior. Esos datos solo los había servido el motor anterior, así que los lee; un motor con la guarda también la acepta. El manifiesto lleva el campo nuevo `servedBy`, y un motor anterior lo ignora.
- **Una copia posterior a la actualización** (la diaria de `0.44.0-e4e`, por ejemplo) **nunca se restaura con programas de antes de `0.44.0`.** Esos programas no tienen la guarda, así que no la rechazarían, pero su motor no leería las mesas.

## Límites y diferido

- **Las mesas abiertas al actualizar** conservan su estado: sus pases sin enviar no quedan marcados hasta el siguiente cambio de restricciones en esa mesa.
- **El agujero del instalador:** tras un intento de bajar de versión, en `C:\Program Files\Costina` quedan los programas viejos hasta que se ejecute un instalador de la versión buena. Cerrarlo del todo obliga a extraer el servidor nuevo aparte y comprobar antes de sustituir los programas: es rehacer el NSIS.
- **Un arranque negado** no deja rastro en `<datos>/logs`, porque falla antes de que exista el registro: el servicio sale como fallido y `status` dice NOT ANSWERING.
- **Un 422 de `review-preparations` no dice qué plato falló.** Sería un campo aditivo si la pantalla del pase lo necesita.
- **La marca solo existe desde `0.44.0-e4e`.** Los datos que solo sirvió un motor anterior llevan `servedBy: []`.
- **«Deshacer «Listo»»** en el motor sigue diferido: el promotor no lo ha pedido.
- **A C3d** (`0.45.0-c3d`, Costiña Cocina con comandas por mesa): en las estaciones solo «Listo» (respuesta 1); ámbar a los 15 y a los 25 minutos (respuesta 3); el sonido tras «Empezar turno» (respuesta 4); el «Marcar Cocina fría como lista» del pase en «Más opciones» (respuesta 2), que sustituye a los botones de hoy en todo lo que falta; «Por revisar» del pase con atajos de nota; agrupar en cocina con la tabla común `kitchen-cases.json`; y las palabras de cocina, iguales a las de Sala y el PC.
- **A C3e** (`0.46.0-c3e`, Costiña Sala): «Mis mesas» con `openedBy` y `openedStation`; abrir con alergias desde Sala en tres toques; «Carta y menús»; y el «Preguntar al servidor» de una orden de otra instalación.
- **Sin fecha:** los alérgenos de cada plato (esquema propio; cruzar el texto libre de una restricción sería una regla inventada), la hora por estación y la intención fijada al emitir el código de emparejamiento.
