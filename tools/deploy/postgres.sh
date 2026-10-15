# shellcheck shell=bash disable=SC2034,SC2154  # variables used and script_name set by the scripts that source it
# PostgreSQL of the deployment for tools/deploy/backup and restore (SPEC-0066). Sourced after lib.sh, never run.

database="apogeu"
postgres_user="${APOGEU_POSTGRES_USER:-postgres}"

# The container: APOGEU_POSTGRES_CONTAINER, or the running postgres service of the compose project.
postgres_container() {
  if [[ -n "${APOGEU_POSTGRES_CONTAINER:-}" ]]; then
    printf '%s' "$APOGEU_POSTGRES_CONTAINER"
    return
  fi
  local id
  id="$(docker ps --quiet \
    --filter "label=com.docker.compose.project=$APOGEU_COMPOSE_PROJECT" \
    --filter "label=com.docker.compose.service=postgres" | head -n 1)"
  [[ -n "$id" ]] || die "no running postgres in the compose project $APOGEU_COMPOSE_PROJECT"
  printf '%s' "$id"
}

# Runs a PostgreSQL client inside the container. The compose of SPEC-0065 asks for a password on local connections too
# (scram-sha-256), so the client takes it from the POSTGRES_PASSWORD of the container itself: it never leaves the
# container nor reaches a command line or a log.
pg_exec() {
  local container="$1"
  shift
  docker exec --interactive "$container" sh -c 'PGPASSWORD="${POSTGRES_PASSWORD:-}" exec "$@"' pg_exec "$@"
}

# Data Protection key ring of the api (SPEC-0066 Emenda v3): without it the unrevealed seeds of the database are
# unreadable, so it is backed up and restored with every dump. The volume is read and written through a throwaway
# container of a small pinned image, never mounted on the host.
keys_volume="${APOGEU_KEYS_VOLUME:-${APOGEU_COMPOSE_PROJECT}_dataprotection-keys}"
keys_helper_image="busybox:1.37.0-musl"

# The keys archive of a dump: apogeu-<stamp>.dump → apogeu-keys-<stamp>.tar.gz in the same directory.
keys_archive_of() {
  local dump="$1" name stamp
  name="$(basename "$dump")"
  stamp="${name#apogeu-}"
  stamp="${stamp%%.*}"
  printf '%s/apogeu-keys-%s.tar.gz' "$(dirname "$dump")" "$stamp"
}

require_keys_volume() {
  docker volume inspect "$keys_volume" >/dev/null 2>&1 || die "no Docker volume $keys_volume (APOGEU_KEYS_VOLUME)"
}

# Writes the files of the keys volume (numeric owners and modes kept) as a tar.gz on stdout.
keys_export() {
  docker run --rm --network none --volume "$keys_volume:/keys:ro" "$keys_helper_image" \
    tar -czf - -C /keys .
}

# Replaces the files of the keys volume by the archive on stdin (owners and modes of the archive, e.g. 1654 of the api).
keys_import() {
  docker run --rm --interactive --network none --volume "$keys_volume:/keys" "$keys_helper_image" \
    sh -c 'set -e; find /keys -mindepth 1 -delete; tar -xzpf - -C /keys'
}
