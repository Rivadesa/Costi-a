# Plan: Costiña Sala y Costiña Cocina, una aplicación para cada sitio (cortes C3c, E4e, C3d y C3e; reordena lo que queda del plan del 15-10)

Fecha: 2026-10-22. **Estado: APROBADO por el promotor el 22-10-2026**, con sus palabras: «Si a todo, la 5, va haber android y apple». Acepta las cuatro propuestas de la sección 10 y contesta la quinta: habrá Android y también iPhone y iPad. Las respuestas, y en qué corte entra cada una, están en las secciones 9 y 10. **Da permiso para subir y mezclar C3c** cuando la CI esté en verde; para E4e, C3d y C3e se vuelve a pedir. La revisión crítica contra el código está hecha (sección «Revisión», al final), y la maqueta está corregida según esa revisión.

**Maqueta:** `docs/plans/maquetas/2026-10-22-sala-y-cocina.html` (se abre en el navegador; pestañas: instalar, mis mesas, mesa, estación, pase, aviso y servido) y capturas en `docs/plans/maquetas/2026-10-22-sala-y-cocina/`.

Al aprobarse, este plan:
- sustituye el orden de la sección 4 de `docs/plans/2026-10-15-menus-carta-y-bodega.md` a partir de E4e;
- reparte el C3c de ese plan (sección 4.6) entre C3d (cocina) y C3e (sala).

**Maqueta:** hay un primer borrador de estudio (`SalaCocina.dc.html`). Se corrige con la lista de la revisión y se copia a `docs/plans/maquetas/2026-10-22-sala-y-cocina.html`.
- Pestañas del borrador: 0 · Instalar, 1 · Sala · Mis mesas, 2 · Sala · Mesa, 3 · Cocina · estación, 4 · Cocina · pase, 5 · Sala · aviso y 6 · Sala · servido.
- Le faltan: el emparejamiento en iPhone y iPad (Safari que no deja emparejar, código corto en el icono), la pantalla que rechaza una credencial de la otra aplicación y «Por revisar» con el menú entero de un comensal.
- La maqueta aprobada del 15-10 no tiene ni pantalla de cocina ni «Mis mesas»: sus pestañas van de «0 · Abrir mesa» a «5 · En el PC» (`docs/plans/maquetas/2026-10-15-comandero.html:784-786`).
- Se aprueba antes de empezar C3d.

## Resumen para el promotor

Esto es lo que tendrás, en este orden, después de la versión que usas ahora (C3b):
1. **Dos aplicaciones (C3c).** «Costiña Sala» va en los móviles de los camareros y «Costiña Cocina» en la tablet fija de cada estación (caliente, fría y pase). Cada una tiene su nombre, su icono y su color. El QR del PC abre la que toca, y si alguien abre la otra, la pantalla lo dice. Ya trae el aviso «Mesa 4 · Principal listo para servir»: sale en grande en cualquier pantalla de Sala, vibra en Android y la mesa se pone en verde.
2. **Alergias antes de enviar (E4e, ya aprobado).** Cocina deja escrito qué adapta antes de que salga el pase. Además, el programa apunta desde qué móvil se abrió cada mesa.
3. **Costiña Cocina (C3d).** Cada estación ve una comanda por mesa y pase, solo con lo suyo: «Merluza × 4», la alergia en rojo con su adaptación, cuánto tiempo lleva y un botón grande «Listo». El pase ve la mesa entera y qué estaciones han terminado, revisa las alergias y valida. Ese toque es el que avisa al camarero.
4. **Costiña Sala (C3e).** Primero «Mis mesas» y después «Todas». «Mis mesas» son las que abriste tú y las que se abrieron desde los otros móviles de tu sala; con una sola sala, como en Costiña, son todas las mesas ocupadas. Las mesas libres salen al final, para que abrir una siga siendo cosa de 3 toques, con el menú del día, las personas y las alergias. Luego, «Enviar siguiente pase». Solo te avisan tus mesas, y «Servido» es un toque.
5. **Después, lo que ya estaba previsto:** el menú en una pantalla (E4f), los combinados (E4c) y la carta por familias (E4g).

**Límites que conviene saber:**
- El aviso llega si la aplicación está abierta, o en cuanto enciendes la pantalla. Con el móvil bloqueado o la aplicación cerrada no llega hasta que haya certificado público con dominio propio.
- El iPhone no vibra. Un Android solo vibra si se ha tocado la pantalla alguna vez desde que se abrió la aplicación (lo normal al trabajar con ella).
- «Listo» en una estación todavía no se puede deshacer: si se toca por error, el pase no valida hasta ver el plato.
- Con la conexión sencilla, en Android cada icono abre una pestaña del navegador con su nombre, no una aplicación completa.
- En iPhone y iPad, el emparejamiento se hace desde el icono, tecleando un código corto.
- En la sala no suena nada: el móvil solo vibra.

**Lo que no cambia:** ni precios ni importes en los móviles ni en cocina; el servidor decide qué botones salen; no se borra nada.

**Preguntas** (basta una palabra o una línea; entre paréntesis, lo que proponemos). **Contestadas el 22-10** («Si a todo, la 5, va haber android y apple»); el detalle está en la sección 10:
1. En las estaciones, ¿solo «Listo», o también «Empezar»? (solo «Listo») → **solo «Listo»** (C3d).
2. Si se estropea la tablet de una estación, ¿puede el pase marcar «Listo» por ella? (sí) → **sí** (E4e y C3d).
3. ¿A los cuántos minutos avisa la cocina de que una comanda se retrasa? (en ámbar a los 15 y más fuerte a los 25; nunca en rojo, que es para las alergias) → **así** (C3d).
4. ¿Quieres que suenen las tablets de cocina cuando llegue una comanda? (sí, después de tocar «Empezar turno» al encenderlas) → **sí** (C3d).
5. ¿Los móviles de sala y las tablets de cocina son iPhone/iPad o Android? (dinos cuáles: de eso depende cómo se instalan y si vibran) → **habrá de los dos**: los dos caminos de instalación de C3c tienen que quedar sólidos y probados.

## Base técnica

- **Base:** `develop` en `c32f53d` (`0.42.0-c3b`, esquema v8: `Core.Persistence/PostgresStore.cs:25`). Hasta hoy, el siguiente corte previsto era E4e (`docs/STATUS.md:119`).
- **De dónde salen las referencias.** Se leyó la copia de estudio, sin modificarla, y no se compiló ni se ejecutó nada. Las referencias `fichero:línea` son de `c32f53d` y salen de tres lecturas: la PWA, el motor y la práctica del sector (fuentes al final).
- **Rutas cortas.** Son las del plan del 15-10:
  - `Dining/` = `dotnet/src/Costina.Modules.Dining/` y `Dining.Domain/` = `dotnet/src/Costina.Modules.Dining.Domain/`;
  - `Core.Domain/`, `Core.Persistence/`, `Server/`, `Desktop/` y `Client/` = `dotnet/src/Costina.<nombre>/`;
  - `pwa/` = `frontend/pwa/src/`;
  - `plan:` = `docs/plans/2026-10-15-menus-carta-y-bodega.md`.

## Origen

El 22-10, después de usar `0.42.0-c3b`, el promotor escribió lo siguiente. Es la versión en castellano del traspaso; antes de aprobar el plan se sustituye por sus palabras literales.

> «No entiendo muy bien los comanderos: necesitamos una pantalla para los comanderos de los camareros de sala y otra para los de cocina; tal como está, no parece muy intuitivo.»

> «Explícame cómo funcionaría: el camarero lleva el comandero y ve las mesas que tiene abiertas o que lleva; en cocina ven el panel de cocina, los pases que se van abriendo y cómo avanzan, y eso le llega al camarero para que esté al tanto, ¿no?»

**Sus respuestas:**
- **Cocina:** una tablet fija por estación (caliente, fría y pase).
- **Instalación:** dos aplicaciones separadas, «Costiña Sala» y «Costiña Cocina», cada una con su nombre, su icono y su color. El QR del PC abre la que toca.
- **Lo que ve cocina:** comandas por mesa. Hay una tarjeta por mesa y pase, con:
  - los platos agrupados («Merluza × 4»);
  - la alergia en rojo, con su adaptación;
  - el tiempo que lleva;
  - un botón grande «Listo».

**Lo que se le explicó y aceptó:**
1. El camarero abre la mesa: el menú del día ya viene elegido y él pone las personas y las alergias. Después toca «Enviar siguiente pase».
2. Cada tablet de estación recibe al momento **su** comanda.
3. Cada estación toca «Listo».
4. La tablet del **pase** ve la mesa entera y valida cuando todas las estaciones han terminado.
5. El camarero recibe un aviso claro, «Mesa 4 · Principal listo para servir»:
   - una banda grande, con vibración en Android;
   - también cuando está en otra pantalla de la aplicación;
   - límite del iPhone: con la aplicación cerrada no hay aviso mientras no haya certificado público con dominio.
6. Su mesa se pone en verde. Sirve el pase y toca «Servido».
7. **Sala** enseña primero «Mis mesas» y después «Todas». «Mis mesas» son las que abrió él y las de su sala; cada móvil ya va emparejado a una estación de sala, como «Comandero · Sala 1».
8. **Cocina** trabaja por comandas, sin líneas por comensal salvo las de alergias.

**Principios del promotor:**
- más intuitivo: los menos toques posibles, palabras llanas y lo normal ya elegido;
- tema claro, acento `#0E7A5F` y rojo solo para las alergias;
- castellano con tildes, nombres y nunca códigos;
- ni dinero ni importes en los móviles ni en cocina.

En el menú de Costiña todos comen lo mismo en cada pase, salvo las alergias e intolerancias, que cocina adapta (`plan:892-894`).

## 1. Punto de partida

### Una sola aplicación para los dos papeles

- **Una compilación y una sola entrada.**
  - `frontend/pwa/index.html:22` carga `pwa/main.ts:5`, que monta `App.vue`.
  - `frontend/pwa/vite.config.ts:7` fija `base: '/app/'`.
- **La pantalla se elige por el rol, después de emparejar.**
  - `pwa/views/HomeView.vue:28` monta el comandero si el rol no es `kitchen`; si lo es, monta la cocina (`:30`).
  - Por eso `service` y el `main` antiguo reciben el mismo comandero.
- **La cabecera dice «Comandero» o «Pantalla de cocina»** (`pwa/App.vue:25-27`), y la marca está escrita «Costina», sin eñe (`App.vue:88`).
- **El rol y la estación se deciden en el PC al aprobar, no al emitir el código.**
  - El código se pide con el cuerpo vacío (`Desktop/ViewModels/DevicesViewModel.cs:467`; `Server/Program.cs:435-438`).
  - La intención existe solo en el asistente del PC (`DevicesViewModel.cs:473-480`) y viaja al aprobar (`:521`).
  - El servidor exige dos cosas (`Program.cs:441-458`):
    - un dispositivo nuevo nunca es `main` (`:447`);
    - el rol tiene que encajar con el tipo de estación (`:453-454`).
  - `core.pairings` no tiene columna de intención (`Core.Persistence/schema.sql:199-207`), y cada dispositivo tiene exactamente una estación (`schema.sql:194`).
- **El mismo QR sirve para los dos tipos de dispositivo.**
  - `Client/PairingLink.cs:44-48` construye siempre `<dirección>/app/#pair=CÓDIGO`.
  - La pantalla de emparejar explica los dos asistentes del PC (`pwa/views/PairView.vue:53`) y pone «tablet-sala-1» como ejemplo en el campo del nombre (`placeholder`, `:65`).
  - La recogida devuelve solo `pairingId` y `pollSecret` (`Program.cs:477`): **antes de la aprobación el dispositivo no sabe su estación**, solo qué aplicación lo abrió.
- **Cambiar de papel es desvincular y volver a emparejar** («Opciones», `HomeView.vue:36-50`). El guion físico de C3a lo hace con el iPad (`docs/native/C3a-comandero-nuevo.md:253`). En su paso 6 se marcan las elaboraciones en el PC, porque no había pantallas de estación (`:248`). Probablemente de aquí viene la confusión del promotor.
- **Lo que una pantalla pinta no da permisos.** El servidor filtra por rol y estación:
  - `Dining/Affordances.cs:14-20,26-33`, aplicado al tablero en `Dining/DiningModule.cs:51` y como política de la ruta en `:68`;
  - `wrong_station` en `Dining/DiningOperations.cs:64-79`.

  Qué aplicación enseña qué es una decisión de uso, no de seguridad.

### Instalar, emparejar y guardar

- **Un solo manifiesto** (`frontend/pwa/public/manifest.webmanifest`):
  - nombre «Costina» (`:2-3`);
  - `start_url`, `scope` e `id` en `/app/` (`:6,7,12`);
  - color `#F3F4F6` (`:10-11`).

  `index.html` repite el nombre y el color (`:6,10,12,18`). El icono es una «C» blanca sobre `#0E7A5F` (`public/icon.svg:2-3`), con PNG generados por `scripts/make-icons.mjs:8-9`. `tests/install.test.ts:20` fija `/app/`.
