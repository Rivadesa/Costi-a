# C3c — Dos aplicaciones: Costiña Sala y Costiña Cocina

Fecha: 2026-10-22. Rama `feat/c3c-dos-apps` sobre `develop` (`70bdfbb`, `0.42.0-c3b`). Primer corte del plan `docs/plans/2026-10-22-sala-y-cocina.md`, sección 4.1, que el promotor aprobó el 22-10 con «Si a todo, la 5, va haber android y apple». Esa aprobación da también el permiso para subir y mezclar C3c cuando la CI esté en verde. Versión **`0.43.0-c3c`**. Decisión escrita: **ADR-015** (`docs/ADR/ADR-015-dos-aplicaciones-mismo-origen.md`).

**Sin cambio de esquema** (sigue la v8) y **la CSP no cambia**. El motor **solo añade**:
- un código corto de emparejamiento, para teclearlo en iPhone y iPad;
- las redirecciones de `/app/sala` y `/app/cocina`.

Lo demás es de la PWA, que pasa a ser dos aplicaciones, y de Inicio › Dispositivos en el puesto principal.

La respuesta 5 del promotor manda en todo el corte: **habrá Android y también iPhone y iPad**. Por eso los dos caminos de instalación están hechos y probados del mismo modo, ninguno a medias. Lo que una prueba automática no puede ver (un iPhone de verdad, la vibración, los iconos de la pantalla de inicio) está en el guion físico del final.

## Lo que notará el promotor

### Dos iconos, cada uno con su nombre y su color

- **«Costiña Sala»**, para el móvil de cada camarero: un plato con campana sobre el verde de siempre (`#0E7A5F`).
- **«Costiña Cocina»**, para la tablet fija de cada estación (caliente, fría y pase): un gorro de cocinero sobre azul pizarra (`#2F4A6D`). Debajo del icono puede leerse solo «Cocina», porque muchos teléfonos cortan los nombres largos.
- **«Costina» pasa a «Costiña»** en todo lo que se ve en los móviles y las tablets: iconos, títulos y cabeceras.
- Dentro de las dos aplicaciones, los botones siguen en el verde de siempre y el tema sigue claro. El color de cada aplicación solo está en su icono, en la barra del sistema y en la marca de la cabecera.
- **El QR del PC abre la aplicación que toca.** Si alguien abre la otra con ese dispositivo, la pantalla lo dice y no enseña nada: «Este dispositivo está conectado como Costiña Cocina · Pase. Ábrelo con el icono de Costiña Cocina.».

### En el PC (Inicio › Dispositivos)

- **Inicio** tiene dos botones: «Conectar Costiña Sala» y «Conectar Costiña Cocina», cada uno con su icono.
- **Dispositivos** tiene un bloque por aplicación, con su icono, para qué sirve y un desplegable de estaciones. El botón dice exactamente qué se va a conectar: «Conectar Costiña Sala · Sala 1» o «Conectar Costiña Cocina · Cocina caliente».
- **El asistente** enseña:
  - el QR;
  - **«Código para teclear»**, en grande y en dos grupos: «K7M2Q 9XR4T»;
  - el enlace del QR;
  - debajo, los dos caminos, **Android** e **iPhone y iPad**, con las palabras de los botones que salen en el móvil.

  El asistente se desplaza solo hasta verse entero. Ya no dice «No hay que instalar nada»: en los dos caminos se añade el icono de la aplicación. Con la conexión sencilla dice que el dispositivo no necesita ningún certificado.
- **La lista de dispositivos** va agrupada por aplicación: primero Costiña Sala, después Costiña Cocina y, al final, lo antiguo o lo que no se conoce, tal cual. Cada tarjeta dice su estación. Arriba: «5 dispositivos activos: 2 en Costiña Sala y 3 en Costiña Cocina · 1 quitado».

### En Costiña Sala: el aviso de «listo para servir»

Es la pestaña «5 · Sala · aviso» de la maqueta.
- Cuando el pase valida un plato, en el móvil sale una **banda verde grande**: «**Mesa 3 · Aperitivos** listo para servir», con «El pase lo validó a las 13:01».
- Sale en **cualquier pantalla** de Sala: el plano, la ficha de otra mesa, «Abrir mesa», «Carta y menús» o «Bodega y bebidas». Va arriba y ocupa su sitio: nunca tapa el botón de volver, el buscador de Bodega ni la ronda con «Enviar».
- Tiene tres botones:
  - **«Servido · Aperitivos»**: un toque y está servido, sin volver al plano. Es la misma orden que el botón de la ficha;
  - **«Ver mesa»**: lleva a la ficha de esa mesa. Si hay una ronda sin enviar en otra, pregunta antes;
  - **«Visto»**: la quita de este móvil sin servir.
- Con varias, sale la más antigua y debajo «+1 más: Mesa 4 · Aperitivos».
- **En Android vibra** (dos golpes cortos). En iPhone y iPad no: Apple no deja vibrar a una web. La banda sale igual.
- **En el plano, la mesa se pone entera en verde.** Si tiene una alergia que alarma, conserva el borde rojo.
- **En la sala no suena nada.**
- Si después de validar alguien anota una alergia en esa mesa, la banda se quita hasta que cocina la revise; entonces vuelve y vibra otra vez.
- En este corte avisa de **todas** las mesas. En Costiña, con una sola sala, es lo mismo; desde C3e, solo de «Mis mesas».

### En Costiña Cocina

- La cabecera dice «Costiña Cocina | Cocina caliente», con la estación en grande. Una tablet de cocina sin estación dice «Sin estación asignada».
- Desaparece el párrafo de ayuda de arriba.
- **Nada más por ahora:** la comanda por mesa («Merluza × 4», un solo «Listo», el pase con la mesa entera, el sonido y «Empezar turno») llega en C3d.

### Criterios de la sección 2 del plan

| Tarea | Antes | Ahora |
|---|---|---|
| Poner en marcha un móvil de camarero | un único «Costina» que se volvía comandero o cocina según lo que se aprobara | Android: QR de «Conectar Costiña Sala» → nombre → «Aprobar» en el PC. iPhone: QR → Safari explica el icono → icono → código corto → «Aprobar». Se abre «Costiña Sala», con su icono |
| Enterarse de que un pase está listo | mirar el plano, donde solo cambiaba la pastilla; dentro de una mesa o en Bebidas no se veía nada | banda grande en cualquier pantalla de Sala, vibración en Android y la mesa en verde |
| Servir un pase listo | volver al plano, abrir la mesa y «Marcar servido» | «Servido» en la banda: **1 toque** |

## Cómo se instala

