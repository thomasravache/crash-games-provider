# Deployment tests (SPEC-0066)

| Test | What | Runs in |
|---|---|---|
| `Unit/CaddyfileTests` (UT-01) | `infra/caddy/Caddyfile` under `caddy validate` and `caddy adapt` of `caddy:2.11.7` (Docker): only the three sites, `/casino/*` → `demo-casino:8080`, api → `api:8080`, static files with SPA fallback, automatic HTTPS with the redirect on, HSTS / `X-Content-Type-Options` / `Referrer-Policy`; no `trusted_proxies`, and forged `X-Forwarded-For/Host/Proto` from the client reach an echo backend (as `api` and `demo-casino`) replaced by the proxy's values (Emenda v3); the access log (`docker logs` of the proxy) keeps no `lt` nor `access_token` of the query, no Cookie and no Authorization, while the other query parameters stay (Emenda v4) | normal suite |
| `Unit/EnvFileTests` (UT-02) | `tools/deploy/vps-deploy --prepare`: `.env` with random secrets, mode 600, nothing printed, same values on a second run | normal suite |
| `Integration/DeployRollbackTests` (IT-01) | a compose project of its own (test ports, Caddy internal certificates for `*.localhost`): release A, B without health (goes back to A by itself), B, `vps-rollback` (back to A) | normal suite (Docker) |
| `Integration/BackupRestoreTests` (IT-02) | `backup` of a PostgreSQL with data (Testcontainers) and of a `dataprotection-keys` volume with keys, `restore` into a clean PostgreSQL and a clean volume: same row counts, identical keys (content, owner 1654, mode); a truncated keys archive is refused before the database and the volume are touched (Emenda v4); 7-day retention (Emenda v3) | normal suite (Docker) |
| `Contract/ComposeContractTests` (CT-01) | the compose of SPEC-0065@3 (fixture with light images, plus the short-lived `keys-init`) read by `vps-deploy`: services, single network, volumes, variables; `keys-init` exited 0 without ports and left the keys volume to 1654:1654; the proxy reaches `api` and `demo-casino` by name, publishes only on `APOGEU_BIND_ADDRESS` (127.0.0.1) and its Docker logs rotate (`max-size`, `max-file`); a compose without a contract service (`keys-init` included) or with any service other than `proxy` publishing ports (`api`, or an extra service) is refused before anything is created, naming the service (Emenda v4) | normal suite (Docker) |
| `e2e/carteira-publica.spec.ts` (E2E-01) | the wallet journey of SPEC-0047 against `https://cassino.143-95-218-240.sslip.io`, without the test hook (production build) | only with `APOGEU_E2E_PUBLISHED=1`, at G6 |

```bash
dotnet test tests/deploy/Deploy.Tests/Deploy.Tests.csproj
APOGEU_E2E_PUBLISHED=1 pnpm -C src/frontend exec playwright test --config ../../tests/deploy/e2e/playwright.config.ts
```

The tests never use the ports 80/443 nor the ports of a developer's AppHost: the proxy of a test deployment listens on
free ports picked at random, the compose project and the images are named `apogeu-test-*` and removed at the end.

## Interface of `tools/deploy` fixed by the tests

All scripts read their configuration from the environment; without it they use the values of the VPS.

