#!/bin/sh
# One backup (T-114): the database as pg_dump -Fc, the attachments and the
# DataProtection keys, into the restic repository. restic encrypts on this
# side, so the storage never sees a PESEL or an attachment in the clear.
#
# The repository and its password come from RESTIC_REPOSITORY and
# RESTIC_PASSWORD, the storage's keys from its own variables (AWS_*, B2_*).
set -eu

restic cat config > /dev/null 2>&1 || restic init

mkdir -p /backup
PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -Fc -h db -U ocwip -d ocwip -f /backup/ocwip.dump
restic backup --host ocwip --tag nightly /backup /data/attachments /data/keys
rm -f /backup/ocwip.dump

# The server's key may only add (append only storage), so a stolen server
# cannot delete the copies. Forgetting and pruning then run with a key that
# may delete, from outside the server: BACKUP_PRUNE=1 there, never here.
if [ "${BACKUP_PRUNE:-0}" = "1" ]; then
  restic forget --host ocwip --tag nightly --prune \
    --keep-daily 7 --keep-weekly 4 --keep-monthly 12 --keep-yearly "${BACKUP_KEEP_YEARLY:-6}"
fi

echo "Backup done: $(date -Iseconds)."
