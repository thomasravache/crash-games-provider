---
id: SPEC-0051
title: Gates locais só com os testes afetados pela mudança
tier: lite
type: refactor
user_facing: false
status: in-progress
created: 2026-10-12
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: ["tools/ci/affected-tests", "docs/specs/sdd-config.yml", "tests/ci/Ci.Tests/**", "CLAUDE.md"]
adrs: [ADR-0003]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0051 — Gates locais só com os testes afetados pela mudança

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Atual: os comandos `build`, `test` e `coverage` do `sdd-config.yml` rodam a solução inteira (todos os projetos .NET, Performance com `-m:1`, frontend) em todo gate local (G1, G2), mesmo para mudanças de 2 arquivos de CSS — 15 a 25 min por rodada, e falhas por carga quando a máquina está ocupada. Esperado: localmente, os gates rodam só os projetos de teste afetados pelos arquivos alterados desde a branch de integração; a suíte completa continua rodando na CI do GitHub em toda PR (G5) e no G5 local.

## 2. Causa Raiz
Os comandos de gate em `docs/specs/sdd-config.yml` apontam para `Apogeu.slnx` e para o workspace inteiro; não há seleção por impacto.

## 3. Mudança Proposta
Novo script `tools/ci/affected-tests` (Python, sem dependências): recebe a base (padrão `sdd/SPEC-0001`, depois `origin/sdd/SPEC-0001`, depois `main`), lista os arquivos alterados (`git diff --name-only --no-renames base...HEAD` + não commitados, para que uma renomeação conte o caminho antigo e o novo) e mapeia para alvos. **Emenda v2:** qualquer `src/backend/**` → solução inteira (o Host referencia todos os módulos e Security, Contracts, AppHost e os testes de módulo sobem o Host ou usam internals de outro módulo); `tests/**/Support/**` e `tests/backend/TestKit/**` → solução inteira (arquivos linkados em outros projetos de teste); `tools/contracts/**` → solução inteira (ADR-0008); `Directory.*`, `*.slnx`, `global.json`, lock files → solução inteira; config do front (`tsconfig*.json`, `eslint.config.*`, `package.json`, `pnpm-workspace.yaml`, `.prettierrc*`, `.dependency-cruiser.*`) → front + Repository.Tests; demais `src/frontend/**` e `tests/frontend/**` → `pnpm -C src/frontend test`; `tests/e2e/**` → front (a jornada E2E roda no G5 e na CI); demais `tests/backend/<P>/**` → o próprio projeto; `tests/ci/**`, `tools/ci/**`, `tools/sdd/**`, `.github/**` → Ci.Tests; só `docs/**` ou `*.md` da raiz → nada a testar; arquivo não mapeado ou nenhum arquivo → solução inteira. Executa com a mesma separação de `Category=Performance` (`-m:1`). Os comandos `test` e `coverage` do sdd-config (G1, G2) passam a chamar o script. **G5 local roda a suíte inteira:** o `test_integration` do sdd-config (só usado no G5) passa a ser a suíte completa (unitários, integração, contrato e Performance, como o `test` antigo), e `test_e2e`, `arch_test` e `security_scan` não mudam. O modo coverage avisa (stderr) quando um projeto selecionado com coverlet não gerou relatório. Na CI nada muda.

**Padrão seguido:** `tools/ci/check-licenses` e `tools/ci/run-e2e.sh`; `tests/ci/Ci.Tests` (testes dos scripts de CI).

