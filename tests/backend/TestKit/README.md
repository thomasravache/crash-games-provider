# Test harness (TestKit)

Test harness of the repository (SPEC-0003, ADR-0009). Paths below are relative to `tests/`.

| Folder | What | Command (from the repository root) |
|---|---|---|
| `backend/Architecture.Tests` | ArchUnitNET rules of ADR-0001, ADR-0002 and ADR-0006, proven against fixture assemblies with known violations | `dotnet test Apogeu.slnx --filter Category=Architecture` |
| `backend/TestKit` | Shared fixtures: Testcontainers (PostgreSQL, Redis, Keycloak), `TestClock`, `DeterministicSeedSource`, `TestDataBuilder<T>`, `LogCapture` | used by the module test projects |
| `backend/Repository.Tests` | Repository conventions (SPEC-0002) | `dotnet test Apogeu.slnx` |
| `frontend` | Vitest projects `unit` and `arch` (dependency-cruiser rules of ADR-0004) | `pnpm -C src/frontend test`, `pnpm -C src/frontend test:arch` |
| `e2e` | Playwright on Chromium, WebKit mobile and Firefox, with @axe-core/playwright | `pnpm -C src/frontend test:e2e` |

## Categories (.NET)

- Unit and architecture: `dotnet test Apogeu.slnx --filter "Category!=Integration&Category!=Nightly"`
- `[Trait("Category", "Integration")]`: real dependencies in containers (Docker required): `--filter Category=Integration`
- `[Trait("Category", "Architecture")]`: architecture rules: `--filter Category=Architecture`
- `[Trait("Category", "Nightly")]`: long runs (RTP simulation), only in the nightly CI

## Traceability tag `SPEC-NNNN:<ID>`

Every test names the spec and the test plan ID it proves (section 7 of the spec), so `spec_graph.py verify` can trace
the plan to the code:

```csharp
[Fact(DisplayName = "Dado …, quando …, então …")]
[Trait("Spec", "SPEC-0003:UT-01")]
[Trait("Category", "Architecture")]
public void GivenWhenThen() { }
```

```ts
test("SPEC-0003:E2E-01 o app game-pipa abre com o título Pipa visível e sem violações axe", async ({ page }) => {});
```

The IDs are those of the spec's test plan (for example `UT-01`, `IT-01`, `E2E-01`). A test without a tag, or with an ID
that is not in the plan of its spec, is a review finding.
