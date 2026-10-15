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