Lo de abajo es lo que hará el responsable con cada dispositivo. El PC tiene que estar en `0.43.0-c3c` y, en Costiña, con la **conexión sencilla** activa (B.2). Los dispositivos van en el Wi-Fi del personal.

### En Android (móvil o tablet, con Chrome)

1. En el PC, **Inicio › Dispositivos**. En el bloque de la aplicación, elige la estación en el desplegable y pulsa «Conectar Costiña Sala · Sala 1» (o «Conectar Costiña Cocina · Cocina caliente»).
2. En el Android, abre la **cámara** y apunta al QR. Toca el enlace: Chrome abre **Costiña Sala** (o Costiña Cocina) con el código ya puesto.
3. Escribe el nombre del dispositivo (por ejemplo, «móvil de Ana»). Tiene que ser distinto en cada uno: es el nombre con el que el PC lo reconoce. Toca **«Solicitar emparejamiento»**.
4. En el PC aparece la solicitud con ese nombre: compruébalo y pulsa **«Aprobar»**. En el Android se abre la aplicación.
5. En el menú de Chrome (⋮), **«Añadir a pantalla de inicio»** (o «Instalar aplicación», según la versión). Queda el icono con su nombre y su color.
   - Con la conexión sencilla (http), ese icono abre una pestaña de Chrome con el nombre de la aplicación, no una aplicación aparte: el navegador no deja instalar de verdad sin https. Funciona igual.
   - Con https (certificado de la instalación o, más adelante, el público), es una aplicación a pantalla completa, y sin red abre igual y dice que no hay conexión.

Si el código caduca (5 minutos) antes de aprobar, en el PC: **«Generar otro código»**.

### En iPhone y iPad (con Safari)

En iPhone y iPad, la cámara siempre abre el enlace en Safari, y **lo que guarda Safari no lo ve el icono** de la pantalla de inicio: cada icono guarda aparte (ya visto en D6.5). Si se emparejara en Safari, el servidor tendría un dispositivo aprobado que el icono no puede usar. Por eso se empareja **desde el icono, tecleando el código corto**.

1. En el PC, igual que en Android: «Conectar Costiña Sala · Sala 1» (o la de Cocina). Deja el asistente abierto.
2. En el iPhone o el iPad, lee el QR con la **cámara** y toca el enlace. Safari abre una página de **Costiña Sala** que **no deja emparejar**: «Añádela a la pantalla de inicio».
3. Toca **Compartir** (abajo en el iPhone, arriba en el iPad) › **«Añadir a pantalla de inicio»** › **«Añadir»**. Queda el icono «Costiña Sala».
4. Abre **el icono**. Sale «Conectar este móvil» con el código vacío. **Teclea el código que enseña el PC**, como sale («K7M2Q 9XR4T»). Vale con o sin el espacio, con guiones y en minúsculas. Escribe el nombre del dispositivo y toca **«Solicitar emparejamiento»**.
5. En el PC, **«Aprobar»**. En el icono se abre la aplicación.

Si el código caduca mientras se añade el icono, en el PC: **«Generar otro código»**, y se teclea el nuevo. El código corto tiene 10 caracteres sin los que se confunden (ni 0 ni O, ni 1 ni I). Como el largo, sirve una sola vez y durante 5 minutos, y el freno de intentos es el mismo. Sobre todo, nada entra sin la aprobación en el PC, con el nombre a la vista.

### Si se abre la aplicación equivocada

- **Al emparejar:** si en el PC se aprueba el código para la otra aplicación (por ejemplo, el QR de Cocina leído desde Costiña Sala), el dispositivo **no guarda nada**, se da de baja solo en el servidor y dice qué pedir: «Este código era de Costiña Cocina: pide en el PC uno de Costiña Sala.». No queda ningún dispositivo suelto.
- **Ya emparejado:** abrir la otra aplicación no enseña nada. Dice cómo está conectado el dispositivo y ofrece «Desvincular» en dos pasos.
- **En Android, un dispositivo es una aplicación.** Para pasar un móvil de Sala a Cocina: «Desvincular» y emparejarlo de nuevo con el QR de Cocina.
- **Un QR o un enlace de antes** (`…/app/#pair=…`) abre `/app/`, que ahora es un **enrutador**: pregunta «¿Este dispositivo es para la sala o para la cocina?» y pasa el código a la elegida.

### Un icono «Costina» de antes

Un dispositivo emparejado con una versión anterior tiene su credencial (y quizá una orden sin confirmar) en la base de antes. **No hay que emparejarlo otra vez:** al abrir su icono, el enrutador lo lleva a la aplicación de su papel, que **adopta** la credencial y la orden. Lo de antes no se borra.

| Dónde | Qué hace con `0.43.0-c3c` | Qué hacer y cuándo |
|---|---|---|
| **Android con «Costina» instalada por https** (D6.5) | Abre y lleva a su aplicación, ya conectada. Pero sigue llamándose «Costina» y, como abarca todo `/app/`, puede quedarse con los enlaces de las dos aplicaciones nuevas: el QR acabaría abriéndose dentro de ella. | **Quitarla.** Antes, ábrela una vez (adopta lo suyo). Después, en Chrome, abre la aplicación nueva y añade su icono. Por último, quita «Costina» (mantener pulsado › Desinstalar). Si Android pregunta si borrar también los datos del sitio, contesta que no: son los de la aplicación nueva. |
| **Android con un acceso directo por http** (conexión sencilla) | Abre una pestaña en `/app/`, y el enrutador la lleva a su aplicación. | Quitarlo cuando el icono nuevo esté puesto. No estorba, pero confunde. |
| **iPhone o iPad con el icono «Costina»** | El icono guarda aparte. Al abrirlo, el enrutador lo lleva a su aplicación **dentro del mismo icono** y adopta lo suyo: sigue funcionando sin emparejar. | Puede quedarse. Para tener el nombre y el icono nuevos: añade el nuevo desde Safari y emparéjalo con un código corto (es otro dispositivo). Después, quita el viejo (al quitarlo se borra lo que guardaba) y, en el PC, quita el dispositivo viejo de la lista. |

Una orden sin confirmar que quedó **sin credencial** (después de que el PC quitara ese dispositivo) no se adopta, porque nada dice de qué aplicación era. Se queda en la base de antes y ninguna de las dos la ve (ver «Límites»).

## El aviso y sus límites en cada plataforma

El aviso sale de la **relectura** del tablero, que llega con el aviso del tiempo real en menos de un segundo en una red normal. Si el tiempo real se cae, llega por la relectura de cada 15 segundos, y al volver a la pantalla siempre se relee. Nunca sale del contenido del aviso del canal: si un aviso se repite o se pierde, no cambia nada.

