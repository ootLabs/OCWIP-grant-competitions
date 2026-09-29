#!/bin/sh
# Runs backup.sh on BACKUP_SCHEDULE (cron syntax, the container's TZ). Cron
# starts its jobs with an empty environment, so the settings are written to
# a file the job reads first.
set -eu

export -p | grep -E ' (RESTIC_|AWS_|B2_|POSTGRES_PASSWORD|BACKUP_|TZ)' > /etc/backup.env
echo "${BACKUP_SCHEDULE:-0 2 * * *} . /etc/backup.env && /usr/local/bin/backup.sh >> /proc/1/fd/1 2>&1" > /etc/crontabs/root
echo "Backups on schedule: ${BACKUP_SCHEDULE:-0 2 * * *} (${TZ:-UTC})."
exec crond -f -l 8
