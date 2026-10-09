---
id: SPEC-0017
title: Endurecimento dos testes e da CI da onda 3
tier: lite
type: refactor
user_facing: false
status: proposed
created: 2026-10-08
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: ["tests/backend/Contracts.Tests/**", "tests/ci/**", "tools/contracts/**", ".semgrepignore", "src/frontend/eslint.config.js", "src/frontend/package.json", ".github/workflows/ci.yml"]
adrs: [ADR-0008, ADR-0011]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0017 — Endurecimento dos testes e da CI da onda 3

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados minor/nit dos reviews G4 da onda 3 deixados fora do escopo: (1) testes com `WebApplicationFactory<Program>` em paralelo podem falhar sob carga (IT-03 da SPEC-0006 falhou uma vez); (2) o gerador de hubs não exercita objetos, enums e datas; (3) as regras novas do `tools/ci/check-workflows` (`pull_request_target`, `write-all`) não têm caso no UT-01 da SPEC-0004; (4) Semgrep no Docker ignora o `.gitignore` e acusa arquivos gerados quando roda local; (5) `tools/contracts/*.mjs` fora do lint/format; (6) `HubDiscovery` engole `ReflectionTypeLoadException` sem avisar; (7) PRs do Dependabot falham no `pr-check` por não citarem spec.

## 2. Causa Raiz
Contexto técnico: escopo das SPEC-0004 e SPEC-0006 fechado no G4; itens sem risco imediato.

## 3. Mudança Proposta
Coleção xUnit única sem paralelismo (ou fixture compartilhada) para os testes com `WebApplicationFactory`; método de cliente com objeto/enum/data no hub de fixture; dois casos novos no UT-01; `.semgrepignore` (artifacts, coverage, node_modules, bin, obj, dist); `tools/contracts/*.mjs` no lint e format do front; aviso em stderr para tipos que não carregam; tratamento dos PRs do Dependabot no job `sdd` (ex.: `[no-spec]` via título configurado ou exceção para o autor dependabot). Não muda contratos.

**Padrão seguido:** testes existentes de `tests/backend/Contracts.Tests` e `tests/ci/Ci.Tests/Unit`.

**Rollback:** revert dos commits.

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0017:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — os testes atuais já cobrem o comportamento.
- **UT-01** — Dado um workflow com `pull_request_target` ou `permissions: write-all`, quando `tools/ci/check-workflows` roda, então falha (novos casos do UT-01); dado um hub de fixture com método de cliente recebendo objeto com `Money`, `Multiplier`, data e enum, quando o contrato é gerado, então o TS tem `{ stake: Money; cashout: Multiplier; startedAt: string; phase: "betting" | "inFlight" }`.
- Integração/E2E: N/A — mudanças de harness e CI cobertas pelos unitários e pelo próximo PR.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1ddb579 (árvore suja) | 2026-10-08 |
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
