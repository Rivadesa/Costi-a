# Traspaso 2026-10-29: C3e.1 (arreglos rápidos de usabilidad en Sala, Cocina y el PC); siguiente, E4f o el resto de los arreglos rápidos

Para seguir desde cualquier equipo. **Es el punto de entrada**: sustituye al traspaso `2026-10-27-c3e-costina-sala.md`. Ese sigue valiendo para C3e: sus 35 decisiones y sus ocho diferencias con la maqueta, las mesas de las pruebas (M1 a M8), el guion manual, la vuelta atrás y las trampas.

Leer antes, en este orden:
1. este documento;
2. `docs/native/C3e.1-arreglos-rapidos-usabilidad.md`:
   - lo que notará el promotor y los nueve defectos;
   - qué cambia, la revisión y las seis capturas en `docs/native/images/c3e.1/`;
   - lo que no cambia y las cifras;
   - las cinco cosas para revisar con el promotor, el guion de laboratorio y lo diferido;
3. `docs/reviews/2026-10-28-usabilidad-sala-y-cocina.md`, la evaluación de la que sale el corte:
   - las notas de cada perfil (camarero 3,5, estación 3,5, pase 3, PC 3) y los 76 hallazgos H01 a H76;
   - en la sección 5, los arreglos rápidos (5a), los cortes mayores (5b) y las 15 decisiones para revisar (5c);
   - en la 6b, los ocho huecos que añadió la revisión final;
4. el traspaso de C3e (arriba) y, para E4f, lo que dice;
5. `dotnet/AGENTS.md`, el párrafo C3e.1 (y C3e, C3d, E4e), y `docs/STATUS.md`.

## Estado