| | Android (Chrome) | iPhone y iPad (desde el icono) |
|---|---|---|
| Banda y mesa en verde | Sí, en cualquier pantalla de Sala | Sí |
| Vibración | Sí, si la pantalla se ha tocado alguna vez desde que se abrió la aplicación (lo normal trabajando). Tras recargar, no vibra hasta el primer toque | No: Apple no deja vibrar a una web |
| Sonido | Nunca en la sala | Nunca en la sala |
| Aplicación abierta pero detrás de otra, o pantalla apagada | No vibra. Al volver o al desbloquear, la banda sale enseguida | Al volver, la banda sale enseguida |
| Aplicación cerrada o móvil bloqueado | Ningún aviso hasta abrirla | Ningún aviso hasta abrirla |
| Por la conexión sencilla (http) | Igual: ni la banda ni la vibración necesitan https | Igual |

- **Un aviso con la aplicación cerrada** necesita notificaciones del sistema: https de confianza, que llega con el certificado público y dominio propio, y además Push. Está diferido en la sección 6 del plan y el promotor lo aceptó el 22-10.
- **«Visto» se recuerda solo en memoria.** Tras recargar la página, el aviso vuelve a salir, pero sin vibrar: la primera lectura nunca vibra.
- **Si el camarero ha bajado mucho** en una lista larga (la bodega entera), la banda nueva queda arriba, fuera de la vista. En Android lo avisa la vibración. No se desplaza sola, para no arrancarle la lista de la mano.
- **El pase no sabe si el aviso ha llegado** a un móvil, y nunca dice «Sala avisada».

## Qué cambia

### Motor (solo añade)

- **Código corto:** `POST /auth/pairings` acepta un cuerpo opcional `{typed: true}`.
  - Sin cuerpo, con `{}` o con `typed: false`, el código es el largo de siempre. Un `typed` que no es booleano da 422.
  - El corto tiene 10 caracteres de `23456789ABCDEFGHJKLMNPQRSTUVWXYZ`, unos 50 bits (`DeviceStore.ShortCode`, con `RandomNumberGenerator.GetString`).
  - **`claim` busca primero el código tal cual llega.** Solo si no lo encuentra, prueba su forma corta (`DeviceStore.ShortForm`): sin espacios ni guiones, en mayúsculas, solo letras y cifras ASCII y exactamente 10 del alfabeto.
  - **El código largo nunca se normaliza.** Es base64url y distingue mayúsculas: cambiado de mayúsculas, no vale (`pairing_http.test_17`).
  - Se guarda solo la huella, que es única en toda la tabla, también contra las filas viejas. Si una emisión choca, se genera otro código (`IssueFreshAsync`, hasta 8 intentos y solo ante una violación de unicidad) en lugar de dar un 500.
  - Un solo uso, 5 minutos y el freno de intentos por origen y destino, como siempre. Sin columna nueva.
- **Redirecciones:** `PwaHosting` lleva `GET /app/sala` a `/app/sala/` y `GET /app/cocina` a `/app/cocina/`, como ya hacía con `/app`. Las dos carpetas se sirven con los mismos ficheros estáticos y con la misma CSP.
- **Un motor anterior** no lee el cuerpo y devuelve el código largo. El PC lo enseña tal cual.

### Enlace del QR (`Costina.Client`)

- `PairingLink.Build(dirección, código, aplicación)` da `…/app/sala/#pair=…` o `…/app/cocina/#pair=…`. Sin aplicación, `…/app/#pair=…`, que es el enrutador. Una aplicación desconocida se rechaza.
- `PairingLink.AppFor(rol)`: `service` va a Sala, `kitchen` a Cocina y cualquier otro rol al enrutador.
- `PairingLink.ShortAlphabet` (el mismo alfabeto del motor), `IsShortCode` y `DisplayCode`. `DisplayCode` enseña un código corto en dos grupos de cinco y deja cualquier otro tal cual.
- El QR sigue llevando **solo el código de un uso, en el fragmento**, y lo valida el mismo alfabeto cerrado de siempre.

### PWA: dos entradas de la misma compilación

- **Vite con tres páginas** y `base: '/app/'`:
  - `index.html` → `main.ts` → `RouterApp.vue`: el **enrutador** de `/app/`;
  - `sala/index.html` → `main-sala.ts` → `SalaApp.vue`, con el tablero (`BoardView`);
  - `cocina/index.html` → `main-cocina.ts` → `CocinaApp.vue`, con la cocina (`KitchenView`).

  Lo común está en `views/Shell.vue` (lo que era `App.vue`), que recibe la aplicación como dato: arranque, emparejamiento, lectura en vivo, orden sin confirmar y panel de estado. `HomeView` ya no elige la pantalla por el rol. Los trozos compartidos van a `/app/assets/`, inmutables. Las entradas se llaman `main-*.ts` para dejar `src/sala.ts` al aviso y `src/cocina.ts` libre para C3d.
- **Un manifiesto por aplicación**, en `public/sala/` y `public/cocina/`:
  - `id`, `start_url` y `scope` en su carpeta;
  - el nombre; el `short_name` de Cocina es «Cocina»;
  - `theme_color` `#0E7A5F` o `#2F4A6D`, y `background_color` `#F3F4F6`.

  Cada página lleva su título, sus metas de iOS, su `theme-color` y su `apple-touch-icon` de 180 px. **`/app/` ya no tiene manifiesto ni metas de iOS**, así que no se puede instalar.
- **Iconos:** `src/app-icons.ts` es el único dibujo. De él salen los PNG de 192, 512, maskable 512 y 180 y el `icon.svg` de cada aplicación (`scripts/make-icons.mjs`, sin dependencias), y el icono de la cabecera (`views/AppIcon.vue`, el único `v-html` de la PWA, fijado por `views.test.ts`). Son trazos propios, sin imágenes de fuera.

### Un service worker por aplicación

- `/app/sala/sw.js` y `/app/cocina/sw.js` son **el mismo fichero** (`install.test.ts` lo comprueba). Cada uno saca su carpeta de su propia dirección:
  - caché `costina-sala-v1` o `costina-cocina-v1`;
  - guarda su página, su manifiesto, sus iconos y los `/app/assets/` que pide su página, así que la primera apertura sin red ya arranca;
  - al activarse, **borra solo las cachés de su prefijo**. El de antes borraba todas las del origen: con dos aplicaciones, cada una habría vaciado la de la otra;
  - sin red, contesta **solo desde su caché**;
  - nunca toca `/api/`, `/health`, el enrutador ni la carpeta de la otra aplicación. Sin Background Sync.