- **El motor sirve `wwwroot/app`** (`Server/PwaHosting.cs:17`).
  - Solo redirige la ruta exacta `/app` (`:24`).
  - Ficheros por defecto y estáticos bajo `/app` (`:27-28`).
  - La CSP ya incluye `manifest-src` y `worker-src 'self'` (`:39-40`).
  - Sin cámara (`:44`).
  - Lo de `/app/assets` es inmutable (`:47`).
- **Service worker:**
  - una sola caché, `costina-shell-v1` (`frontend/pwa/public/sw.js:7`);
  - **al activarse, borra todas las demás cachés del origen** (`sw.js:14`);
  - si no hay red, vuelve a `/app/` (`:33`);
  - se registra con alcance `/app/` y solo en producción (`pwa/main.ts:9-11`).
- **Una sola base IndexedDB, `costina`** (`pwa/db.ts:2-4`).
  - La credencial está en la clave `credential` (`pwa/credentials.ts:8`) y no dice de qué rol es (`:10`).
  - La recogida del emparejamiento sí devuelve el rol y la estación (`pwa/pairing.ts:38-40`; `Program.cs:479-493`), pero `PairView.vue:42` los descarta.
- **El código de emparejamiento** tiene 43 caracteres base64url, que distingue mayúsculas (`Core.Persistence/DeviceStore.cs:24-25`), y vale 5 minutos y un solo uso (`:20`). Se guarda solo su huella SHA-256, y la huella es única entre **todas** las filas de `core.pairings`, también las viejas (`DeviceStore.cs:23,45`; `schema.sql:201`). La PWA admite de 8 a 128 caracteres del fragmento (`pwa/pairing.ts:65-68`).
- **Conexión sencilla (B.2, http en la red del local).**
  - El QR lleva una IPv4 privada y el puerto 5089 (`PairingLink.cs:17-38`; `dotnet/AGENTS.md:81`).
  - El PC dice «No hay que instalar nada» (`DevicesViewModel.cs:382`).
  - Por http el navegador no deja instalar ni proteger los datos (`docs/ADR/ADR-014-conexion-sencilla-lan.md:36`; `pwa/storage.ts:14`).
  - El certificado público es el siguiente corte de transporte (`docs/native/B.2-conexion-sencilla.md:51`).
- **En iPad, el icono puede no ver lo guardado en Safari.** Ya se avisó en D6.5 (`docs/native/D6.5-qr-install-physical.md:64`).

### Lo que ve el camarero cuando un pase está listo

- **Cadena actual.**
  1. El pase toca «Validar pase» (`pwa/views/KitchenView.vue:106-108`).
  2. El dominio emite `course.ready` (`Dining.Domain/DiningService.cs:71`).
  3. El aviso va al grupo `ops` (`Core.Persistence/OutboxPublisher.cs:48`), donde están todas las conexiones (`Server/RealtimeEvents.cs:62`).
  4. La PWA ignora a propósito el contenido del aviso (`pwa/realtime.ts:22`) y vuelve a leer el tablero entero (`pwa/live.ts:96`).
  5. Además, relee cada 60 s si hay tiempo real y cada 15 s si no (`live.ts:88`), al reconectar (`:98`) y al volver la pantalla (`pwa/operations.ts:71-72,76`).
- **Cómo se pinta:**
  - el estado de la mesa pasa a «Listo para servir» (`pwa/floor.ts:37`; `pwa/labels.ts:31`);
  - **solo cambia la pastilla** (`pwa/style.css:149`); la tarjeta solo tiene las variantes libre y alarma (`style.css:124-125`; `pwa/views/FloorGrid.vue:50`);
  - en la ficha abierta, el botón grande pasa a servir (`pwa/ficha.ts:10`).
- **Nadie detecta el cambio.**
  - `pwa/views/BoardView.vue:67-70` solo vigila `readAt`.
  - En `pwa/` no hay ni banda, ni vibración, ni sonido, ni notificación.
  - Con una ficha, «Abrir mesa» o Bebidas abiertas, el plano no se pinta (`BoardView.vue:100-122`): nada avisa de otra mesa.
  - La maqueta promete «Te avisa cuando esté listo para servir» (`maqueta:971`), y no está hecho.
  - El documento de cocina lo dice: «Sin sonido ni notificaciones del sistema al entrar un pase» (`docs/native/D6.4-pwa-kds.md:48`). D6.1 deja fuera Background Sync (`docs/native/D6.1-pwa-base.md:21`).
- **Un pase validado puede dejar de poderse servir.** Una alergia declarada después de validar marca para revisar también el pase listo, porque «activo» incluye «Listo» (`Dining.Domain/CourseExecution.cs:32,141-147`). El pase sigue en «Listo para servir», pero `serve` deja de anunciarse (`AllowedActions.cs:43`), y el plano lo sigue pintando como listo (`floor.ts:37`).

### «Mis mesas»: faltan dos datos

- **Nadie guarda quién abrió la mesa.** No está en `DiningView` (`Dining.Domain/ServiceModels.cs:42-50`), ni en la instantánea (`Dining.Domain/Snapshots.cs:8-11`), ni en `dining.services` (`Dining/schema.sql:5-11`). El actor solo queda en la auditoría y en el aviso (`DiningOperations.cs:52-58`).
  - Las órdenes sí conocen el actor y la estación: `ExecutionIdentity` (`Core.Persistence/Contracts.cs:38`).
  - `/session` da la estación del dispositivo y su nombre (`Server/DesktopReadRoutes.cs:27-30`).
- **Las estaciones no saben de qué sala son.** Las mesas tienen sala (`Core.Domain/Organization.cs:10`), pero las estaciones no (`:11`; `schema.sql:66-71`). En el laboratorio, la sala `sala` y la estación `sala-1` no tienen relación (`Server/LabConfiguration.cs:17-20`).
- **Atribuir a una persona** (el camarero) quedó para el Hito 7 y «exige ADR» (`docs/plans/2026-09-24-inicio-y-dispositivos.md:135`). «Mis mesas» por **dispositivo** no es atribución a una persona: el nombre del dispositivo ya es el actor de la auditoría (`dotnet/AGENTS.md:25,69`).
- **El nombre del dispositivo no es único.** El PC solo avisa de un nombre repetido (F12, `AGENTS.md:69`), y el actor es `device:<nombre>`. Dos móviles con el mismo nombre compartirían «mis mesas»; uno que se empareja de nuevo con su mismo nombre conserva las suyas.
- **El puesto principal también abre mesas como dispositivo:** en el nivel Puesto es un dispositivo `service` con una estación de sala (`AGENTS.md:71`). Con la contraseña del responsable es una persona, sin estación (`DiningOperations.cs:66`).
- **Plano actual:** todas las mesas de todos los móviles. Las pestañas de sala solo salen con dos salas o más (`FloorGrid.vue:15-22`).

### La pantalla de cocina

Lógica en `pwa/kitchen.ts` y vista en `pwa/views/KitchenView.vue`.
- **Qué ve cada pantalla.**
  - El pase lo ve todo (`kitchen.ts:19`).
  - Una estación ve el pase entero, con lo de las demás atenuado y marcado «(otra estación)» (`kitchen.ts:31-33`; `KitchenView.vue:75-77`). Es una regla escrita de D6.4 (`dotnet/AGENTS.md:47`).
  - Lo que aún no se ha enviado nunca sale (`kitchen.ts:29`).
  - D6.4 también deja escrito «sin alarmas por tiempo» y «no hay vista […] de "lo siguiente" sin enviar» (`D6.4-pwa-kds.md:48`). C3d cambia las dos cosas.
- **Una línea por elaboración, y las elaboraciones son una por comensal** («una POR COMENSAL», `frontend/pwa/e2e/kitchen.spec.ts:17,89`).
  - Cada línea lleva «Comensal n» (`KitchenView.vue:77`) y sus botones «Empezar» y «Marcar lista» (`:44-47,85-91`).
  - «Merluza × 4» son 4 órdenes, 8 con «Empezar».
  - Cada orden bloquea la pantalla hasta la relectura (`operations.ts:38`).
- **Revisar exige escribir una nota** (`kitchen.ts:57`; `KitchenView.vue:93-102`). Validar está en `:106-108`.
- **Lo de ayuda y lo aplazado.**
  - Arriba, un párrafo de ayuda largo (`KitchenView.vue:28-30,62`).
  - Agrupar por plato quedó para C3c (`KitchenView.vue:16-17`).
  - Sin sonido al entrar un pase (`D6.4-pwa-kds.md:48`).
- **La comanda y su tiempo.** La comanda ya es una por mesa y pase (`kitchen.ts:35`), de la más antigua a la más nueva (`:41-45`). Debajo, «enviado hace N min» (`:50-54`).

### El motor de cocina

- **Enviar el siguiente pase (`fire-next`).**
  - Exige la mesa «En servicio» (`DiningService.cs:43`).
  - Se niega si queda cualquier revisión pendiente (`:44`) u otro pase activo (`:45`).
- **Marcar lista una elaboración** admite «Enviada» o «En preparación» (`Dining.Domain/CourseExecution.cs:106-107`): «Empezar» es opcional, y el servidor ya anuncia `preparation-ready` desde «Enviada» (`Dining.Domain/AllowedActions.cs:56-57`).
- **Validar un pase (`ready`).**
  - Exige que todo lo obligatorio esté listo y nada por revisar (`CourseExecution.cs:112-120`).
  - Se anuncia solo en el pase (`AllowedActions.cs:41-42`; `Affordances.cs:32-33`).
- **Servir (`serve`).**
  - Exige el pase validado y nada por revisar (`CourseExecution.cs:122-128`).
  - Un camarero puede servir (`Affordances.cs:17`).
  - No hay forma de recuperar un pase ya enviado: `skip` solo vale para pases pendientes (`CourseExecution.cs:130-137`).
- **Matriz de estación** (`DiningOperations.cs:64-79`).
  - `preparation-start|ready` exigen la estación de la elaboración.
  - `ready` y `review-preparation` exigen `pase`.
  - **Cualquier otra acción pasa sin comprobar** (`:66-78`).
  - **Por rol, la sala solo tiene prohibidas** `complete`, `cancel-unstarted`, `release` y `review-preparation` (`Affordances.cs:17`). `preparation-start|ready` y `ready` le están permitidos por rol, y lo que los frena es la estación. Un comandero de sala no tiene elaboraciones de su estación; uno de barra sí podría marcar las de la barra.
  - En `Affordances`:
    - `KitchenAllowed` (`:10`);
    - lo que la sala tiene excluido (`:17`);
    - `StationKeeps` y `PassKeeps` (`:26-33`);
    - `Filter` (`:35-46`).
- **Una orden por elaboración** (`DiningOperations.cs:89-90`), y cada una sube la versión de la mesa (`:82`).
- **El tablero es el mismo para todos los roles**; solo se filtran las acciones (`DiningModule.cs:48-51`).

### Reglas que ponen límites

- **Tiempo real:**
  - los avisos son finos y sin datos;
  - cada aviso y cada reconexión provocan la misma relectura autoritativa;
  - nunca se mete en el canal la confirmación de cocina (`dotnet/AGENTS.md:11,43`).
- **Una sola orden incierta durable** por cliente (`AGENTS.md:45`), y una orden por gesto del usuario (`plan:197`).
- **Sin dinero ni palabras de caja.**
  - La guarda rechaza claves con `price|cents|amount|…` (`pwa/money-guard.ts:4`).
  - Las E2E fallan si se ve «precio», «cuenta», «pago», «saldo» o «€»: en sala como palabras enteras y en cocina incluso dentro de otra palabra (`frontend/pwa/e2e/support.ts:14-15,60-66`).
- **Lo que exige https** (contexto seguro):
  - service worker, instalación, almacenamiento persistente, notificaciones, Push y pantalla siempre encendida (Wake Lock). Por http, `main.ts:9` no registra el service worker. **Con la conexión sencilla no hay service worker ni caché de la aplicación**: separar cachés y service workers solo cuenta con https (D5.3 o el certificado público);
  - la vibración y el sonido con Web Audio no lo exigen, pero los dos necesitan que la página se haya tocado al menos una vez desde que se cargó (activación del usuario; tras recargar, otra vez). Se confirma en el guion físico.

  Son hechos de la plataforma.
- **iPhone y iPad:**
  - no hay `navigator.vibrate`;
  - las notificaciones web solo llegan a una aplicación añadida a la pantalla de inicio (iOS 16.4 o posterior), con https de confianza y el servicio de Apple.
- **El laboratorio tiene 8 mesas y todas tienen dueño en las E2E** (`LabConfiguration.cs:18`):
  - M1 la usa `drinks.spec.ts`;
  - M2, `pwa_http.py`;
  - M3–M5, el proyecto móvil;
  - M6–M8, el de escritorio (`e2e/support.ts` LANES; `AGENTS.md:47,99`).

  Los proyectos corren uno detrás de otro (`playwright.config.ts:21`).
