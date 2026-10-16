---
id: SPEC-0015
title: Proteção da branch main no GitHub
tier: lite
type: feature
user_facing: false
status: approved
created: 2026-10-08
parent: SPEC-0001
depends_on: [SPEC-0004]
consumes_contract: []
touches: ["tools/ci/**", "tests/ci/**"]
adrs: [ADR-0011]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0015 — Proteção da branch main no GitHub

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: feature | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Hoje a `main` não tem proteção no servidor: o repositório é privado no GitHub Free e a API de proteção de branch e de rulesets devolve 403 ("Upgrade to GitHub Pro or make this repository public"). A emenda v2 da SPEC-0004 tirou de lá a proteção e o IT-06. Esperado: PR obrigatório, checks `ci/*` obrigatórios, sem push direto nem force-push, merge commit — aplicados pelo GitHub.

## 2. Causa Raiz
Limitação do plano do GitHub, não do código. Enquanto ela existir, o controle compensatório é de processo: a CI roda em todo PR, o Architect só mergeia após G5 verde e `validate` aponta commits diretos na `main`.

## 3. Mudança Proposta
Quando o repositório tiver plano que permita (GitHub Pro ou repositório público): script `tools/ci/protect-main` aplica o ruleset da `main` via `gh api` (PR obrigatório, checks `ci/backend`, `ci/frontend`, `ci/e2e`, `ci/security`, `ci/contracts`, `ci/sdd` obrigatórios, sem push direto nem force-push, só merge commit) e o IT-06 volta à suíte `CiLive`. Não muda os workflows da SPEC-0004.

**Padrão seguido:** testes `CiLive` de `tests/ci/Ci.Tests/Live` (SPEC-0004) e scripts de `tools/ci`.

**Rollback:** remover o ruleset (`gh api -X DELETE repos/thomasravache/crash-games-provider/rulesets/<id>`) e reverter o commit.

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0015:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — configuração nova no GitHub, sem código existente afetado.
- **UT-01** — Dado o ruleset gerado por `tools/ci/protect-main --dry-run`, quando o JSON é inspecionado, então exige PR sem número mínimo de aprovações, os seis checks `backend`, `frontend`, `e2e`, `security`, `contracts` e `sdd` (nomes reais dos jobs da CI; Emenda v1), bloqueia push direto, force-push e exclusão da `main` e permite só merge commit.
- **IT-01** — Dada a proteção aplicada na `main`, quando se tenta push direto na `main`, então o GitHub rejeita (antigo SPEC-0004:IT-06); e um PR com check vermelho fica com merge bloqueado.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Quando o repositório passa a ter plano que permita proteção (GitHub Pro ou público)? — Sem data; a spec fica aprovada e parada por impedimento externo até o repositório ter GitHub Pro ou ficar público (thomas, 2026-10-09)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-15). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01 e IT-01 com a tag `SPEC-0015:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: `tools/ci/protect-main` (dry-run e aplicação idempotente) e aplicação do ruleset na `main`
- [ ] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A — configuração do GitHub aplicada pelo script, sem ambiente de deploy até a SPEC-0012
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ea23fac (árvore suja) | 2026-10-09 |
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
| IMP-01 | 2026-10-09 | G1 | externo | Branch protection e rulesets indisponíveis no GitHub Free para repositório privado (API 403) | Consulta a branches/main/protection e rules/branches/main (SPEC-0004 IMP-01) | thomas: GitHub Pro ou repositório público | Repositório público desde 2026-10-15 (decisão de thomas): a API de rulesets fica disponível; thomas autorizou ativar a proteção (2026-10-15) | 2026-10-15 |

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
| 1 | 2026-10-15 | Checks obrigatórios com os nomes reais dos jobs (`backend`, `frontend`, `e2e`, `security`, `contracts`, `sdd`, sem prefixo `ci/`); PR obrigatório sem número mínimo de aprovações (um só mantenedor; a aprovação humana do processo é o H2); exclusão da `main` também bloqueada | Nomes conferidos nos check-runs da `main`; revisão obrigatória travaria todo merge com um só mantenedor | Nenhuma | decisão recomendada em piloto automático; thomas autorizou ativar a proteção (2026-10-15) |
