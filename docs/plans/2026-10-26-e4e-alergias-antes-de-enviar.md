# Plan E4e: alergias antes de enviar, órdenes de grupo y quién abrió la mesa

Fecha: 2026-10-26. **Estado: APROBADO.** Aquí solo se baja a pasos lo que ya está aprobado:
- la sección 4.5 de `docs/plans/2026-10-15-menus-carta-y-bodega.md` (`plan15:587-656`), aprobada el 15-10;
- la sección 4.2 de `docs/plans/2026-10-22-sala-y-cocina.md` (`plan22:545-606`), aprobada el 22-10 con «Si a todo, la 5, va haber android y apple».

**Entregado el 26-10**: `docs/native/E4e-alergias-antes-de-enviar.md`, con sus capturas en `docs/native/images/e4e/`. Incluye todo el alcance de abajo, con las concreciones anotadas en cada sección (la marca en los datos de la sección 8 en lugar de `Backup.Latest()`, `cancel-unstarted` que también retira, `nothing_to_remake` como 409) y estas diferencias:
- **«No le afecta a nada más» en la pantalla del pase**, adelantado de C3d (sección «Diferido»): una orden `review-preparations` con lo que queda del comensal, solo con dos platos o más por decidir y como mucho 60.
- **El botón grande** no pone «Iniciar servicio» mientras el pase que va a salir espera la revisión, en el PC y en Sala: se apaga con «Esperando la revisión de cocina».
- **El puesto revisa por plato agrupado con casillas** (todas marcadas si los comensales del plato llevan las mismas restricciones; ninguna si no, desde la revisión), y sigue ofreciendo «Rehacer» en un pase sin enviar para que el servidor conteste `nothing_to_remake` en castellano.
- **Revisión antes de subir** (26-10): la excepción del pase mira también el rol (un comandero de sala que quedó en `pase` antes de A2 no marca por otras estaciones), la fila de cada pase en el PC cuenta platos agrupados, y los campos de nota del pase toman su `id` de su posición en la lista. Detalle en el documento del corte.
- **Para revisar con el promotor:** once decisiones pequeñas, en «Decisiones tomadas al construir» del documento del corte.

**Respuestas del promotor que fija este corte:**
- **cocina decide cada plato de un comensal con alergia o intolerancia antes de que salga el pase**, y el pase espera; también las intolerancias leves y las preferencias, que cocina despacha con un toque (preguntas 1 y 2 del 15-10, `plan15:902-903`, confirmadas el 22-10, `plan22:875`);
- **el pase puede marcar «Listo» por una estación con la tablet estropeada** (pregunta 2 del 22-10, `plan22:889-893`);
- **«sube y mezcla» (26-10)**: permiso para subir y mezclar E4e cuando la CI esté en verde.

- **Base:** `develop` en `9e6befe` (`0.43.0-c3c`, esquema v8).
- **Rama:** `feat/e4e-alergias-antes-de-enviar`.
- **Versión:** `0.44.0-e4e`.
- **Sin cambio de esquema; sí cambian las reglas de las instantáneas, sin vuelta atrás** (sección 7).
- **ADR nuevo:** `docs/ADR/ADR-016-revision-de-alergias-antes-de-enviar.md`. Sustituye a D3.5 en lo que dice de «solo lo enviado» y, solo para el pase, a D4.3a.

Rutas cortas como en los dos planes: `Dining/` = `dotnet/src/Costina.Modules.Dining/` y `Dining.Domain/` = `dotnet/src/Costina.Modules.Dining.Domain/`. Las referencias `fichero:línea` son de `9e6befe`.

## Alcance

### 1. Revisión antes de enviar (dominio; puntos 1 a 3 de `plan15:592-605`)

- **Se marcan «por revisar» las elaboraciones afectadas que no estén servidas, también las de los pases sin enviar.** Hoy solo se marcan las enviadas (`Dining.Domain/CourseExecution.cs:139-148`). Un pase servido u omitido no se marca: ya no se cocina.
  - Vale al declarar y al retirar una restricción (`Dining.Domain/Restrictions.cs:31-72,87-88`): la retirada vuelve a pedir decisión de lo que se adaptó.
  - **Toda la mesa:** una restricción de toda la mesa afecta a todas las elaboraciones, y una elaboración sin comensal (un plato de carta para compartir) recibe cualquier restricción (`Restrictions.cs:28-29`). Las dos se marcan.
