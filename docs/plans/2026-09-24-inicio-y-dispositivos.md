# Plan (borrador): Inicio y dispositivos, y puesto principal conectado al arrancar

Fecha: 2026-09-24. **Estado: BORRADOR, pendiente de las decisiones del promotor (sección final).** No se ha escrito código. Base: `develop` con E4b mezclada (`7d276b7`, PR #66).

Origen: al revisar la implementación, el promotor pidió (24-09) una **pantalla de inicio** con un apartado para **conectar dispositivos** (una opción para comanderos y otra para las pantallas de cocina de cada estación) y preguntó:

1. ¿Por qué aparece una pantalla de conexión al abrir el programa? ¿Hay que hacerla cada vez? ¿No puede arrancar ya conectado y operativo?
2. Si un comandero se apaga o se reinicia, ¿hay que volver a conectarlo o es persistente?

Este documento recoge las respuestas, **comprobadas leyendo el código** (cuatro lecturas independientes, cada afirmación con su referencia y revisada por un segundo lector que intentó refutarla), los fallos encontrados y la propuesta de dos cortes. Las referencias `fichero:línea` son de `develop` en `7d276b7`.

## 1. Respuestas

### 1.1 La pantalla de conexión del programa de escritorio

- **No arranca nada.** El motor es un servicio de Windows (`Costina.Server/Program.cs:198`, `ServiceInstaller.cs:27-29`: arranque automático retardado, sin sesión de usuario). Los comanderos y las pantallas de cocina funcionan con el programa del PC cerrado (`docs/native/D5.6-physical-install.md:44-47`).
- **Solo identifica a quien usa el PC.** Desde D4.3 el motor no atiende a nadie sin identidad: sesión de persona (usuario y contraseña) o token de dispositivo emparejado `dev.<id>.<secreto>` (`Program.cs:267-306`). Anónimos solo `/health`, `/ca.crt`, login, reclamar y recoger un emparejamiento, y los ficheros de la PWA.
- **Hoy hay que entrar cada vez**, a propósito:
  - la contraseña nunca pasa por bindings ni por disco (`MainWindow.xaml.cs:17-18,49-53`);
  - el token de sesión vive solo en memoria (`ShellViewModel.cs:29`; regla en `dotnet/AGENTS.md:25`); cerrar la ventana ni siquiera cierra la sesión en el servidor;
  - la sesión dura 30 min deslizantes y 14 h como máximo (`Costina.Core.Persistence/IdentityStore.cs:38-39`);
  - la ventana no intenta conectar al abrirse (`App.xaml:1` solo tiene `StartupUri`; `MainWindow.xaml.cs:26-31`).
- **La tarjeta mezcla tres cosas** (`MainWindow.xaml:58-80`), y por eso no se entiende: la dirección del servidor (por defecto `http://127.0.0.1:5088`, no se recuerda: `ShellViewModel.cs:24`), la entrada con usuario y contraseña (el usuario tampoco se recuerda) y «Emparejar este puesto» (conectar este PC como un dispositivo más, con código y nombre).
- **Ya existe un camino sin contraseña**, pero es incómodo e inseguro: emparejar el propio PC como dispositivo. Su token se guarda cifrado con DPAPI en `%LOCALAPPDATA%\Costina\device-<host>-<puerto>.bin` (`DpapiDeviceStore.cs:10-40`) y reconecta con un clic en «Conectar puesto emparejado» (`ShellViewModel.cs:145-158`), no solo. Para aprobarse a sí mismo hace falta otra ventana u otro PC con sesión de principal (`ShellViewModel.cs:284`). Si se aprueba con rol **Principal** (el desplegable lo ofrece: `MainWindow.xaml:543`; el servidor lo acepta: `DeviceStore.cs:85`), cualquiera sentado delante tiene caja y configuración sin contraseña, y la auditoría dice `device:<nombre>`, no la persona (`Program.cs:311`).

### 1.2 Un comandero o pantalla de cocina que se apaga o se reinicia

- **No hay que volver a conectarlo.** La PWA guarda su credencial en IndexedDB del origen (`frontend/pwa/src/credentials.ts:3-10`, base `costina`, almacén `device`); sobrevive a cerrar la pestaña, el navegador, reiniciar y actualizar la PWA (el service worker solo borra su caché de ficheros: `public/sw.js:7,13-15`). En el servidor el token **no caduca**: solo se comprueba el hash y `revoked_at IS NULL` (`DeviceStore.cs:140-150`).
- Al abrir el icono entra directo al comandero o a la cocina; si el servidor no responde muestra «Sin conexión» y reintenta cada 15 s y al volver la red (`App.vue:59-63,77-82`). El tiempo real se reconecta solo, sin límite (`realtime.ts:9-18`, `live.ts:88-126`).
- **Se pierde la conexión** (hay que emparejar otra vez) si:
  - se revoca desde el PC (la PWA recibe 401, borra la credencial y lo explica: `App.vue:31-36`);
  - alguien pulsa «Desvincular este dispositivo»: un toque, sin confirmación, siempre visible (`HomeView.vue:27-30`); además solo borra en local y el dispositivo sigue activo en el servidor (`App.vue:52-57`);
  - se borran los datos del sitio, se usa modo incógnito u otro navegador;
  - se abre por otra dirección (IP en vez de nombre, otro puerto): IndexedDB es por origen;
  - en iPad/iPhone se emparejó en Safari y luego se abre desde el icono (almacenamiento aparte), o iOS purga los datos de una web no instalada (`docs/native/D6.5-qr-install-physical.md:64,99`);
  - se restaura una copia anterior al emparejamiento o se reinstala con base nueva (401 → la PWA borra la credencial y dice «revocado» aunque nadie lo revocó).
- Tras reiniciar la tablet **nada abre la app sola**: hay que tocar el icono. La prueba física de apagar y encender está pendiente (`docs/STATUS.md`, guion D6.5 sin ejecutar; ese guion no tiene el paso «apagar la tablet»).

### 1.3 Conectar dispositivos

- Ya existe, pero escondido y genérico: menú **Configuración › Puestos y emparejamiento**, solo con rol principal (`MainWindow.xaml:29,515-557`). Un único «Generar código» (QR `https://EQUIPO:PUERTO/app/#pair=CÓDIGO`, 5 min, un uso: `DeviceStore.cs:20`, `PairingLink.cs`).
- **Comandero o pantalla de cocina lo decide solo el rol elegido al aprobar**: `kitchen` abre la pantalla de cocina, cualquier otro rol el comandero (`frontend/pwa/src/views/HomeView.vue:19-24`). El desplegable viene en **Sala** y la **primera estación activa** (`DevicesViewModel.cs:21,80-81`), que en una instalación nueva es `pase` (`PostgresStore.cs:87-95`). Resultado: **un comandero aprobado sin tocar nada queda como Sala/pase y puede validar pases** (`Affordances.cs:15,30-31`; `DiningOperations.cs:67-71`). El servidor no comprueba que el tipo de estación encaje con el rol (`Program.cs:386-388`).
- La lista de puestos es texto con códigos internos (`service`, `kitchen`, `sala-1`), mezcla revocados y activos y solo permite revocar (`Models.cs:14-18`, `MainWindow.xaml:549-554`). No hay renombrar, ni cambiar rol o estación (hay que revocar y emparejar otra vez), ni «conectado ahora» o última conexión (`core.devices` no tiene esas columnas: `schema.sql:174-182`).
- Una estación por dispositivo (`schema.sql:178`, `NOT NULL`); una pantalla de cocina ve el pase entero pero solo actúa sobre lo suyo, y solo `pase` valida y revisa (`kitchen.ts:18-38`, `DiningOperations.cs:54-71`).

## 2. Fallos y huecos encontrados

| # | Qué pasa | Dónde |
|---|---|---|
| F1 | Si la sesión del PC caduca, cada acción da un error genérico («El servidor rechazó…») y la pantalla parece conectada. Si la acción era una orden, queda **pendiente**: «Desconectar» se deshabilita y «Reintentar» recibe 401 una y otra vez; solo se sale cerrando el programa. | `ShellViewModel.cs:87-96,290`; `ApiClient.cs:157,170` |
| F2 | El tiempo real del WPF se rinde tras unos 28 s de reintentos (reinicio del motor, Windows Update) y no vuelve hasta reconectar a mano; la PWA sí reintenta sin límite. | `Client/Realtime.cs:26-27`; `ShellViewModel.cs:217,240-245` |
| F3 | Tras encender el PC el motor tarda 2-3 min (arranque retardado); el WPF da «Servidor sin respuesta» sin esperar ni reintentar, y un fallo de certificado sale con el mismo mensaje. | `ShellViewModel.cs:92`; `AuthClient.cs:21`; `D5.6:45` |
| F4 | La dirección del servidor y el usuario no se recuerdan: en un puesto adicional hay que teclear `https://EQUIPO.local:5443` cada vez. «Conectar puesto emparejado» no se reevalúa al teclear la dirección (no hay `OnEndpointChanged`), así que puede quedar deshabilitado (sin comprobar en ejecución). | `ShellViewModel.cs:24-25,73-74,285-288` |
| F5 | Aprobación por defecto Sala + primera estación (`pase` en instalación nueva): comandero que valida pases. Sin coherencia rol ↔ tipo de estación en el servidor. | `DevicesViewModel.cs:21,81`; `Program.cs:386-388` |
| F6 | Una tablet puede recibir el rol **Principal** (caja y configuración sin contraseña, auditoría a nombre del dispositivo). | `MainWindow.xaml:543`; `DeviceStore.cs:85` |
| F7 | La petición de una tablet no aparece sola en el PC (no hay aviso en tiempo real ni sondeo): hay que pulsar Actualizar. La tablet espera 5 min; el PC la lista 30 min: aprobar tarde crea un emparejamiento que nadie recoge. | `DeviceStore.cs:53-72`; `pairing.ts:19,34-36` |
| F8 | La PWA puede quedarse en «Cargando…» para siempre si al abrir el servidor responde un error que no es 401 (p. ej. 503 con PostgreSQL caído) o IndexedDB no abre: el temporizador de 15 s y el aviso `online` se instalan después de la primera lectura. | `App.vue:41,59-63`; `db.ts:12-14` |
| F9 | «Desvincular» sin confirmación y solo local; el dispositivo sigue activo en el servidor. Cualquier 401 se explica como «revocado». | `HomeView.vue:29`; `App.vue:31-36,52-57` |
| F10 | El certificado TLS del servidor dura 397 días y se renueva a mano (`renew-tls`); nadie avisa. Si caduca, todas las tablets y puestos adicionales se quedan sin conexión (no se desemparejan). | `LocalTls.cs:98-110`; `D5.3-lan-https.md:49` |
| F11 | Emparejar, aprobar, revocar y entrar no dejan evento ni fila en `core.audit` (solo columnas `created_by/decided_by/revoked_by`). | `DeviceStore.cs`; `docs/SECURITY.md:43-44` |
| F12 | Nombres de dispositivo no únicos, y el actor de auditoría e idempotencia es `device:<nombre>`: dos tablets con el mismo nombre comparten actor. | `schema.sql:176`; `Program.cs:311` |
| F13 | Al cerrar la sesión de una persona el servidor no corta su conexión de tiempo real (solo registra las de dispositivos). | `RealtimeEvents.cs:41-56`; `Program.cs:430,440-444` |

Documentación desactualizada detectada (corregir dentro del corte): `docs/STATUS.md` bloqueos 1 y 5 (ya resueltos en código por D4 y E2–E4); `D4.3b-wpf-login.md:29` («sin teclear nada» solo vale por loopback); `D4.2-device-pairing.md:7,18` (tablas `native_d1`, «la estación aún no restringe»); `D6.4-pwa-kds.md:10` (cocina «sin estación», imposible desde E2); `D6.1-pwa-base.md:47` (la revocación ya se detecta sola); `dotnet/AGENTS.md:25` («por puerto»: es por host y puerto); `INSTALLATION_ARCHITECTURE.md` y ADR-003 (Laravel/Tauri, superados por ADR-008); texto inicial de Puestos («tecléaselo», ya hay QR: `DevicesViewModel.cs:18`).

## 3. Propuesta: dos cortes antes de E8

Sin cambio de esquema (sigue v6) ni de `grants.sql` (el rol de ejecución ya tiene UPDATE sobre `core.devices` y `core.pairings`). Un corte por PR desde `develop`, en este orden. Si el promotor lo aprueba, E8 reservas pasa detrás; el resto del orden no cambia.

### Corte A — Inicio y dispositivos

Lo que verá el promotor: al entrar, una pantalla **Inicio** con el estado (servidor, tiempo real, quién está conectado), accesos grandes a Comedor y Caja, y el bloque **Dispositivos** con **[Conectar comandero]** y **[Conectar pantalla de cocina]** (un botón por estación: Cocina fría, Cocina caliente, Pase, Barra…). Al pulsar, QR grande; la tablet lo lee; en el PC aparece sola la petición «"Tablet fría" quiere ser la pantalla de Cocina fría» con [Aprobar] [Rechazar]. Lista agrupada (comanderos / pantallas por estación) con «conectado ahora» y [Quitar] con confirmación.

**Motor (núcleo; ningún módulo cambia):**
- A1. **Ningún dispositivo nuevo con rol `main`**: `approve` con `role=main` → 409 `device_role_not_allowed` (`DeviceStore.cs:85`). Los existentes siguen funcionando y la lista los marca. Resuelve F6.
- A2. **Coherencia rol ↔ tipo de estación** en `approve` (junto a la comprobación de estación activa, `Program.cs:386-388`; el tipo se lee con `OrganizationUnit`): `kitchen` en estaciones Cocina, Pase o Barra; `service` en Sala o Barra (y en Pase solo si la decisión 3 es que el PC principal hace de pase; aun así el asistente de comanderos nunca ofrece el pase). Si no encaja, 409 `station_kind_mismatch`. Solo afecta a aprobaciones nuevas. Resuelve F5 en el servidor (el cliente no decide reglas).
- A3. **`online` en `GET /auth/devices`**, sacado del registro en memoria de conexiones de tiempo real que ya existe (`RealtimeEvents.cs:38-47`): «canal abierto ahora». Sin columna `last_seen_at`.
- A4. **`stationName` en `/session`** (solo añade), para la cabecera de la tablet.
- A5. **`POST /auth/unpair`**: el propio dispositivo se da de baja (reutiliza la revocación y el corte del tiempo real). Una sesión de persona no puede usarlo.

**WPF:**
- A6. **Inicio** como primera vista (`TabItem` nuevo antes de Comedor, `MainWindow.xaml:95-98`) y entrada «Archivo › Inicio»; la barra de menús de primer nivel no cambia (la fijan los checks del WPF).
- A7. **Centro de dispositivos** (rehace `DevicesTab` y `DevicesViewModel.cs`): asistentes «Conectar comandero» (estaciones Sala/Barra; si no hay ninguna de sala, ofrece crear «Sala» con `station-create`, auditado a nombre del responsable) y «Conectar pantalla de cocina» (estaciones Cocina/Pase/Barra, por su nombre); cada asistente recuerda **su** `pairingId`, consulta las peticiones cada 2 s mientras vive el código y solo aprueba la suya (las ajenas solo se pueden denegar); sin desplegable de rol ni opción «Principal»; aviso si ya hay un dispositivo activo con el mismo nombre (F12); dirección para los dispositivos propuesta desde `/diagnostics` en vez de teclearla; lista agrupada con `online`, revocados ocultos tras un conmutador, «este PC» marcado y confirmación al quitar. Resuelve F5, F7 (en el PC) y la parte visible de F12.
- A8. **Aviso de certificado** en Inicio cuando falten menos de 30 días (`/diagnostics` ya expone la caducidad). Mitiga F10.

**PWA:**
- A9. Arranque sin atascos: temporizador y `online` antes de la primera lectura; cualquier error que no sea 401 (o IndexedDB que no abre) pasa a «Servidor no listo, reintentando». Resuelve F8.
- A10. «Desvincular» dentro de Opciones, en dos pasos (sin `window.confirm`, CSP intacta) y llamando a `/auth/unpair`. Resuelve F9 (el texto del 401 deja de decir siempre «revocado»).
- A11. Cabecera grande «PANTALLA DE COCINA · Cocina caliente» o «COMANDERO · nombre» (con `stationName`).

**Pruebas del corte A:** `pairing_http` (matriz rol × tipo de estación con 409/200, veto a `main`, `online:false` sin tiempo real, `unpair` revoca y después 401, sesión de persona rechazada); `station_http` (`stationName`); RealtimeChecks (`online` true/false al conectar, cerrar, revocar y `unpair`); ClientChecks (`DeviceRowDto.Online` con y sin el campo, `SessionInfo.StationName`); DesktopChecks con capturas (Inicio, asistente de comandero, asistente de cocina con QR y petición reconocida, lista agrupada; «Principal» no aparece; «Aprobar» deshabilitado si el `pairingId` no coincide; menú intacto); vitest (arranque con 503 que se recupera solo, desvincular en dos pasos, cabecera); Playwright (aprobación como cocina/`hot` con cabecera correcta, 503 al abrir que se recupera, desvincular deja el dispositivo revocado en el servidor); batería completa `sh tools/agent/local-tests.sh`.

### Corte B — El puesto principal arranca conectado

Lo que verá el promotor: al encender el PC, Costiña se abre sola; si el servidor aún arranca, muestra «Arrancando el servidor (1-3 min tras encender)…» y espera; después entra en **Inicio** sin teclear nada y trabaja como un comandero grande (o como pase, según la decisión 3). Cobrar, cerrar cuentas, Configuración y conectar dispositivos piden la contraseña del responsable («Entrar como responsable»); tras 15 min sin tocar nada, o si la sesión caduca, vuelve solo al modo puesto con un aviso claro.

**Decisión central (a fijar en ADR-013 «Puesto principal: identidad de equipo + responsable»):** el PC principal lleva **identidad de equipo** (un dispositivo `service` con estación, token DPAPI, como cualquier comandero) para lo operativo, e **identidad de persona** (sesión en memoria, 30 min / 14 h) para lo sensible. El único secreto en disco es el del dispositivo, ya permitido por `dotnet/AGENTS.md:25`.

**WPF (sin cambios en el motor):**
- B1. **Arranque**: `App.OnStartup` en lugar de `StartupUri`; instancia única (Mutex que trae al frente la ventana abierta); `Shell.StartAsync()` consulta `/health` (anónimo) cada 3-5 s sin rendirse durante el arranque en frío, distingue «no responde» de «certificado no fiable» (este sin reintento ni atajo: la validación TLS sigue estricta, `ApiClient.cs:65-76`) y, si hay credencial DPAPI del puesto para esa dirección, conecta solo con `ConnectPaired`. Los checks construyen la ventana sin tocar la red. Resuelve F3.
- B2. **`DesktopSettings`** en `%LOCALAPPDATA%\Costina\desktop.json`: dirección del servidor, usuario y dirección para los dispositivos (absorbe `device-address.txt`). **Nunca secretos.** `OnEndpointChanged` → `Sync()`. Resuelve F4.
- B3. **Dos niveles**: *Puesto* (el dispositivo del PC: Inicio y Comedor; Caja, Configuración y ERP siguen ocultos porque dependen de `IsMain`) y *Responsable* (sesión de persona). Cambiar de nivel solo sin órdenes pendientes; al cambiar se cierran la conexión y el tiempo real anteriores (hoy `ConnectWithToken` sustituye `Api` sin cerrar la anterior: `ShellViewModel.cs:201-230`). «Salir del modo responsable» hace logout y vuelve a Puesto.
- B4. **Sesión caducada**: un 401 de la sesión de persona dentro de `Run` pasa a «Tu sesión ha caducado» y vuelve a Puesto (o a la tarjeta de entrada si el PC no está conectado como puesto); la orden sin confirmar sigue en disco y reaparece al volver a entrar con el mismo rol. Un 401 del puesto (revocado) borra la credencial y lo explica. Salida automática del responsable tras 15 min sin teclado ni ratón (reloj inyectable para los checks). Resuelve F1.
- B5. **Tiempo real que se recupera solo**: cuando el canal queda cerrado, reintento cada 30 s como la PWA. Resuelve F2.
- B6. **«Dejar este PC conectado para el servicio»**: desde el nivel Responsable, con una sola sesión, `POST /auth/pairings` → `claim` → `approve(service, estación)` → `collect`, comprobando que el `pairingId` reclamado es el emitido; guarda con DPAPI; `decided_by` registra a la persona. Sustituye la fila «Emparejar este puesto» de la tarjeta de entrada.
- B7. **Tarjeta de entrada simplificada** (solo cuando el PC no está conectado como puesto): «Entrar como responsable» (usuario recordado y contraseña) y «Este PC no es el servidor…» plegado (dirección y emparejar como puesto adicional).

**Motor:**
- B8. **Cerrar la sesión corta también su tiempo real**: registrar las conexiones del hub de sesiones de persona por `sessionId` y abortarlas en `logout` (con este corte entrar y salir como responsable es rutina, y el grupo `fin` no debe quedar abierto). Resuelve F13 para el logout (la caducidad sin logout queda para E5).

**Instalador:**
- B9. Componente marcado por defecto «Abrir Costiña al iniciar Windows» (`$SMSTARTUP\Costina.lnk`) en los modos Completo y Puesto adicional, no en Solo servidor; la desinstalación lo borra; comprobado en `installer_checks.ps1`.

**Pruebas del corte B:** `pairing_http` (autoemparejamiento con una sola sesión: `approvedBy == user:<responsable>`, `/session` con rol `service` y estación, `/checkout/*` → 403); RealtimeChecks (`logout` cierra la conexión de la sesión); ClientChecks (clasificación de fallos de arranque: red → reintentar, TLS → no, 401 de sesión frente a 401 de dispositivo); DesktopChecks con capturas (arrancando, primera vez, Inicio como puesto, Inicio como responsable; `desktop.json` sin `dev.` ni tokens; cambiar la dirección reevalúa el puesto; 401 del responsable vuelve a Puesto conservando el aviso de la orden pendiente; salida por inactividad; con sesión simulada de dispositivo `service`, Caja/Configuración/ERP ocultos); instalador (acceso directo por modo y borrado al desinstalar); guion manual (encender el PC en frío, entrar como responsable y salir, 30 min sin uso, reiniciar tablet/PC/router, revocar).

## 4. Qué NO cambia

Esquema (v6), `grants.sql`, cadena `upgrade-v*.sql`, copias y restauración. Contrato de login y sesión (30 min / 14 h). La contraseña nunca va a disco y el token de sesión vive en memoria. Sin credenciales por defecto ni claves compartidas; TLS sin atajos. Protocolo de emparejamiento: código de un uso y 5 min, aprobación explícita de una persona principal, secreto entregado una vez y solo hashes en reposo. La PWA sigue autenticándose solo como dispositivo, sin dinero (ADR-007, `money-guard.ts`), mismo origen y CSP estricta. La orden incierta (D3.3/D6.3/D6.6). El módulo Dining y la regla de estaciones.

## 5. Decisiones y alternativas descartadas

1. **PC como puesto `service` + responsable para lo sensible.** Descartados: guardar la contraseña o la sesión cifradas (lo prohíben `dotnet/AGENTS.md:25` y ADR-007: los tokens de persona caducan); el PC como dispositivo «Principal» (nadie identificado al cobrar; puede aprobar más «Principales»; decisión abierta en `D6.6-hardening.md:58`); una sesión larga tipo «recordarme» (equivale a una contraseña en disco); confiar en todo lo que llega por loopback (cualquier proceso o usuario de Windows tendría acceso y no habría nada que revocar); autenticación integrada de Windows (esquema nuevo en el servidor, casa mal con una LAN sin dominio).
2. **Autorizar el PC con los cuatro endpoints existentes y una sola sesión.** Descartados: hacerlo desde el instalador o `first-user` (el DPAPI del usuario elevado puede no ser el del uso diario y el instalador solo copia y llama al motor) y un endpoint nuevo «enrolar este PC» (más superficie sin necesidad).
3. **La intención de cada botón vive en el cliente, ligada a su `pairingId`.** Descartado: columnas nuevas en `core.pairings` (esquema v7) y aprobar automáticamente al reclamar (una foto del QR daría acceso durante 5 min).
4. **Coherencia rol ↔ estación y veto a `main` en el servidor.** Descartado: filtrar solo en la pantalla (la interfaz no autoriza).
5. **Presencia desde las conexiones vivas** (`online`). Descartado: `last_seen_at` actualizado en cada petición (escrituras en el camino caliente y esquema).
6. **Renombrar un dispositivo se aplaza**: el nombre es el actor de auditoría e idempotencia (`Program.cs:311`, `PostgresStore.cs:218-219,341`); renombrar con una orden pendiente podría ejecutarla dos veces. Necesita una etiqueta aparte (E5).
7. **Sesión caducada → volver al nivel Puesto.** Descartados: mantener viva la sesión con peticiones periódicas (anula el límite de inactividad) y volver a entrar sin preguntar (exige guardar la contraseña).
8. **Inicio es una vista más con la barra de menús intacta** (estructura clásica de Windows).
9. **Acceso directo en Inicio de Windows desde el instalador.** Descartados: el inicio de sesión automático de Windows (ajuste de seguridad del equipo, no nuestro) y lanzar la interfaz desde el servicio.

## 6. Diferimientos conscientes

- **E5 (usuarios):** PIN corto o cambio rápido de persona en un PC de confianza (antes hay que aislar la orden pendiente **por actor**: hoy se aísla por rol, `DpapiPendingStore.cs:11,43`); gestión de usuarios y cambio de contraseña en pantalla; auditoría de login, emparejamiento y revocación en `core.audit` (F11); actor de dispositivo por id o etiqueta aparte, renombrar y nombres únicos (F12); cortar el tiempo real cuando la sesión caduca sin logout.
- **Hito 7 (caja y TPV táctil):** modo quiosco, pantalla de cocina siempre encendida (Wake Lock), reapertura automática tras reiniciar la tablet, atribución del camarero en el comandero (exige ADR: hoy la PWA solo se identifica como dispositivo).
- **Corte propio de certificados:** instalación guiada de la CA en las tablets y renovación automática del certificado (toca la regla de D5.3: HTTP solo por loopback).
- **Corte correctivo propio:** «generación de recuperación» tras restaurar una copia antigua (`D6.6-hardening.md:54`).
- **Más adelante:** pantallas de cocina de varias estaciones (estación única y obligatoria en `schema.sql:178`), `last_seen` persistido, límite de dispositivos (licencias).

## 7. Riesgos

- **iPad/iPhone:** Safari y el icono guardan datos por separado e iOS puede purgar una web no instalada; `persist()` no está garantizado. Mitigación: emparejar desde el icono instalado, el aviso de la PWA y la marca `online` en la lista.
- **Cambio de dirección del servidor:** la credencial de cada tablet va ligada al origen exacto; cambiar nombre, IP o puerto obliga a reconectarlas todas. El PC avisará antes de cambiarla.
- **Certificado:** si caduca, todo deja de conectar; el corte A solo avisa.
- **PC en modo puesto:** cualquiera delante toma comandas como `device:<PC>`, igual que con una tablet; caja, cierre y configuración siguen protegidos.
- **DPAPI por usuario de Windows:** otro usuario de Windows o una reinstalación obliga a reconectar el PC (un clic como responsable).
- **Tope de 14 h:** en jornadas largas corta al responsable; el puesto sigue operativo.
- **Reglas nuevas del servidor (A1, A2):** pueden rechazar una combinación que alguien usara; los dispositivos existentes no se tocan.
- **Presencia:** puede dar falsos «sin conexión» mientras el tiempo real está degradado o justo tras reiniciar el motor.

## 8. Decisiones pendientes del promotor

1. ¿Se hacen los cortes A y B **ahora, antes de E8 reservas**? (Recomendación: sí; es lo que se usa cada día y lo que falta para la prueba en el local.)
2. Sin contraseña, el PC toma comandas pero no cobra, ni cierra cuentas, ni configura, ni conecta dispositivos: eso pide la contraseña del responsable hasta que E5 traiga el PIN. ¿Vale así?
3. Durante el servicio, ¿el PC principal hace de **sala** (comandero grande) o de **pase de cocina** (valida pases y revisa alergias)?
4. ¿Comanderos y pantallas de cocina serán **Android o iPad**? El iPad obliga a instalar desde el icono y tiene más riesgo de perder la conexión.

Con las respuestas, este borrador pasa a plan aprobado (se anotan aquí las decisiones) y el corte A arranca desde `develop`.
