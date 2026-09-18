# Laboratorio de ingeniería en un PC Windows (sin instalador)

Procedimiento para montar en cualquier PC Windows un entorno de ensayo del motor nativo con binarios de CI, **sin SDK, sin Docker y sin permisos de administrador**. No es el instalador de producto (eso es D1.3/PR #23 y el hito #27): es el montaje manual que usa el promotor para ejecutar los guiones de prueba de `docs/native/D*.md`.

Todo vive en una única carpeta (recomendado: `.lab/` dentro del clon, añadida a `.git/info/exclude`). Borrar la carpeta desinstala todo. Los secretos generados son locales de esa máquina y nunca se suben a GitHub ni se pegan en conversaciones.

## Componentes

1. **PostgreSQL 17.11 portable** (EDB, versión exacta fijada — la misma que D1.3):
   `https://get.enterprisedb.com/postgresql/postgresql-17.11-3-windows-x64-binaries.zip` → descomprimir; queda `pgsql/`.
2. **Binarios del último run verde de CI** del workflow "Native WPF desktop acceptance" sobre `develop`: artefactos `costina-windows-server` → `server/` y `costina-windows-desktop` → `client/` (hasta D3.3 se llamaban `costina-d1.2-*`). Anotar el run y commit exactos usados (el manifiesto `build-manifest.json` de cada artefacto lleva versión, commit y hashes).

## Montaje (una vez)

1. Generar y **persistir primero** los secretos en ficheros locales de la carpeta: `pgpass.txt` (contraseña de la BD, hex aleatorio ≥32). Desde D4.3 ya no existe `claves.txt`: las identidades son usuarios y puestos emparejados (paso 4). Persistir antes de usar: si el montaje se corta a medias, nada queda inutilizable.
2. Inicializar la base:
   `pgsql\bin\initdb.exe -D data -U costina -A scram-sha-256 --pwfile=pgpass.txt -E UTF8 --no-locale`
   y añadir a `data/postgresql.conf`: `port = 5433` y `listen_addresses = '127.0.0.1'` (5433 para no chocar con otros PostgreSQL; solo loopback siempre).
3. Arrancar y crear la base aislada:
   `pgsql\bin\pg_ctl.exe -D data -l pg.log -w start`
   `createdb -h 127.0.0.1 -p 5433 -U costina costina_d1_lab` (con `PGPASSWORD` desde `pgpass.txt`).
4. Inicializar esquema y fixtures **una sola vez** con las variables de `docs/native/D1-postgres-api.md`:
   `COSTINA_LAB_MODE=true`, `COSTINA_DB=Host=127.0.0.1;Port=5433;Database=costina_d1_lab;Username=costina;Password=<pgpass>`, ámbito (`COSTINA_TENANT/COMPANY/LOCATION`, p. ej. `retiro-lab/anitsoc-lab/costina-lab`), `COSTINA_PORT=5088` → ejecutar `server\Costina.Server.exe init-lab`. Desde D4.3 NO hay claves `COSTINA_KEY_*`: tras el `init-lab`, crear al menos un usuario con `server\Costina.Server.exe create-user <usuario> main` (contraseña de 12+ por stdin) y entrar en los clientes con usuario y contraseña o emparejando el puesto. Repetirlo es seguro (no repone datos); tras actualizar binarios con cambios de esquema/índices, repetirlo una vez.
5. Crear tres `.cmd` en la carpeta (todas las variables dentro; los secretos solo viven en estos ficheros y en los `.txt`):
   - `1-arrancar-servidor.cmd`: variables + `pg_ctl -w start` + `server\Costina.Server.exe` (cerrar la ventana detiene el motor — útil para los guiones de reconexión).
   - `2-abrir-cliente.cmd`: `start "" client\Costina.Desktop.exe` (ejecutar una vez por puesto; se entra con usuario y contraseña —que no se guarda— o como puesto emparejado).
   - `0-parar-todo.cmd`: `taskkill /IM Costina.Server.exe /F` + `pg_ctl -w stop`.
6. Verificar: con el servidor arrancado, `curl http://127.0.0.1:5088/health` debe responder `status: ready` con la versión esperada.

## Actualizar binarios tras integrar una PR

Cerrar todo (`0-parar-todo.cmd`), sustituir `server/` y `client/` por los artefactos del nuevo run verde, y repetir `init-lab` solo si el corte tocó esquema o índices (el doc del corte lo dice; D3.4 lo requiere una vez por la tabla `installation`). `data/` y los secretos se conservan. Si el motor arranca con "run init-lab once more", es exactamente eso.

## Trampas conocidas del montaje

- **PowerShell + pg_ctl**: capturar la salida de `pg_ctl start` (pipe a `Out-Null` o similar) cuelga para siempre — el demonio hereda el handle y la tubería no se cierra. Usar `Start-Process -Wait` con redirección a fichero, o cmd/bash.
- **Windows + heredocs**: al generar ficheros por herramientas intermedias, `\\b` puede llegar como retroceso (`\b`) y corromper rutas (`pgsql\bin` → `pgsqlin`). Construir las barras de forma explícita y verificar el contenido final.
- **Codificación**: ficheros de script en UTF-8; cuerpos JSON para la API de GitHub siempre desde fichero (`--data-binary @f.json`), nunca inline en consola (los multibyte se corrompen).

## Límites

Entorno de ensayo de ingeniería: loopback, usuarios creados por CLI, sin servicio de Windows, sin backups, sin LAN/TLS. Nada de datos reales. La seguridad de `pgpass.txt` y de los `.cmd` es la del usuario de Windows; el instalador real (hito #27) sustituirá este montaje.
