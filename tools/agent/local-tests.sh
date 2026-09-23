#!/bin/sh
# Pruebas .NET en un PC Windows de desarrollo, con el MISMO orden y entorno que .github/workflows/dotnet-d1.yml.
#
# Requisitos (ninguno necesita administrador):
#   - SDK .NET de dotnet/global.json en %LOCALAPPDATA%\Microsoft\dotnet  (script oficial firmado: https://dot.net/v1/dotnet-install.ps1
#     -Version <la de global.json> -InstallDir %LOCALAPPDATA%\Microsoft\dotnet -NoPath). Con el Python de Microsoft Store,
#     instalarlo en %USERPROFILE%\.dotnet-sdk (ver abajo); COSTINA_DOTNET=<carpeta> para cualquier otra ubicacion.
#   - Binarios portables de PostgreSQL 17 en .lab/pgsql/bin (los del laboratorio, docs/native/lab-engineering.md), o PG_BIN=<carpeta bin>
#   - Python 3 y, para pwa_http, el build de la PWA (npm run build en frontend/pwa) ANTES de compilar el servidor
#
# Usa un cluster PostgreSQL PROPIO y desechable en .lab-test/ (puerto 5434, base costina_d1_test, superusuario costina/ci-only como en CI).
# JAMAS toca el laboratorio (.lab, puerto 5433) ni una instalacion real. .lab-test/ esta fuera de git (.git/info/exclude).
#
# Uso:   sh tools/agent/local-tests.sh                 todo: compilar, dominio, cliente, bateria HTTP, tiempo real
#        sh tools/agent/local-tests.sh http [suite...] solo la bateria HTTP (o las suites indicadas; packaging_http reinicia la base)
#        sh tools/agent/local-tests.sh desktop         checks del WPF (abre una ventana real un instante)
#        sh tools/agent/local-tests.sh stop            para el cluster desechable
# No corre en local: la historia TLS de packaging_http (provision-tls deja tls\ solo a administradores: se salta sola), el E2E de
# Playwright (COSTINA_E2E=1 con `npx playwright install chromium`) ni los jobs windows-service e installer (servicio real, NSIS).
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
T="$ROOT/.lab-test"; PG_BIN="${PG_BIN:-$ROOT/.lab/pgsql/bin}"
# SDK: COSTINA_DOTNET=<carpeta>, o %LOCALAPPDATA%\Microsoft\dotnet, o %USERPROFILE%\.dotnet-sdk. La ultima existe porque el Python de
# Microsoft Store corre con AppData virtualizado y NO VE nada bajo %LOCALAPPDATA%: las suites HTTP caerian al dotnet del sistema.
# En forma POSIX: una ruta C:\... dentro de PATH se parte por los dos puntos.
for D in "$COSTINA_DOTNET" "$LOCALAPPDATA/Microsoft/dotnet" "$USERPROFILE/.dotnet-sdk"; do
  [ -n "$D" ] && [ -x "$D/dotnet.exe" ] && break
done
DOTNET_ROOT="$(cygpath -u "$D" 2>/dev/null || printf '%s' "$D")"
export DOTNET_ROOT DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 PYTHONIOENCODING=utf-8
export PATH="$DOTNET_ROOT:$PG_BIN:$PATH"
export PGHOST=127.0.0.1 PGPORT=5434 PGUSER=costina PGPASSWORD=ci-only PGDATABASE=costina_d1_test COSTINA_PG_BIN="$PG_BIN"
BOOTSTRAP='Host=127.0.0.1;Port=5434;Database=costina_d1_test;Username=costina;Password=ci-only'
SERVER=src/Costina.Server/bin/Release/net10.0/Costina.Server.dll
ALL="packaging_http native_http security_http desktop_reads affordances_http restrictions_http checkout_http identity_http pairing_http station_http organization_http catalog_http offers_http pwa_http"
[ -x "$DOTNET_ROOT/dotnet.exe" ] || { echo "Falta el SDK .NET en $DOTNET_ROOT (ver cabecera)"; exit 2; }
python -c "import os,sys; sys.exit(not os.path.exists(os.path.join(os.environ['DOTNET_ROOT'],'dotnet.exe')))" || {
  echo "python no ve $DOTNET_ROOT (Python de Microsoft Store?): instala el SDK en %USERPROFILE%\\.dotnet-sdk o indica COSTINA_DOTNET"; exit 2; }