- **Una elaboración que nace para un comensal con restricción nace por revisar:**
  - al abrir, con las restricciones de la misma orden (punto 2);
  - con `choose`, que sustituye la elaboración (`CourseExecution.cs:62-70`): el plato nuevo vuelve a necesitar revisión;
  - con `add-dish` (`CourseExecution.cs:72-81`), también un plato sin comensal.
- **Revisar también lo que no ha salido.**
  - `review-preparation` se admite en pases sin enviar y con la mesa «Sentados». Hoy pasa por `KitchenAllowed`, que exige «En servicio» o «En pausa» (`Dining.Domain/DiningService.cs:152-158`; `Restrictions.cs:77-85`), y el pase tiene que estar activo (`CourseExecution.cs:150-152`).
  - El anuncio cambia con ella: hoy `review-preparation` solo se anuncia en un pase activo (`Dining.Domain/AllowedActions.cs:58`).
  - La nota sigue siendo obligatoria. La de «adaptar» es el plato adaptado («tosta de maíz en vez de pan»): el nombre del plato no cambia.
  - **«Rehacer» sigue siendo solo para lo enviado:** en un pase sin enviar da `nothing_to_remake`. Es un `409`, como el resto de reglas del dominio (el plan del 15-10 decía 422; todas las reglas del dominio salen como 409, `Server/Program.cs:384`).
- **Bloqueo por pase, no global.**
  - `fire-next` se niega con `restrictions_unreviewed` solo si **el pase que va a salir** tiene algo por revisar. Hoy se niega ante cualquier pase (`DiningService.cs:44`), y el anuncio igual (`AllowedActions.cs:18-19`): los dos cambian juntos. Con un pase activo que tiene algo por revisar sigue diciendo `restrictions_unreviewed`, como hoy; sin revisiones, `active_course`.
  - Validar y servir no cambian (`CourseExecution.cs:112-128`).
- **Omitir un pase retira sus revisiones pendientes** (`CourseExecution.cs:130-137`): lo que no se va a cocinar no se revisa. Las decisiones ya tomadas se conservan.
  - **Concreción:** cancelar una mesa sin empezar (`cancel-unstarted`, `DiningService.cs:137-145`) también las retira, por la misma razón. Sin eso, una mesa cancelada se quedaría con «por revisar» para siempre y su instantánea no se podría leer.
  - El evento (`course.skipped`, `service.cancelled`) lleva `reviews_withdrawn` con el número retirado, solo si es mayor que cero: lo de siempre no cambia.

### 2. Abrir con alergias (`plan15:606`; `plan22:562`)

- `OpenRequest` (`Dining/DiningOperations.cs:9`) gana `Restrictions` (`IReadOnlyList<RestrictionRequest>?`), con `{guestPosition?, kind, substance, severity}`.
- Las restricciones se declaran **en la misma orden y transacción** que la apertura (`DiningOperations.cs:23-60`): una sola orden incierta. Una mala no abre nada.
- **Un tipo o una gravedad desconocidos (o vacíos) dan `invalid_restriction`** (409). `declare-restriction` no cambia: sigue dando `422 invalid_request` con un valor desconocido (`Core.Persistence/Contracts.cs:44-46`).
- Las demás reglas son las de `declare-restriction`: comensal dentro de la mesa (`invalid_guest`), sustancia obligatoria y sin duplicados (`duplicate_restriction`).
- Auditoría: un `restriction.declared` por restricción, justo después de `service.created`. Una apertura sin restricciones deja los mismos eventos, en el mismo orden, que hoy.

### 3. `fire-next` desde «Sentados» (`plan15:616`)

