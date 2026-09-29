#!/usr/bin/env bash
# One deployment on the server (T-115), called by .github/workflows/deploy.yml
# over SSH, or by hand: scripts/deploy.sh <commit>
#
# In the repository checkout next to .env.prod: a backup first, the images of
# that commit from GHCR, migrate before the API (docker-compose.prod.yml),
# then wait for every service to be healthy. If they are not within
# DEPLOY_TIMEOUT seconds, the previous commit goes back up. The migrations
# of the failed version stay: a schema the old version cannot read is a
# restore from the backup just taken (docs/wdrozenie.md, "Wycofanie wersji").
set -euo pipefail

cd "$(dirname "$0")/.."
tag="${1:?the commit to deploy}"
env_file="${ENV_FILE:-.env.prod}"
state=".deployed-tag"
previous="$(cat "$state" 2>/dev/null || true)"
export IMAGE_REGISTRY="${IMAGE_REGISTRY:-ghcr.io/ootlabs/}"
compose=(docker compose -f docker-compose.prod.yml --env-file "$env_file")

healthy() {
  local deadline=$(( $(date +%s) + ${DEPLOY_TIMEOUT:-300} ))
  while [ "$(date +%s)" -lt "$deadline" ]; do
    local waiting
    waiting=$("${compose[@]}" ps --format '{{.Service}} {{.Health}}' | grep -E '^(backend|frontend|caddy|db) ' | grep -vc ' healthy$' || true)
    [ "$waiting" = "0" ] && return 0
    sleep 5
  done
  return 1
}

start() {
  git fetch --quiet origin
  git checkout --quiet "$1"
  IMAGE_TAG="$1" "${compose[@]}" pull --quiet
  IMAGE_TAG="$1" "${compose[@]}" up -d --no-build
}

if "${compose[@]}" ps --services --status running | grep -qx backup; then
  echo "Backup before the update."
  "${compose[@]}" exec -T backup backup.sh
fi

echo "Deploying $tag (previous: ${previous:-none})."
start "$tag"
if healthy; then
  echo "$tag" > "$state"
  echo "Deployed $tag."
  exit 0
fi

echo "Not healthy within ${DEPLOY_TIMEOUT:-300} s." >&2
if [ -n "$previous" ]; then
  echo "Rolling back to $previous." >&2
  start "$previous"
  healthy && echo "Rolled back to $previous." >&2
fi
exit 1
