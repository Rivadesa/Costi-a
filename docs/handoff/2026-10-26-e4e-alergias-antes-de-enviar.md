# Traspaso 2026-10-26: E4e (alergias antes de enviar, órdenes de grupo y quién abrió la mesa); siguiente, C3d

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-22-c3c-dos-apps.md`. El anterior sigue valiendo para C3c, sus doce decisiones, su guion físico y las trampas de las dos aplicaciones.

Leer antes, en este orden:
1. este documento;
2. `docs/native/E4e-alergias-antes-de-enviar.md`: el aviso **sin vuelta atrás** antes de instalar, la regla nueva en palabras llanas, lo que cambia en el PC, en el pase y en Sala, las guardas contra bajar de versión, las once decisiones para revisar, las cifras y las pruebas en rojo, las capturas en `docs/native/images/e4e/`, el guion manual de laboratorio y lo que va a C3d y C3e;
3. `docs/ADR/ADR-016-revision-de-alergias-antes-de-enviar.md` (sustituye a D3.5 en ese punto y cambia D4.3a solo para el pase);
4. `docs/plans/2026-10-26-e4e-alergias-antes-de-enviar.md` (los pasos de E4e, con sus concreciones);
5. `docs/plans/2026-10-22-sala-y-cocina.md` (APROBADO el 22-10): la sección 4.3 es C3d y la 4.4 C3e. Leer también las secciones 5 a 10 y la «Revisión»;
6. `dotnet/AGENTS.md`, sobre todo E4e, C3c, D6.4 (la regla que C3d cambia), D3.5 y D4.1–D4.3b con sus notas de E4e, y `docs/STATUS.md`.

## Estado

- **`develop` = `9e6befe`** (Merge PR #92: el traspaso del 22-10 al día). Versión **`0.43.0-c3c`**, esquema v8.
- **Rama `feat/e4e-alergias-antes-de-enviar`**, sacada de `9e6befe`, con E4e completo en local. Versión **`0.44.0-e4e`**, esquema v8. Commits:
  - `7676bc0` docs(plans): el plan de E4e, aprobado;
  - `da5bb0d` docs(adr): ADR-016;
  - `4af115e` feat(dining): la revisión antes de enviar, abrir con alergias y `fire-next` desde «Sentados»;
  - `6f9de6d` feat(dining): las órdenes de grupo, el pase que marca por cualquier estación y quién abrió la mesa;
  - `6b1efac` feat(packaging): nunca bajar de versión, guardas del instalador y de `restore`;
  - `538749b` feat(desktop): abrir con alergias y revisar por plato agrupado en el Comedor;
  - `ab8111e` feat(pwa): el pase revisa antes de enviar y Sala espera la revisión de cocina;
  - `4eeefb3` docs(native): el documento, AGENTS, STATUS, los planes entregados y la versión `0.44.0-e4e`;
  - el commit de este traspaso;
  - la revisión antes de subir, `fix(e4e)`: la excepción del pase mira el rol (`Affordances.PassMarksAll`; `station_http` test_09), la fila del pase en el PC cuenta platos agrupados, con restricciones distintas ninguna línea sale marcada (`DishGroups.SameRestrictions`) y las notas del pase toman su `id` de su posición.
- **Permiso de subida y mezcla: dado el 26-10** («sube y mezcla»), para cuando la CI esté en verde. Este traspaso se escribe antes de subir: mira `git log origin/develop` para saber si ya está mezclada, con qué PR y qué dijo la CI.
- **Las respuestas del promotor** que fija E4e:
  1. cocina decide cada plato de un comensal con alergia o intolerancia **antes** de que salga el pase, también las intolerancias leves y las preferencias (15-10, confirmado el 22-10);
  2. el pase puede marcar «Listo» por una estación con la tablet estropeada (22-10, pregunta 2).
- **Qué trajo E4e:**
  - **La regla nueva** (ADR-016): un plato afectado se marca «por revisar» también en los pases sin enviar y con la mesa «Sentados»; lo que nace para un comensal con restricción nace por revisar (abrir, `choose`, `add-dish`); solo espera el pase que va a salir; omitir un pase o cancelar la mesa retira lo pendiente; «Rehacer», solo para lo enviado.
  - **Motor:** `open` con `restrictions` en la misma orden; `fire-next` desde «Sentados» (empieza y envía en una orden); órdenes de grupo `preparations-start|ready` (1 a 200) y `review-preparations` (1 a 60), todo o nada con una subida de versión; matriz de estación sin huecos; el pase marca por cualquier estación; `openedBy` y `openedStation` en el JSON de la instantánea.
  - **Nunca bajar de versión:** la marca `served-by:<versión>` en `core.commands`, que deja cada motor de una instalación al arrancar; unos programas más antiguos no arrancan ni hacen `backup`, `upgrade` ni altas; el manifiesto lleva `servedBy` y `restore` rechaza una copia de datos que sirvió un motor más nuevo, pero acepta la previa a actualizar. Solo desde `0.44.0`.
  - **PC:** «¿Alergias o intolerancias?» al abrir mesa; platos agrupados con la línea de cada comensal; revisión por plato agrupado con casillas en una orden; «Empezar plato» y «Marcar plato listo»; botón grande «Esperando la revisión de cocina».
  - **Costiña Cocina (pase):** «Por revisar antes de enviar» arriba, con «No le afecta», «Adaptado» y «No le afecta a nada más» (adelantado de C3d). Hasta C3d, el pase ve «Empezar» y «Marcar lista» en lo que falta de todas las estaciones.
  - **Costiña Sala:** «Por revisar en cocina» y «Adaptado: …» desde que se sientan; «Enviar siguiente pase» en grande desde «Sentados», con «Iniciar servicio» en «Más opciones».
- **Vuelta atrás: no hay.** Una mesa guardada por `0.44.0-e4e` no la lee `0.43.0-c3c`. Volver es restaurar la copia previa a la actualización, en un equipo limpio y con el instalador anterior. Sigue valiendo lo de C3b (cuentas) y lo de C3c (dispositivos).
- **Cifras en la punta** (en este PC, el 26-10, tras el cambio de versión):
  - vitest 401, en 19 ficheros; `npm run build` en verde;
  - ClientChecks 277;
  - dominio 97;
  - PersistenceChecks 88 (las de instantáneas de la CI, también aquí);
  - las 14 suites HTTP (packaging 15 con 1 saltada, restrictions 9, station 11, affordances 8, offers 8, pwa_http 8, pairing 18, checkout 8) y Realtime 13;
  - Playwright 30 (15 de escritorio y 15 de móvil);
  - DesktopChecks en verde.
- **Lo que solo verá la CI:** `installer_checks.ps1` con el instalador real (la actualización encima de una marca más nueva se para sin copia y con el servicio parado; al quitarla, se completa). Nunca se ha ejecutado. El workflow «Native Windows installer» salta en la PR porque cambian `Backup.cs`, `ServerSetup.cs`, `EngineVersions.cs`, `deploy/windows` y `tests/service`.

## Novedad del entorno y trampas

- **Capturas de E4e:** las de las aplicaciones se rehacen con `E4E_CAPTURES` (`e2e/captures-e4e.spec.ts`), sobre una base nueva y con el motor compilado con el build actual de la PWA; el comando está en el documento del corte. Las del PC salen de DesktopChecks en `dotnet/artifacts/desktop/` (`comedor-abrir-alergias.png`, `comedor-linea-adaptada.png`) y se copian a mano.
- **PersistenceChecks corre también en este PC**, sin base: `dotnet run --project tests/Costina.PersistenceChecks -c Release` en `dotnet/`. `local-tests.sh` no lo incluye.
- **`offers_http.test_07` crea su propia mesa** (`E4E-<hora>`) y la desactiva al terminar: cuando llega, todas las mesas del laboratorio están ocupadas. `test_05` cuenta todos los `course.chosen` de su ámbito: una prueba nueva que elija platos en otra mesa lo rompe.
- **Con la mesa «Sentados» el servidor anuncia `fire-next` y `start`.** `commands.spec` pulsa «Enviar siguiente pase» directamente; `banner.spec` revisa el pase sin enviar antes de validar. Una E2E nueva que abra una mesa con alergias tiene que revisar antes de enviar (`support.ts`, `reviewCourse`).
- **`kitchen.spec`** tiene un límite de 300 s (tarda unos 10 por proyecto) y fija que el pase ve «Marcar lista» en lo que falta de otra estación: C3d lo cambia.
- **La marca de versión solo la deja una instalación**, nunca el laboratorio. La base de la batería sí la tiene, porque `packaging_http` arranca el motor en modo instalación; `local-tests.sh` la recrea siempre, así que cambiar de rama no bloquea nada si se corre la batería entera.
- **DesktopChecks falló una vez** en este corte, en la comprobación de que la ficha de precios del catálogo no recorta texto (`Fits("catalog-price")`, `Program.cs:1916`), y pasó en las dos repeticiones siguientes sin tocar nada. Parece inestable. `local-tests.sh desktop` solo enseña las dos últimas líneas: para ver el mensaje entero, `dotnet run --project tests/Costina.DesktopChecks -c Release` en `dotnet/`.
- **Las ADR viven en dos carpetas** que en Windows son la misma: ADR-014, ADR-015, ADR-016 y una de las dos ADR-007 en `docs/ADR/`; las demás en `docs/adr/`. En Linux son distintas: al enlazar una ADR, escribe la ruta con la carpeta en la que está.
- **Pruebas en rojo:** para devolver un fichero tras romperlo a propósito, usar una copia en el `scratchpad`, nunca `git checkout`.
- **La batería completa con navegador** tarda unos 6 o 7 minutos; `pwa_http`, con Playwright, unos 175 segundos. Hay que esperarla en primer plano, y solo una a la vez. Los registros quedan en `.lab-test/` (los de este corte, `e4e-*.log`). El job `postgres-http` de la CI tiene 20 minutos de límite y tardaba unos 17: no añadas E2E largas.
- **Las mesas de las pruebas** no cambian: M1 la usa `drinks.spec.ts`, M2 `pwa_http.py`, M3–M5 el proyecto móvil y M6–M8 el de escritorio.

## Para el promotor

1. **Antes de poner `0.44.0-e4e` en su instalación** (lo explica el documento del corte, «Antes de instalar»):
   - **no tiene vuelta atrás** instalando encima: volver es restaurar la copia previa en un equipo limpio;
   - **actualizar con el comedor cerrado**, sin mesas abiertas;
   - **nunca** instalar después un instalador anterior a `0.44.0`;
   - desde `0.44.0`, el instalador no deja bajar de versión.
2. **Guion manual de E4e**, solo en un PC de laboratorio, con una tablet en el pase, otra en Cocina caliente y un móvil con Costiña Sala: 10 pasos, con su tabla, en el documento del corte.
3. **Guion físico de C3c**, pendiente, con un Android, un iPhone y un iPad: 15 pasos en `docs/native/C3c-dos-apps.md`. Se puede hacer ya con `0.44.0-e4e` en el laboratorio. Solo cambia el paso 11: tras abrir la mesa, basta «Enviar siguiente pase», que ya empieza el servicio («Iniciar servicio» está en «Más opciones»). Lo más importante sigue siendo el paso 7: si el icono del iPhone abierto por http se abre a pantalla completa.
4. **Decisiones pequeñas para revisar:**
   - las **once de E4e**, en «Decisiones tomadas al construir» del documento del corte. Por ejemplo: el botón grande «Enviar siguiente pase» desde «Sentados»; la revisión del PC con casillas; la ficha que cuenta platos; «No le afecta a nada más» ya en el pase;
   - las **doce de C3c**, en «Decisiones y diferencias con la maqueta» de su documento;
   - siguen pendientes las 24 de C3b y las 5 de C3a.
5. **Varios tamaños de copa por vino** («Copa 15 cl»): la pregunta está en el documento de C3b.
6. **Pendiente de antes:**
   - el IVA que diga su gestor;
   - el dominio para el certificado público: además de quitar la conexión sin cifrar, es lo que permitirá avisar con la aplicación cerrada;
   - la exportación de Verial;
   - las claves de WooCommerce;
   - los guiones físicos de C3a y de C3b;
   - el LICENSE;
   - las 4 fotos;
   - la hora de apertura de la mesa.

## Siguiente trabajo, en orden

1. **Subir y mezclar E4e** con el permiso del 26-10, cuando la CI esté en verde: PR desde `feat/e4e-alergias-antes-de-enviar` a `develop`, leer la CI en los logs (también el workflow del instalador, que estrena `installer_checks.ps1`), merge «Merge PR #N: …» en local y push. Después, el instalador de `develop` (`Costina-Setup-0.44.0-e4e.exe`).
2. **C3d: Costiña Cocina con comandas por mesa** (`0.45.0-c3d`, sin cambio de esquema ni de motor; sección 4.3 del plan del 22-10, ya aprobado).
   - Una comanda por mesa y pase, con solo el trabajo de esa estación: cambia la regla de D6.4 («una estación ve el pase entero») y lleva ADR.
   - En las estaciones solo «Listo» (respuesta 1), en una orden `preparations-ready`; ámbar a los 15 y a los 25 minutos, nunca rojo (respuesta 3); el sonido tras «Empezar turno» (respuesta 4).
   - El pase: «Por revisar» con atajos de nota, la mesa entera con el progreso de cada estación, «Esperando al camarero» y «Marcar <estación> como lista» en «Más opciones» (respuesta 2), que sustituye a los botones de hoy en todo lo que falta.
   - `pwa/cocina.ts` sustituye a `kitchen.ts`, con la tabla común `kitchen-cases.json` (vitest y ClientChecks) y el proyecto de Playwright `tableta`.
   - **Antes de empezar:** el plan dice que la maqueta (`docs/plans/maquetas/2026-10-22-sala-y-cocina.html`) se aprueba antes de C3d. Confírmalo con el promotor, propón los pasos del corte y **pide permiso para subir y mezclar C3d**.
3. **Después, en este orden:**
   - C3e, Costiña Sala con «Mis mesas» (usa `openedBy` y `openedStation`), abrir con alergias desde Sala, «Carta y menús» y el «Preguntar al servidor» de una orden de otra instalación;
   - E4f, E4c (esquema v9) y E4g.

   Cada uno, con su permiso de subida y mezcla.

## En el PC del promotor

- **Instalación real**, leída el 19-10: versión 0.37.1-c2.1. Servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089. **No se toca.** El instalador de `develop` la pone al día; la guarda contra bajar de versión empieza a valer cuando tenga `0.44.0-e4e`.
- **Pruebas en este PC:** la batería corre con `.lab/pgsql/bin`, en el puerto 5090, sin administrador y una sola a la vez. Playwright corre en local con Chromium en `%USERPROFILE%\.ms-playwright`. Nunca se ejecuta aquí el instalador real.

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado. Lee primero, en este orden: docs/handoff/2026-10-26-e4e-alergias-antes-de-enviar.md (punto de entrada), docs/native/E4e-alergias-antes-de-enviar.md, docs/ADR/ADR-016-revision-de-alergias-antes-de-enviar.md, docs/plans/2026-10-22-sala-y-cocina.md (APROBADO el 22-10; 4.3 es C3d y 4.4 C3e, con sus secciones 5 a 10 y la Revisión) y dotnet/AGENTS.md (sobre todo E4e, C3c, D6.4, D3.5 y D4.1–D4.3b).
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ o frontend/ corre `npm run build` y `COSTINA_E2E=1 sh tools/agent/local-tests.sh` entero, y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina: puerto 5090, nunca como administrador, sin tocar sus servicios, puertos 5088/5089 ni carpetas, y nunca su instalador real); una sola batería a la vez; los tests de CI se LEEN en los logs; GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; habrá Android y también iPhone y iPad: los dos caminos tienen que funcionar; seguridad alimentaria primero: ningún plato afectado por una alergia llega al comensal sin una decisión de cocina registrada; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Háblame siempre en castellano.
Estado: E4e (alergias antes de enviar, órdenes de grupo, el pase que marca por una estación, quién abrió la mesa y las guardas contra bajar de versión; 0.44.0-e4e, esquema v8, SIN vuelta atrás) hecha en la rama feat/e4e-alergias-antes-de-enviar desde develop 9e6befe, con mi permiso «sube y mezcla» para subirla y mezclarla con la CI en verde; mira en git si ya está mezclada. Siguiente: C3d (Costiña Cocina con comandas por mesa, sección 4.3 del plan del 22-10): confírmame antes la maqueta, propón los pasos y pídeme permiso para subir y mezclar. Tengo pendiente el guion manual de E4e en un laboratorio, el guion físico de C3c con un Android, un iPhone y un iPad, las once decisiones de E4e y las doce de C3c, y los varios tamaños de copa («Copa 15 cl») de C3b.
```
