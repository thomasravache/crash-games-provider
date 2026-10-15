# shellcheck shell=bash disable=SC2034,SC2154  # variables used and script_name set by the scripts that source it
# Shared pieces of the deployment scripts of tools/deploy (SPEC-0066, ADR-0029). Sourced, never run.
#
# Configuration (environment; the defaults are the ones of the VPS — interface in tests/deploy/README.md):
#   APOGEU_DEPLOY_DIR       deployment directory: checkout (src/), releases/, state/, .env, backups/
#   APOGEU_DEPLOY_REPO      git repository the ref is fetched from
#   APOGEU_COMPOSE_PROJECT  compose project name
#   APOGEU_DOMAIN           base domain of cassino., jogo. and api.
#   APOGEU_HTTP_PORT        host ports of the proxy
#   APOGEU_HTTPS_PORT
#   APOGEU_BIND_ADDRESS     host address the proxy publishes those ports on (default 0.0.0.0)
#   APOGEU_IMAGE_PREFIX     prefix of the image names of a version
#
# Rule of every script: no value of the .env is ever printed. Compose only gets the .env through --env-file, the
# configuration is never rendered (`docker compose config` is read by python into memory, never shown).

APOGEU_DEPLOY_DIR="${APOGEU_DEPLOY_DIR:-$HOME/apogeu-deploy}"
APOGEU_DEPLOY_REPO="${APOGEU_DEPLOY_REPO:-git@github.com:thomasravache/crash-games-provider.git}"
APOGEU_COMPOSE_PROJECT="${APOGEU_COMPOSE_PROJECT:-apogeu}"
APOGEU_DOMAIN="${APOGEU_DOMAIN:-143-95-218-240.sslip.io}"
APOGEU_HTTP_PORT="${APOGEU_HTTP_PORT:-80}"
APOGEU_HTTPS_PORT="${APOGEU_HTTPS_PORT:-443}"
APOGEU_BIND_ADDRESS="${APOGEU_BIND_ADDRESS:-0.0.0.0}"
APOGEU_IMAGE_PREFIX="${APOGEU_IMAGE_PREFIX:-apogeu}"
export APOGEU_DEPLOY_DIR APOGEU_DEPLOY_REPO APOGEU_COMPOSE_PROJECT APOGEU_DOMAIN APOGEU_HTTP_PORT APOGEU_HTTPS_PORT \
  APOGEU_BIND_ADDRESS APOGEU_IMAGE_PREFIX

env_file="$APOGEU_DEPLOY_DIR/.env"
releases_dir="$APOGEU_DEPLOY_DIR/releases"
state_dir="$APOGEU_DEPLOY_DIR/state"

# Secrets of the .env (SPEC-0065@3 §6), generated on the first run.
secret_names=(OPERATORS_DEMO_KEY_ID OPERATORS_DEMO_KEY_SECRET PLAYER_SESSION_SIGNING_KEY POSTGRES_PASSWORD REDIS_PASSWORD)
# Variables of a version (release.env): they must come from the files of that version, never from the shell — an
# exported API_IMAGE would win over --env-file and a rollback would run the new images again.
release_names=(APOGEU_VERSION API_IMAGE DEMO_CASINO_IMAGE PROXY_IMAGE)
# Contract SPEC-0065@3: services and volumes of the published compose; keys-init is short-lived (it hands the keys
# volume to the api user), the others are long-running on the network of the proxy.
contract_services=(api demo-casino postgres redis keys-init)
networked_services=(api demo-casino postgres redis)
contract_volumes=(postgres-data dataprotection-keys)

log() { printf '[%s] %s\n' "$script_name" "$*" >&2; }
die() {
  log "error: $*"
  exit 1
}

