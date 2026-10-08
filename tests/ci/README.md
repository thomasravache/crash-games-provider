# CI pipeline tests (SPEC-0004)

| Test | What | Runs in |
|---|---|---|
| `Unit/WorkflowSecurityTests` (UT-01) | actionlint 1.7.12 (local binary of that version, otherwise the `rhysd/actionlint:1.7.12` Docker image), every `uses:` pinned by a full commit SHA, read-only top-level `permissions`, and `tools/ci/check-workflows` | normal suite |
| `Unit/ContractsJobTests` (CT-01, static) | the `contracts` job of `ci.yml` runs `pnpm -C src/frontend contracts:check` | normal suite |
| `Live/*` (IT-01..IT-06, CT-01 live) | open real, disposable pull requests on `thomasravache/crash-games-provider` and assert the checks, the merge state, the images artifact and the branch protection | only on demand (`Category=CiLive`) |

## Live suite

The live tests spend GitHub Actions minutes and change GitHub (probe branches `ci-live/*`, PRs titled
`test(ci): ci-live probe <ID> [no-spec]`, closed and deleted at the end; IT-05 merges one PR with a single empty commit
into `main`). They are skipped unless `APOGEU_CI_LIVE=1`, run one at a time, and check their preconditions (workflow and
jobs present, token, workflow already on `main`, branch protection for IT-06) before touching GitHub. Run them after the
workflows are on `main` (rollout of SPEC-0004):

```bash
export GH_TOKEN="$(gh auth token --user thomasravache)"
APOGEU_CI_LIVE=1 dotnet test tests/ci/Ci.Tests/Ci.Tests.csproj --filter "Category=CiLive" --logger "console;verbosity=detailed"
```

One test: add `&FullyQualifiedName~PathFilterTests` (IT-01), `BrokenTestTests` (IT-02), `SecretScanTests` (IT-03),
`StaleContractsTests` (IT-04, CT-01), `ImagesOnMergeTests` (IT-05) or `DirectPushTests` (IT-06). The PR links are printed
as test output (evidence for the spec's Verificação table).
