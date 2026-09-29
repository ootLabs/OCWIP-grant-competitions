# Nightly backups (T-114): restic with the PostgreSQL client and busybox cron.
FROM restic/restic:0.18.0
RUN apk add --no-cache postgresql16-client tzdata
COPY backup.sh entrypoint.sh restore-files.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/entrypoint.sh /usr/local/bin/restore-files.sh
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