**Rollback:** revert do commit (o sdd-config volta aos comandos da solução inteira).

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0051:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — a CI não muda; os gates G5 continuam com a suíte inteira.
- **UT-01** — Dados conjuntos de arquivos alterados (só CSS do front; um arquivo do módulo Betting; do Operators; do Fairness.Domain; BuildingBlocks; `Directory.Packages.props`; só `docs/`; só um teste do Wallet; um `Support/` linkado do Repository.Tests; `tools/contracts/`; `src/frontend/tsconfig.base.json`; `tools/ci/check-licenses`; um arquivo não mapeado), quando o script calcula os alvos, então devolve exatamente: frontend; solução; solução; solução; solução; solução; nada; Wallet.Tests; solução; solução; frontend + Repository.Tests; Ci.Tests; solução (Emenda v2).
- **IT-01** — Dado um repositório git temporário com uma branch base e um commit que altera só um arquivo do front, quando o script roda com `--dry-run`, então imprime só `pnpm -C src/frontend test` e sai com 0; com uma alteração em `Directory.Packages.props`, imprime os comandos da solução inteira; com uma renomeação `git mv` de um arquivo de `src/backend/Host/` para `src/frontend/`, imprime os comandos da solução inteira (Emenda v2).
- **IT-02** — Dado o repositório git temporário com uma alteração não commitada (e outra não rastreada) num teste do Wallet e um `pnpm`/`dotnet` falsos no PATH, quando o script roda sem `--dry-run`, então executa os comandos do Wallet.Tests e devolve o código de saída do primeiro que falhar; com `AFFECTED_BASE` apontando outra ref, usa essa base (Emenda v2).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Pode reduzir os gates locais mantendo a suíte inteira na CI? — Sim, mantendo a qualidade (thomas, 2026-10-12)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
**Fase 1: Seleção por impacto**
- [x] Red: escrever UT-01 e IT-01 em `tests/ci/Ci.Tests` com a tag `SPEC-0051:<ID>` e confirmar que falham (script inexistente)
- [x] Green: `tools/ci/affected-tests` (Python stdlib) seguindo `tools/ci/check-licenses`; `test`/`coverage` do sdd-config chamam o script
- [x] Refactor mantendo tudo verde
- [x] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy via pipeline (G6: N/A — ferramenta local)
- [x] Relatório de Entrega, CLAUDE.md e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 8ec0e8a | 2026-10-12 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:12.06]     Dada uma rodada encerrada com Postgres real, quando GET /verif) — 112e042 | 2026-10-13 |
| G2 Green | PASS | build exit 0 (✓ built in 170ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 7c473cf | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  762ms (tests 97%, transform 2%, import 1%)) — 7c473cf | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINORS (2ª revisão, 7c473cf; minors: G5 roda a suíte duas vezes, IT-02 não isola staged/não rastreado, aviso de cobertura e fallback origin/ sem teste, nome test_integration) — 7c473cf | 2026-10-13 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #30 verde (run 38351277297, commit b3a04b0): backend (suíte .NET completa + Performance), frontend, contracts, e2e (Chromium, WebKit e Firefox; Firefox E2E-04 da SPEC-0026 instável no 1º attempt, verde no re-run), security e sdd; local em 85dd922: build, arquitetura e segurança PASS; testes do AppHost e E2E locais falham só por conflito de portas 5080/5173 com o AppHost do thomas aberto desde 2026-10-12 08:03 (não encerrado por ser sessão dele) | 2026-10-13 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-13 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem); E2E com backend real na CI (PR #30, merge 4933105) | 2026-10-13 |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-13 | G4 | spec | Regra de mapeamento da §3 é insegura: o Host referencia todos os módulos e vários projetos de teste sobem o Host ou referenciam internals de outros módulos; o G5 local passou a usar o comando test seletivo | review G4 com --list e repositório git temporário | Architect + thomas | Emenda v2 — mapeamento seguro (backend, Support linkados e tools/contracts → solução; config do front → front + Repository.Tests; --no-renames; G5 local com a suíte inteira), aprovada por thomas | 2026-10-13 |

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
Os gates locais G1/G2 (`test` e `coverage` do sdd-config) rodam só os testes afetados pelos arquivos alterados desde a branch de integração; o G5 local (`test_integration`) e a CI do GitHub continuam rodando a suíte inteira. G2 de mudanças só de front, docs ou scripts de CI caiu de 15–25 min para ~1 min.

### Como foi feito
Script `tools/ci/affected-tests` (Python stdlib, padrão de `tools/ci/check-licenses`): diff `base...HEAD` com `--no-renames` + não commitados e não rastreados; mapeamento seguro (Emenda v2): todo `src/backend/**`, `tests/**/Support/**`, TestKit, `tools/contracts/**`, props, lock files e arquivo não mapeado → solução inteira; front → `pnpm test`; config do front → front + Repository.Tests; projeto de teste → ele mesmo; `tools/ci|sdd`, `.github` → Ci.Tests; docs → nada. Modo coverage com aviso quando um projeto com coverlet não gera relatório. `test_integration` passou a ser a suíte completa, usada só pelo G5.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | mapeamento de arquivos alterados para alvos (Emenda v2) | PASS | G2 do registro de gates; CI do PR #30 verde (run 38351277297, b3a04b0), merge 4933105 |
| IT-01 | seleção via git com --dry-run, inclusive renomeação | PASS | G2 do registro de gates; CI do PR #30 verde (run 38351277297, b3a04b0), merge 4933105 |
| IT-02 | execução com dotnet/pnpm falsos: não commitados, AFFECTED_BASE e código de saída | PASS | G2 do registro de gates; CI do PR #30 verde (run 38351277297, b3a04b0), merge 4933105 |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — N/A até a SPEC-0012; em main pelo PR #30
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [x] Outras ocorrências registradas como novas specs (ou nenhuma)
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
Ferramenta local de desenvolvimento; disponível em main (merge 4933105).

### Pendências
Minors do 2º review: o G5 roda a suíte duas vezes (o `test` cai em solução numa branch limpa); IT-02 não isola staged/não rastreado; aviso de cobertura e fallback `origin/` sem teste; a chave `test_integration` agora significa suíte completa (sem spec aberta).

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v2 | 2026-10-13 | Todo `src/backend/**`, `tests/**/Support/**` e `tools/contracts/**` → solução; config do front → front + Repository.Tests; `--no-renames`; base `origin/` antes de `main`; G5 local volta a rodar a suíte inteira via `test_integration`; aviso de cobertura ausente; UT-01/IT-01 revistos e IT-02 novo | Review G4: o mapeamento por módulo pulava testes que sobem o Host ou usam internals de outro módulo, renomeações escondiam o caminho antigo e o G5 local deixou de rodar tudo | Nenhuma | thomas (2026-10-13) |
