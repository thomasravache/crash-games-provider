---
id: SPEC-0048
title: Capacidade do cash-out em rajada na infraestrutura real
tier: lite
type: refactor
user_facing: false
status: proposed
created: 2026-10-11
parent: SPEC-0019
depends_on: []
consumes_contract: []
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**", "src/backend/Host/Program.cs"]
adrs: [ADR-0019]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0048 — Capacidade do cash-out em rajada na infraestrutura real

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Na CI (runner do GitHub, 2 vCPU, PostgreSQL em container no mesmo host), a rajada de 300 cash-outs em 1 s da SPEC-0023 (IT-05) tem p99 ≈ 807 ms e p50 ≈ 420 ms (PR #25); no ambiente de referência local o p99 fica ≤ 50 ms. O alvo da SPEC-0023 §5 (p99 ≤ 50 ms) precisa valer na infraestrutura em que o produto vai rodar, não só na máquina do desenvolvedor.

## 2. Causa Raiz
Hipótese a confirmar com medição: o pool de conexões por módulo é limitado (padrão 10, ADR-0019) e cada cash-out faz a transação do cash-out e, no caminho manual, a tentativa de crédito em linha; numa rajada as requisições fazem fila no pool. CPU do runner compartilhada com o PostgreSQL.

## 3. Mudança Proposta
Medir onde o tempo vai (pool, transação, crédito em linha, relay) e reduzir: dimensionar o pool por carga esperada, responder ao jogador após gravar a decisão (crédito sempre pelo handler durável) ou agrupar escritas. O contrato do hub e as regras de dinheiro não mudam. Ao fim, o IT-05 volta a exigir p99 ≤ 50 ms também na CI (ou num ambiente de carga definido na SPEC-0012).

**Padrão seguido:** `src/backend/Modules/Betting/Betting.Features/CashOut/*`, ADR-0019 (pool limitado por módulo).

**Rollback:** revert do commit.

**Outras ocorrências:** A liquidação em massa da SPEC-0024 (IT-04) deve ser observada no mesmo runner.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0048:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — coberta pelos testes da SPEC-0023.
- **UT-01** — Dada a configuração de pool e o caminho de resposta escolhidos, quando o cash-out é decidido, então a resposta ao jogador não espera a chamada à carteira (crédito pelo handler durável) e o resultado é o mesmo do contrato v2.
- **IT-01** — Dados 300 cash-outs em 1 s no runner da CI, quando medidos, então p99 ≤ 50 ms (o IT-05 da SPEC-0023 volta a usar 50 ms em qualquer ambiente).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [ ] O alvo de 50 ms deve valer no runner gratuito da CI ou só num ambiente de carga dedicado (SPEC-0012, custo)? (thomas)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PENDING | | |
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
