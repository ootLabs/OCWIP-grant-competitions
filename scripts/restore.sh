#!/usr/bin/env bash
# Restores a backup onto an empty machine (T-114): the repository checked
# out, .env.prod with the same secrets as the server that made the backup
# (above all FIELD_ENCRYPTION_KEY and RESTIC_PASSWORD), and no volumes yet.
#
#   scripts/restore.sh [snapshot]      (default: latest)
#
# ENV_FILE picks another settings file, COMPOSE_FILES adds compose files
# (a test machine adds docker-compose.backup-test.yml).
set -euo pipefail

cd "$(dirname "$0")/.."
env_file="${ENV_FILE:-.env.prod}"
compose=(docker compose -f docker-compose.prod.yml)
for file in ${COMPOSE_FILES:-}; do compose+=(-f "$file"); done
compose+=(--env-file "$env_file")
started=$(date +%s)

echo "Starting an empty database."
"${compose[@]}" up -d --build db
until "${compose[@]}" exec -T db pg_isready -h 127.0.0.1 -U ocwip -d ocwip > /dev/null 2>&1; do sleep 2; done

tables=$("${compose[@]}" exec -T db psql -U ocwip -d ocwip -tAc \
  "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'public'")
if [ "$tables" != "0" ]; then
  echo "The database is not empty ($tables tables). A restore starts from empty volumes: stop, and remove them first." >&2
  exit 1
fi

echo "Restoring the files and the database."
RESTORE_SNAPSHOT="${1:-latest}" "${compose[@]}" --profile restore run --rm --build restore

echo "Starting everything."
"${compose[@]}" up -d --build

echo "Restore done in $(( $(date +%s) - started )) s. Check https://<domain>/api/health and sign in."