- `fire-next` se admite también con la mesa «Sentados»: empieza el servicio (`service.started`) y envía el primer pase (`course.fired`) en la misma orden. Un toque menos.
- Todo o nada: si el pase no puede salir (`restrictions_unreviewed`, `choice_missing`, `course_empty`), la mesa sigue «Sentados».
- `start` sigue existiendo. Con la mesa «Sentados» el servidor anuncia los dos; el botón grande pasa a ser «Enviar siguiente pase», porque va antes que «Empezar» en el orden de C2 (`frontend/pwa/src/ficha.ts:10`).

### 4. Órdenes de grupo (contrato exacto de `plan22:559-578`)

- **`DiningCommand`** (`DiningOperations.cs:11-14`) gana `ItemIds` (`IReadOnlyList<string>?`) e `Items` (`IReadOnlyList<ReviewItem>?`), con `ReviewItem(CourseId, ItemId, Decision, Note)`.
- **`preparations-start|preparations-ready`** `{expectedVersion, courseId, itemIds}`: de 1 a 200 elaboraciones de ese pase (`too_many_items`), todo o nada, **una** subida de versión y un evento por elaboración con los tipos de hoy (`course_item.started|ready`).
- **`review-preparations`** `{expectedVersion, items}`: de 1 a 60 decisiones (caben en los 32 KiB de `Core.Hosting/Requests.cs:30`), nota obligatoria en cada una y un `restriction.reviewed` por elaboración. Decisión y nota por elaboración: el menú entero de un comensal se revisa en una orden.
- **Anuncio, en el pase:** cada orden de grupo sale si alguna elaboración anuncia la individual **después** de filtrar por rol y estación (`Dining/Affordances.cs:35-46`). Es la primera acción que se deduce en el filtro: se documenta.
- **Concreción:**
  - una lista vacía, una elaboración repetida en la misma orden o una decisión sin pase, plato, decisión conocida o nota es una petición mal formada: `422 invalid_request`. Pasar de 200 o de 60 da `409 too_many_items`;
  - los identificadores van tal cual, sin recortar, igual que los busca la matriz de estación: un espacio de más no puede esconder la estación de una elaboración;
  - todo o nada también en memoria: el dominio comprueba todas las elaboraciones antes de tocar ninguna. Las órdenes individuales pasan a ser el grupo de una, con las mismas reglas, los mismos errores y el mismo evento.

### 5. Matriz de estación sin huecos y el pase que marca por una estación (`plan22:574-583`)

- `StationAllowed` (`DiningOperations.cs:64-79`) comprueba `preparations-*` elaboración por elaboración y deja `review-preparations` solo al pase. Una persona con sesión (sin estación) sigue sin restricción.
- `KitchenAllowed` (`Affordances.cs:10`) gana las tres acciones. **Por rol, cada orden de grupo vale lo mismo que su individual** (`Affordances.cs:17`): la sala queda fuera de `review-preparations`, como de `review-preparation`.
- `StationKeeps` y `PassKeeps` (`Affordances.cs:26-33`) las reflejan.
- **El pase puede marcar `preparation(s)-start|ready` de cualquier estación** (respuesta 2 del 22-10). Cambia D4.3a solo para el pase (`dotnet/AGENTS.md:25`); las demás estaciones siguen marcando solo lo suyo. Revisión del 26-10: la excepción es de un dispositivo de cocina (o un principal antiguo) en el pase, nunca de uno de sala que quedó ahí antes de A2 (`Affordances.PassMarksAll`, con el rol).
- **Consecuencia en pantalla:** mientras el paso de la PWA (sección 10) o C3d no decidan otra cosa, la pantalla del pase de Costiña Cocina pinta «Empezar» y «Marcar lista» en lo que falta de **todas** las estaciones, porque el servidor se lo anuncia. `e2e/kitchen.spec.ts` lo fija. C3d lo lleva a «Más opciones» de cada tarjeta (`plan22:666`).

### 6. Quién abrió la mesa (`plan22:585-594`)