- **Los datos del laboratorio se siembran una sola vez**, con la clave idempotente `lab-fixtures-v1` (`LabConfiguration.cs:14`): una mesa o una estación nuevas en ese fichero no llegan a una base ya sembrada. Lo que una prueba necesita de más se crea por API con el token principal (`organization/commands/table-create|station-create`, `Server/OrganizationOperations.cs:18-19`), con códigos propios de la ejecución, y se desactiva al terminar.

### Referencias desfasadas del plan del 15-10

- `plan:607` cita `StationAllowed` en `DiningOperations.cs:57-72`; ahora está en `:64-79`. También cita la exclusión de sala en `Affordances.cs:15` (ahora `:17`) y `StationKeeps`/`PassKeeps` en `:24-31` (ahora `:26-33`).
- `plan:668` cita `ServiceActions.vue:84,151-155`, que ya no existe: ahora es `pwa/views/TableCard.vue`.
- `plan:678` cita `kitchen.ts:120-138`, pero el fichero tiene 63 líneas; el filtro está en `:29`.
- `docs/plans/2026-09-24-inicio-y-dispositivos.md:137` cita `schema.sql:178`; ahora es `:194`.
- `plan:694` cita `board.spec.ts:103` y `kitchen.spec.ts:151`, que ya no existen (los ficheros tienen 96 y 113 líneas). La regla de las palabras de caja vive en `e2e/support.ts:14-15,60-66`.

## 2. Cómo funciona

1. **Instalar.**
   - En el PC, Inicio › Dispositivos: «Conectar Costiña Sala · Sala 1» o «Conectar Costiña Cocina · Cocina caliente».
   - El QR abre la aplicación que toca; se escribe el nombre del dispositivo y el responsable lo aprueba en el PC.
   - En iPhone y iPad, la página que abre el QR en Safari **no deja emparejar**: explica «Compartir › Añadir a pantalla de inicio». Después se abre el icono y se teclea el código corto que enseña el PC. Si caduca mientras tanto, el PC da otro con «Generar otro código» (`Desktop/Views/DevicesView.xaml:102`; `DevicesViewModel.cs:461-463`).
2. **Abrir la mesa (Sala).** Se toca una mesa libre y sale la hoja: personas, menú del día ya elegido y alergias. «Abrir mesa» es una orden.
3. **Primer pase.**
   - El botón grande dice «Enviar siguiente pase · Aperitivo» (E4e: `fire-next` desde «Sentados»).
   - Si hay una alergia, el pase la revisa antes (E4e). Mientras tanto, el botón queda apagado y dice lo mismo que hoy y que el puesto: «Esperando la revisión de cocina» (`pwa/ficha.ts:52`).
4. **Cocina.**
   - Cada estación recibe **su** comanda en la siguiente relectura, que llega con el aviso del canal, en menos de un segundo en una red normal.
   - Toca «Listo», que es una orden.
5. **Pase.**
   - Ve la mesa entera: «Cocina caliente: lista · Cocina fría: 1 de 2».
   - Cuando el servidor anuncia `ready`, toca «Validar pase: listo para servir», que es una orden.
6. **Sala.**
   - En la siguiente relectura, la banda «Mesa 4 · Principal listo para servir» sale en cualquier pantalla, el móvil vibra (Android) y la mesa se pone en verde. La banda solo sale mientras el servidor anuncia `serve` en ese pase.
   - «Servido», en la banda o en la ficha, es una orden.
7. Se repite desde el paso 3 con el siguiente pase.

**«Más intuitivo» en este plan** (sigue valiendo la sección 2 del plan del 15-10):

| Tarea | Hoy | Objetivo |
|---|---|---|
| Poner en marcha un móvil de camarero | un único «Costina» que se vuelve comandero o cocina según lo que se apruebe (`HomeView.vue:28-30`) | QR de «Conectar Costiña Sala» → nombre → aprobar: se abre «Costiña Sala», con su icono |
| Enterarse de que un pase está listo | mirar el plano, donde solo cambia la pastilla; dentro de una mesa o en Bebidas no se ve nada (`BoardView.vue:100-122`) | banda grande en cualquier pantalla de Sala, vibración en Android y la mesa en verde |
| Servir un pase listo | volver al plano, abrir la mesa y «Marcar servido» | «Servido» en la banda: 1 toque |
| «Merluza × 4» lista en una estación | 4 órdenes (8 con «Empezar»), con la pantalla bloqueada entre ellas (`operations.ts:38`) | «Listo»: 1 toque, 1 orden |
| Saber en el pase qué falta | recorrer líneas por comensal de todas las estaciones | una tarjeta por mesa: «Cocina caliente: lista · Cocina fría: 1 de 2» |
| Ver solo las mesas propias | todas las mesas del local | «Mis mesas» primero |
| Abrir una mesa libre | mesa libre → personas → «Abrir»: 3 toques (`plan:206`) | igual desde «Mis mesas»: las libres salen al final |

## 3. Vocabulario

Solo cambian palabras visibles. Los códigos de la API (`service`, `kitchen`, `ready`, `serve`…) se quedan.

| Palabra | Dónde | Sustituye a |
|---|---|---|
| «Costiña Sala» | aplicación de los camareros: icono, título, cabecera y botón del PC «Conectar Costiña Sala» | «Comandero» como tipo de dispositivo (`App.vue:25`), «Costina» |
| «Costiña Cocina» | aplicación de las tablets de cocina | «Pantalla de cocina» |
| «Mis mesas» · «Todas» | pestañas de Sala | «Todas las salas», que sigue dentro de «Todas» cuando hay 2 salas o más |
| «Comanda» | tarjeta de cocina: una mesa y un pase | (en pantalla no se dice «ticket») |
| «Listo» | botón de una estación | «Marcar lista» en cada línea |
| «Hecho · esperando al pase» | estación: comandas suyas terminadas que el pase aún no ha validado | — |
| «También en: Cocina fría» | estación: las otras estaciones del pase | líneas atenuadas «(otra estación)» |
| «Lleva 18 min» | tiempo desde que se envió | «enviado hace 18 min» (`kitchen.ts:53`) |
| «Validar pase: listo para servir» | botón del pase | igual que hoy (`KitchenView.vue:108`) |
| «Esperando al camarero · listo hace 3 min» | pase: comandas validadas y sin servir, con el tiempo desde `readyAt` | — (nunca «Sala avisada»: el servidor no sabe si el aviso llegó, `AGENTS.md:11`) |
| «Visto» | banda de Sala: la quita en este móvil sin servir | — |
| «Por empezar», «En preparación», «Lista» | estado de un plato, donde haga falta | igual que hoy (`labels.ts:28`); nada de «en marcha» |
| «Siguiente: Postre (sin enviar)» | pase | — |
| «Por revisar en el pase» | estación, cuando una alergia llega con el pase ya enviado | «PENDIENTE DE REVISIÓN» |
| «Servido» | botón del camarero | «Marcar servido» |
| «Empezar turno» | primera pantalla de una tablet de cocina al encenderla | — |

- Se mantienen las palabras de C3a, C3b y E4d: «Sentados», «En cocina», «Listo para servir», «Toda la mesa», «Comensal n», «Enviar siguiente pase», «Adaptado: …» y «Por revisar en cocina», que es lo que ve el camarero.
- «Toda la mesa» significa lo que hoy: un plato sin comensal o una restricción de toda la mesa (`labels.ts:78`). Un grupo «Merluza × 4» no la lleva.
- Las estaciones se nombran con su nombre configurado entero («Cocina caliente»), nunca con abreviaturas inventadas («Caliente»).
- Ninguna palabra nueva contiene «precio», «cuenta», «pago», «saldo» ni «€».

## 4. Cortes

Una PR por corte. Las versiones siguen a `0.42.0-c3b`.

| Orden | Corte | Versión | Esquema | Qué gana el promotor |
|---|---|---|---|---|
| 1 | **C3c**: dos aplicaciones y aviso de «listo para servir» | `0.43.0-c3c` | sin cambio (el motor solo añade) | dos iconos con nombre y color, el QR abre la que toca, la banda y la mesa en verde |
| 2 | **E4e**: alergias antes de enviar, órdenes de grupo y quién abrió la mesa | `0.44.0-e4e` | sin cambio (sí cambian las reglas de las instantáneas) | la adaptación queda escrita; base para «Listo» y «Mis mesas» |
| 3 | **C3d**: Costiña Cocina, comandas por mesa (estaciones y pase) | `0.45.0-c3d` | sin cambio, motor intacto | «Merluza × 4», «Listo» con un toque y el pase con la mesa entera |
| 4 | **C3e**: Costiña Sala, mis mesas y abrir y servir el menú | `0.46.0-c3e` | sin cambio, motor intacto | «Mis mesas», abrir en 3 toques con alergias, «Servido» |
| 5 | **E4f**: el menú en una pantalla | `0.47.0-e4f` | sin cambio | como en el plan del 15-10 |
| 6 | **E4c**: combinados a precio fijo | `0.48.0-e4c` | **v9** | como en el plan del 15-10 |
| 7 | **E4g**: carta por familias y comanda de una vez | `0.49.0-e4g` | sin cambio | como en el plan del 15-10 |

**Qué se mueve y por qué:**
- **C3c pasa delante de E4e.**
  - Responde a lo que pidió el promotor y no depende de E4e.
  - Antes de construir pantallas encima, prueba con sus dispositivos lo que más riesgo tiene: dos aplicaciones en el mismo origen, el iPad con su almacenamiento aparte, Android por http y los dispositivos ya emparejados.
  - Se puede volver atrás en el servidor: no guarda nada que un motor anterior no lea. En los dispositivos, uno emparejado ya con C3c tiene su credencial en `costina-sala` o `costina-cocina`, que la versión anterior no mira: habría que emparejarlo otra vez y quitar el viejo desde el PC.
  - Si el promotor prefiere antes las alergias, se intercambian los dos sin tocar nada más.
- **E4e no cambia lo aprobado** (`plan:583-652`). Añade tres concreciones (sección 4.2):
  - el contrato exacto de las órdenes de grupo;
  - la matriz de estación con las líneas de hoy, con las órdenes de grupo permitidas a los mismos roles que las individuales;
  - quién abrió la mesa.
- **La cocina nueva (C3d) sí necesita E4e.** Sin `preparations-ready`, «Listo» serían N órdenes bloqueantes (`operations.ts:38`; `plan:197`), y sin revisión antes de enviar no hay «Por revisar» de pases sin enviar (`kitchen.ts:29`). Por eso C3d va detrás de E4e y no puede adelantarse.
- **El C3c aprobado** («Comandero y cocina: abrir mesa y servir el menú», `plan:654-698`) **se parte en dos**:
  - la mitad de cocina pasa a **C3d**, cambiada según el promotor: cada estación ve solo lo suyo, hay un único «Listo» y el pase ve la mesa entera con el progreso de cada estación. En lugar de «Empezar todas» y «Listas todas», solo «Listo», y «Empezar» si el promotor lo pide;
  - la mitad del camarero pasa a **C3e**, con «Mis mesas», el aviso solo de las mesas propias y «Servido».
  - El nombre «C3c» pasa a las dos aplicaciones. Ya hay un precedente de renumeración (`plan:966`, revisión 29). Lo que `dotnet/AGENTS.md:99` deja para «C3c» en el comandero pasa a C3e: «Preguntar al servidor» para una orden de otra instalación, y la carta.
- **C3d va antes que C3e** porque cocina es donde más toques sobran (`plan:211`) y porque el aviso al camarero, que ya llega en C3c, depende de que el pase valide con facilidad.
- **E4f, E4c y E4g** no cambian de contenido ni de orden entre ellos; solo de versión. E4c sigue siendo el que sube el esquema a v9 (`plan:700`): este rediseño no toca el esquema.
- **Cada corte lleva lo de siempre** (`plan:270-276`):
  - rama propia y un commit por paso;
  - su documento `docs/native/<corte>.md`;
  - su párrafo en `dotnet/AGENTS.md` y su línea en `docs/STATUS.md`;
  - capturas;
  - el permiso de subida y mezcla pedido al promotor;
  - un ADR nuevo donde cambia una regla escrita:
    - C3c: dos aplicaciones operativas en el mismo origen, que precisa D6.5;
    - E4e: revisión antes de enviar y el pase que puede marcar por una estación;
    - C3d: la estación solo ve lo suyo, que sustituye ese punto de D6.4;
    - C3e: «Mis mesas» por dispositivo, que no es atribución a una persona.

### 4.1. C3c: dos aplicaciones, Costiña Sala y Costiña Cocina (`0.43.0-c3c`, sin cambio de esquema; el motor solo añade)

**PWA: dos entradas de la misma compilación**
- **Vite multipágina**, con `base: '/app/'` de siempre:
  - `sala/index.html` → `pwa/sala.ts` → `SalaApp.vue` (cabecera y `BoardView`);
  - `cocina/index.html` → `pwa/cocina.ts` → `CocinaApp.vue` (cabecera y `KitchenView`);
  - lo común queda en un `Shell.vue` que recibe la aplicación como dato: el arranque (`App.vue:29-52`), `PairView`, `operations.ts`, `StatusPanel.vue`, `api.ts`, `money-guard.ts` y `startup.ts`;
  - los trozos compartidos van a `/app/assets/`, inmutables (`PwaHosting.cs:47`).
