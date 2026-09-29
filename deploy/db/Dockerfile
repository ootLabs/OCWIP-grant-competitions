# PostgreSQL with the bootstrap of an empty volume inside the image (T-111):
# the extensions from db/init and the two roles from docs/wdrozenie.md.
FROM postgres:16-alpine
COPY db/init/ /docker-entrypoint-initdb.d/
COPY deploy/db/010-roles.sh /docker-entrypoint-initdb.d/010-roles.sh

# Started as postgres rather than as root (T-115): the entrypoint then skips
# gosu, the Go binary it would use to drop privileges, which Trivy reports
# with a critical Go standard library flaw. Removed, since nothing calls it.
RUN rm /usr/local/bin/gosu
USER postgres
