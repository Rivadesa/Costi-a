# Traspaso 2026-10-20: C3a mezclada (comandero nuevo); siguiente, C3b

> **Ya no es el punto de entrada:** desde el 21-10 lo es `docs/handoff/2026-10-21-c3b-bodega-y-bebidas.md`. Este documento sigue valiendo para C3a, sus cinco decisiones para revisar y su guion con móvil e iPad.

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-19-d6.7-y-e3.3.1.md`. El anterior sigue valiendo para lo diferido a C3b, para las trampas y para el PC del promotor.

Leer antes, en este orden:
1. este documento;
2. `docs/native/C3a-comandero-nuevo.md` (qué cambió, capturas en `docs/native/images/c3a/` y límites) y su plan `docs/plans/2026-10-20-c3a-comandero-nuevo.md`;
3. `docs/plans/2026-10-15-menus-carta-y-bodega.md` (APROBADO; la sección 4.4 es C3b) y su maqueta `docs/plans/maquetas/2026-10-15-comandero.html`;
4. `docs/handoff/2026-10-19-d6.7-y-e3.3.1.md`, por lo diferido a C3b;
5. `dotnet/AGENTS.md`, sobre todo los párrafos D6.7, E3.3.1 y C3a, y `docs/STATUS.md`.

## Estado

- **`develop`**: C3a mezclada (`0.41.0-c3a`, esquema v8). La PR de C3a, la CI leída y el enlace del instalador están en la descripción de la PR y en la conversación con el promotor.
- **Permiso**: el promotor aprobó C3a y su subida y merge el 20-10 («merge y sigue»). **Para C3b hay que volver a pedirlo.**
- **Qué trajo C3a en el comandero** (PWA, sin cambios en el motor):
  - el aspecto y el vocabulario del puesto;
  - las mesas como tarjetas por sala, con el mismo estado escrito que el Comedor;
  - la ficha de mesa con «Carta y menús» y «Bodega y bebidas», que todavía abren lo de antes con el aspecto nuevo;
  - un botón grande con la acción anunciada por el servidor;
  - `/configuration` releída al abrir mesa y cada 5 minutos;
  - la cocina con nombres de estación.
- **Pruebas compartidas**: `floor.ts` (PWA) y `FloorPlan.cs` (puesto) comparten sus casos en `dotnet/tests/fixtures/floorplan-cases.json`, y `labels.ts` y `Labels.cs` los suyos en `vocabulary-cases.json`.
- **Playwright** corre con dos proyectos (escritorio y móvil a 375 × 812), con selectores `data-testid`.
- **Cifras en la punta**:
  - vitest 198;
  - ClientChecks 201;
  - dominio 83;
  - las 14 suites HTTP y Realtime 12;
  - Playwright 12 (6 de escritorio y 6 de móvil).
- **Revisiones**: tres, independientes, con 15 hallazgos; todos se aplicaron.

## Novedad del entorno

- **Playwright corre en local** en el PC de desarrollo, con Chromium en `%USERPROFILE%\.ms-playwright`, instalado el 20-10 con `PLAYWRIGHT_BROWSERS_PATH=… npx playwright install chromium`.
  - La batería con navegador es `COSTINA_E2E=1 sh tools/agent/local-tests.sh`.
  - Antes, `npm run build`, porque el motor sirve la PWA desde su build.
- **Capturas de C3a**: se rehacen con el modo `C3A_CAPTURES` de `e2e/captures.spec.ts`. El comando está en la cabecera de ese fichero.
- **Mesas propias de cada historia E2E**: carril de escritorio M6, M7 y M8; carril móvil M3, M4 y M5; capturas, M1 y M3 a M8 (`e2e/support.ts`, `LANES`).

## Para el promotor (diferencias deliberadas, en el documento de C3a)

1. El botón dice «Enviar siguiente pase · <pase>»; en el PC dice «… a cocina».
2. Una oferta que ya no está vigente se omite.
3. La orden pendiente nombra la mesa.
4. «¿Para quién?» no tiene un valor por defecto.
5. Mesa cerrada en Caja: «Esta mesa ya está cerrada en Caja: avisa en el PC principal».
6. En el móvil, con una ficha abierta, se ocultan la marca y el nombre del dispositivo.

## Siguiente trabajo, en orden

1. **C3b, Bodega y bebidas** (`0.42.0-c3b`): en el comandero y «Añadir consumición» en la mesa del puesto, según la sección 4.4 del plan.
   - Incluye lo diferido de D6.7 y E3.3.1: la defensa del servidor (un identificador del cargo derivado de la clave, único por cuenta), el orden `Pending`/`Save` en `ApiClient.SendAsync`, la copa tecleada con otro código y los textos de `reserved_category`, `nothing_to_do` y `product_ids_required`.
   - Hay que proponer el plan del corte y **pedir permiso para subir y mezclar**.
2. Después: E4e, C3c (incluye la mesa abierta con una oferta que ya no está vigente: el motor debe servirla), E4f, E4c y E4g.
3. **Pendiente del promotor**:
   - el IVA que diga su gestor;
   - el dominio para el certificado;
   - la exportación de Verial;
   - las claves de WooCommerce;
   - los guiones físicos, incluido el de C3a con móvil e iPad;
   - el LICENSE;
   - las 4 fotos;
   - la hora de apertura de la mesa.

## En el PC del promotor

- **Instalación real**, leída el 19-10: versión 0.37.1-c2.1. Servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089. **No se toca.** El instalador de `develop` la pone al día.

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado. Lee primero, en este orden: docs/handoff/2026-10-20-c3a-comandero-nuevo.md (punto de entrada), docs/native/C3a-comandero-nuevo.md, docs/plans/2026-10-15-menus-carta-y-bodega.md (APROBADO; 4.4 para C3b) y su maqueta, docs/handoff/2026-10-19-d6.7-y-e3.3.1.md (lo diferido a C3b) y dotnet/AGENTS.md.
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ o frontend/ corre `npm run build` y `COSTINA_E2E=1 sh tools/agent/local-tests.sh` entero, y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina: puerto 5090, nunca como administrador, sin tocar sus servicios, puertos 5088/5089 ni carpetas); una sola batería a la vez; los tests de CI se LEEN en los logs; GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Háblame siempre en castellano.
Estado: develop con C3a mezclada (0.41.0-c3a, esquema v8). Siguiente: C3b (Bodega y bebidas en el comandero y «Añadir consumición» en la mesa del puesto, con lo diferido de D6.7 y E3.3.1), sección 4.4 del plan aprobado: propón el plan del corte en pocas líneas y pídeme permiso para subir y mezclar.
```