- `HomeView.vue:28-30` deja de elegir la pantalla por rol: cada aplicación monta la suya, y el rol solo se **comprueba** (más abajo).
- **Todo sigue bajo `/app/`, en el mismo origen** (`AGENTS.md:49`). `PwaHosting` sirve `/app/sala/` y `/app/cocina/` con el mismo código (`PwaHosting.cs:27-28`) y la misma CSP (`:39-40`), que no cambia.

**Instalación: un manifiesto, un icono y un color por aplicación**
- **`public/sala/manifest.webmanifest`:**
  - `id`, `start_url` y `scope` en `/app/sala/`;
  - `name` y `short_name` «Costiña Sala»;
  - `theme_color` `#0E7A5F`.
- **`public/cocina/manifest.webmanifest`:**
  - `id`, `start_url` y `scope` en `/app/cocina/`;
  - «Costiña Cocina»;
  - su color. Propuesta: azul pizarra `#2F4A6D`, que no es rojo (alergias) ni ámbar (pendiente). El teja que probó la maqueta (`#9E4A1C`) se descarta: junto al rojo de las alergias (`#A3231A`, `style.css:11`) se confunde a distancia.
- **`short_name`:** «Costiña Sala» cabe (12 caracteres). «Costiña Cocina» tiene 14 y muchos lanzadores la cortan, así que su `short_name` es «Cocina». `name`, `<title>`, `application-name` y `apple-mobile-web-app-title` llevan el nombre entero.
- **`background_color` sigue en `#F3F4F6`:** el tema claro no cambia. El color de cada aplicación solo se ve en el icono, en `theme_color` y en la marca de la cabecera. **Dentro de las dos aplicaciones, los botones siguen con el acento `#0E7A5F`**, como hoy (`style.css:8,58`), también «Listo» y «Validar pase».
- **Cada `index.html` lleva lo suyo:** `<title>`, `apple-mobile-web-app-title`, `application-name`, `theme-color`, `apple-touch-icon` de 180 px y el enlace a su manifiesto. Hoy todo eso es uno solo (`index.html:6-18`).
- **Iconos.** `scripts/make-icons.mjs` (`:8-9`) genera dos juegos (192, 512, maskable 512 y 180):
  - Sala: un plato con campana sobre el verde de acento;
  - Cocina: un gorro de cocinero sobre azul pizarra.

  Siguen siendo trazos propios, sin imágenes externas.
- **«Costina» pasa a «Costiña»** donde se ve: `manifest.webmanifest:2-3`, `index.html:10,12,18` y `App.vue:88`.

**Un service worker por aplicación**
- **Cada uno con lo suyo:**
  - `/app/sala/sw.js`: alcance `/app/sala/` y caché `costina-sala-v1`;
  - `/app/cocina/sw.js`: alcance `/app/cocina/` y caché `costina-cocina-v1`.

  Cada uno guarda su página, su manifiesto, sus iconos y los `/app/assets/` que pide su página; sin red, vuelve a **su** página.
- **Arreglo previo imprescindible.** Hoy, al activarse, cada service worker borra todas las cachés del origen que no sean la suya (`sw.js:14`): con dos aplicaciones, cada una borraría la de la otra. Cada una pasa a borrar solo las de su prefijo.
- **El `/app/sw.js` de hoy se retira.** Su alcance, `/app/`, cubre también las dos rutas nuevas (`main.ts:10`). Se cambia por uno que borra `costina-shell-v1` y se da de baja (`registration.unregister()`). Como el navegador solo lo actualiza al navegar por páginas que controla (el enrutador), el enrutador además da de baja cualquier registro de alcance exacto `/app/` (`navigator.serviceWorker.getRegistrations()`).
- Un service worker de alcance `/app/sala/` recibe igualmente las peticiones a `/app/assets/` de sus páginas, así que los trozos compartidos se cachean sin ampliar alcances.
- **Con la conexión sencilla nada de esto se ejecuta**: por http no hay service worker (`main.ts:9`). Las pruebas de cachés aisladas valen para https y para la E2E en loopback.
- Sigue sin tocar `/api/` y sin Background Sync (`AGENTS.md:41`).

**Lo guardado, separado por aplicación**
- **Una base por aplicación.** Hoy hay una sola base IndexedDB, `costina` (`db.ts:2`). En Android, dos aplicaciones del mismo origen la compartirían, con la misma credencial y el mismo hueco de orden pendiente. Pasan a `costina-sala` y `costina-cocina`, con los mismos almacenes.
- **Adopción de lo de antes.**
  1. Si la base propia está vacía y la vieja `costina` tiene credencial, la aplicación lee `/session` con ella.
  2. Solo si el rol le corresponde, la copia.
  3. También copia la orden pendiente con `reserve` (D6.7: comprobar y escribir en una transacción) y después la descarta de la base vieja **por su clave**.

  La base vieja no se borra, porque la usa el enrutador. Una credencial de otro rol se deja donde está. Un 401 con la credencial vieja la borra, como hoy (`AGENTS.md:69`: el 401 es la única señal).
- **En iPhone y iPad**, cada icono de la pantalla de inicio guarda aparte, separado de Safari (`D6.5-qr-install-physical.md:64`): un icono nuevo se empareja desde el propio icono, con el código corto.
  - Un icono «Costina» ya instalado (alcance `/app/`) probablemente sigue funcionando: al ir del enrutador a `/app/sala/` no sale de su alcance ni de su almacén, y adopta su credencial. Seguiría llamándose «Costina» hasta instalar el nuevo. Se comprueba en el guion físico.

**`/app/` pasa a ser un enrutador, no una aplicación**
- No tiene manifiesto ni las metas de iOS (`apple-mobile-web-app-capable`, `index.html:8-10`), así que no se instala como aplicación.
- **Con una credencial** (primero en `costina-sala` y `costina-cocina`, que en Android comparten origen; si no, en la vieja `costina`), lee `/session` y lleva a `/app/sala/` o a `/app/cocina/` según el rol. Así un acceso directo viejo a `/app/` sigue llegando a su aplicación después de la adopción.
- **Con `#pair=`** (un QR o un enlace de antes), pregunta «¿Este dispositivo es para la sala o para la cocina?» y pasa el fragmento a la elegida.
- **Sin nada**, explica cómo conectar el dispositivo desde el PC.
- Un Android con el «Costina» de alcance `/app/` ya instalado tiene que quitar ese icono, y el guion físico lo dice. Si no, ese icono puede quedarse con los enlaces de `/app/sala/`.

**Cada aplicación comprueba su rol**
- **`appFits(app, role)`**, función pura como `startup.ts`, probada con vitest:
  - Sala admite `service`, y también el `main` antiguo con el aviso de siempre (`App.vue:25`);
  - Cocina solo admite `kitchen`;
  - un rol que no se conoce no se monta en ninguna de las dos y se enseña tal cual (fallo cerrado, `AGENTS.md:75`).
- **Al arrancar**, con una credencial que no le toca, no monta nada. Dice «Este dispositivo está conectado como Costiña Cocina · Pase. Ábrelo con el icono de Costiña Cocina» y ofrece «Desvincular».
- **Al emparejar.** Hoy se descartan el rol y la estación de la recogida (`PairView.vue:42`). Si el rol no corresponde a la aplicación:
  - no se guarda la credencial;
  - se da de baja en el servidor con `/auth/unpair`, como «Desvincular»;
  - se dice «Este código era de Costiña Cocina: pide en el PC uno de Costiña Sala».

  Así no queda ningún dispositivo huérfano en el servidor.
- **En iPhone y iPad, fuera del icono** (Safari, `navigator.standalone` falso), la página no ofrece «Solicitar emparejamiento»: explica cómo añadir el icono. Si emparejara en Safari, el servidor tendría un dispositivo aprobado que el icono no puede usar.
- **Opcional, más fuerte y con esquema:** fijar la intención al emitir el código (`POST /auth/pairings {role, station}`, columna nueva en `core.pairings`, `schema.sql:199-207`). Así una solicitud desde la aplicación equivocada se rechaza antes de aprobar. Queda diferido (sección 6): la comprobación de arriba basta, porque la seguridad no depende de ella.
- **La seguridad no cambia.** El servidor sigue siendo la frontera (sección 1).

**El QR del PC abre la aplicación que toca**
- **`PairingLink.Build(address, code, app)`** (`PairingLink.cs:44-48`) da `/app/sala/#pair=…` o `/app/cocina/#pair=…`.
  - La aplicación sale de la intención del asistente, que se conoce antes de emitir el código (`DevicesViewModel.cs:473-480`, `:467`).
  - Sin aplicación, `/app/#pair=…`, que es el enrutador.
- **Textos del PC.**
  - Los botones y títulos pasan a «Conectar Costiña Sala · <estación>» y «Conectar Costiña Cocina · <estación>» (`DevicesViewModel.cs:70,100-101,477`; `MainWindow.xaml:316`).
  - Los grupos de la lista de dispositivos usan los mismos nombres. Los tipos de dispositivo **no** están en la tabla común `vocabulary-cases.json` (sus familias son dining, course, preparation, tableState, offerKind, stationKind, restrictionKind, severity, review y action): cambian a mano `Client/Labels.cs:40-41` (`DeviceRoles`, `SessionRoles`) y su copia `pwa/App.vue:25` (`KINDS`), con sus comprobaciones en ClientChecks y DesktopChecks. `HomeViewModel.cs:321` y `DevicesView.xaml` llevan los mismos textos.
  - El texto del QR deja de decir «No hay que instalar nada» (`DevicesViewModel.cs:382`) y explica cómo añadir el icono.
- **Nombre al emparejar:** el ejemplo del campo deja de ser «tablet-sala-1» (`PairView.vue:65`) y pasa a ser uno por aplicación («móvil de Ana», «tablet del pase»). Sigue siendo un ejemplo y no un valor relleno: antes de aprobar el dispositivo no sabe su estación (`Program.cs:477`), y el nombre es el actor de la auditoría y de «Mis mesas», así que tiene que ser distinto en cada dispositivo (el PC ya avisa de los repetidos, F12).

**Código corto para teclear (motor, solo añade)**
- **Por qué hace falta.** En iPhone y iPad la cámara abre el enlace en Safari, nunca en el icono, y el icono no ve lo que guarda Safari. Hay que emparejar desde el icono, y hoy el código tiene 43 caracteres (`DeviceStore.cs:24-25`).
- **Cómo funciona.**
  - `POST /auth/pairings` acepta `{typed: true}`, un campo nuevo y opcional: hoy el cuerpo va vacío (`Program.cs:435-438`).
  - El código pasa a tener 10 caracteres de un alfabeto sin ambigüedades: `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`, 50 bits.
  - El PC lo enseña en dos grupos, por ejemplo «K7M2Q 9XR4T»; el QR lo lleva sin el espacio.
  - `claim` lo acepta en mayúsculas o minúsculas y con espacios o guiones. **La normalización no puede tocar el código largo**, que es base64url y distingue mayúsculas (`DeviceStore.cs:24-25`): `claim` busca la huella del código tal cual y, solo si sin espacios ni guiones y en mayúsculas son 10 caracteres del alfabeto corto, también la de esa forma.
  - La huella es única en toda la tabla (`schema.sql:201`): si una emisión choca con una vieja, se genera otro código en vez de dar un 500.
  - Sin `typed`, el código es el largo de siempre. El asistente del PC lo pide siempre, y el QR lleva ese mismo código.
  - Un motor anterior ni lee el cuerpo (`Program.cs:435-438`) y devuelve el largo: el PC enseña el que reciba.
- **Sigue igual de protegido:**
  - un solo uso y 5 minutos (`DeviceStore.cs:20`);
  - el freno de intentos por origen y destino (`Program.cs:466-476`; `AGENTS.md:31,71`);
  - sobre todo, la aprobación en el PC, con el nombre y la intención a la vista.

  Cabe en el alfabeto cerrado del QR (`PairingLink.cs:41-42`; `pairing.ts:66`).

**Redirecciones (motor):** `PwaHosting` redirige `/app/sala` a `/app/sala/` y `/app/cocina` a `/app/cocina/`, como ya hace con `/app` (`PwaHosting.cs:24`).

