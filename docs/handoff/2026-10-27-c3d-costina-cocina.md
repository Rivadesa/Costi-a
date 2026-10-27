# Traspaso 2026-10-27: C3d (Costiña Cocina, comandas por mesa); siguiente, C3e

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-26-e4e-alergias-antes-de-enviar.md`. El anterior sigue valiendo para E4e, sus once decisiones, su guion manual y sus trampas.

Leer antes, en este orden:
1. este documento;
2. `docs/native/C3d-costina-cocina.md`: lo que notarán cada estación y el pase, «Empezar turno», el sonido y la pantalla encendida, el cambio de regla frente a D6.4 y su razón, lo que no cambia, las cifras y las pruebas en rojo, las capturas en `docs/native/images/c3d/`, las dieciocho decisiones y las cinco diferencias con la maqueta para revisar, el guion manual de laboratorio y lo que va a C3e;
3. `docs/ADR/ADR-017-la-estacion-ve-solo-lo-suyo.md` (sustituye la regla de D6.4 «una estación ve el pase entero» y tres frases de sus límites);
4. `docs/plans/2026-10-27-c3d-costina-cocina.md` (los pasos de C3d, con sus concreciones);
5. `docs/plans/2026-10-22-sala-y-cocina.md` (APROBADO el 22-10): la sección 4.4 es C3e. Leer también las secciones 5 a 10 y la «Revisión»;
6. `dotnet/AGENTS.md`, sobre todo C3d, E4e, C3c, C3a (las tablas de casos comunes y los `data-testid`) y D6.4 con su nota, y `docs/STATUS.md`.

## Estado

- **`develop` = `d4f095b`** (Merge PR #94: el traspaso del 26-10 al día). Versión **`0.44.0-e4e`**, esquema v8.
- **Rama `feat/c3d-costina-cocina`**, sacada de `d4f095b`, con C3d completo en local. Versión **`0.45.0-c3d`**, esquema v8. Commits:
  - `0033019` docs(plans): el plan de C3d, aprobado;
  - `b9b90ff` feat(pwa): `cocina.ts`, la lógica de Costiña Cocina, con la tabla común de platos `kitchen-cases.json` y `DishGroups` alineado;
  - `3c1b4b0` feat(pwa): la pantalla nueva, cada estación con lo suyo y el pase con la mesa entera, «Empezar turno», el sonido y la pantalla encendida; se retira `kitchen.ts`;
  - `68d5b1e` test(pwa): el proyecto `tableta` de Playwright, `kitchen.spec` reescrito y las capturas;
  - `4422e6d` docs(native): el documento, ADR-017, AGENTS, STATUS, los planes entregados y la versión `0.45.0-c3d`;
  - el commit de este traspaso;
  - `fix(pwa): C3d revision - ...`, lo que pidieron las revisiones de seguridad, aspecto y pruebas: lo enviado por revisar en ámbar, la nota que sobrevive a un `version_conflict`, «Esperando al camarero» siempre a la vista en el pase, 64 px para los atajos, «Más opciones», «Actualizar» y la orden sin confirmar, y las capturas rehechas (la del pase entero pasa a ser la del pase desplazado).
- **Permiso de subida y mezcla: dado el 27-10** («si, adelante»), para cuando la CI esté en verde. Este traspaso se escribe antes de subir: mira `git log origin/develop` para saber si ya está mezclada, con qué PR y qué dijo la CI.
- **Las respuestas del promotor** que fija C3d (22-10):
  1. en las estaciones, solo «Listo», sin «Empezar»;
  2. el pase puede marcar el trabajo de una estación como listo, en «Más opciones» de cada comanda;
  3. el tiempo, neutro, ámbar desde 15 minutos y ámbar oscuro con letra blanca desde 25; nunca rojo y siempre escrito, «Lleva N min»;
  4. suena al llegar una comanda nueva, solo después de «Empezar turno».
- **Qué trajo C3d:**
  - **La regla nueva** (ADR-017): cada estación ve solo su trabajo, una comanda por mesa y pase, y nombra las demás estaciones en «También en: Cocina fría»; el pase ve la mesa entera.
  - **Estaciones:** platos agrupados («Merluza á galega × 4 (1 adaptada)»), las alergias escritas bajo su plato y nunca atenuadas, la de toda la mesa una vez arriba, un solo «Listo» por comanda en una orden `preparations-ready` (y uno por plato con dos platos o más), nada que escribir y la franja «Hecho · esperando al pase».
  - **Pase:** el progreso de cada estación, «Falta …», «Siguiente: Postre (sin enviar)», «Validar pase: listo para servir», «Más opciones» › «Marcar Cocina fría como lista», «Esperando al camarero · listo hace 3 min» y «Por revisar antes de enviar» con los atajos «No lo lleva», «Sin gluten» y «Cambiado por …».
  - **«Empezar turno»:** un tono sintetizado con Web Audio con cada comanda nueva (sin ficheros: la CSP no cambia); con https, la pantalla encendida (Wake Lock); por http, el aviso de dejarla encendida en los ajustes de la tablet.
  - **Palabras de cocina iguales a las de Sala y el PC:** «Por revisar en el pase», «Adaptado: …», «No le afecta».
  - **PC:** el Comedor agrupa con la misma tabla común; una restricción de toda la mesa ya no se repite en cada línea y un plato para compartir dice de quién es la alergia.
  - **Pruebas:** el proyecto `tableta` (1180 × 820, táctil), que corre solo la historia de cocina.
  - **Sin cambio de esquema, de motor, de API ni de CSP.**
- **Vuelta atrás:** C3d no cambia los datos, pero la guarda de E4e se estrena con él: desde que `0.45.0-c3d` sirve los datos, el instalador de `0.44.0-e4e` se para con `SETUP FAILED`. Volver es restaurar la copia previa a la actualización, en un equipo limpio y con el instalador anterior. Las tablets no hay que emparejarlas de nuevo.
- **Cifras en la punta** (en este PC, el 27-10, con la versión `0.45.0-c3d` y la revisión):
  - vitest 467, en 20 ficheros (`cocina.test.ts` 59 y `turno.test.ts` 10, nuevos; `kitchen.test.ts`, retirado); `npm run build` en verde;
  - ClientChecks 288;
  - dominio 97;
  - PersistenceChecks 88;
  - las 14 suites HTTP (packaging 15 con 1 saltada, native 11, security 6/6, desktop_reads 9, affordances 8, restrictions 9, checkout 8, identity 10, pairing 18, station 11, organization 5, catalog 10, offers 8 y pwa_http 8) y Realtime 13;
  - Playwright 29 (14 de escritorio, 14 de móvil y 1 de `tableta`), en 2,7 min;
  - DesktopChecks en verde;
  - la batería completa con `COSTINA_E2E=1`, `rc=0` en 312 s tras la revisión; `pwa_http` tardó 165 s (registro en `.lab-test/c3d-rev-battery.log` y `.lab-test/c3d-rev-battery-pwa_http.log`).
- **Lo que solo verá la CI:**
  - el proyecto `tableta` corre por primera vez en la CI, en el job `postgres-http`. Ese job tiene 20 minutos y tardaba unos 17; con una prueba de navegador menos que en E4e no se ha subido `timeout-minutes` (`.github/workflows/dotnet-d1.yml`). Si roza el límite, se sube con su comentario;
  - el workflow «Native Windows installer» no salta en la PR (C3d no toca nada de lo que invoca el instalador); corre al mezclar en `develop`.

## Novedad del entorno y trampas

- **Las mesas de las pruebas cambian:** M1 la usa `drinks.spec.ts`, M2 `pwa_http.py`, M3 y M4 el proyecto móvil, M6 y M7 el de escritorio, y **M8 el proyecto `tableta`** (`support.ts`, `kitchenTable()`). **M5 queda libre.** La nota del traspaso anterior («M3–M5 el proyecto móvil y M6–M8 el de escritorio») está desfasada.
- **`kitchen.spec` deja M8 libre en `finally`**, también si la historia falla a medias: revisa, marca, valida y sirve lo que quede como el PC, y la libera. Una segunda ejecución sobre la misma base la encuentra libre.
- **Una historia de cocina nueva va al proyecto `tableta`**: `playwright.config.ts` lo elige por el nombre del fichero (`KITCHEN_SPECS`, hoy solo `kitchen.spec.ts`). Otro fichero de cocina tiene que entrar en esa expresión, o correrá en escritorio y en móvil sin mesa de cocina.
- **El sonido en las pruebas** se cuenta envolviendo `AudioContext.prototype.createOscillator` antes de cargar la página (cada tono son dos notas). Nunca suena de verdad: que se oiga lo dice solo el guion manual.
- **El `version_conflict` de `kitchen.spec`** se provoca interceptando el primer `preparations-ready` de la tablet fría (`route`): antes de dejarlo pasar, el PC empieza el plato caliente por API.
- **Capturas de C3d:** se rehacen con `C3D_CAPTURES` (`e2e/captures-c3d.spec.ts`), sobre una base nueva, con el motor compilado con el build actual de la PWA y con `psql` en el `PATH` (lo pone `local-tests.sh`): atrasan `firedAt` y `readyAt` en la base para los colores del tiempo. El comando está en el documento del corte. Las capturas de cortes anteriores (`C3A_CAPTURES`, `C3C_CAPTURES`, `E4E_CAPTURES`) no se han vuelto a ejecutar con la cocina nueva.
- **Vite y la instalación real:** el servidor de desarrollo de Vite lleva su proxy a 5088, el puerto de la instalación real de este PC. Para mirar la pantalla sin la batería, el corte usó un arnés en el `scratchpad` servido solo por el enrutado de peticiones de Playwright, nunca `npm run dev`.
- **Problemas de antes, sin tocar:** `npm run build` no comprueba los tipos de `e2e/` (un `tsc` a mano encuentra un error antiguo en `banner.spec.ts:19`), y la prueba de `pairing.spec` del móvil que sobrevive a una recarga tarda entre 3 y 18 s, lo que mueve `pwa_http` unos 15 s de una ejecución a otra.
- **Sigue valiendo** lo del traspaso anterior: PersistenceChecks corre también en este PC sin base; la batería entera, una sola a la vez y en primer plano (unos 5 o 6 minutos); `local-tests.sh desktop` solo enseña las dos últimas líneas; las ADR viven en dos carpetas que en Windows son la misma (ADR-014 a ADR-017 y una ADR-007 en `docs/ADR/`); las roturas a propósito se devuelven desde una copia en el `scratchpad`, nunca con `git checkout`.

## Para el promotor

1. **Antes de poner `0.45.0-c3d` en su instalación** (lo explica el documento del corte, «Antes de instalar»):
   - no hay nada que migrar; después de actualizar, recargar Costiña Cocina en cada tablet y tocar «Empezar turno»;
   - con la conexión sencilla (http), dejar la pantalla de cada tablet siempre encendida en sus ajustes;
   - desde que `0.45.0-c3d` sirva los datos, el instalador de `0.44.0-e4e` ya no se instala encima: volver es restaurar la copia previa a la actualización en un equipo limpio.
2. **Guion manual de C3d**, solo en un PC de laboratorio, con una tablet en Cocina caliente, otra en Cocina fría, otra en el pase y un móvil con Costiña Sala: 10 pasos, con su tabla, en el documento del corte. Lo más importante: si se oye desde el sitio de quien cocina (paso 1), si la pantalla sigue encendida (paso 2) y si se lee a uno o dos metros (paso 4). Lleva un paso opcional para ver la guarda contra bajar de versión.
3. **Siguen pendientes** el guion manual de E4e (10 pasos, laboratorio) y el guion físico de C3c (15 pasos, con un Android, un iPhone y un iPad). El paso 11 del de C3c ya no necesita marcar las elaboraciones desde el PC: basta «Listo» en cada estación, o «Marcar … como lista» en el pase.
4. **Decisiones pequeñas para revisar:**
   - las **dieciocho de C3d** y sus **cinco diferencias con la maqueta**, en «Decisiones tomadas al construir» del documento del corte. Por ejemplo: «Empezar turno» como franja y no como pantalla que tape las comandas; el toque que suena una vez; las comandas del pase en filas y no en dos columnas; la franja «Hecho» al pie y sin límite; un atajo que sustituye la nota;
   - las once de E4e, las doce de C3c, las 24 de C3b y las 5 de C3a.
5. **Varios tamaños de copa por vino** («Copa 15 cl»): la pregunta sigue en el documento de C3b.
6. **Pendiente de antes:**
   - el IVA que diga su gestor;
   - el dominio para el certificado público: además de quitar la conexión sin cifrar, es lo que dejará la pantalla encendida sin tocar los ajustes y permitirá avisar con la aplicación cerrada;
   - la exportación de Verial;
   - las claves de WooCommerce;
   - los guiones físicos de C3a y de C3b;
   - el LICENSE;
   - las 4 fotos;
   - la hora de apertura de la mesa.

## Siguiente trabajo, en orden

1. **Subir y mezclar C3d** con el permiso del 27-10, cuando la CI esté en verde: PR desde `feat/c3d-costina-cocina` a `develop`, leer la CI en los logs (sobre todo el tiempo del job `postgres-http` con el proyecto `tableta` y su `kitchen.spec`), merge «Merge PR #N: …» en local y push. Después, el instalador de `develop` (`Costina-Setup-0.45.0-c3d.exe`).
2. **C3e: Costiña Sala, mis mesas y abrir y servir el menú** (`0.46.0-c3e`, sin cambio de esquema ni de motor; sección 4.4 del plan del 22-10, ya aprobado).
   - «Mis mesas» primero y «Todas» después, con `openedBy` y `openedStation` (presentación, nunca atribución a una persona); las libres al final; el aviso de «listo para servir», solo de «Mis mesas».
   - Abrir con alergias desde Sala en una sola orden `open` con `restrictions`, en tres toques con el menú del día.
   - «Servido · Principal» en el botón grande; «Carta y menús» con los platos agrupados; el «Preguntar al servidor» de una orden de otra instalación (arrastrado de C3b).
   - La línea de excepción de Sala (`TableCard.vue`) con la regla de la tabla común `kitchen-cases.json`, que hoy solo siguen Costiña Cocina y el PC.
   - La prueba de navegador crea por API una segunda estación de sala y abre la mesa **desde el comandero** (con el token principal la mesa no tendría estación y sería «mía» para todos).
   - **Antes de empezar:** propón al promotor los pasos del corte (plan) y **pídele permiso para subir y mezclar C3e**; el de C3d no vale para C3e.
3. **Después, en este orden:** E4f, E4c (esquema v9) y E4g. Cada uno, con su permiso de subida y mezcla.

## En el PC del promotor

- **Instalación real**, leída el 19-10: versión 0.37.1-c2.1. Servicios `Costina` y `CostinaPostgres`, puertos 5088 y 5089. **No se toca.** El instalador de `develop` la pone al día; la guarda contra bajar de versión vale desde que tenga `0.44.0-e4e`.
- **Pruebas en este PC:** la batería corre con `.lab/pgsql/bin`, en el puerto 5090, sin administrador y una sola a la vez. Playwright corre en local con Chromium en `%USERPROFILE%\.ms-playwright`. Nunca se ejecuta aquí el instalador real, y nunca `npm run dev` (su proxy apunta a 5088).

## Prompt para continuar en otra sesión

```text
Soy Santi, promotor de Costiña / Hospitality OS (repo público Rivadesa/Costi-a, rama de integración `develop`; `main` NO se toca). Continúas el trabajo de otra sesión.
Antes de nada, `git fetch` y el último docs/handoff, porque otra sesión puede haber avanzado. Lee primero, en este orden: docs/handoff/2026-10-27-c3d-costina-cocina.md (punto de entrada), docs/native/C3d-costina-cocina.md, docs/ADR/ADR-017-la-estacion-ve-solo-lo-suyo.md, docs/plans/2026-10-22-sala-y-cocina.md (APROBADO el 22-10; 4.4 es C3e, con sus secciones 5 a 10 y la Revisión) y dotnet/AGENTS.md (sobre todo C3d, E4e, C3c, C3a y D6.4).
Reglas: un corte por PR desde `develop`; propón el alcance (plan) antes de codificar; me pides confirmación para push+PR y, aparte, para el merge (puedo darla por adelantado para un corte concreto); merge commit "Merge PR #N: ..." hecho en local y push; antes de cada push que toque dotnet/ o frontend/ corre `npm run build` y `COSTINA_E2E=1 sh tools/agent/local-tests.sh` entero, y `sh tools/agent/local-tests.sh desktop` (en un PC con una instalación real de Costina: puerto 5090, nunca como administrador, sin tocar sus servicios, puertos 5088/5089 ni carpetas, nunca su instalador real ni `npm run dev`); una sola batería a la vez; los tests de CI se LEEN en los logs; GitHub sin gh CLI (tools/agent/gh.sh, cuerpos JSON en fichero UTF-8, tools/agent/wait-ci.sh); cada corte trae doc en docs/native/ con capturas, párrafo en dotnet/AGENTS.md, docs/STATUS.md y versión en csproj Server/Desktop y frontend/pwa/package.json (+lock); commits convencionales sin acentos con la línea Co-Authored-By que indique la sesión; nunca cierres #26/#27/#28 ni toques #25; pantallas cuidadas con el tema Fluent de C1 y barra de menús clásica, y el sistema MÁS INTUITIVO; habrá Android y también iPhone y iPad: los dos caminos tienen que funcionar; seguridad alimentaria primero: ningún plato afectado por una alergia llega al comensal sin una decisión de cocina registrada, y las alergias nunca se esconden ni se atenúan; nunca metas credenciales en ninguna web; la importación en mi instalación la hago yo.
Háblame siempre en castellano.
Estado: C3d (Costiña Cocina con comandas por mesa: cada estación solo con lo suyo y un «Listo», el pase con la mesa entera, ámbar a los 15 y a los 25 minutos, sonido tras «Empezar turno»; 0.45.0-c3d, sin cambio de esquema ni de motor) hecho en la rama feat/c3d-costina-cocina, con mi permiso de subida y mezcla del 27-10: mira en origin/develop si ya está mezclado. Siguiente: C3e (Costiña Sala con «Mis mesas», sección 4.4 del plan del 22-10): propón los pasos y pídeme permiso para subir y mezclar. Tengo pendientes el guion manual de C3d (tres tablets y un móvil, en un laboratorio), el de E4e, el guion físico de C3c con un Android, un iPhone y un iPad, las dieciocho decisiones de C3d, las once de E4e y las doce de C3c, y los varios tamaños de copa («Copa 15 cl») de C3b.
```
