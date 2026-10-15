# Traspaso 2026-10-14, al día el 15-10: cortes A, B, B.1, C1, E3.1, E3.2, B.2 y C2 mezclados; siguiente C2b y C3

Para seguir desde cualquier equipo. Sustituye como punto de entrada al traspaso `2026-09-23-hito6-e4.md`, que queda como histórico y aún dice «faltan las cuatro decisiones».

Leer antes, en este orden:
1. este documento;
2. `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md` (C1 y C2 hechos; **C2b y C3 son lo siguiente**);
3. `dotnet/AGENTS.md` (párrafos A, B, B.1, C1, E3.1, E3.2, B.2 y C2);
4. `docs/STATUS.md`;
5. los documentos de corte `docs/native/A-…`, `B-…`, `B.1-…`, `C1-nuevo-aspecto.md`, `E3.1-…`, `E3.2-imagenes-y-tarifa-por-sala.md`, `B.2-conexion-sencilla.md` (con `docs/ADR/ADR-014-conexion-sencilla-lan.md`) y `C2-inicio-y-plano-de-mesas.md`.

Los traspasos anteriores de `docs/handoff/` siguen valiendo para E1–E4 y las trampas del entorno.

## Decisiones del promotor (desde el 24-09)

1. **06-10, plan «Inicio y dispositivos»** (`docs/plans/2026-09-24-inicio-y-dispositivos.md`, sección 8):
   - cortes A y B antes de E8;
   - sin contraseña, el PC solo toma comandas;
   - «el PC principal actúa como ERP: acceso a todo» (nada se oculta; lo sensible pide contraseña);
   - la PWA, en Android y en iPad.
2. **14-10, aspecto**: «El entorno es feísimo». De la propuesta del lienzo privado https://claude.ai/artifact/2nAGKqQSYsvpdMvXi2eCQZ eligió **tema claro, barra lateral y menú** y dijo «Adelante con el rediseño» antes de E8.
   - La barra de menús clásica se mantiene: la pidió el 22-09.
   - El color de acento no lo eligió: se usó el verde `#0E7A5F` del lienzo.
3. **14-10, catálogo real**:
   - Importar el catálogo sincronizado con su WordPress (costinagourmet.com).
   - **Los precios de la web son los de sala** y van a la tarifa General.
   - «Para los artículos todos debemos poder definir más de un precio, uno para sala, otro para tienda online y otras opciones», pero «de momento deja la opción de poder añadir más tarifas, **no las cubras**».
   - Pidió también **las imágenes**.
4. **Orden acordado**: E3.2 (hecho) → **C2** (hecho) → **C2b** (reordenar Caja, Catálogo, Oferta, Salas y Dispositivos) → **C3** (PWA) → E8 reservas → E6 stock y compras → E7 web → E5 usuarios → Hito 7 caja y TPV táctil → Hito 8 fiscal → informes.
5. **Permisos.** Para C1 y E3.2 autorizó de antemano subir, abrir la PR y mezclar en cuanto la CI estuviera verde («permiso concedido», «Adelante»), y después pasarle el instalador. E3.1 también («adelante!») y C2 («seguimos», 15-10). Es un permiso **por corte**: para C2b hay que volver a pedirlo.
6. **15-10, conexión sencilla (B.2, hecho en otro equipo)**: HTTP por la red del local hasta tener un certificado público. Queda por decidir el **dominio propio** para ese certificado (ADR-014).
7. **15-10, idioma**: con el promotor, todo en castellano.

## Estado verificable

- **Rama**: `develop` = la mezcla de la PR de C2, versión **`0.37.0-c2`**, esquema **v8**.
- **PR mezcladas**, con la CI leída en todas: dominio, D1 con 14 suites y Playwright, escritorio, servicio e instalador 7/7:

| PR | Corte | Versión | Qué trajo | Instalador |
|---|---|---|---|---|
| #68 | **A** | `0.32.0-a1` | Inicio y dispositivos | — |
| #69 | **B** | `0.33.0-b1` | El PC arranca conectado (ADR-013) | — |
| #70 | **B.1** | `0.33.1-b1.1` | La copia previa a una actualización admite una base antigua. Fallaba al actualizar de 0.29 a 0.33 en el PC del promotor | — |
| #71 | **C1** | `0.34.0-c1` | Fluent claro, barra lateral, Primeros pasos, todo en castellano (`Labels`). Arreglos: arranque de un puesto adicional, checks aislados de la instalación real, SignalR.Client 10.0.13 | run 38542067597 |
| #72 | **E3.1** | `0.34.1-e3.1` | El importador reconoce la categoría por nombre y padre: con la bodega real se mezclaban 785 vinos | run 38546455062 |
| #73 | **E3.2** | `0.35.0-e3.2` | Fotos de los artículos en la base (esquema v7) e importación con fotos. Tarifa de cada sala en Configuración | run 38559656920 (artefacto `costina-windows-installer`) |
| #75 | **B.2** | `0.36.0-b2` | Conexión sencilla por HTTP en la red del local, apagada por defecto (esquema v8, ADR-014). Hecho en otro equipo | run 38611824729 |
| siguiente | **C2** | `0.37.0-c2` | Inicio con cifras y mini plano; Comedor con plano de mesas y ficha; `stations` en `/configuration`; consola sin permisos que pide abrir PowerShell como administrador | el de la mezcla |

- **Cifras locales en la punta (C2)**: dominio 81, ClientChecks 123, 14 suites HTTP (`packaging_http` 13, `pairing_http` 14, `catalog_http` 8), Realtime 12, checks del WPF con capturas (`docs/native/images/c1/`, `e3.2/`, `c2/`), vitest 63.

## En el PC del promotor (no está en GitHub)

- **Instalación real**: servicios `Costina` y `CostinaPostgres`, puerto 5088, `C:\Program Files\Costina`. **No tocarla.**
  - Se actualizó a 0.33.1-b1.1 tras el fallo de B.1, y creó un responsable nuevo con `first-user`.
  - Se le pasaron los instaladores 0.34.0-c1 y **0.35.0-e3.2**; queda por confirmar que los instaló.
- **Importación de los vinos: hecha por él el 15-10.** La primera vez la lanzó sin administrador y el motor respondió «Set COSTINA_MODE…» (arreglado en C2). Para repetirla, en una consola de administrador:
  ```powershell
  & "C:\Program Files\Costina\server\Costina.Server.exe" import-catalog "$env:USERPROFILE\Documents\costina-vinos-costinagourmet.csv"
  ```
  Debe responder 1.037 filas e `images set 1017`.
  - El CSV y la carpeta `costina-vinos-costinagourmet-imagenes` (1.017 fotos, 25 MiB) están en `Documentos` de ese PC.
  - **En otro equipo se regeneran**, porque son datos públicos de la tienda:
    ```powershell
    python tools\catalog\woocommerce-to-csv.py https://costinagourmet.com --category 881 --images --out costina-vinos-costinagourmet.csv
    ```
- **Fotos inválidas en la tienda**: 4509, 4510 y 4512 son AVIF con nombre `.jpg`; 543 es un BMP. Las tiene que volver a subir en JPEG. Hay 16 vinos sin foto.

## Siguiente trabajo, en orden

