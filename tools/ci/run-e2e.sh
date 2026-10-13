#!/usr/bin/env bash
# E2E against the real backend (SPEC-0010, ADR-0003/ADR-0009/ADR-0013): sdd-config `test_e2e` and the `ci / e2e` job.
#
# Usage: tools/ci/run-e2e.sh [playwright test arguments...]   e.g. tools/ci/run-e2e.sh --project=chromium
#
# 1. Builds and starts the Aspire AppHost (postgres, redis, keycloak, api, game-pipa, ...) in the background, with every
#    port picked free at random (the API through AppHost__ApiHttpPort; dashboard and its OTLP/resource endpoints too),
#    so it never collides with a developer's own AppHost or another run on the same machine.
# 2. Waits until GET {api}/health answers 200 and the game behind GET {api}/dev/launch-demo answers too; then until the
#    demo-site (port fixed through AppHost__DemoSiteHttpPort) answers and forwards /casino to the DemoCasino (SPEC-0043).
# 3. Exports APOGEU_API_URL and APOGEU_DEMO_SITE_URL and runs `pnpm -C src/frontend test:e2e "$@"` (the harness keeps its own webServer).
# 4. Always stops the AppHost on exit; Aspire (DCP) then removes the containers it started (the postgres data volume
#    stays, as in a normal local run).
#
# The AppHost console goes to a log file, printed only on failure and with the dashboard login token redacted.
# Requirements: .NET SDK of global.json, pnpm with src/frontend installed, Playwright browsers, a Docker engine, python3.
# Environment: APOGEU_E2E_STARTUP_TIMEOUT (seconds, default 300).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$repo_root"

startup_timeout="${APOGEU_E2E_STARTUP_TIMEOUT:-300}"
log_dir="$(mktemp -d "${TMPDIR:-/tmp}/apogeu-e2e.XXXXXX")"
apphost_log="$log_dir/apphost.log"
apphost_pid=""

log() { printf '[run-e2e] %s\n' "$*" >&2; }

free_port() {
  python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()'
}

# Tail of the AppHost console without the dashboard login token (?t=...).
show_apphost_log() {
  if [[ -s "$apphost_log" ]]; then
    log "last lines of the AppHost console:"
    tail -n 80 "$apphost_log" | sed -E 's/([?&]t=)[^[:space:]&]+/\1[redacted]/g' >&2
  fi
}

# Descendants of a process (depth first), so a hard stop reaches the AppHost under `dotnet run`.
descendants() {
  local child
  for child in $(pgrep -P "$1" 2>/dev/null || true); do
    descendants "$child"
    printf '%s\n' "$child"
  done
}

stop_apphost() {
  local status=$?
  trap - EXIT INT TERM
  if [[ -n "$apphost_pid" ]] && kill -0 "$apphost_pid" 2>/dev/null; then
    log "stopping the AppHost"
    local tree member orchestrator
    tree="$(descendants "$apphost_pid") $apphost_pid"
    # The orchestrator (DCP) detaches from the tree and watches the AppHost by pid (`--monitor <pid>`): add it and its
    # children, so the wait below covers the removal of the containers.
    for member in $tree; do
      for orchestrator in $(pgrep -f -- "--monitor $member( |\$)" 2>/dev/null || true); do
        tree="$tree $(descendants "$orchestrator") $orchestrator"
      done
    done
    # Graceful first: SIGTERM (a background job of a script ignores SIGINT) to `dotnet run` and the AppHost under it;
    # the AppHost asks DCP to stop its processes and remove its containers.
    # shellcheck disable=SC2046 # word splitting of the pid list is intended
    kill -TERM "$apphost_pid" $(pgrep -P "$apphost_pid" 2>/dev/null || true) 2>/dev/null || true
    # The orchestrator outlives the AppHost for a while as it removes the containers: wait for all of it.
    local waited=0 pid alive
    while ((waited < 180)); do
      alive=""
      for pid in $tree; do
        if kill -0 "$pid" 2>/dev/null; then alive="$pid"; fi
      done
      [[ -z "$alive" ]] && break
      sleep 1
      waited=$((waited + 1))
    done
    if [[ -n "$alive" ]]; then
      log "the AppHost did not stop in time; killing it and removing its containers"
      # shellcheck disable=SC2086 # word splitting of the pid list is intended
      kill -KILL $tree 2>/dev/null || true
      # DCP labels what it creates with its own pid: remove those containers and networks.
      local label="com.microsoft.developer.usvc-dev.creatorProcessId"
      for pid in $tree; do
        # shellcheck disable=SC2046 # word splitting of the id list is intended
        docker rm --force $(docker ps --all --quiet --filter "label=$label=$pid") >/dev/null 2>&1 || true
        # shellcheck disable=SC2046 # word splitting of the id list is intended
        docker network rm $(docker network ls --quiet --filter "label=$label=$pid") >/dev/null 2>&1 || true
      done
    fi
    wait "$apphost_pid" 2>/dev/null || true
  fi
  if ((status != 0)); then
    show_apphost_log
  fi
  rm -rf "$log_dir"
  exit "$status"
}
trap stop_apphost EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