- **El de antes se retira.** `/app/sw.js` (alcance `/app/`, que cubría también las dos carpetas nuevas) pasa a ser uno que no intercepta nada, borra `costina-shell-v1` y se da de baja. Como el navegador solo lo actualiza al navegar por páginas que controla, el enrutador y las dos aplicaciones dan de baja, además, cualquier registro de alcance exacto `/app/` (`worker.ts`).
- **Con la conexión sencilla (http) no hay service worker**: el navegador no lo permite y no se registra ninguno. Nada depende de él.

### Lo guardado: una base por aplicación y la adopción

- **Una base IndexedDB por aplicación**, `costina-sala` y `costina-cocina`, con los mismos almacenes de siempre (credencial y orden sin confirmar). Se elige en la entrada, antes de montar nada (`db.ts`), y el cerrojo entre pestañas también es por base. Mirar otra base nunca la crea.
- **Adopción** (`adoption.ts`), solo cuando la base propia no tiene credencial:
  1. Si la base vieja `costina` tiene credencial, la aplicación lee `/session` con ella. Solo si el papel le corresponde, la adopta.
  2. Primero pasa la orden sin confirmar, tal cual, al hueco vacío de su base, comprobando y escribiendo en una sola transacción (D6.7). Un hueco ocupado nunca se sustituye.
  3. Después guarda la credencial.
  4. Por último, quita la orden de la base vieja **por su clave**.

  La base vieja y su credencial **no se borran**: las usan el enrutador y una vuelta atrás. Un 401 borra la credencial donde estaba, como siempre (es la única señal de que ya no vale); cualquier otro fallo no decide nada y se reintenta.
- **Una credencial de la otra aplicación** (en su base o en la vieja) significa que el dispositivo es de la otra: esta no monta nada (`OtherApp.vue`).

### El enrutador `/app/`

`router.ts`, sin DOM:
- **con `#pair=`** pregunta «¿Este dispositivo es para la sala o para la cocina?» y pasa el código, ya validado, a la elegida, sin dejarlo en el historial;
- **con una credencial en `costina-sala` o `costina-cocina`** va a esa aplicación sin preguntar al servidor, también sin red;
- **con la credencial vieja** lee `/session` y va a la aplicación de su papel. Un papel que no se conoce se dice tal cual. Sin red, lo dice y reintenta cada 15 segundos;
- **sin nada** explica cómo conectar el dispositivo desde el PC.

### Cada aplicación comprueba su papel

- `apps.ts`, `appFits`: Sala admite `service` y el `main` antiguo («Principal (antiguo)», con el aviso de siempre); Cocina, solo `kitchen`; un papel que no se conoce, ninguna (fallo cerrado).
- **Es una decisión de uso, no de seguridad.** El servidor sigue siendo la frontera: filtra por papel y estación lo que se lee y lo que se puede hacer, la pinte la aplicación que la pinte.
- La comprobación al emparejar usa el papel que devuelve la recogida (antes se descartaba): si no es el suyo, no guarda la credencial y da de baja el dispositivo con `/auth/unpair`.

### iPhone y iPad

- `pairingPlace` reconoce un iPhone o un iPad (también un iPad que se presenta como Mac, por la pantalla táctil) que **no** está abierto desde su icono (`navigator.standalone` o `display-mode: standalone`). Ahí no hay formulario: se explica cómo añadir el icono, y el código del QR se quita de la dirección.
- Desde el icono, el campo del código acepta el corto como lo enseña el PC. `pairing.ts` (`typedCode`) lo manda en su forma canónica con la misma regla que el motor, y nunca toca un código largo.

### El aviso de «listo para servir» (sin tocar el motor)

- **`src/sala.ts`**, sin DOM y probado con vitest:
  - lista los pases en los que el servidor **anuncia `serve`**, no los que están en estado «Listo». Una alergia anotada después de validar deja el pase «Listo», pero sin `serve`, hasta que cocina la revisa;
  - la clave es `servicio:pase:readyAt`. «Rehacer» quita `readyAt`, así que la siguiente validación vuelve a avisar;
  - del más antiguo al más nuevo; los que no tienen hora, al final; a igual hora, por mesa, en orden ordinal;
  - `ReadyWatch.read()` corre tras cada lectura correcta y devuelve las claves nuevas respecto a la anterior. La primera lectura no trae ninguna, así que lo que ya estaba listo sale sin vibrar;
  - «Visto» se recuerda en memoria por clave mientras siga en la lectura. Si sale y vuelve, avisa otra vez;
  - `buzz` vibra solo con claves nuevas, la página a la vista y una página ya tocada; si no, ni llama a `vibrate`.
- **`views/ReadyBanner.vue`**, en `BoardView`, bajo la fila del estado de la conexión y por encima de la ficha, de «Abrir mesa» y del plano. Ocupa su sitio (nunca flota) y, desde 900 px de ancho, va en una fila.
  - «Servido» sale solo mientras el servidor anuncia `serve` en ese pase, se apaga mientras hay una orden en curso y es la misma orden que la ficha (`ficha.ts`), con la versión leída de esa mesa (F01) y por la orden sin confirmar de siempre.
  - «Ver mesa» usa `TableCard.show()` y `leave()`, que pregunta antes de dejar una ronda sin enviar.
  - Sus `data-testid` (`ready-banner`, `banner-title`, `banner-serve`, `banner-view`, `banner-seen`, `banner-more`) no empiezan por `do-` a propósito: debajo puede estar la ficha de la misma mesa con su propio botón de servir.
- **La mesa en verde:** `.tile.ready` pinta de verde (tonos `--ok`) una mesa «Listo para servir», que sale del mismo estado de la tarjeta. `floor.ts` y su tabla de casos común no cambian.

### Costiña Cocina, lo mínimo hasta C3d

Fuera el párrafo de ayuda. La cabecera lleva la marca y la estación en grande (`station-title`; `apps.ts`, `headerRole`).

### Puesto principal (WPF)