**Aviso de «listo para servir» (Sala, sin tocar el motor)**
- **De dónde sale.** `pwa/sala.ts`, sin DOM, lista los pases de lo leído que **anuncian `serve`**, con la clave `servicio:pase:readyAt`.
  - No basta con el estado Ready: una alergia declarada después de validar deja el pase en Ready pero sin `serve` (sección 1; `AllowedActions.cs:43`). Ese pase no saca banda: la tarjeta enseña su aviso rojo «· por revisar», y la banda vuelve, con vibración, cuando cocina lo revisa.
  - Para un camarero, `serve` se anuncia exactamente cuando el pase está listo, sin nada por revisar y la mesa en servicio o en pausa (`AllowedActions.cs:43`; `Affordances.cs:17`).
  - Sale de la relectura autoritativa, **nunca** del contenido del aviso del canal (`realtime.ts:22`; `AGENTS.md:11`): que un aviso se repita o se pierda no cambia nada.
  - Un «Rehacer» del pase quita `readyAt` (`CourseExecution.cs:159-160`), así que la siguiente validación avisa otra vez.
- **Una banda grande en cualquier pantalla de Sala.**
  - Dice «Mesa 4 · Principal listo para servir», con nombres y nunca códigos.
  - Lleva tres botones:
    - «Ver mesa»;
    - «Servido», solo si el servidor anuncia `serve` en ese pase, con la versión leída (F01; `ficha.ts:10`);
    - «Visto».
  - Va en `BoardView`, por encima de la ficha, de «Abrir mesa» y del plano, que hoy se excluyen entre sí (`BoardView.vue:100-122`).
  - **Ocupa su sitio debajo de la cabecera, no tapa**: no puede esconder el botón de volver, el buscador de Bodega ni la bandeja de la ronda con «Enviar», porque se queda hasta «Servido» o «Visto».
  - Si hay varias, la más antigua arriba y «+2 más».
- **Vibración.**
  - Solo cuando aparece una clave nueva entre dos lecturas correctas y con la página a la vista: `navigator.vibrate`, Android con Chrome.
  - Chrome solo deja vibrar si la página se ha tocado desde que se cargó; tras una recarga, hasta el primer toque no vibra. No necesita https.
  - En la primera lectura no vibra: lo que ya estaba listo sale en la banda y en verde.
  - **En la sala no suena nada.** Supuesto: un restaurante con estrella no pita en el comedor. Se le dice al promotor en el resumen.
- **Mesa en verde:** una variante `.tile.ready` con los tonos `--ok` (`style.css:9`), además de la pastilla de hoy (`style.css:149`; `FloorGrid.vue:50`).
  - Sale del mismo estado de la tarjeta (`floor.ts:37`), sin tocar `floor.ts` ni su tabla de casos común.
  - Con una alergia que alarma, el borde rojo de `.tile.alarm` (`style.css:125`) manda sobre el verde: el fondo es verde y el borde, rojo.
- **Cuánto dura.** La banda sigue hasta que el pase se sirve (desaparece de la lectura) o se toca «Visto», que se recuerda en memoria por clave.
- **En C3c avisa de todas las mesas.** En Costiña, con una sola sala, es lo mismo. Desde C3e, solo avisa de «Mis mesas».
- **Al volver a la pantalla** (al desbloquear el móvil), la relectura (`operations.ts:72`) saca la banda enseguida.

**Cocina, lo mínimo hasta C3d**
- Sale de la pantalla el párrafo de ayuda (`KitchenView.vue:28-30,62`), y la cabecera dice «Costiña Cocina · Cocina caliente».
- Nada más: la comanda nueva llega en C3d.

**Envío a estaciones:** sin cambio.

**Vuelta atrás:** sí en el servidor. No cambia nada guardado que un motor anterior no lea (el código corto usa la misma columna). La base `costina` vieja sigue ahí para el enrutador o para una versión anterior. Un dispositivo emparejado por primera vez con C3c tendría que emparejarse otra vez (sección 4).

**Pruebas:**
- **vitest:**
  - `install.test.ts` reescrito: dos manifiestos con su `id`, `scope` y nombre, los títulos de iOS, los iconos por aplicación y nada de terceros ni en línea (hoy `:20-53`);
  - `appFits`, incluido el rol desconocido;
  - la adopción de lo guardado, con fake-indexeddb (como `tests/operations.test.ts`);
  - `sala.ts`: claves nuevas entre lecturas, primera lectura sin vibrar, «Visto», «Rehacer» que vuelve a avisar, varias a la vez, y un pase Ready sin `serve` (alergia declarada tras validar) que no avisa hasta que se revisa;
  - `views.test.ts` sigue sin palabras de caja, sin `style=` y sin `<img>` nuevas.
- **ClientChecks:** `PairingLink` por aplicación, que cambia el enlace fijado por D6.5.
- **DesktopChecks:**
  - los asistentes «Conectar Costiña Sala/Cocina»;
  - el QR de cada intención;
  - el código corto en dos grupos;
  - captura de Dispositivos.
- **`pairing_http.py`:**
  - `typed` da 10 caracteres del alfabeto;
  - se acepta en minúsculas y con guiones;
  - un solo uso, caducidad y freno de intentos;
  - sin `typed`, el código largo, que se sigue aceptando solo tal cual (cambiado de mayúsculas, no vale).
- **Playwright** (escritorio y móvil):
  - emparejar por `/app/sala/#pair=` y por `/app/cocina/#pair=` (cambian `e2e/support.ts:118` y `pairing.spec.ts`);
  - una credencial de cocina abierta en Sala no monta nada y lo dice;
  - la caché de Sala no tiene rutas de Cocina, y al revés (hoy `pairing.spec.ts:32-34` espera solo `/app/`);
  - la cocina valida un pase (por API) y en Sala, **con «Bodega y bebidas» abierto** (la ficha sustituye al plano, `BoardView.vue:100-103`), sale la banda; «Servido» desde la banda es una orden y la versión sube en uno. Usa la mesa del propio carril: no hace falta otra mesa;
  - un iPhone simulado (`navigator.standalone === false` con agente de iOS) no ofrece «Solicitar emparejamiento» en Safari;
  - ninguna palabra de caja.
- **Guion físico corto** (sección 7).

### 4.2. E4e: alergias antes de enviar, órdenes de grupo y quién abrió la mesa (`0.44.0-e4e`, sin cambio de esquema)

**Se queda todo lo aprobado** en la sección 4.5 del plan del 15-10 (`plan:583-652`):
- la revisión antes de enviar (puntos 1-3);
- `open` con `restrictions` (`:602`);
- las órdenes de grupo (`:603-611`);
- `fire-next` desde «Sentados» (`:612`);
- las reglas de las instantáneas, sin vuelta atrás (`:614-619`);
- el ADR nuevo;
- el WPF (`:621-631`);
- el mínimo de la PWA (`:633`), que desde C3c vive en la pantalla del pase de Costiña Cocina.

Sigue siendo requisito la guarda del instalador contra bajar de versión (`AGENTS.md:99`).

**Contrato exacto** (todos los campos son nuevos y opcionales; una orden de hoy vale igual, `Contracts.cs:12`):
- **Registros que cambian.**
  - `DiningCommand` (`DiningOperations.cs:11-14`) gana `ItemIds` (`IReadOnlyList<string>?`) e `Items` (`IReadOnlyList<ReviewItem>?`), con `ReviewItem(CourseId, ItemId, Decision, Note)`.
  - `OpenRequest` (`:9`) gana `Restrictions` (`IReadOnlyList<RestrictionRequest>?`), con `{guestPosition?, kind, substance, severity}`. Un valor desconocido da `invalid_restriction`, como hoy.
- **`POST /dining/services/{id}/commands/preparations-start|preparations-ready`**, con `{expectedVersion, courseId, itemIds}`.
  - De 1 a 200 elaboraciones (`too_many_items`), todas de ese pase.
  - Todo o nada, con **una** subida de versión.
  - Un evento por elaboración, con los tipos de hoy (`course_item.started|ready`, `DiningService.cs:53-65`), en la misma transacción. Los clientes ya juntan las ráfagas (`live.ts:64-85`).
- **`review-preparations`**, con `{expectedVersion, items}`.
  - De 1 a 60 decisiones, que caben en `Requests.MaxCommandBody`, de 32 KiB (`Core.Hosting/Requests.cs:30`).
  - Nota obligatoria en cada una.
  - Un `restriction.reviewed` por elaboración (`Restrictions.cs:83`).
- **Anuncio, en el pase de la mesa (curso).**
  - `preparations-start|ready` sale si alguna elaboración anuncia la acción individual **después** de filtrar (`Affordances.cs:35-46`; `plan:609`).
  - `review-preparations` sale si alguna anuncia `review-preparation`.
- **Matriz de estación, sin huecos** (`plan:607`, con las líneas de hoy).
  - `StationAllowed` (`DiningOperations.cs:64-79`) comprueba `preparations-*` elaboración por elaboración y deja `review-preparations` solo al pase. Una persona con sesión (sin estación) sigue sin restricción: el principal también revisa (`plan:605`).
  - `KitchenAllowed` (`Affordances.cs:10`) gana las tres acciones.
  - **Por rol, cada orden de grupo vale lo mismo que su individual** (`:17`): la sala queda fuera de `review-preparations`, como de `review-preparation` (`plan:607`), y conserva `preparations-start|ready` igual que conserva las individuales. Lo que impide a un comandero de sala marcar platos de cocina es la estación, que lo comprueba elaboración a elaboración. Quitarle a la sala las individuales sería una regla nueva, que no pidió nadie y que dejaría a un comandero de barra sin marcar lo de su barra.
  - `StationKeeps` y `PassKeeps` (`:26-33`) las reflejan.

**Nuevo: el pase puede marcar por una estación** (si el promotor dice sí a la pregunta 2)
- `StationAllowed` y `StationKeeps` dejan al `pase` hacer `preparation(s)-start|ready` de cualquier estación.
- Solo para el pase, cambia la regla «un dispositivo de cocina solo marca su estación» (D4.3a, `AGENTS.md:25`). Va en el ADR del corte y en `station_http.py`.
- Si dice no, el único camino sigue siendo el PC con la contraseña del responsable, porque una sesión de persona no tiene estación (`DiningOperations.cs:66`).

**Nuevo: quién abrió la mesa** (lo necesita «Mis mesas» de C3e)
- **Qué se guarda.** `DiningService` guarda `OpenedBy` (el actor: `device:<nombre>` o `user:<nombre>`) y `OpenedStation` (la estación del dispositivo; vacía si es una persona). Se toman de `ExecutionIdentity` (`Contracts.cs:38`) en `Open` (`DiningOperations.cs:23-60`).
  - El actor es el **nombre** del dispositivo, que no es único (sección 1). Guardar el identificador del dispositivo obligaría a llevarlo en `ExecutionIdentity`; no compensa mientras el PC avise de los nombres repetidos.
  - La instantánea se guarda en el JSON de `dining.services.payload` (`Dining/schema.sql:9`): no hay columna nueva ni cambio de esquema.
- **Dónde aparece.**
  - Son campos aditivos de `DiningSnapshot` (`Snapshots.cs:8-11`); una mesa guardada antes los restaura vacíos. Al revés no: el decodificador estricto (`Contracts.cs:12`) de un motor anterior rechaza el campo aunque vaya a `null`, y toda mesa que se vuelva a guardar lo lleva.
  - También de `DiningView` (`ServiceModels.cs:42-50`), como `openedBy` y `openedStation`, que se omiten si están vacíos.
  - No llevan ninguna palabra de dinero: pasan la guarda.
- **Sin evento nuevo:** `service.created` ya lleva el actor (`DiningOperations.cs:53`). Leerlo de `core.audit` en cada tablero queda descartado: no hay índice y cada aviso relee el tablero.
- **Va en E4e** porque E4e ya no tiene vuelta atrás (`plan:618`). Así hay un solo punto sin retorno, no dos.

**WPF:** solo lo aprobado; el Comedor no usa todavía estos campos.

**Pruebas añadidas a las aprobadas** (`plan:637-652`):
- `station_http.py`:
  - un grupo con una elaboración de otra estación da `wrong_station` y no aplica nada, con el token del propio dispositivo;
  - el pase marcando por otra estación, según la pregunta 2.
- `affordances_http.py`: la sala no ve `review-preparations`; un comandero con estación de sala no ve `preparations-*` en un pase de cocina; el anuncio del grupo sale filtrado por rol y estación, y `Allows` da lo mismo para cada orden de grupo que para su individual.
- Dominio y `pwa_http.py`:
  - `openedBy` y `openedStation` desde un dispositivo y desde una persona;
  - una instantánea anterior se lee sin ellos;
  - las claves no tienen dinero.

### 4.3. C3d: Costiña Cocina, comandas por mesa (`0.45.0-c3d`, sin cambio de esquema ni de motor)

Necesita E4e (órdenes de grupo y revisión antes de enviar) y la maqueta aprobada.

**Estación** (caliente, fría o cualquiera que no sea el pase)
- **Una comanda por mesa y pase**, con **solo su trabajo**. La clave y el orden son los de hoy, de la más antigua a la más nueva (`kitchen.ts:35,41-45`).
  - Sustituye la regla de D6.4: «una estación ve el pase entero» (`AGENTS.md:47`; `kitchen.ts:31-33`).
  - El documento del corte y `dotnet/AGENTS.md` lo dicen, con la razón: la decisión del promotor del 22-10 y la práctica del sector.