- `DiningService` guarda `OpenedBy` (`device:<nombre>` o `user:<nombre>`) y `OpenedStation` (vacía para una persona), tomados de `ExecutionIdentity` (`Core.Persistence/Contracts.cs:38`) en `open`.
- Campos aditivos de `DiningSnapshot` (`Dining.Domain/Snapshots.cs:8-11`), en el JSON de `dining.services.payload`: sin columna nueva. Una mesa guardada antes los restaura vacíos.
- En `DiningView` (`Dining.Domain/ServiceModels.cs:42-50`) como `openedBy` y `openedStation`, omitidos si están vacíos. Sin evento nuevo: `service.created` ya lleva el actor.
- **Concreción:** en la instantánea las dos claves se escriben siempre, también a `null`, como `offerId`. Un valor en blanco cuenta como desconocido.

### 7. Instantáneas: reglas nuevas sin vuelta atrás (`plan15:618-623`)

- Los campos de la revisión ya existen (`ReviewPending` y `Review`). Cambian las reglas con que se leen:
  - una revisión pendiente se admite en un pase pendiente (hoy solo en uno enviado y sin servir, `Snapshots.cs:80-81`); en un pase servido u omitido se sigue rechazando;
  - una revisión pendiente se admite con la mesa «Sentados» (hoy solo «En servicio» o «En pausa», `Snapshots.cs:55-57`); nunca en una mesa terminada o cancelada;
  - la compatibilidad con D3.2 (`Snapshots.cs:61-62`) sigue marcando **solo los pases activos**, y un acuse global sin marcas sigue exigiendo una mesa en servicio o en pausa.
- **Las instantáneas de antes se leen igual** (D3.2, D3.5 y C3c).
- **Sin vuelta atrás:** un motor anterior a E4e no lee una mesa guardada con revisiones en pases sin enviar, con la mesa «Sentados», ni con `openedBy`/`openedStation` (su decodificador estricto rechaza el campo aunque vaya a `null`, `Contracts.cs:12`). Bajar de versión exige restaurar la copia previa con los binarios anteriores.
- Una mesa abierta al actualizar conserva su estado: sus pases sin enviar no quedan marcados (se marcarán con el siguiente cambio de restricciones). El documento del corte lo dice.

### 8. Guardas de instalación (`dotnet/AGENTS.md:99`; `plan22:557`)

Abiertas desde C3b, y necesarias antes de E4e porque ni C3b ni E4e tienen vuelta atrás:
- **El instalador nunca baja de versión.** `setup-server` (`Server/ServerSetup.cs:104-110`), sobre una raíz de datos existente y **antes** de la copia y del `upgrade`, compara su versión con la del motor que escribió la última copia (`Backup.Latest()`, `Server/Backup.cs:133`; el servicio hace una al día y `setup-server` otra antes de cada actualización). Si esa es más nueva, para con `SETUP FAILED: …` y no toca nada.
- **`restore` rechaza una copia hecha por un motor más nuevo** (`ServerVersion` del manifiesto, `Backup.cs:21,143-148`), antes de tocar la base.
- Las dos comparan solo la parte numérica (mayor.menor.parche: `0.44.0` de `0.44.0-e4e`). La lógica vive en `Costina.Server.exe` (D5.5); el NSIS no decide nada.
- **Límite:** solo protege desde E4e. Un instalador anterior no tiene la guarda: nunca hay que instalarlo encima de datos de E4e.
- Pruebas: `packaging_http` (una copia de un motor más nuevo se rechaza al restaurar y al actualizar) y el workflow del instalador, que salta solo porque cambian `ServerSetup` y `Backup` (`AGENTS.md:55`).

**Concreción al codificar (paso 4): la señal es una marca en los propios datos, no `Backup.Latest()`.**
- **Por qué.** `ServerVersion` del manifiesto es la versión de los binarios que **hicieron** la copia, no la del motor que **sirvió** los datos. Las dos difieren justo en la copia que `setup-server` toma con los binarios **nuevos** antes de actualizar. Con `Backup.Latest()`:
  - tras un `upgrade` fallido, reinstalar la versión anterior se rechazaría, aunque ningún motor nuevo haya tocado los datos;
  - la vuelta atrás documentada (restaurar con los binarios anteriores la copia previa a la actualización) quedaría bloqueada, porque esa copia dice que la hizo la versión nueva.
