# Deployment tests (SPEC-0066)

| Test | What | Runs in |
|---|---|---|
| `Unit/CaddyfileTests` (UT-01) | `infra/caddy/Caddyfile` under `caddy validate` and `caddy adapt` of `caddy:2.11.7` (Docker): only the three sites, `/casino/*` → `demo-casino:8080`, api → `api:8080`, static files with SPA fallback, automatic HTTPS with the redirect on, HSTS / `X-Content-Type-Options` / `Referrer-Policy` | normal suite |
| `Unit/EnvFileTests` (UT-02) | `tools/deploy/vps-deploy --prepare`: `.env` with random secrets, mode 600, nothing printed, same values on a second run | normal suite |
| `Integration/DeployRollbackTests` (IT-01) | a compose project of its own (test ports, Caddy internal certificates for `*.localhost`): release A, B without health (goes back to A by itself), B, `vps-rollback` (back to A) | normal suite (Docker) |
| `Integration/BackupRestoreTests` (IT-02) | `backup` of a PostgreSQL with data (Testcontainers) and `restore` into a clean one: same row counts; 7-day retention | normal suite (Docker) |
| `Contract/ComposeContractTests` (CT-01) | the compose of SPEC-0065@2 (fixture with light images) read by `vps-deploy`: services, single network, volumes, variables; the proxy reaches `api` and `demo-casino` by name; a compose without a contract service is refused | normal suite (Docker) |
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
| `APOGEU_IMAGE_PREFIX` | prefix of the image names of a version | `apogeu` |
| `APOGEU_DEPLOY_BUILD` | build hook, relative to the root of the checkout and run there, in place of `aspire publish` + image builds | the real build |
| `APOGEU_DEPLOY_SMOKE` | smoke command (absolute path) in place of `tools/deploy/smoke` | `tools/deploy/smoke` |
| `APOGEU_DEPLOY_HEALTH_TIMEOUT` | seconds to wait for the health of a version before going back | chosen by the scripts |
| `APOGEU_POSTGRES_CONTAINER` | `backup`/`restore`: container of the PostgreSQL | the `postgres` service of the project |
| `APOGEU_BACKUP_DIR` | `backup`: directory of the dumps | `backups/` under the deployment directory |

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
- `backup`: dumps the database `apogeu` into `APOGEU_BACKUP_DIR` as `apogeu-*`, prints the path of the file as the last
  line, and removes the `apogeu-*` files older than 7 days of that directory. `restore <file>`: loads it into the
  PostgreSQL of `APOGEU_POSTGRES_CONTAINER`, creating the database when it does not exist.
