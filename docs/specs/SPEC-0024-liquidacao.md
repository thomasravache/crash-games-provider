---
id: SPEC-0024
title: "Liquidação da rodada e estorno"
tier: full
type: feature
user_facing: false
status: proposed
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0023]
consumes_contract: [SPEC-0021@1]
contract_version: 1
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**"]
adrs: [ADR-0002, ADR-0007, ADR-0010, ADR-0017, ADR-0018]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0024 — Liquidação da rodada e estorno

## 1. Visão Geral
Fecha cada rodada: no crash, apostas ativas sem cash-out viram perdidas; numa rodada anulada, as apostas não retiradas são estornadas integralmente. Grava o resumo imutável da rodada por operador e moeda (apostado, pago, GGR) e garante que todo crédito e estorno pendente seja reenviado até confirmar.

## 2. Motivação & Escopo
**Motivação:** sem liquidação, apostas ficam em limbo e não há como auditar nem conciliar (ADR-0007, ADR-0018: toda rodada termina).

**Objetivos (dentro do escopo):**
- No `RoundCrashed`: apostas `active` sem cash-out → `lost` (sem movimento de carteira, o débito já ocorreu); evento `BetSettled` ao jogador.
- No `RoundAnnulled`: apostas `placed`/`active` não retiradas → estorno `bet:{betId}:rollback` via outbox → `refunded`; evento `BetRefunded`. Cash-outs já feitos permanecem.
- Resumo da rodada por operador e moeda: total apostado, total pago, GGR, quantidade de apostas — imutável.
- Varredura de pendências: créditos e estornos não confirmados são reenviados até confirmar, inclusive após reinício.

**Não-objetivos (fora do escopo):**
- Relatórios de backoffice (épico de backoffice); conciliação com operador real.

## 3. Dependências
- **Implementações necessárias:** SPEC-0023 (cash-out e créditos).
- **Contratos consumidos:** SPEC-0021@1 (estorno idempotente).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0007 (outbox, reenvio), ADR-0018 (rodada sempre termina; anulação com estorno). Implementação de referência: handlers de eventos do Betting da SPEC-0022/0023.

**Decisão:** liquidação dirigida pelos eventos de integração do motor, idempotente por `roundId` (reprocessar o mesmo evento não muda nada); resumo gravado na mesma transação que fecha a última aposta da rodada.

**Justificativa:** reprocessamento seguro e trilha auditável.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** liquidar por varredura periódica (atraso e corrida com cash-outs).

**ADRs:** ADR-0002, ADR-0007, ADR-0010, ADR-0017, ADR-0018.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** liquidar 5.000 apostas de uma rodada em ≤ 2 s — IT-04 (`Category=Performance`).
- **Segurança:** N/A — processamento interno.
- **Privacidade e dados pessoais:** N/A além do ID pseudônimo.
- **Disponibilidade e resiliência:** evento duplicado e reinício no meio da liquidação não duplicam estorno nem resumo — IT-02, IT-03.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** eventos ao jogador no hub `/hubs/bets` + `Betting.Contracts` (consulta do resumo).

```text
Servidor → jogador
  BetSettled  { betId, roundId, status:"lost" }
  BetRefunded { betId, roundId, refunded:Money, balance:Money }
Betting.Contracts
  IRoundSettlementQuery.GetAsync(roundId) → [RoundSettlement]
  RoundSettlement { roundId, operatorId, currency, betsCount, totalWagered:Money, totalPaid:Money, ggr:Money, settledAt }
Regras
  crash: active sem cash-out → lost ; anulada: placed|active não retiradas → refunded (estorno integral)
  ggr = totalWagered − totalPaid − totalRefunded ; resumo imutável e único por (roundId, operatorId, currency)
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Crash | apostas active retiradas e não retiradas | não retiradas → lost; BetSettled; retiradas intactas | UT-01, IT-01 |
| Anulação | apostas placed, active e uma já retirada | placed/active estornadas; retirada mantida | UT-02, IT-01 |
| Resumo | rodada liquidada | apostado, pago, estornado e GGR corretos e únicos | UT-03, IT-01 |
| Evento repetido | RoundCrashed/RoundAnnulled processado de novo | nada muda | IT-02 |
| Reinício e pendências | estorno falhou e o processo reiniciou | estorno reenviado até confirmar, uma vez só | IT-03 |
| Volume | 5.000 apostas | liquidadas em ≤ 2 s | IT-04 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — comportamento novo.

### 7.2 Testes Unitários
- **UT-01** — Dada uma rodada com 3 apostas active (1 retirada), quando o crash é processado, então 2 viram lost com BetSettled e a retirada não muda.
- **UT-02** — Dada uma rodada com apostas placed, active e uma retirada, quando a anulação é processada, então placed e active viram refunded com estorno integral e a retirada não muda.
- **UT-03** — Dadas apostas de 10,00, 20,00 e 30,00 com um pagamento de 24,70, quando o resumo é calculado, então apostado 60,00, pago 24,70 e GGR 35,30.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com duas rodadas (uma termina em crash e outra anulada), quando liquidadas, então estados, saldos e resumos batem com o livro-razão da carteira.
- **IT-02** — Dado um evento de crash entregue duas vezes, quando processado, então resumo e estados não mudam na segunda vez.
- **IT-03** — Dada uma carteira que falha no estorno e um reinício do processo, quando a varredura roda, então o estorno é confirmado uma única vez e BetRefunded chega ao jogador.
- **IT-04** — Dadas 5.000 apostas active numa rodada, quando o crash é liquidado, então termina em ≤ 2 s (`Category=Performance`).

### 7.4 Testes de Contrato
- N/A — eventos ao jogador seguem o contrato do hub gerado e são validados no CT-01 da SPEC-0026; `IRoundSettlementQuery` não tem consumidor neste épico.

### 7.5 Testes E2E
- N/A — a jornada do jogador (apostar, retirar, perder) é provada pelos E2E da SPEC-0026, que consome este contrato (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** carteira de teste com falha programada no IT-03.

**Ambiente de execução:** Testcontainers (Postgres, Redis).

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** tabela de resumo no schema `betting` (expand).
- **Compatibilidade:** eventos novos ao jogador (aditivo).
- **Observabilidade:** métricas `betting.settled{status}`, `betting.refunds_pending`, duração da liquidação; alerta para pendência > 1 min.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1e9b543 (árvore suja) | 2026-10-09 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0024`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