- **Cabecera:** «Mesa 4 · Principal», «4 personas» y «Lleva 12 min», contado desde `firedAt` y nunca negativo (`kitchen.ts:50-54`).
- **El tiempo** (pregunta 3):
  - neutro al principio;
  - ámbar desde 15 minutos;
  - ámbar oscuro con letra blanca desde 25.

  **Nunca rojo** (`plan:907`), y siempre con palabras, no solo con color (WCAG 1.4.1): «Lleva 12 min» en los tres tramos. Cambia la frase «sin alarmas por tiempo» de D6.4 (`D6.4-pwa-kds.md:48`), y el documento del corte lo dice.
- **Platos agrupados por nombre y estación**, sumando las cantidades: «Merluza × 4».
  - Es solo presentación: `PreparationView` no lleva el identificador del plato (`ServiceModels.cs:20-29`), y el sufijo del identificador cambia según la oferta (`DiningOperations.cs:34-35`; `CourseExecution.cs:62-70`).
- **Las excepciones van aparte y nunca atenuadas.** Cada elaboración con restricción o con revisión sale en su propia línea, bajo su plato:
  - «Comensal 3 · ALERGIA marisco (grave) · Adaptado: vieira», en rojo si es alarmante;
  - «Por revisar en el pase», en ámbar.

  El grupo dice «Merluza × 4 (1 adaptada)», en una sola tarjeta de plato con un solo «Listo»: la línea adaptada va debajo, no como un grupo aparte «Merluza × 1». Siempre con tipo, sustancia y gravedad (D3.2).
  - **Una restricción de toda la mesa** sale una sola vez, en la cabecera. Si no, «Merluza × 4» serían cuatro líneas rojas iguales (`Restrictions.cs:28-29`).
- **«También en: Cocina fría»**, con los nombres de las demás estaciones del pase, en lugar de sus líneas atenuadas.
- **Un botón grande «Listo»**, de 64 px y a todo el ancho, en **todas** las comandas: **una** orden `preparations-ready` con las elaboraciones propias que anuncian `preparation-ready`.
  - Con dos platos propios distintos o más en la comanda, cada plato lleva también su «Listo», que envía solo los suyos. También de 64 px de alto (`plan:196`), aunque sea más estrecho.
  - «Empezar» solo si el promotor lo quiere (pregunta 1): `preparations-start`, en una orden.
- **«Hecho · esperando al pase».**
  - Cuando lo propio está listo, la comanda baja a esa franja y desaparece cuando el pase valida. Hoy se queda entera hasta entonces (`kitchen.ts:29`).
  - La franja no lleva hora: el motor no guarda cuándo se marcó cada plato (`ServiceModels.cs:20-29`).
  - **Sin deshacer.** El motor no vuelve una elaboración de «Lista» a «Por empezar» (`CourseExecution.cs:102-110`); solo «Rehacer» del pase, que exige una revisión pendiente (`:150-161`). Si alguien toca «Listo» por error, el pase lo ve y no valida sin el plato, y la estación sigue viendo la comanda en la franja. Deshacer «Listo» en el motor queda diferido (sección 6), así que ni la franja ni la maqueta lo ofrecen.
- **Nada que escribir** en una estación.
- **Sonido** (pregunta 4):
  - un tono corto con Web Audio cuando llega una comanda nueva (clave nueva entre dos lecturas);
  - solo después de tocar «Empezar turno» al encender, que es lo que permite sonar al navegador;
  - sin ficheros de audio, así que la CSP no cambia.
- **Pantalla siempre encendida.**
  - Con https, Wake Lock si el navegador lo da.
  - Con la conexión sencilla (http) no existe: «Empezar turno» recuerda dejarla encendida en los ajustes de la tablet.
  - El modo quiosco sigue en el Hito 7 (`2026-09-24-inicio-y-dispositivos.md:135`).

**Pase**
- **«Por revisar», siempre arriba** (`plan:677-680`), también de pases sin enviar (E4e).
  - Se ordena por mesa y comensal, con sus platos afectados de todo el menú.
  - **Afectados son todos los platos de ese comensal** (y todos los de la mesa, si es de toda la mesa), no solo los que llevan la sustancia: el motor marca por comensal (`Restrictions.cs:28-29,87-88`) y no sabe qué alérgenos lleva cada plato (`plan:200`). Con un menú degustación de 7 pases son 7 platos por comensal.
  - Por eso hay un «No le afecta a nada más», que da esa decisión a los platos que queden sin decidir del comensal, en la misma orden.
  - En cada plato, «No le afecta» o «Adaptado: …», con atajos que rellenan la nota a la vista: «No lo lleva», «Sin gluten», «Cambiado por …».
  - El menú entero de un comensal se guarda en **una** orden `review-preparations`. La nota sigue siendo obligatoria (`kitchen.ts:57`).
- **En cocina: una tarjeta por mesa y pase**, con la mesa entera:
  - los platos agrupados, cada uno con su estación;
  - las excepciones en rojo;
  - arriba, el progreso de cada estación, con su nombre entero: «Cocina caliente: lista · Cocina fría: 1 de 2 platos». No lleva hora por estación, porque el motor no la guarda (diferido).
  - El estado de cada plato con las palabras de hoy: «Lista» o «Por empezar» (`labels.ts:28`).
  - Mientras no se puede validar, en lugar del botón dice qué falta: «Falta Cocina fría» o «Falta revisar la alergia». El botón aparece solo cuando el servidor anuncia `ready`.
- **Lo que se emplata en el pase** (platos con estación `pase`) tiene su propio «Listo».
- **«Validar pase: listo para servir»**, grande, solo cuando el servidor anuncia `ready` (`AllowedActions.cs:41-42`). **Ese toque es la llamada al camarero.**
- **«Esperando al camarero»:** las comandas validadas, con «Listo hace 3 min» (desde `readyAt`), hasta que se sirven. Hoy el pase ya las ve (`kitchen.ts:29`). Nunca dice «Sala avisada»: el servidor no sabe si el aviso llegó a un móvil (`AGENTS.md:11`).
- **«Siguiente: Postre (sin enviar)»** en cada mesa, solo de lectura: el jefe ve lo que viene, pero el ritmo lo marca el camarero. Cambia la frase de D6.4 «no hay vista de lo siguiente sin enviar» (`D6.4-pwa-kds.md:48`).
- **Si la pregunta 2 es «sí»:** en «Más opciones» de cada tarjeta, «Marcar Cocina fría como lista».
- «Rehacer» sigue solo en el pase y solo para lo enviado (D3.5).

**Común**
- **`pwa/cocina.ts`, sin DOM, sustituye a `kitchen.ts`.** Calcula:
  - las comandas de estación y de pase;
  - los grupos y las excepciones;
  - el progreso de cada estación y la franja «Hecho»;
  - los tiempos.

  E4e agrupa en el PC («Ostra × 7», `plan:628`), así que la regla de agrupar va a una tabla de casos común, `dotnet/tests/fixtures/kitchen-cases.json`, leída por vitest y por ClientChecks, como `floorplan-cases.json` (`AGENTS.md:97`).
- **Misma lectura en vivo y misma orden incierta** (`operations.ts`, `StatusPanel.vue`; `AGENTS.md:47`).
  - Un `version_conflict` (dos estaciones a la vez en la misma mesa) relee y pide tocar otra vez.
  - Nunca se reenvía solo, con otra clave (`plan:870`).
- **Tablet en horizontal (1180 × 820), leída a uno o dos metros:** rejilla de comandas, «Mesa 4 · Principal» de 24 px, platos con letra de 22 px y cantidad de 28 px, la línea de alergia de 18 px en negrita, el tiempo de 18 px y botones de 64 px. Nada operativo por debajo de 16 px.
- **Sin franja de totales** («Merluza: 11 en marcha»): queda diferida (sección 6).

**Pruebas:**
- **vitest:** `cocina.ts` con la tabla común:
  - grupos y excepciones;
  - restricción de toda la mesa una sola vez;
  - franja «Hecho»;
  - progreso por estación;
  - umbrales de tiempo;
  - comanda nueva para el sonido.
- **ClientChecks:** la misma tabla.
- **Playwright**, con un proyecto nuevo `tableta` (1180 × 820, táctil):
  - **Carril propio.** Las 8 mesas del laboratorio ya tienen dueño. Hay dos opciones, que se deciden en el corte:
    - mesas nuevas creadas por API al empezar (`table-create`, con códigos de la ejecución), revisando las suites que cuentan mesas. En `LabConfiguration` no sirven: la siembra idempotente no las añade a una base ya sembrada (sección 1);
    - una mesa en serie, liberada en `finally`, como `drinks.spec.ts`.
  - **`kitchen.spec.ts` reescrito** (hoy fija lo atenuado, `:33-34,72`). Cubre:
    - la estación no ve lo ajeno y su «Listo» es una orden, con la versión que sube en uno;
    - `wrong_station` con su propio token;
    - el pase revisa antes de enviar, ve «Cocina caliente: lista» y valida;
    - «Esperando al camarero»;
    - ninguna palabra de caja (`:151`).
- **Capturas:** estación, pase, por revisar y franja «Hecho».

### 4.4. C3e: Costiña Sala, mis mesas y abrir y servir el menú (`0.46.0-c3e`, sin cambio de esquema ni de motor)

Es la mitad del camarero del C3c aprobado (`plan:656-672`), más «Mis mesas».

**«Mis mesas» primero y «Todas» después**
- **Qué cuenta como «mía»** (presentación, en `sala.ts`):
  - la abrió este dispositivo (`openedBy` es el actor de `/session`);
  - **o** se abrió desde la misma estación de sala (`openedStation` es `session.station`): los demás comanderos de «Sala 1», y también el PC si su estación es «Sala 1» (`AGENTS.md:71`);
  - **o** no se sabe quién la abrió: abierta antes de E4e, o desde el PC con la contraseña del responsable, que no tiene estación. Mejor avisar de más que de menos.
- **En Costiña**, con una sola estación de sala, todas las mesas ocupadas son «mías» para todos los comanderos, y todos reciben el aviso. «Mis mesas» se distingue de «Todas» cuando hay varias estaciones de sala.
- **Cómo se ve.**
  - «Mis mesas» sale primero. Dentro, arriba las que están listas para servir y luego las demás ocupadas, por el orden de las mesas.
  - **Al final, las mesas libres**, para que abrir una siga siendo de 3 toques (`plan:206`). No se sabe de qué sala es un comandero (falta unir estación y sala, sección 6), así que salen todas las libres, agrupadas por la sala de cada mesa (`/configuration`, `Organization.cs:10`).
  - Las mesas mías se agrupan igual, por la sala de la mesa. No hay «mi sala» ni «otras salas»: eso necesitaría el mismo dato que falta.
  - «Todas» es el plano de hoy, con pestañas por sala cuando hay dos o más (`FloorGrid.vue:15-22`).
  - La pestaña elegida vive en `BoardView`, como la sala (`BoardView.vue:55`).
- **Supuesto.** Cada sala tiene su estación de sala («Sala 1», «Terraza»), y los comanderos se emparejan a la de su sala. Si un local con varias salas necesita contar también las mesas de su sala abiertas desde otra, o enseñar solo las libres de su sala, hay que unir estación y sala, y eso es esquema (sección 6).
- **El aviso, solo de «Mis mesas».** En «Todas», el resto de mesas se sigue pintando en verde.

**Abrir mesa** (`plan:657-663`)
- Una hoja con:
  - las personas, en botones de 1 a 8 y «Más…»;
  - el menú del día ya elegido;
  - «¿Alergias o intolerancias?», una fila por comensal.
- Es **una** orden `open` con `restrictions` (E4e). Al confirmarse, se abre la ficha.

**La ficha**
- **El botón grande** es lo que anuncia el servidor (`ficha.ts:10`):
  - justo después de abrir, «Enviar siguiente pase · Aperitivo» (E4e);
  - con una alergia por revisar, apagado, con «Esperando la revisión de cocina» (`ficha.ts:52`, igual que el puesto);
  - con el pase listo, **«Servido · Principal»**. Hoy dice «Marcar servido»: `ficha.ts:18` gana la palabra, igual que «Enviar siguiente pase». Es un texto del botón, no de la tabla común de vocabulario: `action.serve` sigue siendo «Marcar servido» en el PC y en la orden pendiente (`labels.ts:43`).
- **«Carta y menús»** (`plan:664-669`):
  - platos agrupados, con la línea adaptada aparte y en rojo;
  - omitir, con su propia hoja;
  - «Más opciones», con pausar y omitir.
- **Alergias** desde la franja roja de la ficha (`plan:672`). Menú con elección y carta, como en `plan:670-671`.
- **Se arrastra de C3b** (`AGENTS.md:99`): «Preguntar al servidor» para una orden de otra instalación.

**Pruebas:**
- **vitest:**
  - la regla de «Mis mesas»: propia, de la misma estación, sin dato, de otra estación;
  - el orden, con las listas arriba y las libres al final;
  - el aviso filtrado.
