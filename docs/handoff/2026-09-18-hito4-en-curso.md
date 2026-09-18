# Traspaso de sesión — 18-09-2026 — Hito 4 (#27) en curso

Documento para **retomar el trabajo en otro equipo o en otra sesión de agente**. Todo lo necesario está en el repositorio: este documento, `tools/agent/` y la documentación de cada corte. Lo único que no viaja (a propósito) es el laboratorio local `.lab/` con sus secretos y los permisos por máquina de la herramienta.

## Estado verificable

`develop` = `e2fffca`. `main` no se toca.

| Corte | PR | Estado | Doc |
| --- | --- | --- | --- |
| D4.3b login WPF, emparejamiento, pestaña Puestos (cierre en código de #26) | #43 | mezclada, CI verde | `docs/native/D4.3b-wpf-login.md` |
| D5.1 roles PostgreSQL, `provision`/`init`/`upgrade`, modo `installation`, config por fichero, ADR-011 | #44 | mezclada, CI verde | `docs/native/D5.1-packaging-base.md` |
| D5.2 servicio de Windows (cuenta virtual, ACL, log, `/diagnostics`) | #45 | mezclada, CI verde | `docs/native/D5.2-windows-service.md` |
| D5.3 HTTPS en LAN con CA local restringida por nombre, firewall, freno 429 | #46 | mezclada, CI verde | `docs/native/D5.3-lan-https.md` |
| D5.4 copia desatendida y `restore` verificado por huella de tabla | #47 | mezclada, CI verde | `docs/native/D5.4-backup-restore.md` |
| **D5.5 instalador NSIS con PostgreSQL 17.11-3, tres modos** | **#48** | **ABIERTA** — rama `feat/d5.5-installer` | `docs/native/D5.5-installer.md` |
| D5.6 guion de instalación limpia en Windows físico | — | pendiente | — |

PR #23 (instalador de ensayo D1.3, ADR-010): **cerrada como superada**; lo aprovechable se rehízo en D5.1–D5.5.

### PR #48 — dónde quedó exactamente

- Commit `b0b6f83`: D0, D1 y WPF en verde; el workflow nuevo **Native Windows installer** falló. Causa leída en el log (re-ejecución de diagnóstico de `setup-server`): `initdb` se re-ejecuta con un **token restringido** (sin el grupo Administradores) y no podía leer el fichero temporal de contraseña, que tenía ACL solo para administradores.
- Commit `f6a783b`: arreglo (el fichero pasa a ser legible solo por el usuario que instala; los errores de herramientas muestran sus últimas líneas) + SHA-256 del archivo de PostgreSQL fijado en `deploy/windows/package.ps1` (`4B8DB093…37CF`, observado en el run 35331381837). **Su CI no se llegó a leer.**
- El commit posterior de esta rama solo añade este traspaso y `tools/agent/` (no dispara los workflows de `dotnet/**`): **el CI relevante es el de `f6a783b`.**
- Riesgos conocidos del corte, todos escritos sin poder compilar ni ejecutar en local: `pg_ctl register` + `sc config obj= "NT AUTHORITY\NetworkService"`, ACL del clúster para NetworkService, el script NSIS (`SetCurInstType`, `${GetOptions}`, `/S`, desinstalador con `_?=`), e `installer_checks.ps1`. Las páginas gráficas del instalador **no** se ejercitan en CI: se validan en D5.6.

## Pendiente del promotor (no del agente)

- Guion manual de **D4.3b** → cierra #26. **No cerrar #26 sin ese resultado.**
- Guiones manuales de D5.1–D5.5 y el guion físico D5.6 → cierran #27. **No cerrar #27 sin ese resultado.**
- Issue **#25**: no tocar sin preguntar (espera los guiones de laboratorio de D3.2–D3.6).
- Laboratorio `.lab`: repetir `init-lab`, quitar `COSTINA_KEY_*` de los `.cmd`, crear el primer usuario con `create-user` (`docs/native/lab-engineering.md`).

## Hallazgos de esta sesión que conviene no olvidar

1. La rama heredada de D4.3b **no compilaba** y había **revertido el endurecimiento D3.4** al conectar (constructor inexistente de `DpapiPendingStore`, sin `Blocked` ni comprobación de `installationId`). Se corrigió antes del primer push. Lección: revisar el diff contra `develop` antes de empujar una rama heredada de otra sesión.
2. Las líneas de petición por defecto de ASP.NET registraban la *query* — por donde viaja el billete efímero del hub — pese a que D4.3a afirmaba lo contrario. Corregido en D5.2 (`Microsoft.AspNetCore` a Warning) y cubierto por `station_http.py`.
3. El repositorio tiene **`docs/adr/` y `docs/ADR/`**. Windows no distingue mayúsculas: un ADR nuevo debe añadirse al índice de git como `docs/adr/...` (ver el commit de D5.1).

## Herramientas en `tools/agent/`

- `gh.sh` — API de GitHub sin `gh` CLI (token de `git credential fill` en el momento, nunca impreso ni guardado). `GET/POST/PATCH` con cuerpo JSON **desde fichero**, y `LOGS <job_id>`.
- `wait-ci.sh <rama> <sha7>` — espera a los runs de un commit y lista jobs y pasos fallidos.
- `patch.py` — reemplazos de texto con coincidencia única, respetando CRLF/LF y UTF-8 sin BOM.

Merge de una PR (convención del repo, hecho en local): `git checkout develop && git pull --ff-only && git merge --no-ff <rama> -m "Merge PR #N: ..." && git push origin develop`. GitHub marca la PR como *merged* sola.

## Prompt para la siguiente sesión

```
Continúas el proyecto Costi-a / Hospitality OS en un clon local de Rivadesa/Costi-a (rama develop, main no se toca). Lee docs/handoff/2026-09-18-hito4-en-curso.md (está en la rama feat/d5.5-installer hasta que se mezcle la PR #48), AGENTS.md, dotnet/AGENTS.md, docs/STATUS.md, docs/adr/ADR-011-installation-layout-and-database-roles.md y los docs/native/ de D4.3b a D5.5 antes de nada.

Primera tarea: la PR #48 (D5.5, instalador; rama feat/d5.5-installer) está abierta. Lee GitHub Actions del commit f6a783b (sh tools/agent/wait-ci.sh feat/d5.5-installer f6a783b y después los LOGS de los jobs), corrige lo que salga en el workflow "Native Windows installer" y, con mi confirmación, mézclala con merge commit "Merge PR #48: D5.5 instalador de Windows". installer_checks.ps1 re-ejecuta setup-server como diagnóstico cuando la instalación silenciosa falla: esa salida está en el log del job.

Después: D5.6, último corte de #27: guion de instalación limpia en Windows físico (docs/native/) que recorra los 6 criterios de aceptación de #27 con el instalador real (artefacto costina-windows-installer), incluidas las páginas gráficas del instalador, reinicio sin sesión, puesto adicional y tablet por HTTPS con la CA, restauración en otro equipo y desinstalar/reinstalar, más las correcciones que salgan. No cierres #27 ni #26 sin mi resultado de los guiones. Luego #28 (PWA comandero/KDS: Vue/TypeScript contra /api/native/v1, cola IndexedDB, billete efímero del hub ya disponible, QR visual, servida desde el mismo origen del motor para no abrir CORS). Issue #25: no tocar sin preguntar.

Reglas: un corte por PR desde develop (encadenar sobre la punta del anterior solo si aún no está integrado, y mezclar en orden); te confirmo push+PR juntos y el merge aparte; los pushes correctivos a una PR ya abierta no necesitan nueva confirmación; sin SDK .NET 10 local no se simulan pruebas: empujar y leer GitHub Actions, leyendo los LOGS (comprobar que los tests nuevos se ejecutaron, no solo el verde); merges con merge commit "Merge PR #N: ..." hechos en local + push; GitHub sin gh CLI (tools/agent/gh.sh), cuerpos JSON por fichero UTF-8; versión de paquete en los dos csproj y en MainWindow.xaml por cada corte (formato N.N.N-dX.Y); cada corte lleva su doc en docs/native/ con guion manual y actualiza dotnet/AGENTS.md y docs/STATUS.md; commits convencionales sin acentos.

Invariantes del Hito 4: tabla o ruta de escritura nueva => entrada en grants.sql (CI corre toda la batería con costina_runtime); packaging_http.py es la PRIMERA suite HTTP (aprovisiona la base de CI y exporta las conexiones de rol); el job Linux instala postgresql-client-17 porque pg_dump debe igualar el major del servidor; los jobs Windows usan el PostgreSQL nativo del runner (postgres/root, $env:PGBIN); nunca un bypass de validación TLS ni una CA sin restricciones de nombre; el servicio no lee owner.json ni tls/ca.key; restore verifica huellas por tabla; el instalador solo copia y llama a comandos del motor; nada borra jamás %ProgramData%\Costina.

Trampas del entorno: los heredocs largos en Bash fallan en Windows — escribe los parches como fichero y usa tools/agent/patch.py; docs/adr vs docs/ADR (añadir ADR nuevos al índice como docs/adr/...); quitar el \r a los IDs que salen de python en Windows antes de usarlos en una URL; toda suite HTTP nueva usa ámbito propio y sus POST llevan Idempotency-Key; el aprovisionamiento de identidades de test usa el entorno del servidor arrancado, no el global.
```
