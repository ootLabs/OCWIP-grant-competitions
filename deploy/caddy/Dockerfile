# Caddy with the Caddyfile inside the image: the production compose file
# mounts no host paths (T-111). Built from source on Go 1.26.9 with
# golang.org/x/net 0.60.0, because the published caddy:2.11 images still carry
# Go 1.26.8 and three denial-of-service flaws that the image scan reports as
# HIGH (S-48). Go back to `FROM caddy:2.11-alpine` once an image ships the fix.
FROM caddy:2.11-builder-alpine AS build
ENV GOTOOLCHAIN=go1.26.9
RUN xcaddy build v2.11.7 --replace golang.org/x/net=golang.org/x/net@v0.60.0 --output /usr/bin/caddy

FROM caddy:2.11-alpine
# Upgrades for the Alpine layer: the image scan (T-115) fails on critical flaws of its OpenSSL.
RUN apk upgrade --no-cache
COPY --from=build /usr/bin/caddy /usr/bin/caddy
COPY Caddyfile /etc/caddy/Caddyfile
