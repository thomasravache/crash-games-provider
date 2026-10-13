---
id: SPEC-0051
title: Gates locais só com os testes afetados pela mudança
tier: lite
type: refactor
user_facing: false
status: approved
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
Novo script `tools/ci/affected-tests` (Python, sem dependências): recebe a base (padrão `sdd/SPEC-0001`), lista os arquivos alterados (`git diff --name-only base...HEAD` + não commitados) e mapeia para alvos: módulo .NET `src/backend/Modules/X/**` → `tests/backend/Modules/X.Tests` + Architecture.Tests; `src/backend/BuildingBlocks|ServiceDefaults|Host|AppHost/**`, `Directory.*.props`, `*.slnx`, `global.json` ou lock files → solução inteira; `src/frontend/**` → `pnpm -C src/frontend test`; `tests/**` → o próprio projeto de teste; só `docs/**` → nada a testar. Executa com a mesma separação de `Category=Performance` (`-m:1`). Os comandos `test` e `coverage` do sdd-config passam a chamar o script; `test_integration`, `test_e2e`, `arch_test` e `security_scan` (usados no G5) não mudam e continuam rodando tudo. Na CI nada muda.

**Padrão seguido:** `tools/ci/check-licenses` e `tools/ci/run-e2e.sh`; `tests/ci/Ci.Tests` (testes dos scripts de CI).

**Rollback:** revert do commit (o sdd-config volta aos comandos da solução inteira).

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0051:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — a CI não muda; os gates G5 continuam com a suíte inteira.
- **UT-01** — Dados conjuntos de arquivos alterados (só CSS do front; só o módulo Betting; Betting + Rounds; BuildingBlocks; `Directory.Packages.props`; só `docs/`; só um teste), quando o script calcula os alvos, então devolve exatamente: frontend; Betting.Tests + Architecture.Tests; Betting.Tests + Rounds.Tests + Architecture.Tests; solução inteira; solução inteira; nada; o projeto daquele teste — e, na dúvida (arquivo não mapeado), a solução inteira.
- **IT-01** — Dado um repositório git temporário com uma branch base e um commit que altera só um arquivo do módulo Betting, quando o script roda com `--dry-run`, então imprime só os comandos de Betting.Tests (com e sem `Category=Performance`) e Architecture.Tests e sai com 0; com uma alteração em `Directory.Packages.props`, imprime os comandos da solução inteira.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Pode reduzir os gates locais mantendo a suíte inteira na CI? — Sim, mantendo a qualidade (thomas, 2026-10-12)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
**Fase 1: Seleção por impacto**
- [ ] Red: escrever UT-01 e IT-01 em `tests/ci/Ci.Tests` com a tag `SPEC-0051:<ID>` e confirmar que falham (script inexistente)
- [ ] Green: `tools/ci/affected-tests` (Python stdlib) seguindo `tools/ci/check-licenses`; `test`/`coverage` do sdd-config chamam o script
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline (G6: N/A — ferramenta local)
- [ ] Relatório de Entrega, CLAUDE.md e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 8ec0e8a | 2026-10-12 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:12.06]     Dada uma rodada encerrada com Postgres real, quando GET /verif) — 112e042 | 2026-10-13 |
| G2 Green | PASS | build exit 0 (✓ built in 418ms); test exit 0 ($ dotnet test tests/ci/Ci.Tests/Ci.Tests.csproj --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (affected-tests: no coverage report produced) — da2d6a2 | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  941ms (tests 97%, transform 2%, import 1%)) — da2d6a2 | 2026-10-13 |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