- **La marca** (`Core.Persistence/EngineVersions.cs`): cada motor en modo instalación deja al arrancar **una** fila idempotente en `core.commands`, con actor `engine`, clave `served-by:<versión>`, la versión en `fingerprint` y `{"version":…}` como respuesta. Es como la marca de demo de D5.6:
  - viaja con las copias y no cambia el esquema;
  - no es una orden de dominio: no lleva evento, auditoría ni aviso;
  - nadie la puede pedir por `GET /commands`;
  - el laboratorio no la deja, para que cambiar de rama sobre la misma base de pruebas no lo bloquee.
- **Dónde se niega**, antes de escribir nada, con el mensaje en castellano (sin tildes, por la consola del instalador) y en inglés:
  - en el **arranque** (el servicio). El instalador copia los binarios antes de que `setup-server` se niegue, y el servicio de Windows apuntaría a ellos en el siguiente arranque;
  - en **`backup`**, dentro de su instantánea. Es el primer paso de `setup-server` sobre una raíz existente: ahí para con `SETUP FAILED`, sin copia y sin `upgrade`;
  - en **`upgrade`**, `load-demo` e `init-lab` sobre un esquema existente;
  - en **`create-user`, `first-user` e `import-catalog`**.
- **El manifiesto gana `servedBy`** (aditivo y siempre escrito): los motores que habían servido los datos, leídos en la misma instantánea. **`restore` compara `servedBy`**, y `ServerVersion` solo si el manifiesto es anterior a E4e (no lo trae). Así la copia previa a una actualización se restaura con los binarios anteriores, y la diaria de un motor más nuevo no.
- **Una versión ilegible cuenta como más nueva** (falla cerrado) y se muestra tal cual.
- **Pruebas:**
  - `PersistenceChecks`: la comparación, que es numérica y no de texto, y lo ilegible;
  - `packaging_http.test_06h`: `backup`, `upgrade`, `create-user` y el arranque se niegan sin escribir; `restore` rechaza la copia diaria de un motor más nuevo y un manifiesto antiguo que lo dice; la copia previa a una actualización sí se restaura;
  - `installer_checks.ps1` (solo en la CI, con el instalador real): una actualización encima de datos con la marca de un motor más nuevo se para sin copia y con el servicio parado; al retirar la marca, el mismo instalador la completa.

### 9. Puesto principal (WPF; `plan15:625-635`)

- **Comedor, abrir mesa:** «¿Alergias o intolerancias?» con una fila por comensal (comensal explícito, «Toda la mesa» como opción y nunca por defecto; Alergia y Grave por defecto). Una sola orden `open`.
- **Ficha de mesa:** platos agrupados («Ostra × 7») y, debajo y en rojo, la línea del comensal con «Adaptado: …» o «Por revisar en cocina». `FloorPlan` cuenta platos, no líneas (`Client/FloorPlan.cs:95-115`).
- **Revisión desde el puesto**, por plato agrupado, con `review-preparations`.
- Textos para `nothing_to_remake` e `invalid_restriction` en `Client/Labels.cs` y en la PWA, con su comprobación en ClientChecks.
- El Comedor todavía no usa `openedBy`/`openedStation` (`plan22:596`).

### 10. PWA: el mínimo hasta C3d (`plan15:637`; `plan22:555`)

En la pantalla del pase de Costiña Cocina: lista también lo que está por revisar en pases sin enviar, con los botones que ya existen («No afecta» y «Adaptar», nota obligatoria). Así ningún pase queda bloqueado mientras llega C3d. Nada más: la comanda nueva es de C3d.

### 11. Documentación

`docs/native/E4e-alergias-antes-de-enviar.md` (con capturas de la apertura con alergias y de la ficha con la línea adaptada), el párrafo en `dotnet/AGENTS.md`, la línea en `docs/STATUS.md`, ADR-016 y la versión `0.44.0-e4e` en los csproj del servidor y del escritorio y en `frontend/pwa/package.json` (y su lock).

## Qué no cambia

