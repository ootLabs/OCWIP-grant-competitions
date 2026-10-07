#!/usr/bin/env bash
# One deployment on the server (T-115), called by .github/workflows/deploy.yml
# over SSH, or by hand: scripts/deploy.sh <commit>
#
# In the repository checkout next to .env.prod: a backup first, the images of
# that commit from GHCR, migrate before the API (docker-compose.prod.yml),
# then wait for every service to be healthy. If they are not within
# DEPLOY_TIMEOUT seconds, or the start itself fails (a migration that
# refuses, a service that never becomes healthy), the previous commit goes
# back up. The migrations
# of the failed version stay: a schema the old version cannot read is a
# restore from the backup just taken (docs/wdrozenie.md, "Wycofanie wersji").
#
# Two things about the shape of this file:
#
# - The body is one function, called on the last line together with exit
#   (S-26). A deployment checks out the commit it deploys, which rewrites
#   this very file, and bash reads a script as it runs: a function is parsed
#   whole before it runs, so the new file never lands halfway through the old
#   one.
# - Images are pulled by digest, not by a tag alone (S-39). A tag in GHCR can
#   be moved to another image; the digest CI recorded for this commit cannot.
#   The digests arrive on standard input and are kept per commit, so a
#   rollback starts the images of the commit it rolls back to.
set -euo pipefail

store=".deploy-digests"

healthy() {
  local deadline=$(( $(date +%s) + ${DEPLOY_TIMEOUT:-300} ))
  while [ "$(date +%s)" -lt "$deadline" ]; do
    local waiting
    # -a: a container that has exited is waited for too, not skipped.
    waiting=$("${compose[@]}" ps -a --format '{{.Service}} {{.Health}}' | grep -E '^(backend|frontend|caddy|db) ' | grep -vc ' healthy$' || true)
    [ "$waiting" = "0" ] && return 0
    sleep 5
  done
  return 1
}

# The digests recorded for one commit, as compose variables. Nothing from the
# file reaches a shell: a line that is not a plain digest assignment stops the
# deployment instead of being passed on.
digests_of() {
  local file="$store/$1.env" line
  digests=()
  [ -f "$file" ] || return 0
  while IFS= read -r line || [ -n "$line" ]; do
    [ -n "$line" ] || continue
    if [[ ! "$line" =~ ^IMAGE_DIGEST_[A-Z]+=@sha256:[0-9a-f]{64}$ ]]; then
      echo "$file holds a line that is not an image digest: not deploying $1." >&2
      return 1
    fi
    digests+=("$line")
  done < "$file"
}

# What CI recorded for this commit, piped in by the deploy workflow. A
# terminal is left alone: by hand either the file is already in place, or the
# deployment goes by tag, which is what every deployment did before S-39.
receive_digests() {
  [ -t 0 ] && return 0
  local received
  received="$(head -c 4096)"
  [ -n "$received" ] || return 0
  mkdir -p "$store"
  printf '%s\n' "$received" > "$store/$1.env"
}

start() {
  digests_of "$1" || return 1
  git fetch --quiet origin
  git checkout --quiet "$1"
  env IMAGE_TAG="$1" "${digests[@]}" "${compose[@]}" pull --quiet
  env IMAGE_TAG="$1" "${digests[@]}" "${compose[@]}" up -d --no-build
}

main() {
  cd "$(dirname "$0")/.."

  # Behind a forced command (restrict,command= on the deploy key, S-11) the
  # client's command line never becomes arguments: it arrives in the
  # environment, and the commit is the only thing taken out of it.
  local tag="${1:-}"
  if [ -n "${SSH_ORIGINAL_COMMAND:-}" ]; then
    tag="$(grep -oE '\b[0-9a-f]{40}\b' <<<"$SSH_ORIGINAL_COMMAND" | head -1 || true)"
  fi

  # One full commit SHA, nothing else. The workflow checks its input too, but
  # a script that runs "git checkout $1" on a host where the account is as
  # good as root checks its own (S-26), before anything is fetched, checked
  # out, backed up or started.
  if [[ ! "$tag" =~ ^[0-9a-f]{40}$ ]]; then
    echo "deploy.sh takes one commit, as a full 40 character SHA." >&2
    exit 2
  fi

  local env_file="${ENV_FILE:-.env.prod}"
  local state=".deployed-tag"
  local previous
  previous="$(cat "$state" 2>/dev/null || true)"
  export IMAGE_REGISTRY="${IMAGE_REGISTRY:-ghcr.io/ootlabs/}"
  # Extra compose files of this machine, named in its own settings file:
  # staging has DEPLOY_COMPOSE_FILES=docker-compose.staging.yml (T-117).
  compose=(docker compose -f docker-compose.prod.yml)
  local file
  for file in $(sed -n 's/^DEPLOY_COMPOSE_FILES=//p' "$env_file"); do compose+=(-f "$file"); done
  compose+=(--env-file "$env_file")

  receive_digests "$tag"
  # Before the backup, so a digest file that does not hold digests costs
  # nothing but the message.
  digests_of "$tag"

  if "${compose[@]}" ps --services --status running | grep -qx backup; then
    echo "Backup before the update."
    "${compose[@]}" exec -T backup backup.sh
  fi

  echo "Deploying $tag (previous: ${previous:-none})."
  # Inside the condition, so set -e does not end the script before the
  # rollback: "up" itself fails when migrate exits non-zero or a dependency
  # is never healthy.
  if start "$tag" && healthy; then
    echo "$tag" > "$state"
    echo "Deployed $tag."
    exit 0
  fi

  echo "Not started or not healthy within ${DEPLOY_TIMEOUT:-300} s." >&2
  if [ -n "$previous" ]; then
    echo "Rolling back to $previous." >&2
    if start "$previous" && healthy; then
      echo "Rolled back to $previous." >&2
    else
      echo "The rollback did not come up either: docs/wdrozenie.md, \"Wycofanie wersji\"." >&2
    fi
  fi
  exit 1
}

main "$@"; exit
