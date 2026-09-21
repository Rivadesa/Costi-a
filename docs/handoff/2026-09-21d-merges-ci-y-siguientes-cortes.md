# Traspaso de sesión — 21-09-2026 (cierre) — #54, #55 y #56 mezcladas; decisiones pendientes para los dos cortes siguientes

Documento para **retomar el trabajo en otra sesión o en otro equipo**. Sustituye como punto de entrada a `2026-09-21c-d66-y-pruebas-locales.md`, que **sigue siendo de lectura obligatoria**: contiene "Preparar un equipo nuevo", lo aprendido de los E2E y, en su prompt, las REGLAS, los INVARIANTES, la VERIFICACIÓN VISUAL y las TRAMPAS, todas vigentes. Aquí solo va lo que cambió después.

## Estado verificable

`develop` = `9de6f1b`. `main` no se toca. **No hay PR abiertas.** El repositorio es **público** desde el 21-09 y **GitHub Actions corre** (gratis).

| PR | Merge | Evidencia |
| --- | --- | --- |
| **#54 — D6.5** | `d9bac1c` | Los 4 runs de `cc21e1e` eran restos del bloqueo (0 pasos); relanzados, verdes y **leídos**: 55 vitest, dominio 68, ClientChecks 65, 10 suites HTTP + `pwa_http` con Playwright 5/5 (`commands.spec.ts` a la primera, sin `Route is already handled`), Realtime 10, servicio 11/11, instalador 7/7. Árbol del merge idéntico a `cc21e1e`. |
| **#55 — D6.6** | `e6e3919` | Runs de `6b6d5b8` verdes y **leídos**: 60 vitest, dominio 68, ClientChecks 70, `station_http` 7 tests (incluye `test_06`), Playwright 5/5, Realtime 10, servicio 11/11, instalador 7/7 (`Costina-Setup-0.25.0-d6.6.exe`). Tras mezclar la #54 quedó con solo su diff (23 ficheros). Árbol del merge idéntico a `6b6d5b8`. D0 en `develop`: verde. |
| **#56 — coste de CI** | `9de6f1b` | Runs de `acaa9ec` verdes y **leídos** (arrancaron los cuatro workflows: los YAML nuevos son válidos): 60 vitest, dominio 68 en ambos sistemas, ClientChecks 70, checks del WPF, 11 suites HTTP, Playwright 5/5, Realtime 10, servicio 11/11, instalador 7/7. Árbol del merge idéntico a `acaa9ec`. **Comprobado tras mezclar: en `develop` arrancan D0 (verde) y *Native Windows installer*; D1 y desktop no.** El run del instalador sobre `9de6f1b` seguía en curso al cerrar la sesión: leer su log (es el instalador de los guiones físicos). |