- **Playwright** (móvil y 820 × 1180):
  - abrir en 3 toques con una alergia, **desde «Mis mesas»**;
  - el pase la revisa;
  - «Enviar siguiente pase»;
  - validar en cocina;
  - la banda sale solo en el comandero de esa estación y no en el de otra estación de sala. La segunda estación de sala se crea por API (`station-create`, código de la ejecución) y se desactiva al terminar. **La mesa la abre el comandero, desde la pantalla o con su propio token**: una mesa abierta con el token principal de las pruebas no tiene estación (persona) y sería «mía» para los dos, así que la prueba no probaría nada;
  - «Servido».
- **Capturas.**

### 4.5. E4f, E4c y E4g

Tienen el mismo contenido que en el plan del 15-10 (`plan:700-806`); solo cambian sus versiones (tabla de arriba). Detalles:
- La vista previa de E4f, «Así lo verá el camarero», usa la presentación agrupada de Sala.
- La carta de E4g en el móvil es la de Costiña Sala.
- E4c sigue subiendo el esquema a v9: este rediseño no lo toca.

## 5. Qué NO cambia

- **El servidor decide los botones** (D3.1).
  - Un botón existe solo si se anuncia ya filtrado por rol y estación.
  - Lo que cada aplicación pinta («Mis mesas», grupos, atenuado, colores) es presentación, nunca permiso.
  - Cada acción nueva entra a la vez en `Affordances.Allows` (que es la política de la ruta), en el espejo de estación y en `StationAllowed` (`plan:811`).
- **F01.** «Servido» desde la banda y «Listo» desde una comanda salen de lo que hay en pantalla, con la versión leída. El puesto principal no cambia su contexto.
- **Una sola orden incierta durable**, una por gesto: «Listo», «Validar», la revisión del menú de un comensal, «Servido» o «Abrir mesa». Nunca una cola (`AGENTS.md:45`).
- **Tiempo real con avisos finos y relectura autoritativa.** El aviso al camarero es un estado leído, no un mensaje: no se mete nada en el canal (`AGENTS.md:11`).
- **Sin dinero en los móviles ni en cocina** (ADR-007).
  - Las claves nuevas (`openedBy`, `openedStation`, `itemIds`, `items`, `restrictions`, `typed`) pasan la guarda.
  - No hay palabras de caja en pantalla.
- **Nada se borra.**
  - La base `costina` vieja se conserva.
  - Desvincular revoca, no borra.
  - Lo desactivado sigue desactivado.
- **Lo desconocido sale tal cual:** un rol, una estación, un estado de pase o una restricción que no se conozca, nunca suavizado (`AGENTS.md:75`).
- **Restricciones** siempre escritas con tipo, sustancia y gravedad, nunca solo con color. El rojo es solo para las alergias alarmantes.
- **El pase sigue siendo la única estación que valida y revisa.** La única ampliación posible es que marque por otra estación (pregunta 2).
- **Mismo origen y todo bajo `/app/`.**
  - La CSP no cambia (solo `img-src 'self' blob:`, de C3b).
  - Sin cámara.
  - Dependencias de ejecución: `vue` y `@microsoft/signalr`.
  - La PWA se identifica como dispositivo, nunca como persona.
- **Conexión sencilla (B.2)** igual: http solo con una IPv4 privada, y apagada por defecto (`AGENTS.md:81`).
- **Esquema v8** hasta E4c. Los campos nuevos son aditivos, y una orden de hoy sigue valiendo.
- **Cobrar, anular y cerrar** siguen en Caja.

## 6. Diferimientos conscientes

- **Aviso con la aplicación cerrada o el móvil bloqueado.**
  - Las notificaciones del sistema necesitan https de confianza: el certificado público (ADR-014, punto 2; `B.2-conexion-sencilla.md:51`).
  - Con la aplicación cerrada, además, hace falta Push, que pasa por un servicio de internet de Apple o de Google. Choca con «primero local» (ADR-001) y necesita un ADR propio.
  - Con https y Android, una notificación del service worker mientras la página siga viva se estudia en el corte del certificado.
- **La sala de cada estación:** `core.stations.zone_id`, que es esquema (`Organization.cs:11`; `schema.sql:66-71`). Llegará cuando un local con varias salas lo necesite; hasta entonces, «Mis mesas» por estación y todas las libres al final (sección 4.4).
- **La intención fijada al emitir el código** (rol y estación en `core.pairings`, esquema): rechazaría antes de aprobar una solicitud hecha desde la aplicación equivocada (sección 4.1).
- **Hora por estación** («Cocina caliente: lista hace 3 min»). `PreparationView` no guarda horas (`ServiceModels.cs:20-29`); será un campo aditivo cuando se pida.
- **Deshacer «Listo» en el motor** (volver de «Lista» a «Por empezar» mientras el pase no esté validado) y **recuperar una comanda ya servida**. Sería una acción nueva y aditiva del dominio, con la misma matriz de estación; si el promotor la quiere pronto, cabe en E4e.
- **Volver a avisar** si un pase listo no se sirve en N minutos.
- **Totales del turno en cocina** («Merluza: 11 en marcha»).
- **El aviso en el PC** (Comedor). Su tarjeta ya dice «Listo para servir» (`Client/FloorPlan.cs`).
- **Atribución a la persona** (camarero con PIN: E5 y Hito 7; `2026-09-24-inicio-y-dispositivos.md:134-135`).
- **Modo quiosco y reapertura** tras reiniciar la tablet (Hito 7).
- **Pantalla de barra** (`plan:840`).

## 7. Pruebas (en todas las entregas)

- **Locales:**
  - `sh tools/agent/local-tests.sh` antes de cada subida que toque `dotnet/`;
  - vitest para la PWA;
  - nunca desde una consola de administrador en el PC con la instalación real, y siempre con el puerto de pruebas 5090 (`AGENTS.md:53,67,85`).
- **CI:**
  - D0, D1, desktop, PWA (vitest y Playwright, con el proyecto `tableta` desde C3d), servicio e instalador;
  - en E4c, el workflow del instalador se lanza a mano (`plan:732`).
- **Lo que Playwright no ve.** Corre en loopback (`playwright.config.ts:22`), que cuenta como contexto seguro: la conexión sencilla (http por IP) no se puede probar ahí y va al guion físico.
- **Guion físico al cerrar C3c** (y uno corto al cerrar C3d y C3e), con el móvil Android y el iPad del promotor, por la conexión sencilla y por https si la hay. Se comprueba:
  1. Dos iconos, con su nombre y su color. El QR de cada asistente abre la suya.
  2. Abrir la otra aplicación con esa credencial: lo dice, y no monta nada.
  3. iPad: el QR abre Safari, que no deja emparejar y explica cómo añadir el icono. Añadir «Costiña Cocina» a la pantalla de inicio y emparejar desde el icono con el código corto. El icono no ve lo que hay en Safari.
  4. iPad con el icono «Costina» de antes: sigue funcionando sin emparejar de nuevo (adopción a través del enrutador).
  5. Android por http: qué crea «Añadir a pantalla de inicio» y con qué nombre.
  6. Un Android que ya tenía «Costina»: quitarlo, y que el enrutador lleve a la aplicación buena.
  7. La banda con la ficha de otra mesa abierta y con Bebidas abierto: no tapa ni el botón de volver ni «Enviar».
  8. Vibración en Android, también por http, tras tocar la pantalla una vez; después de recargar la página, sin tocar no vibra. En iPhone no vibra, y la banda sale igual.
  9. Desbloquear el móvil con un pase listo: la banda sale al momento.
  10. Sonido y «Empezar turno» en cocina (C3d), y que la comanda se lee desde el sitio del cocinero.
  11. Ninguna palabra de caja ni ningún importe.
- **Capturas** de cada corte en `docs/native/images/<corte>/`, revisadas una a una antes de cerrarlo.

## 8. Riesgos

- **Dos aplicaciones en el mismo origen.**
  - La guía de web.dev recomienda orígenes distintos, y hay temas abiertos en el W3C sobre varias PWA en un origen.
  - Otro origen exigiría otro puerto (otro listener y otra regla de cortafuegos en B.2, `AGENTS.md:81`) u otro nombre en el certificado: más piezas que mantener, y el QR tendría que saber cuál.
  - Mitigaciones: `id` y `scope` distintos, cachés y bases separadas, y el guion físico en sus dispositivos antes de construir pantallas encima (por eso C3c va primero).
- **Android por http no instala de verdad.** El icono abre una pestaña con la barra de direcciones. Mitigación: decirlo (resumen); la solución es el certificado público.
- **iPad e iPhone guardan aparte en cada icono.** Mitigaciones: el código corto y el guion. El enrutador de `/app/` también sirve para los dispositivos emparejados antes.
- **Dispositivos ya emparejados.**
  - Un service worker viejo con alcance `/app/`.
  - El icono «Costina» instalado.
  - La base `costina` compartida.

  Mitigaciones: el service worker que se da de baja, la adopción por rol y el guion paso a paso.
- **El borrado de cachés de `sw.js:14`.** Si se suelta un service worker nuevo sin arreglarlo, una aplicación vacía la caché de la otra. Mitigación: la prueba E2E de cachés aisladas.
- **El aviso solo llega con la aplicación a la vista.** Un camarero con el móvil en el bolsillo y la pantalla apagada no se entera hasta mirarlo. Mitigaciones: la mesa en verde y la banda que se queda; y el pase lo ve en «Esperando al camarero».
- **Avisar de más.** Las mesas sin dato de quién las abrió avisan a todos. Es lo seguro; se mide en el guion.
- **«Listo» por error no se deshace** (motor, sección 4.3). Mitigaciones: el pase no valida sin ver el plato, y la comanda sigue en la franja «Hecho · esperando al pase». Si pasa a menudo, deshacer entra en E4e.
- **Nombres de dispositivo repetidos.** Dos móviles con el mismo nombre comparten «Mis mesas» y la auditoría. Mitigación: el aviso del PC (F12) y el ejemplo del campo, que no rellena un nombre.
- **Una estación ve menos que hoy** (cambia D6.4). Mitigaciones: «También en: …», y el pase con la mesa entera.
- **`version_conflict` en cocina.** Dos estaciones que tocan a la vez en la misma mesa. Las órdenes de grupo bajan mucho las subidas de versión (de N a 1 por toque); el resto se resuelve con un toque más (`plan:870`).
- **Código corto.** Tiene menos bits que el largo. Mitigaciones: 5 minutos, un solo uso, el freno de intentos y, sobre todo, la aprobación en el PC con el nombre a la vista; `pairing_http.py` lo fija, también que el código largo sigue distinguiendo mayúsculas.
- **Sin vuelta atrás tras E4e** (como estaba aprobado). Los campos de quién abrió la mesa van en el mismo corte para no añadir otro punto sin retorno.
- **Más E2E y un proyecto más** (`tableta`). La CI tardará más. Mitigación: el proyecto `tableta` solo corre las historias de cocina.

## 9. Decisiones ya tomadas por el promotor

- **22-10:**
  - una pantalla para los camareros y otra para cocina: **dos aplicaciones separadas, «Costiña Sala» y «Costiña Cocina»**, cada una con su nombre, su icono y su color; el QR del PC abre la que toca;
  - en cocina, **una tablet fija por estación**: caliente, fría y pase;
  - la cocina ve **comandas por mesa**: una tarjeta por mesa y pase, platos agrupados («Merluza × 4»), la alergia en rojo con su adaptación, el tiempo que lleva y un botón grande «Listo»; sin líneas por comensal, salvo las de alergias;
  - **el flujo de la sección 2**: el camarero envía cada pase; cada estación recibe la suya y toca «Listo»; el pase valida; el camarero recibe el aviso con banda grande, vibración en Android y también en otra pantalla, con su mesa en verde; sirve y toca «Servido»;
  - **el límite del iPhone queda aceptado**: con la aplicación cerrada no hay aviso mientras no haya certificado público con dominio;
  - **Sala enseña primero «Mis mesas»** (las que abrió y las de su sala; cada móvil va emparejado a una estación de sala) y después «Todas».
- **22-10, al aprobar este plan** («Si a todo, la 5, va haber android y apple»):
  - **el plan entero, con su orden de cortes** (C3c, E4e, C3d, C3e, E4f, E4c y E4g) y **permiso para subir y mezclar C3c** cuando la CI esté en verde;
  - **en las estaciones, solo «Listo»**, sin «Empezar» → C3d;
  - **el pase puede marcar «Listo» por una estación con la tablet estropeada** → E4e (la matriz de estación se amplía solo para el pase) y C3d (el botón en la pantalla del pase);
  - **retraso en cocina: ámbar a los 15 minutos y ámbar más fuerte a los 25**, nunca rojo → C3d;
  - **las tablets de cocina suenan al llegar una comanda**, después de tocar «Empezar turno» → C3d;
  - **habrá Android y también iPhone y iPad** → C3c: los dos caminos de instalación tienen que quedar sólidos y probados (sección 10, pregunta 5).
