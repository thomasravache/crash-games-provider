---
id: SPEC-0024
title: "Liquidação da rodada e estorno"
tier: full
type: feature
user_facing: false
status: approved
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0023]
consumes_contract: [SPEC-0021@4]
contract_version: 2
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**", "src/backend/Host/Internal/Generated/**", "src/backend/Host/Program.cs", "src/backend/Modules/Rounds/Rounds.Features/Conductor/**", "src/backend/Modules/Rounds/Rounds.Features/CashOut/**"]
adrs: [ADR-0002, ADR-0007, ADR-0010, ADR-0017, ADR-0018, ADR-0028]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
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
- **Contratos consumidos:** SPEC-0021@4 (estorno idempotente).
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
  BetRefunded { betId, roundId, refunded:Money, refundStatus:"refunded"|"pending", balance:Money? }
    // balance nulo quando pending; BalanceUpdated (SPEC-0023) chega quando o estorno confirmar
Betting.Contracts
  IRoundSettlementQuery.GetAsync(roundId) → [RoundSettlement]
  RoundSettlement { roundId, operatorId, currency, betsCount, totalWagered:Money, totalPaid:Money, totalRefunded:Money, ggr:Money, settledAt }
Regras
  crash: active sem cash-out → lost (aposta com pedido de cash-out pendente só é liquidada depois da decisão do líder: TooLate → lost, aceito → cashed_out) ; anulada: placed|active não retiradas → refunded (estorno integral)
  ggr = totalWagered − totalPaid − totalRefunded ; resumo imutável e único por (roundId, operatorId, currency)
    (índice único; reprocessamento não altera o resumo)
  ordem no RoundCrashed: reconciliação dos automáticos (SPEC-0023) → active sem pedido de cash-out pendente → lost →
    apostas com pedido pendente ficam abertas e são fechadas pela decisão do líder (TooLate → lost; aceito → cashed_out)
  o resumo fecha quando não resta aposta aberta da rodada (placed, active ou com cash-out pendente); quem fecha a última
    aposta tenta fechar o resumo (idempotente)
  o resumo conta só as apostas da rodada fechadas por ela: lost, cashed_out e refunded (canceladas antes do voo não entram)
  fora do escopo: reserva pendente (débito incerto) numa rodada anulada é estornada pela varredura da SPEC-0022
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
| Contrato dos eventos | BetSettled e BetRefunded reais | conferem com os tipos gerados do hub | CT-01 |
| Cash-out pendente no crash | pedido de cash-out gravado (CashOutPending) quando a rodada crasha | aposta não vira lost antes da decisão; decidida pelo horário gravado (cashed_out ou lost) e o resumo só fecha depois | IT-05 |

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
- **IT-03** — Dada uma carteira que falha no estorno e um reinício do processo, quando a varredura roda, então BetRefunded chega com refundStatus "pending" e balance nulo, o estorno é confirmado uma única vez e BalanceUpdated chega ao jogador.
- **IT-04** — Dadas 5.000 apostas active numa rodada, quando o crash é liquidado, então termina em ≤ 2 s (`Category=Performance`).
- **IT-05** — Dada uma aposta active com pedido de cash-out pendente (líder impedido de responder) recebido antes do crash, quando a rodada crasha e o líder volta, então a aposta vira cashed_out com o multiplicador do horário gravado (não lost), o crédito é feito uma vez e o resumo da rodada inclui o pagamento; e um pedido gravado depois do crash termina lost.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens reais BetSettled e BetRefunded (refunded e pending) de IT-01 e IT-03, quando validadas contra os tipos gerados do hub /hubs/bets (SPEC-0006), então conferem (contrato consumido pela SPEC-0026). `IRoundSettlementQuery` não tem consumidor neste épico.

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

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0002, ADR-0007, ADR-0010, ADR-0017, ADR-0018 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0024:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0024:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1e9b543 (árvore suja) | 2026-10-09 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:09:42.54]     Dada uma carteira que falha no estorno e um reinício do proces) — 03821c2 | 2026-10-11 |
| G2 Green | FAIL | build exit 0 (✓ built in 380ms); test exit 0 (Duration  1.32s (tests 66%, transform 19%, import 14%, worker 2%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 1 ([xUnit.net 00:01:14.48]     Dados 300 automáticos em 1,50x com a carteira indisponível, qu) — d300099 | 2026-10-11 |
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0024`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 (inalterado) | 2026-10-11 | Escopo inclui o condutor e o árbitro do cash-out (Rounds) para a regra única de rodada anulada (TooLate nos dois caminhos — minor m-a do review da SPEC-0023); cash-out em aposta já perdida responde CASHOUT_TOO_LATE; tabela `betting.round_closures` marca que a Betting processou o fim da rodada | Implementação 28fb63b | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11) |
| 2 | 2026-10-11 | Contrato v2: `BetRefunded` com `refundStatus` e `balance` opcional (BalanceUpdated depois), `RoundSettlement.totalRefunded`; ordem da liquidação no RoundCrashed com cash-out pendente; fechamento do resumo pela última aposta fechada, único por índice; o resumo conta lost, cashed_out e refunded; reserva pendente em rodada anulada fica com a varredura da SPEC-0022; IT-03 ajustado | Scaffold 8a07dad: estorno pendente sem saldo confirmado, unicidade e fechamento do resumo indefinidos | SPEC-0026 (pin @1 → @2) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11) |
| 1 (inalterado) | 2026-10-11 | Interação com o cash-out pendente da SPEC-0023@2: aposta com pedido gravado só é liquidada depois da decisão do líder (IT-05); escopo inclui o código gerado do Wolverine no Host e o `Program.cs` (ADR-0028); scaffold em commit `chore` antes dos testes | A SPEC-0023 v2 introduziu CashOutPending e o Wolverine com código pré-gerado | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11) |