| Variable | Meaning | Default (VPS) |
|---|---|---|
| `APOGEU_DEPLOY_DIR` | deployment directory, outside the working repository: checkout, releases, `.env`, backups | chosen by the scripts |
| `APOGEU_DEPLOY_REPO` | git repository the ref is fetched from | the GitHub repository |
| `APOGEU_COMPOSE_PROJECT` | compose project name | `apogeu` |
| `APOGEU_DOMAIN` | base domain of `cassino.`, `jogo.` and `api.`; also read by the Caddyfile as `{$APOGEU_DOMAIN}` | `143-95-218-240.sslip.io` |
| `APOGEU_HTTP_PORT`, `APOGEU_HTTPS_PORT` | host ports of the proxy | `80`, `443` |
| `APOGEU_BIND_ADDRESS` | host address the proxy publishes its ports on (the tests use `127.0.0.1`) | `0.0.0.0` |
| `APOGEU_IMAGE_PREFIX` | prefix of the image names of a version | `apogeu` |
| `APOGEU_DEPLOY_BUILD` | build hook, relative to the root of the checkout and run there, in place of `aspire publish` + image builds | the real build |
| `APOGEU_DEPLOY_SMOKE` | smoke command (absolute path) in place of `tools/deploy/smoke` | `tools/deploy/smoke` |
| `APOGEU_DEPLOY_HEALTH_TIMEOUT` | seconds to wait for the health of a version before going back | chosen by the scripts |
| `APOGEU_POSTGRES_CONTAINER` | `backup`/`restore`: container of the PostgreSQL | the `postgres` service of the project |
| `APOGEU_BACKUP_DIR` | `backup`: directory of the dumps | `backups/` under the deployment directory |
| `APOGEU_KEYS_VOLUME` | `backup`/`restore`: Docker volume of the Data Protection keys | `<project>_dataprotection-keys` |

- `vps-deploy [ref]` (default `main`): fetches the ref from `APOGEU_DEPLOY_REPO` into the deployment directory, creates the
  `.env` the first time, runs the build (the hook gets `APOGEU_VERSION`, `API_IMAGE`, `DEMO_CASINO_IMAGE` and `PROXY_IMAGE`,
  tagged with the commit, and leaves `infra/compose/out/docker-compose.yaml`), checks the compose against the contract
  SPEC-0065@2 (a missing service is refused before anything goes up, naming it), brings up the project with a `proxy`
  service (Caddy, the only one publishing ports, on the single network of the compose), waits for the health and runs
  the smoke. Any failure: exit ≠ 0 and the previous version back on the air. Never prints a value of the `.env`.
- `vps-deploy --prepare`: only creates the deployment directory and the `.env` (mode 600; random `OPERATORS_DEMO_KEY_ID`,
  `OPERATORS_DEMO_KEY_SECRET`, `PLAYER_SESSION_SIGNING_KEY`, `POSTGRES_PASSWORD`, `REDIS_PASSWORD`; `BACKOFFICE_AUTHORITY`,
  `PUBLIC_API_URL=https://api.$APOGEU_DOMAIN`, `PUBLIC_GAME_URL=https://jogo.$APOGEU_DOMAIN`), keeping an existing one.
- `vps-rollback`: the previous version back on the air (its images and compose, no rebuild).
- `backup` (Emenda v3): writes two files with the same UTC stamp into `APOGEU_BACKUP_DIR`: the dump of the database
  `apogeu` as `apogeu-<stamp>.dump` and the files of the keys volume (`APOGEU_KEYS_VOLUME`, owners and modes kept) as
  `apogeu-keys-<stamp>.tar.gz`; prints the path of the dump as the last line; removes the `apogeu-*` files (dumps and
  keys archives) older than 7 days of that directory, never other files.
- `restore <dump>`: loads the dump into the PostgreSQL of `APOGEU_POSTGRES_CONTAINER` (creating the database when it does
  not exist) and the `apogeu-keys-<stamp>.tar.gz` beside it into `APOGEU_KEYS_VOLUME`, with the same contents, owners
  (1654) and modes; a dump without its keys archive is refused (without the keys the unrevealed seeds are unreadable).
  The keys archive is checked first (Emenda v4): a corrupted or truncated one is refused with exit > 0 before the
  database is created or loaded and before the current keys of the volume are removed.
- Proxy (Emenda v4): the `proxy` service publishes `APOGEU_HTTP_PORT`/`APOGEU_HTTPS_PORT` on `APOGEU_BIND_ADDRESS` only,
  with Docker log rotation (`logging` with `max-size` and `max-file`); `vps-deploy` refuses, before creating any
  container, a compose where a service other than `proxy` publishes ports, naming it. The Caddy access log erases the
  `lt` and `access_token` query parameters (other parameters stay) and keeps Cookie and Authorization redacted.
