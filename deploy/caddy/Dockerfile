# Caddy with the Caddyfile inside the image: the production compose file
# mounts no host paths (T-111). 2.11 and the Alpine upgrades because the
# image scan (T-115) fails on critical flaws of 2.10 and of its OpenSSL.
FROM caddy:2.11-alpine
RUN apk upgrade --no-cache
COPY Caddyfile /etc/caddy/Caddyfile
