#!/bin/sh
# The two roles of T-113 on the first start of an empty volume: the
# migrations run as ocwip_migrator, the API connects as ocwip_app, which may
# only read and write rows. The passwords come from the environment and reach
# SQL as psql variables, never pasted into the statement text.
set -eu

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v migrator="$OCWIP_MIGRATOR_PASSWORD" -v app="$OCWIP_APP_PASSWORD" <<'SQL'
CREATE ROLE ocwip_migrator LOGIN PASSWORD :'migrator';
CREATE ROLE ocwip_app LOGIN PASSWORD :'app';
GRANT USAGE, CREATE ON SCHEMA public TO ocwip_migrator;
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
SQL