- **Seguridad alimentaria primero:** un plato afectado por una restricción nunca llega al comensal sin una decisión de cocina registrada.
- **El servidor decide los botones** (D3.1). Cada acción nueva entra a la vez en `Affordances.Allows` (la política de la ruta), en el espejo de estación y en `StationAllowed`.
- **F01, una sola orden incierta durable por gesto:** una orden de grupo es **una** orden.
- **Todo o nada** en la transacción de cada orden; cada escritura deja auditoría y outbox; nada se borra (una revisión retirada al omitir deja su evento).
- **Campos aditivos:** las órdenes de hoy valen igual.
- **Sin dinero** en las respuestas ni en las claves nuevas (`restrictions`, `itemIds`, `items`, `openedBy`, `openedStation`); lo desconocido sale tal cual.
- **Esquema v8** y la misma CSP. Castellano con tildes en la interfaz y en los documentos.
- **Envío a estaciones:** sin cambio.

## Diferido

- **Deshacer «Listo»** en el motor (`plan22:800`): no lo ha pedido el promotor; queda como acción aditiva para cuando lo pida.
- Lo de C3d (la comanda nueva de Costiña Cocina, «No le afecta a nada más», agrupar en cocina) y lo de C3e («Mis mesas» con `openedBy`, abrir con alergias desde Sala).
- La intención fijada al emitir el código de emparejamiento y la sala de cada estación (esquema, `plan22:797-798`).

## Pruebas

Cada prueba nueva falla antes de su arreglo.

- **Dominio (`Costina.Acceptance`):** una alergia al abrir deja todo el menú de ese comensal por revisar (y todo, si es de toda la mesa); se revisa un pase sin enviar con la mesa «Sentados»; un pase revisado sale aunque otro siga por revisar, y uno sin revisar no; `fire-next` desde «Sentados» empieza y envía en una orden, y no cambia nada si no puede; `remake` en un pase sin enviar da `nothing_to_remake`; `choose` y `add-dish` para un comensal con alergia nacen por revisar; omitir y cancelar retiran las revisiones pendientes; el barrido «anunciado = ejecutable» con las etapas nuevas; las órdenes de grupo todo o nada y con decisiones distintas.
- **Instantáneas (`Costina.Acceptance` y `Costina.PersistenceChecks`):** una de D3.5 y una de D3.2 se cargan sin cambios; una nueva con revisiones en un pase pendiente y la mesa «Sentados» se guarda y se vuelve a leer; una revisión pendiente en un pase servido u omitido, o en una mesa cancelada, se rechaza; `openedBy` se lee vacío en una instantánea anterior.
- **HTTP:** `restrictions_http` (apertura con alergias, `invalid_restriction`, revisión con la mesa «Sentados», `fire-next` desde «Sentados», bloqueo por pase, omitir retira); `offers_http` (`choose` y `add-dish` nacen por revisar); `station_http` (grupo con una elaboración de otra estación da `wrong_station` sin aplicar nada, con el token del propio dispositivo; el pase marca por otra estación); `affordances_http` (la sala no ve `review-preparations`; un comandero de sala no ve `preparations-*` en un pase de cocina; `Allows` igual para cada orden de grupo que para su individual); `pwa_http` (`openedBy`/`openedStation` sin dinero); `packaging_http` (las guardas de la sección 8).
- **E2E (Playwright):** siguen en verde con la regla nueva; la del pase revisa un pase sin enviar desde Costiña Cocina.
- **ClientChecks, DesktopChecks y vitest**, con capturas de la apertura con alergias y de la ficha con la línea adaptada.
- **Batería completa** con `COSTINA_E2E=1`, `npm run build` antes, y DesktopChecks.

## Pasos (un commit por paso)

1. `docs(plans)`: este plan. `docs(adr)`: ADR-016.
2. `feat(dining)`: secciones 1, 2, 3 y 7, con sus pruebas de dominio, de instantáneas y HTTP, y las E2E que la regla nueva cambia.
3. `feat(dining)`: secciones 4, 5 y 6, con sus pruebas.
4. `feat(server)`: sección 8.
5. `feat(desktop)` y `feat(pwa)`: secciones 9 y 10.
6. `docs(native)`: sección 11 y la versión.
