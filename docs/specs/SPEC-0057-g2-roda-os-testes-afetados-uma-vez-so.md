---
id: SPEC-0057
title: G2 roda os testes afetados uma vez só
tier: lite
type: refactor
user_facing: "false"
status: approved
created: 2026-10-14
parent: SPEC-0001
depends_on: [SPEC-0051]
consumes_contract: []
touches: [tools/ci/affected-tests, docs/specs/sdd-config.yml, tests/ci/Ci.Tests/**, CLAUDE.md]
adrs: []
external: []
size: S
approved_by: thomas (item 1 escolhido no chat, 2026-10-14)
approved_at: 2026-10-14
---

# SPEC-0057 — G2 roda os testes afetados uma vez só

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
O G2 roda a suíte de testes afetada **duas vezes**. O `test` (`affected-tests`) roda os testes afetados sem cobertura e depois os de desempenho. Em seguida, o `coverage` (`affected-tests --mode coverage`) roda **os mesmos testes afetados de novo**, agora com cobertura, e gera o relatório. Com uma mudança que afeta a solução inteira, isso dobra a parte mais lenta do gate (Testcontainers, Host real). Num G2 de 15 a 30 minutos nesta VM, a segunda rodada não detecta nada que a primeira não detecte. O thomas decidiu em 2026-10-14 cortar a repetição sem tirar nenhum teste do G2 (item 1). A ideia de tirar os testes de desempenho de outras specs do G2 foi descartada.

## 2. Causa Raiz
A SPEC-0051 separou `test` e `coverage` em dois modos independentes do `tools/ci/affected-tests`, e cada um executa o `dotnet test` por conta própria. O `sdd-config.yml` chama os dois no G2 (`test` e `coverage`). Por isso as execuções se repetem.

## 3. Mudança Proposta
- **Muda:** o `tools/ci/affected-tests` passa a ter três modos:
  - `--mode coverage`: roda os testes afetados que não são de desempenho **com** cobertura, como hoje, mas **não** gera relatório;
  - `--mode performance` (novo): roda só os testes `Category=Performance` afetados, **sem** instrumentação e com `-m:1`, porque a cobertura distorce o tempo;
  - `--mode report` (novo): gera o relatório e aplica o limite de linhas alteradas a partir dos dados de cobertura já coletados, sem rodar nenhum teste. Se não houver dados de uma rodada `coverage` desta árvore, falha com uma mensagem clara.
- **No `sdd-config.yml`:** `test: "python3 tools/ci/affected-tests --mode coverage && python3 tools/ci/affected-tests --mode performance"` e `coverage: "python3 tools/ci/affected-tests --mode report"`. O modo `test` atual continua disponível para uso manual.
- **Não muda:**
  - o conjunto de testes do G2: todos os afetados, inclusive os de desempenho de qualquer spec;
  - o mínimo de cobertura;
  - o G5 e a CI, que continuam rodando a suíte completa;
  - o G1, que continua usando o `test` e espera red.

**Padrão seguido:** `tools/ci/affected-tests` (SPEC-0051), mapeamento seguro e modo `coverage` existentes; testes em `tests/ci/Ci.Tests`.

**Rollback:** revert do merge commit (o `sdd-config.yml` volta a chamar `test` + `coverage`).

**Outras ocorrências:** Nenhuma. O G5 usa `test_integration` (suíte completa, uma rodada).

## 4. Plano de Testes (TDD)
- Caracterização: N/A — os testes da SPEC-0051 em `tests/ci/Ci.Tests` cobrem o mapeamento e continuam valendo.
- **UT-01** — Dada uma mudança que afeta a solução inteira, quando o `affected-tests --dry-run` roda com `--mode coverage`, `--mode performance` e `--mode report`, então:
  - o `coverage` lista só o `dotnet test` com cobertura, sem `Category=Performance` e sem `reportgenerator`;
  - o `performance` lista só `--filter Category=Performance -m:1`, sem cobertura;
  - o `report` lista só o `reportgenerator` e a verificação do limite, sem nenhum `dotnet test`.
- **UT-02** — Dado um diretório sem dados de cobertura, quando o `--mode report` roda, então sai com código ≠ 0 e uma mensagem que manda rodar `--mode coverage` antes.
- **UT-03** — Dado o `docs/specs/sdd-config.yml`, quando lido, então o `test` do G2 encadeia `--mode coverage` e `--mode performance`, o `coverage` é `--mode report`, e somadas as duas linhas há exatamente uma execução de cada conjunto de testes.
- Integração/E2E: N/A — o G2 da própria spec, rodado com a ferramenta nova, é a prova de ponta a ponta.

## 5. Questões em Aberto
- [x] Cortar a rodada repetida da suíte no G2 sem tirar testes? — Sim, só o item 1; os testes de desempenho de outras specs continuam no G2 (thomas, 2026-10-14)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Uma rodada só**
- [ ] Red: UT-01, UT-02 e UT-03 com a tag `SPEC-0057:<ID>`, falhando pelo motivo certo
- [ ] Green: modos `performance` e `report`, `coverage` sem relatório e `sdd-config.yml`
- [ ] Validar: G2 desta spec com a ferramenta nova + Ci.Tests

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012, registrado no gate
- [ ] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ab04884 | 2026-10-14 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
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

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
