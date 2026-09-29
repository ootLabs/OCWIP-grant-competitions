#!/bin/sh
# The restore half that runs inside the backup image (T-114), called by
# scripts/restore.sh: the attachments and the keys back into their empty
# volumes, then the database dump into the empty database.
set -eu

snapshot="${RESTORE_SNAPSHOT:-latest}"
echo "Restoring snapshot $snapshot."
restic restore "$snapshot" --host ocwip --target / --include /backup --include /data/attachments --include /data/keys

# The roles exist already (deploy/db/010-roles.sh on the empty volume), so
# the objects keep their owner and ocwip_app keeps its grants.
PGPASSWORD="$POSTGRES_PASSWORD" pg_restore -h db -U ocwip -d ocwip --exit-on-error /backup/ocwip.dump
rm -f /backup/ocwip.dump
echo "Files and database restored."