- **Los asistentes** piden siempre el código corto (`{typed: true}`) y lo enseñan con `PairingLink.DisplayCode`. El código largo de un motor anterior sale más pequeño y tal cual. El QR se construye con `PairingLink.AppFor` de la intención del asistente.
- **«Dejar este PC conectado»** sigue pidiendo el código largo, a propósito: el PC recoge su propio código y no lo teclea nadie.
- **Costiña Cocina** tiene ahora un desplegable y un solo botón, como Sala. Antes había un botón por estación. La estación elegida se conserva entre lecturas.
- **La lista** se agrupa por aplicación (`DeviceRow.Group`), y cada tarjeta dice su estación (`DeviceRow.Place`).
- **Palabras** (`Costina.Client/Labels`): los tipos de dispositivo son «Costiña Sala», «Costiña Cocina» y «Principal (antiguo)», las mismas que la PWA. El papel de un puesto de cocina en la sesión pasa a «Puesto de cocina», junto a «Puesto de sala».
- **Los iconos** de las dos aplicaciones están en `Themes/Icons.xaml`, copiados a mano del mismo dibujo que `app-icons.ts`.
- **Textos:** Inicio, el menú, la ayuda de los tipos de estación y la ayuda del campo del código usan las palabras nuevas.

## Decisiones y diferencias con la maqueta, para revisar

Pequeñas, tomadas al construir. Cualquiera se cambia si el promotor lo prefiere:
1. **«Puesto de cocina»:** el papel de un PC conectado como cocina. El plan decía que esa palabra cambiaba, sin decir cuál. Un PC nunca es «Costiña Cocina», y «Pantalla de cocina» es la que se retira.
2. **Costiña Cocina en Dispositivos:** un desplegable y un botón, como en la maqueta. Es un toque más cuando la estación buscada no es la primera de la lista.
3. **Inicio:** los dos botones de conectar van uno encima del otro; juntos no cabían.
4. **El PC pide siempre el código corto**, también para Android: el QR lleva ese mismo código, y así el código que se ve y el del QR son siempre el mismo.
5. **En Android, un dispositivo es una aplicación:** abrir la otra enseña cómo está conectado y ofrece «Desvincular», no un formulario.
6. **Un QR de antes** (`/app/#pair=`) pregunta sala o cocina en el enrutador, en lugar de fallar.
7. **La banda ocupa su sitio arriba** y no flota. En el plano de un móvil empuja las mesas unos 240-280 px hacia abajo. No se desplaza sola.
8. **«Visto» solo en memoria.** Tras recargar vuelve a salir, sin vibrar.
9. **Vibración:** dos golpes de 200 ms con una pausa de 100.
10. **Color de Costiña Cocina:** azul pizarra. El teja de la maqueta se descartó en la revisión del plan, porque junto al rojo de las alergias se confunde a distancia.
11. **Ejemplos de nombre:** «móvil de Ana» y «tablet del pase». Son ejemplos, nunca valores rellenos: antes de aprobar, el dispositivo no sabe su estación.
12. **Un dispositivo «Principal (antiguo)»** se sigue abriendo en Sala, con el aviso de siempre.

## Qué NO cambia

- **El esquema (v8), `grants.sql`, las copias y el instalador.** Solo cambian las versiones.
- **La CSP:** las dos aplicaciones se sirven con la misma (la única ampliación sigue siendo `img-src 'self' blob:`, de C3b). Sin cámara (`Permissions-Policy: camera=()`).
- **Todo bajo `/app/` y en el mismo origen.** Ningún otro puerto ni nombre.
- **El servidor es la frontera.** Qué aplicación monta qué es presentación; los botones solo existen porque el servidor los anuncia, ya filtrados por papel y estación (D3.1).
- **Una sola orden sin confirmar** por aplicación, con las reglas de D6.7: nunca la de otro actor ni de otra instalación, y el hueco se reserva en una transacción. Nunca una cola.
- **F01:** «Servido» desde la banda sale con la versión leída de esa mesa.
- **Tiempo real:** avisos finos y relectura autoritativa. El aviso al camarero es un estado leído, no un mensaje: no se mete nada en el canal.
- **Sin dinero** en los móviles ni en cocina (ADR-007). La guarda de la PWA (`money-guard.ts`) no cambia, ninguna clave nueva (`typed`) lleva palabras de caja y no se escribe «precio», «cuenta», «pago», «saldo» ni «€».
- **El service worker nunca toca `/api/`**, y no hay Background Sync.
- **Nada se borra en el servidor.** «Desvincular» revoca; la base vieja `costina` se conserva.
- **Lo desconocido, tal cual;** nombres, nunca códigos; castellano con tildes; solo tema claro.
- **La conexión sencilla (B.2)** igual: http solo con una IPv4 privada y apagada por defecto.
- **Envío a estaciones y pantalla de cocina:** sin cambios, salvo la cabecera (C3d).

## Comprobaciones automáticas

Cifras en la punta del corte, todas en este PC, una ejecución cada vez y sin administrador:
- **vitest:** 376 en 19 ficheros (antes 306 en 16). `npm run build` (`vue-tsc` y Vite) en verde y sin avisos. Nuevas o rehechas:
  - `apps.test.ts`: qué papel va con qué aplicación, también uno desconocido o heredado (un `Map`: «constructor» no encuentra nada); las palabras; el mensaje del código de la otra aplicación; el lugar de emparejar en iPhone, iPad y Android; la cabecera sin estación;
  - `adoption.test.ts` (con fake-indexeddb): la adopción con su orden sin confirmar (la base vieja conserva la credencial y nunca se borra), el `main` antiguo, la credencial de otro papel que se queda donde está, un papel desconocido, el 401, que sin servidor nada se decide ni se mueve, un hueco ocupado que nunca se sustituye, una orden ilegible que pasa tal cual y sigue bloqueando, la credencial de la otra aplicación en Android y las decisiones del enrutador. Mirar nunca crea una base;
  - `install.test.ts`, rehecho: los dos manifiestos con su `id`, alcance y nombre; los títulos de iOS; los iconos con su tamaño real; el enrutador sin manifiesto; nada de terceros ni en línea. Los dos service workers se **ejecutan** en un navegador de mentira: cada uno solo borra lo suyo y solo contesta desde su caché; el viejo se da de baja;
  - `sala.test.ts`, 19 casos: claves nuevas entre lecturas, primera lectura sin vibrar, «Visto», «Rehacer» que vuelve a avisar, varias a la vez, un pase «Listo» sin `serve` que no avisa hasta la revisión, una mesa en pausa, «+N más», y la vibración solo con la página tocada y a la vista;
  - `core.test.ts`: el código tecleado en dos grupos, con guiones o en minúsculas, sale en su forma canónica; el largo, tal cual;
  - `views.test.ts`: las páginas nuevas, sin palabras de caja, sin `style=` y con un solo `v-html` (el del icono).
