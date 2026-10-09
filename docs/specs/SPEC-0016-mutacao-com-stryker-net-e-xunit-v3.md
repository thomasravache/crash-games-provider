---
id: SPEC-0016
title: Mutação com Stryker.NET e xUnit v3
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-08
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: ["stryker-config.json", ".config/dotnet-tools.json", "tools/ci/stryker-targets", "tests/backend/Modules/Fairness.Tests/Fairness.Tests.csproj", "Directory.Packages.props"]
adrs: [ADR-0011]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0016 — Mutação com Stryker.NET e xUnit v3

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Rodar `dotnet stryker` em `Fairness.Domain` (meta ≥ 85%, SPEC-0007 §7.6) dá 8% com "test coverage capture failed": nenhum mutante aparece coberto por teste, embora os vetores públicos exercitem `SeedChain` e `FairnessVerifier`. O comando padrão também falha porque `stryker-config.json` lista `Wallet.Tests`, que ainda não existe. Sem isso o §7.6 da SPEC-0007 não tem evidência e o G7 dela não fecha.

## 2. Causa Raiz
Provável incompatibilidade entre o runner vstest do Stryker.NET 5.0.0 e os testes em xUnit v3 (`xunit.v3.mtp-off`); o runner `mtp` encontrou 0 testes. Config estática aponta para projeto inexistente (SPEC-0003).

## 3. Mudança Proposta
Diagnóstico do Reviewer da SPEC-0007: runner `mtp` do Stryker com o projeto de teste em MTP (ou `coverage-analysis: off` com filtro tirando o UT-06), `mutate` restrito a `Fairness.Domain` e `break` = 85. Configurar o Stryker para capturar cobertura com xUnit v3 (runner/opções corretos, ou ajuste do projeto de teste), tirar da config fixa os projetos que não existem (o `tools/ci/stryker-targets` do nightly já filtra) e registrar o score real do `Fairness.Domain`. Não muda código de produção nem os testes da SPEC-0007.

**Padrão seguido:** `stryker-config.json` (SPEC-0003) e `tools/ci/stryker-targets` (SPEC-0004).

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0016:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — ferramenta de teste, sem comportamento de produção.
- **UT-01** — Dado `Fairness.Tests` com os vetores públicos, quando `dotnet stryker` roda em `Fairness.Domain`, então o relatório JSON mostra testes cobrindo os mutantes de `SeedChain.cs` e o score ≥ 85% (evidência do SPEC-0007 §7.6).
- Integração/E2E: N/A — a verificação é a própria execução do Stryker, local e no job `mutation` do nightly.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN (2026-10-09). -->

**Fase 1: Regressão**
- [ ] Red: escrever UT-01 com a tag `SPEC-0016:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: corrigir a causa seguindo o padrão existente
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