1. Preguntar al promotor si instaló la 0.37.0-c2 y ver con él Inicio y el Comedor (guion en `docs/native/C2-inicio-y-plano-de-mesas.md`).
2. **C2b**: Caja, Catálogo, Oferta, Salas y Dispositivos reordenados para el ancho que deja la barra lateral (ficha a la derecha y listas con buscador), con capturas. Pedir permiso de subida y merge **antes de empezar** y entregarle el instalador.
3. **C3**: la PWA con el mismo aspecto, el vocabulario (`labels.ts`, que debe aceptar el tipo de estación en minúscula de `/configuration`) y las fotos (`GET /catalog/images/{id}`, con blob y la CSP revisada).
4. Después, el orden de arriba: E8 reservas…
5. **Decisión pendiente de C2**: «sentados hace N minutos» necesita guardar la hora de apertura de la mesa en la vista del motor (cambio pequeño y aditivo). Hoy las tarjetas cuentan los minutos del estado que se ve.
6. **Pendiente del promotor**:
   - dominio propio para el certificado público (ADR-014);
   - exportación de Verial con sus tarifas, para el formato del importador;
   - claves REST de WooCommerce (E7);
   - guiones físicos (#26, #27, #28: no cerrar; #25 no tocar);
   - titular del LICENSE;
   - volver a subir las 4 fotos.

## Trampas nuevas

- **Tema Fluent de WPF** (C1, `AGENTS.md`):
  - `Fluent.Light.xaml` combinado en App.xaml, nunca `ThemeMode` en C# (WPF0001);
  - acento por los 7 `SystemColors.AccentColor*Key`;
  - sin Mica (interruptor en el constructor estático de `App`);
  - todos los estilos `BasedOn`;
  - sin estilo global de `TextBlock`;
  - la letra es `Segoe UI Variable Text, Segoe UI` (el nombre a secas no se resuelve);
  - iconos como geometrías;
  - un botón `GoToPage` dentro de una vista necesita que la vista responda `CanExecute`.
- **Checks del WPF aislados**: `DesktopSettings.DataFolder` apunta a una carpeta temporal y el servidor de los checks es `127.0.0.1:59997`. Ya no tocan `%LOCALAPPDATA%\Costina` ni el puerto 5088.
- **Una sola batería a la vez.** Dos ejecuciones de `local-tests.sh`, o pruebas a mano contra `costina_d1_test` (puerto 5434) mientras corre la batería, se pisan la base y dan fallos falsos (precio del agua 575 en vez de 400).
- **Nunca correr la batería desde una consola de administrador en un PC con instalación real.** Hasta C2.1 las suites arrancaban sin `COSTINA_DATA` y leían `C:\ProgramData\Costina\config`; sin administrador se les negaba y lo ignoraban (C2), pero con administrador habrían mezclado el `server.json` real. Desde C2.1 (`0.37.1-c2.1`) el motor exige al laboratorio una carpeta propia y cada suite usa una temporal; la regla se mantiene por prudencia. Todo lo que arranque el motor en laboratorio debe fijar su propio `COSTINA_DATA`.
- **Puerto 5089**: en un PC con la instalación real es suyo (B.2); los servidores de prueba en modo instalación ponen `COSTINA_LAN_HTTP_PORT=0`.
- **Las baterías lanzadas por un agente de flujo mueren con él**: comprobar la línea `exit` del registro y, si falta, repetirla.
- **Aviso de seguridad de NuGet = error** (NU1903). Si una restauración nueva falla por un paquete con aviso, se actualiza ese paquete en un commit propio.
- **Store API de WooCommerce**:
  - devuelve entidades HTML en los nombres (`&#8217;`): la exportación ya las decodifica;
  - la misma región cuelga de varios tipos: el importador ya lo distingue (E3.1);
  - hay fotos en formatos raros, que se comprueban por sus bytes.
- **Consola de Python en Windows**: los acentos salen como `�` si no se pone `PYTHONIOENCODING=utf-8`, pero los datos están bien.
- **Atribución de commits**: la línea `Co-Authored-By` que indique la sesión (aquí: `Claude Opus 5.5 <noreply@anthropic.com>`; los cortes A y B llevan Fable 5.1).

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Lee primero, en este orden: docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md (estado, decisiones, trampas y siguiente trabajo), docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md (C2b y C3 son lo siguiente), dotnet/AGENTS.md (párrafos A a C2), docs/STATUS.md. Los traspasos anteriores de docs/handoff/ siguen valiendo para el entorno.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ corre `sh tools/agent/local-tests.sh` entero (en mi PC en el puerto 5090: hay una instalación real en 5088 que NO se toca) y `sh tools/agent/local-tests.sh desktop`; una sola batería a la vez; los tests de CI se LEEN en los logs (nunca fiarse del verde); GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); ediciones con scripts + tools/agent/patch.py; cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Háblame siempre en castellano. Nunca corras la batería desde una consola de administrador en mi PC.
Estado: develop con A, B, B.1, C1, E3.1, E3.2, B.2 y C2 mezcladas; esquema v8; versión 0.37.0-c2. Pregúntame si instalé la 0.37.0-c2 y repasa conmigo Inicio y el Comedor. Después, C2b (reordenar Caja, Catálogo, Oferta, Salas y Dispositivos) según el plan del nuevo aspecto; luego C3 (PWA) y E8 reservas.
```