- **ClientChecks:** 273 (antes 263): el enlace por aplicación, también por la conexión sencilla; `AppFor`; una aplicación desconocida; el código corto en dos grupos y lo demás tal cual; los tipos de dispositivo.
- **PersistenceChecks:** 62 (corre en la CI, no en `local-tests.sh`): el alfabeto, la forma corta y el reintento ante una huella repetida.
- **Dominio (D0):** 86, sin cambios.
- **HTTP**, las 14 suites en verde:
  - `pairing_http`, 18 (antes 14): `test_15` (el corto solo si se pide, del alfabeto y solo para el responsable; sin cuerpo, con `{}` o con `false`, el largo; un `typed` que no es booleano, 422), `test_16` (en minúsculas, con espacios o guiones; un solo uso; 5 minutos; solo la huella en reposo), `test_17` (el largo nunca se normaliza: cambiado de mayúsculas o con un espacio no vale) y la clase `PairingTypedThrottle` (10 fallos bloquean también un código corto bueno);
  - `pwa_http`, 7: `test_03` y `test_04` cubren el enrutador y las dos aplicaciones (la misma CSP, su manifiesto, sus iconos y su service worker, y las dos redirecciones);
  - las demás, sin cambios: packaging 14 (1 saltada), native 11, security 6/6, desktop_reads 9, affordances 6, restrictions 6, checkout 8, identity 10, station 8, organization 5, catalog 10 y offers 7.
- **RealtimeChecks:** 13.
- **Playwright** contra el motor real: **26** (antes 14), 13 en el proyecto de escritorio y 13 en el móvil:
  - `install.spec.ts`, nuevo: el enrutador pasa el código a la aplicación elegida; un código de la otra aplicación no guarda nada y da de baja el dispositivo en el servidor; **iPhone**: Safari no empareja y el icono se empareja con el código corto tecleado en dos grupos; un dispositivo de C3b llega a su aplicación por `/app/`, que adopta su credencial y su orden sin confirmar (reintentada con la misma clave); el service worker viejo se retira;
  - `banner.spec.ts`, nuevo: con «Bodega y bebidas» abierto, la banda sale sola con el nombre de la mesa, sin tapar volver, el buscador ni la ronda (se comparan sus cajas), y vibra una vez; una alergia anotada después de validar la quita y, al revisarla, vuelve y vibra; «Servido» desde la banda es **un** POST y la versión sube exactamente en uno; «Ver mesa», la mesa en verde, «Visto» que sobrevive a una relectura y, tras recargar, la banda sin vibrar; sin claves de dinero ni violaciones de la CSP;
  - `pairing.spec.ts`: se empareja por `/app/sala/#pair=` y `/app/cocina/#pair=`; las cachés de las dos aplicaciones no se mezclan; una credencial de cocina abierta en Sala no monta nada; la cabecera de Cocina.
- **DesktopChecks:** en verde. Los asistentes con su título, su QR y sus pasos; el código corto en dos grupos; el código largo de un motor anterior (el `FakeServer` tiene el interruptor `LongCodesOnly`); el cuerpo `{"typed":true}`; el desplegable de Cocina; la lista agrupada con sus iconos; el asistente a la vista; los botones de Inicio.
- **Batería completa** (`COSTINA_E2E=1 sh tools/agent/local-tests.sh`, puerto 5090): compilación con 0 advertencias y 0 errores; dominio 86/86, ClientChecks 273, las 14 suites HTTP, Playwright 26 y Realtime 13/13.

### En rojo antes de su arreglo

Cada prueba nueva falló primero. Las roturas a propósito se restauraron desde una copia en el `scratchpad`, nunca con `git`.
- **Motor:**
  - contra el motor sin cambiar, en una base nueva, fallaron `pairing_http` `test_15`, `test_16` y `PairingTypedThrottle.test_01`, y `pwa_http` `test_04` (`/app/sala` daba 401 en lugar de 302);
  - PersistenceChecks, bloque a bloque: el código corto, la forma corta y el reintento ante una huella repetida (un 23505 que no se reintentaba);
  - `test_17` guarda lo de antes, así que pasaba ya. Para probar que sirve, se rompió a propósito la búsqueda (la huella del código en mayúsculas): falló solo `test_17`, porque aceptaba el largo cambiado de mayúsculas.
- **Enlace:** ClientChecks contra un enlace que ignoraba la aplicación: 3 fallos (enlace por aplicación, `AppFor` y aplicación desconocida).
- **PWA:**
  - las pruebas nuevas de vitest contra la PWA anterior: fallaron los 5 ficheros;
  - nueve roturas hicieron fallar exactamente las 13 pruebas a las que apuntaban: mirar una base la crea; se adopta una credencial de cualquier papel; la orden vieja se suelta aunque no se haya movido; un objeto en lugar del `Map` («constructor» encuentra algo); el código tecleado sin la comprobación ASCII; el service worker borra las cachés de los demás; sin red contesta desde cualquier caché; el viejo no se da de baja; el enrutador conserva la credencial vieja tras un 401;
  - Playwright con seis roturas (mirar crea la base, cachés ajenas borradas, el viejo que no se da de baja, el enrutador que pierde el código, Safari en iPhone que empareja y un código de la otra aplicación que se guarda): fallaron exactamente esas 6 pruebas en los dos proyectos.
- **Aviso:**
  - `sala.test.ts` entero (`sala.ts` no existía) y el caso nuevo de `apps.test.ts`;
  - catorce roturas de `sala.ts`, una a una, cada una en su prueba: avisar por «Listo» en vez de por `serve`, vibrar en la primera lectura, la clave sin `readyAt`, el más nuevo primero, «Visto» que no se olvida o que vale para lo que no ha llegado, vibrar sin toque o con la página oculta, una versión que no es la leída, «+N más» sin «y», una mesa en pausa que no avisa, la entrada de una lectura anterior, un `vibrate` que falla y servir sin que el servidor lo anuncie;
  - Playwright contra la PWA sin el aviso: fallaron `banner.spec` y la cabecera de `pairing.spec` en los dos proyectos;
  - roturas activas en un solo proyecto cada vez: una banda fija que tapa volver (móvil), la mesa sin verde (escritorio), «Ver mesa» que no hace nada (móvil), aviso por «Listo» sin `serve` (escritorio), «Visto» olvidado en cada lectura (móvil), «Servido» enviado dos veces (escritorio: 2 POST) y la primera lectura que vibra (móvil). Cada una falló en su comprobación.
- **Puesto:**
  - ClientChecks: el código en dos grupos y las palabras de los tipos de dispositivo y del papel;
  - DesktopChecks, ocho roturas, cada una en su comprobación: QR sin la aplicación, código sin agrupar, pedir el código largo con `{}`, Cocina que ignora el desplegable, lista sin agrupar, asistente fuera de la vista, «sin instalar nada» de vuelta y el texto viejo del botón de Inicio.

