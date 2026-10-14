@AGENTS.md

# Apogeu — guia para o Claude Code

Provedor B2B de crash games: monólito modular .NET 10 (`src/backend`) + workspace pnpm (`src/frontend`). As decisões estão em `docs/adr/` e as specs em `docs/specs/`; o processo SDD está descrito em `AGENTS.md`.

## Comandos (raiz do repositório)

```bash
dotnet build Apogeu.slnx                        # 0 avisos, 0 erros
dotnet test Apogeu.slnx                         # suíte .NET (integração lenta: git, dotnet e pnpm reais)
python3 tools/ci/affected-tests                 # só os testes afetados pela mudança (--dry-run lista os comandos)
dotnet format Apogeu.slnx --verify-no-changes   # formatação C#
pnpm -C src/frontend install --frozen-lockfile
pnpm -C src/frontend build
pnpm -C src/frontend lint
pnpm -C src/frontend typecheck
pnpm -C src/frontend format:check
```

Limites locais dos testes de desempenho (`Category=Performance`): `APOGEU_PERF_BUDGET_SCALE` é o fator da máquina (decimal > 0, ex.: `5` ou `1.5`), no perfil do shell; sem ela vale 1 (máquina de referência) e na CI é ignorada (cada teste mantém o limite de CI) — SPEC-0052.

## Regras de código

- Avisos de compilação são erros (`TreatWarningsAsErrors`); não suprima regra sem motivo na própria linha.
- Versões de pacote só em `Directory.Packages.props`; pacote não previsto nos ADRs exige ADR e emenda. Atualize os `packages.lock.json` e o `pnpm-lock.yaml` junto com a mudança.
- Módulos se referenciam só pelos projetos `<Modulo>.Contracts` (ADR-0001); `Fairness.Domain` e os demais `Domain` não referenciam ASP.NET Core, EF Core nem Wolverine.
- TypeScript estrito; proibidos `any` explícito, `@ts-ignore` e `console.log`. `packages/contracts` é gerado (ADR-0008), nunca editado à mão.
- Commits em Conventional Commits com `Refs: SPEC-NNNN`; ative os hooks com `git config core.hooksPath .githooks`.
