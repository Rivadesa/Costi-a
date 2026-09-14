#!/usr/bin/env sh
set -eu

echo "[hospitality] waiting for PostgreSQL at ${DB_HOST:-postgres}:${DB_PORT:-5432}..."
until pg_isready -h "${DB_HOST:-postgres}" -p "${DB_PORT:-5432}" -U "${DB_USERNAME:-hospitality}" -d "${DB_DATABASE:-hospitality}" >/dev/null 2>&1; do
  sleep 1
done

echo "[hospitality] PostgreSQL ready"

php artisan config:clear --no-ansi
php artisan migrate --force --no-ansi

if [ "${HOSPITALITY_SEED_DEMO:-false}" = "true" ]; then
  echo "[hospitality] applying Retiro Demo seed"
  php artisan db:seed --force --no-ansi
fi

php artisan route:list --path=api/v1 --no-ansi >/dev/null

echo "[hospitality] API listening on 0.0.0.0:${APP_PORT:-8000}"
exec php artisan serve --host=0.0.0.0 --port="${APP_PORT:-8000}" --no-ansi
