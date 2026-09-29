# PostgreSQL with the bootstrap of an empty volume inside the image (T-111):
# the extensions from db/init and the two roles from docs/wdrozenie.md.
FROM postgres:16-alpine
COPY db/init/ /docker-entrypoint-initdb.d/
COPY deploy/db/010-roles.sh /docker-entrypoint-initdb.d/010-roles.sh