## Capturas

Las de las aplicaciones las hace `e2e/captures-c3c.spec.ts` contra el motor real, sobre una base nueva, con las mesas M3, M4 y M5. Las del PC salen de DesktopChecks.

**El enrutador y la instalación (375 × 812):**
- [El enrutador sin nada guardado](images/c3c/enrutador-375x812.png): cómo conectar el dispositivo desde el PC.
- [Un QR de antes](images/c3c/enrutador-qr-375x812.png): «¿Este dispositivo es para la sala o para la cocina?».
- [Android: Costiña Sala abierta por el QR](images/c3c/sala-emparejar-375x812.png), con el código ya puesto y el nombre.
- [Costiña Sala emparejada](images/c3c/sala-375x812.png).
- [iPhone en Safari](images/c3c/iphone-safari-375x812.png): no empareja y explica cómo añadir el icono.
- [iPhone desde el icono](images/c3c/iphone-icono-375x812.png): el código corto tecleado en dos grupos.
- [Un código de Costiña Cocina leído en Costiña Sala](images/c3c/sala-codigo-de-cocina-375x812.png): no guarda nada y dice qué pedir.
- [Costiña Cocina abierta con una credencial de Sala](images/c3c/cocina-con-credencial-de-sala-375x812.png).

**El aviso:**
- [La banda sobre «Bodega y bebidas»](images/c3c/sala-aviso-en-bodega-375x812.png), con la ronda abierta abajo.
- [El plano con dos mesas en verde](images/c3c/sala-aviso-mesas-en-verde-375x812.png), una con el borde rojo de su alergia, y «+1 más».
- [La banda en una tableta en horizontal](images/c3c/sala-aviso-1180x820.png), en una fila.

**Costiña Cocina:**
- [La tablet del pase](images/c3c/cocina-1180x820.png): «Costiña Cocina | Pase».
- [Un iPad en vertical](images/c3c/cocina-ipad-820x1180.png): «Costiña Cocina | Cocina caliente».
- [La tablet del pase abriendo Costiña Sala](images/c3c/sala-con-credencial-de-cocina-1180x820.png).

**Puesto principal**, de DesktopChecks, con la versión `0.43.0-c3c`:
- [Dispositivos, «Conectar Costiña Sala · Sala 1»](images/c3c/devices-sala.png): una instalación sin dispositivos, el QR, el código en dos grupos y los dos caminos.
- [Dispositivos, «Conectar Costiña Cocina · Cocina caliente»](images/c3c/devices-cocina.png), con la lista agrupada por aplicación.
- [Inicio](images/c3c/inicio.png): los botones «Conectar Costiña Sala» y «Conectar Costiña Cocina», y los dispositivos con su aplicación.

**Para repetir las de las aplicaciones** hacen falta una base nueva y el motor con el build actual de la PWA:
1. `npm run build` en `frontend/pwa`.
2. La batería entera, o `dotnet build` de `src/Costina.Server`.
3. `C3C_CAPTURES="$(cygpath -w ~/Costina/docs/native/images/c3c)" COSTINA_E2E=1 sh tools/agent/local-tests.sh http packaging_http pwa_http`.

En la CI no corre nunca. Las del PC salen de `sh tools/agent/local-tests.sh desktop` en `dotnet/artifacts/desktop/` (`devices-sala.png`, `devices-cocina.png` y `home.png`, copiada como `inicio.png`).

## Guion físico (solo en un PC de laboratorio, con un Android, un iPhone y un iPad)

**Nunca en un PC con una instalación real**, ni sobre sus servicios, puertos o carpetas. Hace falta:
- un PC de laboratorio con una instalación de prueba en `0.43.0-c3c`: la demostración (`/DEMO=1`, D5.6) o el laboratorio de `docs/native/lab-engineering.md`, con la **conexión sencilla** activa (B.2);
- en su Wi-Fi, un **Android** con Chrome, un **iPhone** y un **iPad** con Safari. Anota el modelo y la versión de cada uno.

Las capturas son de un Chromium que se hace pasar por iPhone. Lo que de verdad hace Safari solo lo dice este guion.

**Preparación opcional, para los pasos 2 y 7:** antes de actualizar el laboratorio a `0.43.0-c3c`, con `0.42.0-c3b`, empareja como antes el Android (comandero de Sala 1) y el iPad (cocina del pase), y añade en los dos el icono «Costina». Después actualiza con el instalador. Sin preparación, salta los pasos 2 y 7.