[ -x "$PG_BIN/pg_ctl.exe" ] || { echo "Faltan los binarios de PostgreSQL en $PG_BIN (ver cabecera)"; exit 2; }
mkdir -p "$T"; grep -qs '^.lab-test/' "$ROOT/.git/info/exclude" || echo '.lab-test/' >> "$ROOT/.git/info/exclude"

cluster() {
  if [ ! -d "$T/pg" ]; then
    printf 'ci-only' > "$T/pw.txt"
    initdb -D "$T/pg" -U costina --pwfile="$T/pw.txt" -A scram-sha-256 -E UTF8 --locale=C > "$T/initdb.log" 2>&1 || { tail -3 "$T/initdb.log"; exit 2; }
  fi
  # pg_ctl hereda los descriptores: sin redirigir los tres, la llamada no vuelve nunca en Windows.
  pg_isready -q || pg_ctl -D "$T/pg" -o "-p 5434 -c listen_addresses=127.0.0.1" -l "$T/pg.log" -w start > "$T/start.log" 2>&1 < /dev/null
  pg_isready -q || { echo "El cluster de pruebas no arranca: ver $T/pg.log"; exit 2; }
}
roles() { while IFS= read -r line; do case "$line" in COSTINA_DB=*|COSTINA_DB_OWNER=*) export "$line";; esac; done < "$T/github-env.txt"; }
http() {
  cluster; cd "$ROOT/dotnet" || exit 1
  FAILED=""
  for suite in ${*:-$ALL}; do
    if [ "$suite" = packaging_http ]; then
      # Como en CI: base y roles recien creados. packaging_http usa el superusuario y EXPORTA las conexiones de rol (GITHUB_ENV).
      # (la historia de copia y restauracion deja ademas su propia base: tambien se va.)
      psql -q -d postgres -c "DROP DATABASE IF EXISTS costina_restore_d1_test WITH (FORCE)" \
        -c "DROP DATABASE IF EXISTS costina_v1_d1_test WITH (FORCE)" -c "DROP DATABASE IF EXISTS costina_restore_v1_d1_test WITH (FORCE)" \
        -c "DROP DATABASE IF EXISTS costina_d1_test WITH (FORCE)" -c "DROP ROLE IF EXISTS costina_runtime" \
        -c "DROP ROLE IF EXISTS costina_owner" -c "CREATE DATABASE costina_d1_test" > /dev/null || exit 2
      export COSTINA_DB="$BOOTSTRAP" GITHUB_ENV="$T/github-env.txt"; unset COSTINA_DB_OWNER; : > "$GITHUB_ENV"
    else
      [ -s "$T/github-env.txt" ] || { echo "Ejecuta antes packaging_http: exporta las conexiones de rol"; exit 2; }
      roles
    fi
    printf '== %s ... ' "$suite"
    if python "tests/http/$suite.py" "$SERVER" > "$T/$suite.log" 2>&1; then
      grep -E "^(Ran [0-9]+ tests|OK)|[0-9]+/[0-9]+ passed" "$T/$suite.log" | tr '\n' ' '; echo
    else
      echo FALLA; FAILED="$FAILED $suite"; grep -E "^(FAIL|ERROR):|AssertionError" "$T/$suite.log" | head -6
    fi
  done
  [ -z "$FAILED" ] || { echo "FALLAN:$FAILED  (logs en .lab-test/)"; exit 1; }
}

case "${1:-all}" in
  stop) pg_ctl -D "$T/pg" -m fast stop > /dev/null 2>&1 < /dev/null; echo "cluster de pruebas parado" ;;
  http) shift; http "$@" ;;
  desktop) cd "$ROOT/dotnet" && dotnet run --project tests/Costina.DesktopChecks -c Release 2>&1 | tail -2 ;;
  all)
    cd "$ROOT/dotnet" || exit 1
    dotnet build src/Costina.Server -c Release 2>&1 | grep -E " error |Advertencia|Errores|Warning|Error" | tail -4
    dotnet run --project tests/Costina.Acceptance -c Release 2>&1 | tail -1
    dotnet run --project tests/Costina.ClientChecks -c Release 2>&1 | grep -E "^FAIL|Client checks"
    http || exit 1
    roles; cd "$ROOT/dotnet" && dotnet run --project tests/Costina.RealtimeChecks -c Release -- "$SERVER" 2>&1 | grep -E "^FAIL|Realtime checks"
    ;;
  *) sed -n 2,18p "$0" ;;
esac