Hitos 3 (#26), 4 (#27) y 5 (#28): completos en código; **issues ABIERTAS** hasta los guiones del promotor (D4.3b, D5.6, D6.5). Issue #25: **no tocar sin preguntar**.

### Qué trajo la #56 — coste de CI y pruebas locales

Sin cambio de producto ni de versión (los binarios son los de `0.25.0-d6.6`).

- Los cuatro workflows nativos cancelan el run anterior **de la misma PR** (`concurrency`; los runs de `develop` y los manuales nunca se cancelan) e ignoran cambios solo de Markdown (antes, `dotnet/AGENTS.md` lo relanzaba todo).
- *Native Windows installer* deja de correr en cada push de PR: en una PR solo si cambia `deploy/windows/**`, `dotnet/tests/service/**` o los ficheros de ciclo de vida del motor que invoca (`ServerSetup`, `ServiceInstaller`, `Installation`, `LocalTls`, `Backup`, `StatusReport`); **siempre al mezclar en `develop`** (ese es el instalador de los guiones físicos: ya no hace falta relanzarlo a mano, salvo que se quiera otro) y a petición.
- D1 y desktop siguen corriendo solo en PR. Regla nueva (en `dotnet/AGENTS.md`): **antes de empujar un merge a `develop`, `git diff --stat HEAD origin/<rama>` debe salir vacío** (árbol mezclado = punta verificada en CI); si no, se actualiza la PR y se relanza primero.
- `tools/agent/local-tests.sh` busca el SDK en `COSTINA_DOTNET`, `%LOCALAPPDATA%\Microsoft\dotnet` y `%USERPROFILE%\.dotnet-sdk`, avisa si Python no ve el SDK, mete la ruta en `PATH` en forma POSIX y muestra también el resultado de `security_http`.

Consecuencia práctica: una PR solo de Markdown (como la que actualizó este documento) **no dispara ningún workflow**; se mezcla con la confirmación del promotor y la comprobación del árbol.

## Lo aprendido hoy (no repetir)

1. **El Python de Microsoft Store no ve `%LOCALAPPDATA%`** (AppData virtualizado): `os.path.isdir(r'...\AppData\Local\Microsoft\dotnet')` es `False` aunque exista. Las suites HTTP lanzan `dotnet` desde Python, así que caían al `dotnet` del sistema y `packaging_http` fallaba con *"You must install or update .NET"*. Compilación y checks C# pasaban: el síntoma solo aparece en las suites Python. Solución: SDK en `%USERPROFILE%\.dotnet-sdk` (las instalaciones de dotnet son reubicables: basta mover la carpeta, tras `dotnet build-server shutdown` o da *Permission denied*).
2. Una ruta `C:\...` dentro de `PATH` en `sh` se parte por los dos puntos; funcionaba por casualidad. Usar `cygpath -u`.
3. Un PostgreSQL 17 ya instalado sirve con `PG_BIN="/c/Program Files/PostgreSQL/17/bin"`; su servicio (5432) no se toca.
4. **Una suite HTTP que falla en `setUpClass` deja vivo su motor de pruebas**, y la siguiente compilación falla con `MSB3027` (DLL bloqueada por ".NET Host"). Buscar el `dotnet.exe` cuya línea de comandos es `...Costina.Server.dll` y pararlo. Las suites **no son repetibles sueltas** sobre una base usada (`table_occupied`): para repetir, pasar la batería entera o empezar por `packaging_http`, que recrea la base. Pendiente de arreglar en el script.
5. El clasificador de permisos de Claude Code deniega `git merge`/`git push` a una rama compartida dentro de un comando compuesto (`cd … && …`), y también bloquea lecturas si van en la misma línea. Usar SIEMPRE comandos simples: `git -C <clon> merge …`, `git -C <clon> push …`, uno por llamada. En `settings.local.json` conviene añadir las variantes `Bash(git -C * merge *)` y `Bash(git -C * push *)`.
6. Tras mezclar la primera PR de una cadena, el contador `changed_files` de `GET /pulls/N` de la siguiente queda **cacheado**; el diff real se mira con `GET /compare/develop...<rama>` (o `git diff --stat develop...origin/<rama>`).
7. La restauración de NuGet en la primera compilación y `npm ci` son descargas: pedir permiso al promotor también para ellas.

## Siguiente trabajo — propuesto y A LA ESPERA de decisiones del promotor

Orden acordado: CI (#56, **hecho**) → Release → generación de recuperación. **No codificar los dos últimos sin su respuesta.**

**A. Instalador como Release de GitHub.** Job en *Native Windows installer* que publica una Release **prerelease**, tag `vX.Y.Z-dN.M`, con el `.exe`, su `.sha256` y el manifiesto; `contents: write` solo en ese job; el cuerpo declara "sin firma (#12)". Decisiones: (1) solo a mano por `workflow_dispatch` con un input `publish` [recomendado] o en cada merge a `develop`; (2) con el repo público el `.exe` sin firma sería de **descarga pública** y el repo **no tiene `LICENSE`**: ¿vale así o se decide antes la licencia?

**B. Generación de recuperación.** Ya verificado en el código: `restore` preserva `installationId` a propósito y ambos clientes bloquean un pendiente de otra instalación (`commands.ts` → `blocked: 'foreign'`; `DpapiPendingStore` cabecera V2). Diseño propuesto: columna `recovery_id uuid` en `native_d1.installation` (sin tabla nueva → `grants.sql` no cambia; `ALTER TABLE … ADD COLUMN IF NOT EXISTS` en `schema.sql`), **UUID aleatorio nuevo en cada `restore`, no un contador** (restaurar dos veces la misma copia antigua daría el mismo número con historias distintas); `restore` lo cambia **DESPUÉS** de verificar las huellas por tabla (si no, rompería su propia verificación) y antes de declarar éxito; viaja en `/session`; `restore` invalida todas las sesiones de usuario; PWA y WPF guardan la generación con la orden pendiente (WPF: cabecera V3; un pendiente sin generación solo casa con una instalación nunca restaurada) y tratan una de otra generación como la de otra instalación: nunca se reenvía sola, se puede consultar por clave, y el mensaje dice que el servidor se restauró desde una copia anterior y que "no consta" no prueba que no se cocinara. Verificable en local (`packaging_http` ya hace una restauración real). Decisión: **puestos emparejados tras restaurar** — (a) suspendidos hasta que `main` los revise uno a uno [recomendado: un puesto revocado después de la copia nunca resucita en silencio], (b) solo aviso en la pestaña Puestos, (c) revocarlos todos y re-emparejar por QR.

**C. Resto**, de `2026-09-21c`: guardarraíles de versión/ADR en CI, N→N+1 con orden pendiente, límite de órdenes por dispositivo, matriz de autorización negativa, decisiones de negocio (PIN, RPO/RTO, inventario de datos, licencia), y los "Bloqueos antes de piloto" de `docs/STATUS.md`. Más lo del punto 4 de arriba (motor huérfano en `local-tests.sh`).

## Pendiente del promotor

- Responder a las decisiones A(1), A(2) y B.
- En *Settings → Actions → General*: **"Require approval for all outside collaborators"** (repo público), si aún no está.
- En cada equipo: `<clon>/.claude/settings.local.json` y `.claude/launch.json` (ver `2026-09-21c`, requisitos previos). En el equipo del 21-09 faltaban ambos.
- Guiones: **D4.3b** → #26 · **D5.6** → #27 · **D6.5** → #28. El instalador a usar es el del último run de *Native Windows installer* sobre `develop` (o uno lanzado a mano); el artefacto caduca a los 5 días.

## Prompt para la siguiente sesión

```
Continúas el proyecto Costi-a / Hospitality OS (repo público Rivadesa/Costi-a; rama de integración develop; main no se toca). Trabajas en un equipo Windows que puede ser nuevo: si no hay clon, git clone https://github.com/Rivadesa/Costi-a.git. No hay gh CLI (usa tools/agent/gh.sh).

ANTES DE NADA: git fetch; git checkout develop; git pull --ff-only; y lee ENTEROS, en este orden: docs/handoff/2026-09-21d-merges-ci-y-siguientes-cortes.md (estado, lo aprendido y las decisiones que te debo) y docs/handoff/2026-09-21c-d66-y-pruebas-locales.md (preparar un equipo nuevo y, en su prompt, las REGLAS, los INVARIANTES, la VERIFICACIÓN VISUAL y las TRAMPAS: siguen TODAS vigentes; síguelas como si te las hubiera escrito yo aquí). Después AGENTS.md, dotnet/AGENTS.md, docs/STATUS.md y los docs/native/ D6.3 a D6.6 (y D5.4, D5.5 y ADR-011 antes de tocar copias, restauración o el instalador).

PREPARA EL EQUIPO si hace falta con la sección "Preparar un equipo nuevo" de 21c. Primero COMPRUEBA lo que depende de mí y dime qué falta, sin suplirlo: Git for Windows con Credential Manager y sesión de GitHub con escritura (`sh tools/agent/gh.sh GET /pulls/56`), identidad de git, Python 3, Node 22+, .claude/settings.local.json (con "Bash(git -C * merge *)", "Bash(git -C * push *)", "Bash(git merge *)", "Bash(git push *)", "Bash(curl *)") y la entrada costina-pwa de .claude/launch.json. Después instala lo que sí puedes, pidiéndome permiso antes de CADA descarga con fichero, origen y tamaño (también npm ci y la restauración de NuGet): SDK .NET 10.0.401 con el script oficial firmado de Microsoft — si `python -c "import sys; print(sys.executable)"` apunta a WindowsApps (Python de Microsoft Store), instálalo en %USERPROFILE%\.dotnet-sdk, porque ese Python no ve %LOCALAPPDATA% — y PostgreSQL 17 (si ya hay uno instalado, PG_BIN="/c/Program Files/PostgreSQL/17/bin" y no descargues nada). Termina con la "Comprobación final" de 21c (dominio 68, ClientChecks 70, 11 suites HTTP, Realtime 10, checks del WPF, 60 vitest + build) y enséñame el resultado.

ESTADO (21-09-2026, cierre). develop = 9de6f1b (o posterior si solo entró documentación): PR #54 (D6.5), #55 (D6.6) y #56 (CI más barato y local-tests.sh compatible con el Python de Store) MEZCLADAS, las tres con CI verde y leído en logs; no hay PR abiertas. Actions corre (repo público). Al mezclar en develop arrancan D0 y "Native Windows installer" (no D1 ni desktop: comprobado); una PR solo de Markdown no dispara nada. Hitos 3 (#26), 4 (#27) y 5 (#28) completos en código; sus issues siguen ABIERTAS hasta mis guiones (D4.3b, D5.6, D6.5): no las cierres. Issue #25: no tocar sin preguntar.

PRIMERA TAREA: comprueba que el último run de "Native Windows installer" sobre develop terminó en verde y LEE su log (Installer checks 7/7, Costina-Setup-0.25.0-d6.6.exe): es el instalador de mis guiones físicos; si falló, diagnostica y dime. Después pregúntame las decisiones de los cortes A y B (abajo) si no te las he dado, y propónme el alcance antes de codificar. Usa comandos git SIMPLES, uno por llamada (`git -C <clon> merge …`, `git -C <clon> push …`): el clasificador de permisos deniega los compuestos con `cd … &&`; antes de empujar un merge, `git diff --stat HEAD origin/<rama>` debe salir vacío.

LOS DOS CORTES SIGUIENTES, solo con mis respuestas (están planteadas en el traspaso 21d, sección "Siguiente trabajo"; pregúntamelas si no te las doy): (A) instalador como Release de GitHub — ¿solo a mano o en cada merge?, ¿descarga pública sin LICENSE?; (B) generación de recuperación — recovery_id UUID nuevo por restore, cambiado DESPUÉS de verificar huellas, en /session, sesiones invalidadas, clientes que tratan un pendiente de otra generación como de otra instalación — ¿puestos tras restaurar: (a) suspendidos hasta revisión, (b) solo aviso, (c) revocar todos? Un corte por PR desde develop, cada uno con su versión, su doc en docs/native/ con guion manual, dotnet/AGENTS.md y docs/STATUS.md. De paso, en el corte que toque tools/: que local-tests.sh no deje un motor huérfano cuando una suite falla en setUpClass.

CUANDO YO VAYA A HACER UN GUION FÍSICO (D5.6 o D6.5): el instalador es el del último run de "Native Windows installer" sobre develop; si no lo hay o ha caducado el artefacto (5 días), relánzalo a mano (workflow_dispatch).

AL CERRAR SESIÓN: traspaso nuevo en docs/handoff/ con su prompt, y súbelo.
```