1. **PC.** En Inicio salen «Conectar Costiña Sala» y «Conectar Costiña Cocina», con su icono. En Dispositivos, un bloque por aplicación con su desplegable.
2. **Android con «Costina» de antes.** Abre «Costina»: llega a Costiña Sala ya conectado, con el nombre de antes y sin emparejar. En el PC sale bajo Costiña Sala. En Chrome, abre `http://<IP>:5089/app/sala/` y, en el menú, «Añadir a pantalla de inicio». Quita «Costina» (si Android pregunta si borrar también los datos del sitio, contesta que no) y abre el icono nuevo: sigue conectado. Para seguir con el paso 3: «Opciones» › «Desvincular este dispositivo» › «Sí, desvincular».
3. **Android, Costiña Sala por el QR.** «Conectar Costiña Sala · Sala 1». Lee el QR con la cámara: se abre Costiña Sala con el código puesto (anota si se abre en Chrome o en el icono). Nombre `movil-ana` › «Solicitar emparejamiento». En el PC, «Aprobar». Se abre el plano.
4. **Android, el icono** (si no lo pusiste en el paso 2). Menú de Chrome › «Añadir a pantalla de inicio». Anota qué crea (por http, un acceso que abre una pestaña), con qué nombre y con qué dibujo. Ábrelo: sigue conectado.
5. **iPhone, Safari.** «Conectar Costiña Sala · Sala 1». Lee el QR con la cámara: Safari abre «Añádela a la pantalla de inicio», **sin** formulario. Compartir › «Añadir a pantalla de inicio» › «Añadir».
6. **iPhone, el icono.** Ábrelo. **Comprueba que abre a pantalla completa, sin la barra de Safari**, también por la conexión sencilla (http): si abriera dentro de Safari, seguiría diciendo «Añádela…» y no se podría emparejar. Anótalo. Teclea el código del PC en minúsculas y con el espacio, nombre `iphone-bea` › «Solicitar emparejamiento». En el PC, «Aprobar». Se abre el plano. Si el código caducó mientras tanto, «Generar otro código».
7. **iPad con «Costina» de antes.** Ábrelo: llega a Costiña Cocina ya conectado, sin emparejar, dentro del mismo icono.
8. **iPad, Costiña Cocina.** En el desplegable de Cocina, «Pase» › «Conectar Costiña Cocina · Pase». Como en el iPhone: Safari no empareja, se añade el icono «Costiña Cocina» (azul, con el gorro), se abre y se teclea el código. Nombre `ipad-pase`. La cabecera dice «Costiña Cocina | Pase». Si hiciste el paso 7: quita el icono «Costina» y, en el PC, el dispositivo viejo.
9. **La otra aplicación.** En el Android, en Chrome, abre `http://<IP>:5089/app/cocina/`: «Este dispositivo está conectado como Costiña Sala · Sala 1», sin ninguna mesa ni comanda. No toques «Desvincular». En el PC, la lista de Dispositivos sale agrupada: `movil-ana` e `iphone-bea` bajo Costiña Sala y `ipad-pase` bajo Costiña Cocina.
10. **El aviso.** En el Android, toca la pantalla una vez. Abre una mesa con 2 personas, «Iniciar servicio» y «Enviar siguiente pase». Entra en «Bodega y bebidas» y toca «Copa» en un vino: la ronda queda abajo, sin enviar. Marca las elaboraciones en el PC, como responsable (Comedor y cocina), y en el iPad toca «Validar pase: listo para servir». En el Android sale la banda «Mesa N · <pase> listo para servir» y **vibra**; volver, el buscador y la ronda siguen a la vista. En el iPhone sale la misma banda, **sin vibrar**.
11. **«Servido».** En el Android, «Servido · <pase>» en la banda. La banda se va del Android y del iPhone. En el PC, el pase está servido **una vez**.
12. **La mesa en verde, «Ver mesa» y «Visto».** Envía el siguiente pase desde el Android y valídalo como en el paso 10. En el plano de los dos móviles, la mesa está en verde. En el iPhone, «Ver mesa» abre su ficha. En el Android, «Visto» quita la banda solo de ese móvil.
13. **Recargar y bloquear.** Sirve ese pase. En el Android, recarga la página (o cierra y abre el icono) y no toques la pantalla. Envía y valida otro pase desde el PC y el iPad: la banda sale, pero no vibra hasta haber tocado. Luego bloquea el Android, valida otro pase y desbloquéalo: la banda sale enseguida.
14. **Repaso.** En ninguna pantalla de los tres dispositivos aparecen «precio», «cuenta», «pago», «saldo» ni «€».

| Paso | Android | iPhone | iPad | Notas |
|---|---|---|---|---|
| 1 a 14 | | | | |

Si el laboratorio tiene además https con la CA instalada en los dispositivos, repite los pasos 3 a 8 por https y anota la diferencia: en Android, «Instalar aplicación» y una aplicación a pantalla completa.

## Vuelta atrás

- **En el servidor, sí.** No se guarda nada que `0.42.0-c3b` no lea: el código corto usa la misma columna que el largo.
- **En los dispositivos, no del todo.** Uno emparejado por primera vez con C3c tiene su credencial en `costina-sala` o `costina-cocina`, que la versión anterior no mira: habría que emparejarlo otra vez y quitar el de C3c desde el PC. Los adoptados conservan también su credencial en la base vieja, así que volverían a funcionar si el PC no los ha quitado.
- **Sigue valiendo lo de C3b:** volver a `0.41.0-c3a` o antes exige restaurar la copia previa.

## Límites y diferido

- **iPhone y iPad de verdad no están en la CI**, que solo instala Chromium: se simulan con el agente de iOS y `navigator.standalone`. Lo de Safari, el icono por http, la vibración real y los iconos de la pantalla de inicio se comprueban con el guion.
- **El riesgo principal que queda por medir:** si un icono de iPhone o iPad abierto por la conexión sencilla (http) no se abriera a pantalla completa, la aplicación lo tomaría por Safari y no dejaría emparejar. Es el paso 6 del guion (y el 8 en el iPad). Si pasa, hace falta un arreglo antes de usar iPhone o iPad por http.
- **Una orden sin confirmar que quedó sin credencial** (después de un 401 en C3b) no se adopta, porque no se sabe de qué aplicación era. Sigue en la base vieja, sin borrar, y ninguna de las dos aplicaciones la enseña.
- **En Android, un dispositivo es una aplicación.** Para cambiarlo: «Desvincular» y emparejar de nuevo.
- **«Costina» instalada en Android** (alcance `/app/`) puede quedarse con los enlaces de las aplicaciones nuevas hasta que se quite.
- **Un PC adicional en `0.43.0-c3c` con un servidor anterior** dibujaría un QR a `/app/sala/`, que el servidor viejo no sirve. Hay que actualizar antes el servidor, que es el orden normal.
- **El dibujo de los iconos está dos veces:** `frontend/pwa/src/app-icons.ts` y su copia a mano en `Themes/Icons.xaml`. Nada comprueba que coincidan: un cambio se copia a mano.
- **Un PC adicional que se empareja tecleando el código corto** en «Código del PC» no tiene prueba de escritorio. El motor acepta los espacios y guiones (`pairing_http`) y el PC solo quita los espacios de los extremos.
- **«Ver mesa» de otra mesa con una ronda sin enviar** pasa por la pregunta de salir. Está comprobado por tipos, pero no por una E2E.
- **La banda usa `role="alert"`.** No se ha comprobado cómo la anuncia un lector de pantalla.
- **Tras cerrar un asistente**, la tarjeta de conectar se queda desplazada donde la dejó.
- **A E4e** (`0.44.0-e4e`): la revisión de alergias antes de enviar, las órdenes de grupo, el pase que marca por una estación (respuesta 2), quién abrió la mesa y la guarda del instalador contra bajar de versión.
- **A C3d** (`0.45.0-c3d`): la comanda por mesa en Costiña Cocina, con solo «Listo» (respuesta 1), el ámbar a los 15 y a los 25 minutos (respuesta 3), el sonido tras «Empezar turno» (respuesta 4) y el botón del pase para una estación sin tablet (respuesta 2).
- **A C3e** (`0.46.0-c3e`): «Mis mesas» y el aviso solo de ellas, abrir mesa con alergias en tres toques, «Carta y menús» y el «Preguntar al servidor» de una orden de otra instalación (D6.7).
- **Sin fecha** (sección 6 del plan): el aviso con la aplicación cerrada (certificado público y Push), la intención fijada al emitir el código, volver a avisar si un pase listo no se sirve, el aviso en el PC y el modo quiosco.
