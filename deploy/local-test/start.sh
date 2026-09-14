#!/usr/bin/env sh
set -eu

mode="${1:-serve}"
case "$mode" in
  serve|init|migrate) ;;
  *) echo '[hospitality] expected serve, init or migrate' >&2; exit 64 ;;
esac

cd /app
if [ ! -f public/index.php ]; then
  echo '[hospitality] incomplete package: public/index.php is missing' >&2
  exit 1
fi

echo "[hospitality] waiting for PostgreSQL at ${DB_HOST:-postgres}:${DB_PORT:-5432}..."
attempt=0
until pg_isready -h "${DB_HOST:-postgres}" -p "${DB_PORT:-5432}" -U "${DB_USERNAME:-hospitality}" -d "${DB_DATABASE:-hospitality}" >/dev/null 2>&1; do
  attempt=$((attempt + 1))
  if [ "$attempt" -ge 60 ]; then
    echo '[hospitality] PostgreSQL unavailable; startup aborted' >&2
    exit 1
  fi
  sleep 1
done

php artisan config:clear --no-ansi
case "$mode" in
  init)
    php artisan migrate --force --no-ansi
    exec php artisan hospitality:initialize-demo --no-ansi
    ;;
  migrate)
    exec php artisan migrate --force --no-ansi
    ;;
  serve)
    # Normal restart is read-only with respect to configuration and business data.
    php artisan hospitality:check-installation --no-ansi
    echo "[hospitality] starting TEST API on 0.0.0.0:${APP_PORT:-8000}"
    exec php artisan serve --host=0.0.0.0 --port="${APP_PORT:-8000}" --no-ansi
    ;;
esac
