# Nightly backups (T-114): restic with the PostgreSQL client and busybox cron.
FROM restic/restic:0.19.1
# Upgraded at build: the image scan (T-115) fails on critical flaws of the
# base packages, OpenSSL first of all.
RUN apk upgrade --no-cache && apk add --no-cache postgresql16-client tzdata
# Where backup.sh puts the dump. Made at build time, owned by root, because
# S-13 took DAC_OVERRIDE off this container: root inside now obeys the
# permissions of the root directory like anyone else, and whether that is
# writable is the container runtime's choice rather than the image's. One
# directory in the image instead of a backup that depends on it.
RUN mkdir -p /backup

COPY backup.sh entrypoint.sh restore-files.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/entrypoint.sh /usr/local/bin/restore-files.sh
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