log "building the AppHost and the DemoCasino"
# The DemoCasino is not a project reference of the AppHost (ADR-0027) and Aspire runs it with --no-build: build it too.
for project in src/backend/AppHost/AppHost.csproj src/demo/DemoCasino/DemoCasino.csproj; do
  dotnet build "$project" --nologo -v quiet >"$log_dir/build.log" 2>&1 || {
    cat "$log_dir/build.log" >&2
    exit 1
  }
done

api_port="$(free_port)"
api_url="http://localhost:$api_port"
demo_site_port="$(free_port)"
demo_site_url="http://localhost:$demo_site_port"

log "starting the AppHost (api on $api_url)"
# No launch profile: its fixed dashboard ports would collide with another AppHost on the machine.
ASPNETCORE_ENVIRONMENT=Development \
  DOTNET_ENVIRONMENT=Development \
  ASPNETCORE_URLS="http://127.0.0.1:$(free_port)" \
  ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL="http://127.0.0.1:$(free_port)" \
  ASPIRE_RESOURCE_SERVICE_ENDPOINT_URL="http://127.0.0.1:$(free_port)" \
  ASPIRE_ALLOW_UNSECURED_TRANSPORT=true \
  AppHost__ApiHttpPort="$api_port" \
  AppHost__DemoSiteHttpPort="$demo_site_port" \
  dotnet run --project src/backend/AppHost/AppHost.csproj --no-build --no-launch-profile \
  >"$apphost_log" 2>&1 </dev/null &
apphost_pid=$!

# Status code of a GET without following redirects ("000" while nothing answers).
http_status() {
  curl --silent --output /dev/null --write-out '%{http_code}' --max-time 5 "$1" 2>/dev/null || true
}

wait_until_ok() {
  wait_until_status "$1" "$2" 200
}

wait_until_status() {
  local name="$1" url="$2" expected="$3" deadline=$((SECONDS + startup_timeout))
  log "waiting for $name ($url)"
  until [[ "$(http_status "$url")" == "$expected" ]]; do
    if ! kill -0 "$apphost_pid" 2>/dev/null; then
      log "the AppHost exited before $name was ready"
      exit 1
    fi
    if ((SECONDS >= deadline)); then
      log "$name was not ready after ${startup_timeout}s"
      exit 1
    fi
    sleep 2
  done
}

wait_until_ok "the api health" "$api_url/health"

# The game (Vite dev server under Aspire) starts apart from the api: wait for the origin /dev/launch-demo redirects to.
# The launch token in the Location header is never printed; only the origin is kept.
game_origin="$(curl --silent --output /dev/null --write-out '%{redirect_url}' --max-time 10 "$api_url/dev/launch-demo" |
  sed -E 's#^(https?://[^/]+).*#\1#')"
if [[ -z "$game_origin" ]]; then
  log "GET /dev/launch-demo did not redirect to the game"
  exit 1
fi
wait_until_ok "game-pipa" "$game_origin/"

# SPEC-0043: the demo-site (Vite dev server under Aspire) and, through its /casino proxy, the DemoCasino: GET /casino/me
# without a session answers 401 once both are up.
wait_until_ok "demo-site" "$demo_site_url/"
wait_until_status "the DemoCasino behind the demo-site" "$demo_site_url/casino/me" 401

export APOGEU_API_URL="$api_url"
export APOGEU_DEMO_SITE_URL="$demo_site_url"
log "running the E2E (APOGEU_API_URL=$APOGEU_API_URL, APOGEU_DEMO_SITE_URL=$APOGEU_DEMO_SITE_URL)"
pnpm -C src/frontend test:e2e "$@"