# Random alphanumeric text: no shell, quote, $ or # characters (safe in the .env and in compose interpolation).
random_text() {
  local length="$1" text=""
  while ((${#text} < length)); do
    text+="$(head -c 96 /dev/urandom | base64 | tr -dc 'A-Za-z0-9')"
  done
  printf '%s' "${text:0:length}"
}

# Creates the deployment directory and, the first time, the .env (mode 600). An existing .env is kept as it is, only
# missing variables are appended (so a new variable of the contract does not need a manual edit).
prepare_deploy_dir() {
  umask 077
  mkdir -p "$APOGEU_DEPLOY_DIR" "$releases_dir" "$state_dir"
  local created=0
  if [[ ! -f "$env_file" ]]; then
    : >"$env_file"
    created=1
  fi
  chmod 600 "$env_file"
  append_missing OPERATORS_DEMO_KEY_ID "$(random_text 16 | tr '[:upper:]' '[:lower:]')"
  append_missing OPERATORS_DEMO_KEY_SECRET "$(random_text 48)"
  append_missing PLAYER_SESSION_SIGNING_KEY "$(random_text 64)"
  append_missing POSTGRES_PASSWORD "$(random_text 40)"
  append_missing REDIS_PASSWORD "$(random_text 40)"
  # Until the backoffice is published (SPEC-0036) nothing issues backoffice tokens; the authority only has to be a
  # valid address (SPEC-0065 Emenda v2).
  append_missing BACKOFFICE_AUTHORITY "https://auth.$APOGEU_DOMAIN/realms/apogeu"
  append_missing PUBLIC_API_URL "https://api.$APOGEU_DOMAIN"
  append_missing PUBLIC_GAME_URL "https://jogo.$APOGEU_DOMAIN"
  if ((created)); then
    log "created $env_file (mode 600) with new random secrets"
  else
    log "kept the existing $env_file"
  fi
}

append_missing() {
  local name="$1" value="$2"
  if ! grep -q "^${name}=" "$env_file"; then
    printf '%s=%s\n' "$name" "$value" >>"$env_file"
  fi
}

# docker compose of a version: its compose files and release.env, the .env of the host, never variables of the shell
# for the version or the secrets.
compose_release() {
  local release="$1"
  shift
  local dir="$releases_dir/$release" unset_args=() name
  for name in "${release_names[@]}" "${secret_names[@]}"; do
    unset_args+=(-u "$name")
  done
  env "${unset_args[@]}" docker compose \
    --project-name "$APOGEU_COMPOSE_PROJECT" \
    --project-directory "$dir" \
    --env-file "$env_file" \
    --env-file "$dir/release.env" \
    --file "$dir/docker-compose.yaml" \
    --file "$dir/compose.proxy.yaml" \
    "$@"
}

# Brings a version up and waits for the health of its containers (healthchecks of the compose, or running).
up_release() {
  local release="$1" timeout="${APOGEU_DEPLOY_HEALTH_TIMEOUT:-300}"
  log "starting version $release (health wait up to ${timeout}s)"
  compose_release "$release" up --detach --remove-orphans --wait --wait-timeout "$timeout"
}

# Smoke of the version on the air: tools/deploy/smoke, or APOGEU_DEPLOY_SMOKE.
run_smoke() {
  local smoke="${APOGEU_DEPLOY_SMOKE:-$script_dir/smoke}"
  log "smoke: $(basename "$smoke")"
  APOGEU_DOMAIN="$APOGEU_DOMAIN" APOGEU_HTTPS_PORT="$APOGEU_HTTPS_PORT" "$smoke"
}

read_state() {
  local file="$state_dir/$1"
  if [[ -s "$file" ]]; then
    cat "$file"
  fi
}

write_state() {
  printf '%s\n' "$2" >"$state_dir/$1.tmp"
  mv "$state_dir/$1.tmp" "$state_dir/$1"
}

# Exclusive lock of the deployment directory: one deploy, rollback or restore at a time.
take_lock() {
  mkdir -p "$APOGEU_DEPLOY_DIR"
  exec 9>"$APOGEU_DEPLOY_DIR/.lock"
  if ! flock --nonblock 9; then
    die "another deploy or rollback is running on $APOGEU_DEPLOY_DIR"
  fi
}

# Memory of the host after a change (ADR-0029: the VPS is shared).
report_memory() {
  if command -v free >/dev/null; then
    log "memory of the host:"
    free -m | sed -n '1,2p' >&2
  fi
}
