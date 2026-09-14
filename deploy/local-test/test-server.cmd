@echo off
setlocal EnableExtensions DisableDelayedExpansion
set "ACTION=%~1"
set "INTERACTIVE="
if defined ACTION goto validate
set "INTERACTIVE=1"
echo Hospitality OS - servidor local de PRUEBAS
 echo I: Primera instalacion   A: Arrancar   P: Parar   S: Estado   E: Salir
choice /c IAPSE /n /m "Selecciona una opcion: "
if errorlevel 5 exit /b 0
if errorlevel 4 (set "ACTION=status" & goto validate)
if errorlevel 3 (set "ACTION=stop" & goto validate)
if errorlevel 2 (set "ACTION=start" & goto validate)
set "ACTION=init"

:validate
if /i "%ACTION%"=="init" goto preflight
if /i "%ACTION%"=="start" goto preflight
if /i "%ACTION%"=="stop" goto preflight
if /i "%ACTION%"=="status" goto preflight
echo Uso: test-server.cmd [init^|start^|stop^|status]
exit /b 64

:preflight
pushd "%~dp0\..\.." || exit /b 1
set "COMPOSE_PATH=deploy\local-test\docker-compose.yml"
if not exist "%COMPOSE_PATH%" goto package_missing
where docker >nul 2>&1
if errorlevel 1 goto docker_missing
call docker info >nul 2>&1
if errorlevel 1 goto docker_missing
call docker compose version >nul 2>&1
if errorlevel 1 goto docker_missing
if /i "%ACTION%"=="init" goto init
if /i "%ACTION%"=="start" goto start
if /i "%ACTION%"=="stop" goto stop
goto status

:init
echo Primera instalacion: descarga y construye el servidor de pruebas.
echo Requiere Internet para descargar dependencias. No usar con datos de clientes.
call docker compose -f "%COMPOSE_PATH%" build backend
if errorlevel 1 goto failed
call docker compose -f "%COMPOSE_PATH%" up -d postgres redis
if errorlevel 1 goto failed
call docker compose -f "%COMPOSE_PATH%" run --rm backend init
if errorlevel 1 goto failed
goto start

:start
REM Ordinary start never runs seeds, migrations or image builds.
call docker compose -f "%COMPOSE_PATH%" up -d --no-build --pull never
if errorlevel 1 goto failed
where curl >nul 2>&1
if errorlevel 1 goto health_unavailable
for /l %%I in (1,1,60) do (
  call curl --fail --silent --max-time 2 http://127.0.0.1:8000/api/v1/meta >nul 2>&1
  if not errorlevel 1 goto ready
  timeout /t 2 /nobreak >nul 2>&1
)
echo La API no responde. Revisa los registros; no borres ni reinicialices los datos.
call docker compose -f "%COMPOSE_PATH%" logs --no-color --tail=50 backend
goto failed

:ready
echo Servidor disponible: http://127.0.0.1:8000/api/v1
echo Abre Hospitality OS y pulsa Conectar al servidor local de pruebas.
echo Usuario de pruebas: demo@hospitality.local - Clave: demo1234
echo Solo en este PC. No expongas este servidor a Internet ni a la red local.
goto success

:stop
REM Stop preserves containers and named database volumes.
call docker compose -f "%COMPOSE_PATH%" stop
if errorlevel 1 goto failed
echo Servidor detenido. Datos conservados en el volumen local de PostgreSQL.
goto success

:status
call docker compose -f "%COMPOSE_PATH%" ps
if errorlevel 1 goto failed
goto success

:health_unavailable
echo Falta curl para comprobar la API. No se confirma que el servidor este listo.
goto failed
:docker_missing
echo Abre Docker Desktop en modo contenedores Linux y comprueba Docker Compose.
goto failed
:package_missing
echo Paquete incompleto. Extrae el ZIP completo conservando todas sus carpetas.
goto failed
:failed
popd
if defined INTERACTIVE pause
exit /b 1
:success
popd
if defined INTERACTIVE pause
exit /b 0