- **Siguen valiendo todas las decisiones del 15-10** (`plan:886-909`):
  - menú cerrado en Costiña;
  - alergias decididas por cocina antes de enviar (E4e);
  - E4f antes que E4c;
  - tema claro con acento `#0E7A5F` y rojo solo para alergias;
  - nombres y no códigos;
  - castellano con tildes;
  - sin importes en los móviles ni en cocina.

## 10. Preguntas al promotor (contestadas el 22-10)

El resumen las hace en palabras llanas; aquí está lo que cambia cada respuesta. **Contestadas el 22-10** con «Si a todo, la 5, va haber android y apple»: las cuatro primeras, con la propuesta; la quinta, con los dos tipos de dispositivo.
1. **«Empezar» en las estaciones.**
   - Propuesta: solo «Listo», porque el motor ya admite marcar lista una elaboración recién enviada (`AllowedActions.cs:56-57`).
   - Con «sí», C3d añade un «Empezar» por comanda (`preparations-start`).
   - **Respuesta: solo «Listo».** Entra en **C3d**: la comanda de una estación tiene un único botón «Listo» y ningún «Empezar».
2. **El pase marca por una estación sin tablet.**
   - Propuesta: sí.
   - Con «sí», E4e amplía la matriz de estación solo para el pase (sección 4.2).
   - Con «no», solo puede el PC con la contraseña del responsable.
   - **Respuesta: sí.** Entra en **E4e** (la matriz de estación del motor se amplía solo para el pase, con su prueba de `station_http.py`) y en **C3d** (el botón del pase para una estación que no ha terminado).
3. **Minutos de aviso por retraso en cocina.**
   - Propuesta: ámbar a los 15 y ámbar oscuro a los 25; nunca rojo.
   - Son dos números de C3d; más adelante pueden pasar a la configuración.
   - **Respuesta: así.** Entra en **C3d**: ámbar a los 15 minutos, ámbar más fuerte a los 25 y nunca rojo, que sigue siendo solo de las alergias.
4. **Sonido en las tablets de cocina.**
   - Propuesta: sí, después de «Empezar turno».
   - El plan del 15-10 lo tenía diferido (`plan:847`). En la sala no suena nunca.
   - **Respuesta: sí.** Entra en **C3d**: «Empezar turno» al encender la tablet y un sonido cuando llega una comanda. En la sala sigue sin sonar nada.
5. **Qué dispositivos hay: iPhone/iPad o Android.**
   - Decide si el código corto y el guion del iPad son lo primero.
   - Decide también cuánto vale la vibración.
   - Explica qué verá en Android por http.
   - **Respuesta: habrá Android y también iPhone y iPad.** Entra en **C3c**, y los **dos** caminos de instalación tienen que quedar sólidos y probados, ninguno a medias:
     - **Android:** el QR del PC abre la aplicación que toca (`/app/sala/#pair=…` o `/app/cocina/#pair=…`) y se empareja allí mismo;
     - **iPhone y iPad:** el QR abre Safari, que no deja emparejar y explica «Compartir › Añadir a pantalla de inicio»; después se abre el icono y se teclea el **código corto** que enseña el PC.

     Por eso el código corto del motor, el enlace por aplicación del QR y la página de Safari son parte de C3c, con sus pruebas (sección 4.1), y el guion físico de la sección 7 se hace con un Android **y** con un iPhone o un iPad. La vibración solo existe en Android: en iPhone y iPad la banda sale igual, sin vibrar (resumen, «Límites que conviene saber»).

## Fuentes de la práctica del sector

- **Toast:**
  - flujo con un expedidor: <https://doc.toasttab.com/doc/platformguide/platformKDSWorkflowUsingSingleExpo.html>;
  - pases: <https://doc.toasttab.com/doc/platformguide/platformKDSWorkflowUsingCourses.html>;
  - avisos de pedidos terminados: <https://support.toasttab.com/en/article/Notifications-for-KDS-Fulfilled-Orders-1492809350380>;
  - mesas y camareros: <https://support.toasttab.com/en/article/New-POS-Managing-Tables?lang=en_US>;
  - recuperar una comanda: <https://doc.toasttab.com/doc/platformguide/adminRedisplayingTickets.html>;
  - alérgenos al sentar: <https://updates.toasttab.com/announcements/ann_mFw2OqxxM3ZGx>.
- **Square KDS** (umbrales de tiempo y modo expedidor): <https://squareup.com/help/us/en/article/7944>.
- **Lightspeed:**
  - comandas en cocina: <https://k-series-support.lightspeedhq.com/hc/articles/4418238868763>;
  - alérgenos por comensal: <https://k-series-support.lightspeedhq.com/hc/articles/48681791616539>.
- **Oracle MICROS KDS:** <https://docs.oracle.com/cd/E76065_01/doc.29/e69880/c_featuresandupdates_kds.htm>.
- **Qamarero** (marcha manual y pantalla del pase): <https://intercom.help/qamarero-9e4c4f5026e3/es/articles/12942814-como-usar-el-sistema-kdsv2-en-qamarero>.
- **Last.app KDS:** <https://www.last.app/en/product/kds-for-restaurants>.
- **Annoncer, KDS para alta cocina** (alergias imposibles de pasar por alto, ritmo y no velocidad): <https://annonceronline.com/resources/kitchen-display-systems/choosing-a-kds-for-fine-dining>.
- **Plataforma:**
  - varias PWA en un mismo dominio: <https://web.dev/articles/building-multiple-pwas-on-the-same-domain>;
  - Web Push en iOS: <https://webkit.org/?p=13878>;
  - vibración: <https://caniuse.com/wf-vibration>.

## Revisión

Revisión crítica del 22-10 contra `c32f53d`. Se leyó la copia de estudio sin modificarla y no se compiló ni se ejecutó nada. Cambios hechos en este documento:

**Referencias equivocadas**
1. «Sin notificaciones del sistema» no está en `D6.5-qr-install-physical.md:99`, que habla del QR y mDNS. Solo lo dice `D6.4-pwa-kds.md:48`, y D6.1 deja fuera Background Sync (`D6.1-pwa-base.md:21`). Corregido en la sección 1.
2. `e2e/board.spec.ts:103` y `e2e/kitchen.spec.ts:151` no existen (96 y 113 líneas). La regla de las palabras de caja está en `e2e/support.ts:14-15,60-66`. Corregido en la sección 1 y añadido a las referencias desfasadas (`plan:694` también las cita).
3. Los tipos de dispositivo no están en `vocabulary-cases.json`. Cambian `Client/Labels.cs:40-41`, `pwa/App.vue:25`, `HomeViewModel.cs` y `DevicesView.xaml`, con sus comprobaciones (sección 4.1).
4. «Validar: listo para servir» no es el texto de hoy: es «Validar pase: listo para servir» (`KitchenView.vue:108`). Se conserva el de hoy (secciones 2, 3 y 4.3).
5. «Esperando a que cocina revise la alergia» era una frase nueva. Hoy el comandero y el puesto dicen «Esperando la revisión de cocina» (`ficha.ts:52`), y se conserva (secciones 2 y 4.4).

**Comportamientos que faltaban o eran incorrectos**

6. **El aviso sale de `serve` anunciado, no del estado Ready.** Una alergia declarada después de validar marca para revisar el pase listo: «activo» incluye Ready (`CourseExecution.cs:32,141-147`). Ese pase deja de anunciar `serve` (`AllowedActions.cs:43`). Añadido en las secciones 1, 2 y 4.1, con su prueba de vitest. El borde rojo de alarma manda sobre el verde de «listo».
7. **La banda no tapa.** Va en su sitio, bajo la cabecera, porque se queda hasta «Servido» o «Visto» y no puede esconder volver, el buscador ni «Enviar» (sección 4.1).
8. **Vibrar y sonar necesitan un toque previo** en la página desde que se cargó (activación del usuario), aunque no necesiten https. Añadido al resumen, a las secciones 1 y 4.1 y al guion físico.
9. **Por http no hay service worker.** Separar cachés y service workers solo cuenta con https. Además, el enrutador da de baja el registro de alcance `/app/`, que solo se actualiza desde páginas que controla (secciones 1 y 4.1).
10. **iPhone y iPad:** la página de Safari no ofrece emparejar, porque dejaría un dispositivo aprobado que el icono no puede usar. Un icono «Costina» ya instalado probablemente sigue funcionando a través del enrutador. El enrutador no lleva las metas de iOS y busca la credencial también en las dos bases nuevas (secciones 2, 4.1 y 7).
11. **Código corto.** La normalización no puede tocar el código largo, que distingue mayúsculas. Si la huella choca con una de las viejas (es única en toda la tabla, `schema.sql:201`), se genera otro código. Un motor anterior devuelve el código largo (sección 4.1 y pruebas).
12. **Nombre al emparejar.** El dispositivo no sabe su estación antes de la aprobación (`Program.cs:477`), así que no se puede rellenar «Cocina caliente». Se queda como ejemplo, y el nombre tiene que ser único porque es el actor de «Mis mesas» (secciones 1 y 4.1, y riesgos).
13. **Vuelta atrás de C3c.** Un dispositivo emparejado ya con C3c tiene que emparejarse otra vez si se vuelve a C3b (secciones 4 y 4.1).

**Motor y matriz de estación**

14. **Las órdenes de grupo valen, por rol, lo mismo que sus individuales.** El borrador quitaba a la sala las tres, que es una regla nueva respecto a `plan:607` y a `Affordances.cs:17`. Queda así:
    - la sala sigue fuera de `review-preparations`;
    - el comandero de sala no marca platos de cocina por la estación, no por el rol;
    - el principal también revisa (`plan:605`).

    Pruebas de `affordances_http.py` ajustadas (sección 4.2).
15. **`OpenedBy` es el nombre del dispositivo**, que no es único (F12). Va en el JSON de la instantánea, sin esquema. Un motor anterior lo rechaza incluso a `null` (`Contracts.cs:12`), y eso se suma al «sin vuelta atrás» de E4e (sección 4.2).
16. **«Listo» no se puede deshacer** (`CourseExecution.cs:102-110,150-161`). Ni la franja «Hecho» ni la maqueta lo ofrecen. Va a riesgos y a diferimientos, como acción aditiva que cabría en E4e. La franja no lleva hora, porque el motor no la guarda.
17. **«Por revisar» abarca todos los platos del comensal**, no solo los que llevan la sustancia. El motor marca por comensal (`Restrictions.cs:28-29,87-88`) y no sabe qué alérgenos lleva cada plato (`plan:200`). Se añade «No le afecta a nada más», en la misma orden (sección 4.3).

**Presentación y vocabulario**

18. **Cocina:**
    - «Listo» grande en todas las comandas, y el de cada plato también de 64 px;
    - «Merluza × 4 (1 adaptada)» en una sola tarjeta de plato;
    - estaciones con su nombre entero y estados de plato con las palabras de `labels.ts:28`, sin «en marcha»;
    - «Esperando al camarero», nunca «Sala avisada»;
    - tamaños de letra para leer a distancia;
    - sin franja de totales, que sigue diferida;
    - los cambios a dos frases de D6.4 (tiempo y «lo siguiente») quedan escritos (secciones 3 y 4.3).
19. **Color de Costiña Cocina:** azul pizarra, y el teja de la maqueta queda descartado por parecerse al rojo de las alergias. Dentro de las dos aplicaciones, los botones siguen con el acento `#0E7A5F`. `short_name` «Cocina» (sección 4.1).
20. **«Mis mesas»:**
    - en Costiña, con una sola estación de sala, son todas las ocupadas;
    - las libres van al final, para no pasar de 3 toques al abrir (`plan:206`);
    - no hay «mi sala» ni «otras salas», porque falta unir estación y sala.

    En la prueba E2E, la mesa la abre el comandero: con el token principal, una persona sin estación, no probaría nada. La segunda estación de sala se crea por API (sección 4.4).
21. **Datos del laboratorio:** la siembra es idempotente (`LabConfiguration.cs:14`), así que las mesas y estaciones de más se crean por API, no en `LabConfiguration` (secciones 1, 4.3 y 4.4). El E2E de C3c usa «Bodega y bebidas» en la mesa de su carril y no necesita otra mesa.
22. **Diferimientos y riesgos:** se añaden la intención fijada al emitir el código (esquema), «Listo» sin deshacer y los nombres repetidos. «No hay otro origen posible» se cambia por su coste real (otro puerto u otro nombre).

**Comprobado y sin cambios:**
- `PwaHosting` sirve subcarpetas con su CSP (`PwaHosting.cs:24-47`), y `/app/index.html` tiene que seguir existiendo (`:18`).
- El código de 10 caracteres cabe en `PairingLink.IsCode` y en `pairing.ts:66`.
- `Requests.MaxCommandBody` es de 32 KiB.
- La instantánea va en JSONB.
- `fire-next`, `ready` y `serve` se anuncian donde dice el plan.
- La matriz de estación está en `DiningOperations.cs:64-79`.
- El token principal de las E2E es una persona (`dotnet/tests/http/native_http.py:32-47`).