- **`develop`**: C3e.1 entra con el PR #100 («Merge PR #100: C3e.1 …», el 29-10). Versión **`0.46.1-c3e.1`**, esquema v8. El sha de la mezcla, la CI leída y el instalador se apuntan aquí después de mezclar, con un PR de traspaso, como se hizo con C3e (#98 y #99).
- **C3e.1 está mezclada** desde la rama `fix/c3e1-arreglos-rapidos-usabilidad`, sacada de `dd7c78c` (Merge PR #99), con la punta que verifique la CI (`git diff --stat` entre ella y el merge: vacío).
- **Permiso:** el del 29-10, «Haz los arreglos rápidos», que sigue a la evaluación del 28-10 y, como en los cortes anteriores, incluye subirlo y mezclarlo.
- **Commits:**
  - `4862206` fix(pwa): Costiña Cocina (H34, H53, H56, H48);
  - `7cdac16` fix(pwa): la banda con las alergias del pase y el resultado antes de la acción siguiente (H19, H20);
  - `4335b75` fix(pwa): no perder lo escrito al salir de «Abrir mesa» ni con un rechazo (H06, H03);
  - `ff84a5d` fix(desktop): la fila de anotar una restricción en una mesa abierta es la del alta (H51);
  - `3396041` fix(pwa): las doce correcciones de la revisión;
  - `da742fc` fix(pwa): las correcciones de la revisión final (la apertura en camino, de su mesa y de su hoja; los rechazos de las hojas que ya no se van con la relectura; el resultado y la otra pestaña; el conflicto de «No le afecta a …»; «no lo emplates» bajo un plato ya «Lista»; y «Salir sin abrir» en `mesas.spec`);
  - docs(native): el documento con las capturas, el informe en `docs/reviews/`, AGENTS, STATUS, las notas en C3e y C3d y la versión `0.46.1-c3e.1`;
  - docs(handoff): este traspaso.
- **Instalador de `develop`:** se apunta tras la mezcla.

## Cómo se hizo

1. **Implementación**, con cuatro agentes:
   - el PC (H51), en paralelo;
   - tres para la PWA, uno detrás de otro porque comparten `style.css`: Cocina (H34, H53, H56, H48), la banda y el botón grande de Sala (H19, H20), y no perder lo escrito en Sala (H06, H03).
2. **Revisión**, con tres lentes (seguridad y corrección, usabilidad y coherencia, pruebas) y un verificador escéptico. De 23 hallazgos en bruto quedaron 15 y confirmó 12. Un corrector los aplicó todos.
3. **Revisión final**, del trabajo del corrector y del documento, con tres lentes y su verificador:
   - de 27 hallazgos, el verificador confirmó 16: seis de código (uno de gravedad media: con la apertura de una mesa en camino, la hoja de OTRA mesa salía sin preguntar), ocho del documento y dos de pruebas;
   - un corrector los aplicó y un último revisor encontró uno más, anterior al corte (la hoja nueva de la misma mesa con la apertura en camino), que también se arregló.
4. **La batería completa con `COSTINA_E2E=1`, en este PC**, dos veces (antes y después de la revisión final), `exit 0` las dos: dominio 97, ClientChecks 288, las 14 suites HTTP y Realtime 13. Además:
   - Playwright 31 en 3,0 min;
   - vitest 600 en 23 ficheros;
   - DesktopChecks en verde.

## Cifras en la punta

En este PC, el 29-10, con `0.46.1-c3e.1`, en la punta final (`da742fc`):

- **vitest:** 600 en 23 ficheros (562 en C3e). `npm run typecheck` y `npm run build`, en verde.
- **ClientChecks** 288 y **dominio** 97/97. No cambian.
- **Las 14 suites HTTP**: packaging 15 con 1 saltada, native 11, security 6/6, desktop_reads 9, affordances 8, restrictions 9, checkout 8, identity 10, pairing 18, station 11, organization 5, catalog 10, offers 8 y pwa_http 8. **Realtime** 13.
- **Playwright** 31:
  - `pwa_http` tardó 185 s (208 s en la primera batería); 183 s en C3e. Los 31 tardaron 3,0 min.
  - `mesas.spec`: 12,2 s en `[mobile]` y 12,4 s en `[sala-vertical]`; 6,7 y 6,8 s en C3e. Tiene más pasos y las esperas del resultado.
  - `banner.spec`: 6,2 y 6,3 s.
  - `kitchen.spec`: 10,2 s.
- **DesktopChecks**, en verde a la primera en la punta final. Antes de la revisión final lo estuvo a la tercera. Las dos primeras, con el PC en uso por otras ventanas, fallaron cada una en un sitio distinto que C3e.1 no toca (la ficha de impuestos del Catálogo y las columnas de mesas de Organización). Si en la CI sale verde, eran del entorno.
- **Registros**, en `.lab-test/`: `c3e1-battery.log` y `c3e1-battery-2.log`, `c3e1-pwa_http.log` y `c3e1-pwa_http-2.log`, `c3e1-desktop*.log` y `c3e1-captures*.log`.

## CI leída en los logs

Se apunta tras la mezcla, con el PR de traspaso.

## Trampas nuevas

- **`local-tests.sh desktop` siempre devuelve 0.** Termina con `| tail -2`, así que hay que leer la última línea («WPF startup and viewmodel checks passed»), no el código de salida. Queda para arreglar fuera de este corte.
- **El resultado del botón grande (`do-done`) dura `DONE_MS` = 1,5 s.** Una prueba que toque el botón grande dos veces seguidas tiene que esperar a la acción siguiente por su `data-testid`, con 15 s de margen, como `mesas.spec` y `commands.spec`.
- **El primer toque de «Rehacer» ya no manda nada.** La orden la manda `do-review-remake-…`, que ahora es «Sí, rehacer». El primer paso es `remake-…`, que no empieza por `review-` para no cambiar los recuentos de `[data-testid^="review-"]`.
- **Las hojas de alergias y de omitir ya no se cierran al tocar.** Una prueba que espere el cierre tiene que esperar la confirmación.
- **Hay una especificación de capturas por ejecución** (`playwright.config.ts`). Para rehacer las de este corte:
  - `C3E_CAPTURES`, `C3D_CAPTURES` y `E4E_CAPTURES`, cada una en su ejecución de `sh tools/agent/local-tests.sh http packaging_http pwa_http`;
  - después, quedarse solo con las seis de `images/c3e.1/`.

## Lo siguiente

Cada cosa necesita su propio permiso:

1. **E4f** (`0.47.0-e4f`, el menú en una pantalla, con «Así lo verá el camarero»). Ver el traspaso de C3e.
2. **El resto de los arreglos rápidos** (sección 5a del informe, puntos 7 a 10, y H21):
   - el «Motivo» del PC dentro de cada acción y retirar alergias en dos pasos (H61);
   - aviso de estación distinta (H64);
   - nada preelegido en Caja (H65);
   - la marca «NUEVA» y «SIN SONIDO» en cocina (H36 a H38);
   - textos que orientan (H22, H25, H59, H71 y H27);
   - pulido (H13, H15, H16, H33 y H05);
   - la orden sin confirmar a la vista (H21).
3. **Los cortes mayores** de la sección 5b: Sala con avisos que se ven, el pase con carga, la estación con volumen y el cierre de mesa en el PC.
4. **Para el promotor**, sin bloquear nada:
   - las cinco cosas para revisar del documento de C3e.1; la primera es si «No le afecta a ninguno de estos N platos» lleva un segundo paso;
   - las 15 decisiones de la sección 5c del informe;
   - las 35 decisiones de C3e;
   - el pie de C3b «Los importes solo se ven en Caja.» (ADR-007).

## Lo que no se toca

- `main`.
- La instalación real del PC: los servicios `Costina` y `CostinaPostgres`, los puertos 5088 y 5089, `C:\Program Files\Costina`, `C:\ProgramData\Costina` y `%LOCALAPPDATA%\Costina`. Solo se lee su estado.
- Nunca como administrador ni el instalador real en este PC.
- Nunca `npm run dev`: su proxy apunta a 5088.
- Las incidencias: la #25 no se toca y la #26, la #27 y la #28 no se cierran.
- La importación en su instalación la hace el promotor.
