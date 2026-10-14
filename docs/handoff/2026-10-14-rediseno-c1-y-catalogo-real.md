# Traspaso 2026-10-14: cortes A, B, B.1, C1, E3.1 y E3.2 mezclados; siguiente C2

Para seguir desde cualquier equipo. Sustituye como punto de entrada al traspaso `2026-09-23-hito6-e4.md`, que queda como histórico y aún dice «faltan las cuatro decisiones».

Leer antes, en este orden:
1. este documento;
2. `docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md` (C1 hecho; **C2 es lo siguiente**, con lo que C1 y E3.2 le dejaron);
3. `dotnet/AGENTS.md` (párrafos A, B, B.1, C1, E3.1 y E3.2);
4. `docs/STATUS.md`;
5. los documentos de corte `docs/native/A-…`, `B-…`, `B.1-…`, `C1-nuevo-aspecto.md`, `E3.1-…` y `E3.2-imagenes-y-tarifa-por-sala.md`.

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
4. **Orden acordado**: E3.2 (hecho) → **C2** (plano de mesas e Inicio con cifras) → **C3** (PWA) → E8 reservas → E6 stock y compras → E7 web → E5 usuarios → Hito 7 caja y TPV táctil → Hito 8 fiscal → informes.
5. **Permisos.** Para C1 y E3.2 autorizó de antemano subir, abrir la PR y mezclar en cuanto la CI estuviera verde («permiso concedido», «Adelante»), y después pasarle el instalador. E3.1 también («adelante!»). Es un permiso **por corte**: para C2 hay que volver a pedirlo.

## Estado verificable

- **Rama**: `develop` = `21cc08b`, versión **`0.35.0-e3.2`**, esquema **v7**. Sin PR abiertas antes de esta de traspaso.
- **PR mezcladas**, con la CI leída en todas: dominio, D1 con 14 suites y Playwright, escritorio, servicio e instalador 7/7:

| PR | Corte | Versión | Qué trajo | Instalador |
|---|---|---|---|---|
| #68 | **A** | `0.32.0-a1` | Inicio y dispositivos | — |
| #69 | **B** | `0.33.0-b1` | El PC arranca conectado (ADR-013) | — |
| #70 | **B.1** | `0.33.1-b1.1` | La copia previa a una actualización admite una base antigua. Fallaba al actualizar de 0.29 a 0.33 en el PC del promotor | — |
| #71 | **C1** | `0.34.0-c1` | Fluent claro, barra lateral, Primeros pasos, todo en castellano (`Labels`). Arreglos: arranque de un puesto adicional, checks aislados de la instalación real, SignalR.Client 10.0.13 | run 38542067597 |
| #72 | **E3.1** | `0.34.1-e3.1` | El importador reconoce la categoría por nombre y padre: con la bodega real se mezclaban 785 vinos | run 38546455062 |
| #73 | **E3.2** | `0.35.0-e3.2` | Fotos de los artículos en la base (esquema v7) e importación con fotos. Tarifa de cada sala en Configuración | run 38559656920 (artefacto `costina-windows-installer`) |

- **Cifras locales en la punta**: dominio 81, ClientChecks 110, 14 suites HTTP (`catalog_http` 8, `packaging_http` 12 hasta v7), Realtime 12, checks del WPF con capturas (`docs/native/images/c1/`, `e3.2/`), vitest 62.

## En el PC del promotor (no está en GitHub)

- **Instalación real**: servicios `Costina` y `CostinaPostgres`, puerto 5088, `C:\Program Files\Costina`. **No tocarla.**
  - Se actualizó a 0.33.1-b1.1 tras el fallo de B.1, y creó un responsable nuevo con `first-user`.
  - Se le pasaron los instaladores 0.34.0-c1 y **0.35.0-e3.2**; queda por confirmar que los instaló.
- **Importación pendiente, la hace él** en una consola de administrador:
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

1. Preguntar al promotor si instaló la 0.35.0-e3.2 e importó los vinos, y ver con él el catálogo.
2. **C2**, según la sección C2 del plan del nuevo aspecto (versión `0.36.0-c2`):
   - Inicio con cifras;
   - Comedor con plano de mesas y ficha;
   - `stations` en `/configuration`;
   - reordenar Caja, Catálogo, Oferta y Salas.
   - Pedir permiso de subida y merge **antes de empezar**, como en C1 y E3.2, y entregarle el instalador.
3. **C3**: la PWA con el mismo aspecto, el vocabulario (`labels.ts`) y las fotos (`GET /catalog/images/{id}`, con blob y la CSP revisada).
4. Después, el orden de arriba: E8 reservas…
5. **Pendiente del promotor**:
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
Lee primero, en este orden: docs/handoff/2026-10-14-rediseno-c1-y-catalogo-real.md (estado, decisiones, trampas y siguiente trabajo), docs/plans/2026-10-14-nuevo-aspecto-puesto-principal.md (C2 es lo siguiente), dotnet/AGENTS.md (párrafos A a E3.2), docs/STATUS.md. Los traspasos anteriores de docs/handoff/ siguen valiendo para el entorno.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ corre `sh tools/agent/local-tests.sh` entero (en mi PC en el puerto 5090: hay una instalación real en 5088 que NO se toca) y `sh tools/agent/local-tests.sh desktop`; una sola batería a la vez; los tests de CI se LEEN en los logs (nunca fiarse del verde); GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); ediciones con scripts + tools/agent/patch.py; cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Estado: develop = 21cc08b (A, B, B.1, C1, E3.1 y E3.2 mezcladas; esquema v7; versión 0.35.0-e3.2). Pregúntame si instalé la 0.35.0-e3.2 e importé los vinos. Después, C2 (Inicio con cifras y plano de mesas) según el plan del nuevo aspecto; luego C3 (PWA) y E8 reservas.
```
