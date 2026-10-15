---
id: SPEC-0060
title: CI roda os testes do cassino demo
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-15
parent: SPEC-0035
depends_on: [SPEC-0004, SPEC-0043]
consumes_contract: []
touches: [".github/workflows/ci.yml", "tests/ci/Ci.Tests/**"]
adrs: [ADR-0027]
external: []
size: S
approved_by: thomas (pré-aprovação da jornada do jogador, 2026-10-13)
approved_at: 2026-10-15
---

# SPEC-0060 — CI roda os testes do cassino demo

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Um PR que muda só o cassino demo (`src/demo/**` ou `tests/demo/**`) passa na CI sem rodar o job `backend`: os testes .NET do DemoCasino (entrada, lobby, página do jogo e, a seguir, a carteira) não rodam no GitHub. Visto no PR #36 (run 38748728309, SPEC-0045): `backend: skipped`. A SPEC-0047 só mexe em `src/demo`, então entraria sem nenhum teste .NET na CI. Esperado: mudança no cassino demo roda o job `backend` e o `e2e`.

## 2. Causa Raiz
O job `changes` de `.github/workflows/ci.yml` (dorny/paths-filter) define o grupo `dotnet` com `src/backend/**` e arquivos da solução, e o filtro `backend` com `tests/backend/**` e `tests/ci/**`. O cassino demo nasceu depois (SPEC-0043, ADR-0027) em `src/demo/**` e `tests/demo/**`, que nunca entraram nos filtros, embora `DemoCasino` e `DemoCasino.Tests` estejam no `Apogeu.slnx` e sejam testados pelo `dotnet test Apogeu.slnx` do job `backend`.

## 3. Mudança Proposta
Muda: `src/demo/**` entra no grupo `dotnet` (dispara `backend`, `e2e` e `contracts`, como o resto do .NET) e `tests/demo/**` entra no filtro `backend`.

Não muda: os jobs, os comandos de teste, os runners e os demais filtros.

**Padrão seguido:** testes estáticos do workflow em `tests/ci/Ci.Tests/Unit/ContractsJobTests.cs` (`WorkflowFile`, `CiPipeline`)

**Rollback:** revert do commit

**Outras ocorrências:** Nenhuma (o `tools/ci/affected-tests` local já roda a suíte inteira para arquivos não mapeados).

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0060:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — os filtros atuais seguem cobertos pelos testes do workflow da SPEC-0004.
- **UT-01** — Dado o `ci.yml`, quando os filtros do job `changes` são lidos, então uma mudança só em `src/demo/DemoCasino/Program.cs` aciona os filtros `backend`, `e2e` e `contracts`, e uma mudança só em `tests/demo/DemoCasino.Tests/x.cs` aciona o filtro `backend`.
- Integração/E2E: N/A — o defeito está na configuração estática do workflow; a prova de ponta a ponta é a CI do PR rodar o job `backend` (G5).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-15). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01 com a tag `SPEC-0060:UT-01` e confirmar que falha pelo motivo certo
- [ ] Green: incluir `src/demo/**` no grupo `dotnet` e `tests/demo/**` no filtro `backend`
- [ ] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde, com o job `backend` rodando (G5), e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 49f5df7 | 2026-10-15 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:00.56]     Dado o build de produção do demo-site, quando os arquivos são ) — 364cbd2 | 2026-10-15 |
| G2 Green | PASS | build exit 0 (✓ built in 279ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 8768804 | 2026-10-15 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.04s (tests 96%, transform 2%, import 1%)) — 8768804 | 2026-10-15 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (sem bloqueantes). Minors: alias de âncora inexistente vira glob literal sem erro (PathsFilter.cs:113-120); matcher não trata {a,b}, [...] nem ! (documentar o subconjunto); ExplicitCapture inócuo; Theory só com casos positivos (o Fact de sanidade cobre negativos); contracts acionado por src/demo é um pouco amplo, como decidido na spec — 8768804 | 2026-10-15 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #37 verde (run 38762211508, commit 858842e) com o job backend rodando (suíte .NET completa + Performance) por causa do ci.yml alterado — prova da correção; frontend, contracts, e2e, security e sdd verdes; merge a44a7f7 | 2026-10-15 |
| H2 Integração aprovada | PASS | aprovado por thomas — H2 auto-on-green (CI verde do PR #37, run 38762211508); decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) | 2026-10-15 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem e deploy) | 2026-10-15 |
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

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
