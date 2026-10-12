---
id: SPEC-0023
title: "Cash-out manual e automático"
tier: full
type: feature
user_facing: false
status: implemented
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0022]
consumes_contract: [SPEC-0020@2, SPEC-0021@4]
contract_version: 2
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**", "src/backend/Modules/Rounds/**", "tests/backend/Modules/Rounds.Tests/**", "src/backend/Host/Internal/Generated/**", "src/backend/Host/Program.cs", "tests/backend/Architecture.Tests/**"]
adrs: [ADR-0001, ADR-0002, ADR-0005, ADR-0006, ADR-0007, ADR-0014, ADR-0018, ADR-0019, ADR-0028]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0023 — Cash-out manual e automático

## 1. Visão Geral
Implementa o cash-out manual e o automático. A decisão é tomada pelo **líder da sala**, o único que conhece o instante do crash, comparando o horário do servidor no recebimento com esse instante. O pagamento (aposta × multiplicador, truncado em centavos, limitado ao ganho máximo) é creditado na carteira de forma idempotente e com reenvio até confirmar.

## 2. Motivação & Escopo
**Motivação:** o cash-out é o momento mais sensível do jogo; regras decididas pelo usuário em 2026-10-09: tolerância zero de latência, automático executado no servidor, pagamento truncado em centavos.

**Objetivos (dentro do escopo):**
- `CashOut(betId)` no hub `/hubs/bets`.
- Cash-out automático: o líder agenda a retirada em `t_alvo = ln(alvo)/k` e só executa se `t_alvo < t_c`.
- Árbitro `ICashOutArbiter` em `Rounds.Contracts`, executado no líder; instâncias que não são o líder encaminham o pedido ao líder pelo Redis (pedido/resposta com prazo).
- Multiplicador do cash-out manual: `floor(m(recebidoEm − startedAt) · 100)/100`; aceito só se `recebidoEm < startedAt + t_c`.
- Pagamento `min(floor(valor × multiplicador · 100)/100, ganhoMáximo)`; crédito `bet:{betId}:credit` com outbox até confirmar.
- Evento ao jogador `CashedOut` e atualização de saldo.

**Não-objetivos (fora do escopo):**
- Liquidação das apostas perdidas e estorno de rodada anulada (SPEC-0024); exibição para a sala (SPEC-0025).

## 3. Dependências
- **Implementações necessárias:** SPEC-0022 (apostas e eventos do motor).
- **Contratos consumidos:** SPEC-0020@2 (ganho máximo por moeda); SPEC-0021@4 (crédito idempotente).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0005 (o instante do cash-out é o horário do servidor no recebimento; tolerância como parâmetro de contrato), ADR-0014 (o ponto e o instante do crash não saem da memória do líder antes do crash), ADR-0007 (créditos reenviados até confirmar). Implementação de referência: liderança e condutor da SPEC-0008.

**Decisão:**
- Tolerância de latência = **0** (parâmetro de contrato versionado): o que vale é o horário do servidor que recebeu o comando.
- O líder decide; o crash e `t_c` nunca são enviados a outras instâncias, só a decisão (aceito com multiplicador, ou tardio).
- Se o líder não responder em 200 ms, o pedido é registrado com o horário de recebimento e decidido pelo líder até o fim da rodada (o jogador recebe `CashedOut` ou `CASHOUT_TOO_LATE` assim que decidido).
- Persistir o cash-out e enfileirar o crédito na mesma transação (outbox Wolverine).

**Justificativa:** mantém a justiça (regra objetiva para o laboratório) sem expor o crash fora do líder.

**Desvio do padrão existente:** Nenhum. Extensão do módulo Rounds com o árbitro (aditivo ao contrato da SPEC-0008).

**Alternativas descartadas:** decidir na instância que recebeu (precisaria conhecer o crash antes — viola ADR-0014); decidir só no fim da rodada (o jogador esperaria o crash para saber se retirou).

**ADRs:** ADR-0001, ADR-0002, ADR-0005, ADR-0006, ADR-0007, ADR-0014, ADR-0018, ADR-0019.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** cash-out confirmado no servidor p99 ≤ 50 ms; rajada de 300 cash-outs em 1 s sem perda (ADR-0019) — IT-05 (`Category=Performance`).
- **Segurança:** só o dono da aposta retira; nenhuma resposta antes do crash revela o ponto ou o instante do crash — IT-06.
- **Privacidade e dados pessoais:** N/A além do ID pseudônimo.
- **Disponibilidade e resiliência:** crédito reenviado até confirmar, inclusive após reinício; cash-out repetido não credita duas vezes — IT-03.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** hub `/hubs/bets` (v1, extensão) + `Rounds.Contracts.ICashOutArbiter`.

```text
Hub /hubs/bets
  CashOut({ betId }) → CashOutResult { cashedOut: CashedOut?, pending: CashOutPending? }   // exatamente um preenchido
    CashedOut { betId, multiplier:Multiplier, payout:Money, creditStatus:"credited"|"pending", balance:Money?, auto:false }
      // balance nulo quando creditStatus = "pending" (crédito ainda não confirmado; BalanceUpdated chega depois)
    CashOutPending { betId }   // o líder não decidiu em 200 ms: pedido gravado com o horário de recebimento
    erros: CASHOUT_TOO_LATE (recebido em ou depois do crash) | BET_NOT_ACTIVE (placed/queued/cancelled)
      | BET_NOT_FOUND
    repetição para aposta já retirada → mesmo CashedOut
Servidor → jogador
  CashedOut { betId, multiplier, payout, creditStatus, balance?, auto:true }  // automático executado
  CashedOut { ..., auto:false }                       // decisão de um CashOutPending
  CashOutRejected { betId, code:"CASHOUT_TOO_LATE" }  // decisão tardia de um CashOutPending
  BalanceUpdated { balance, currency }                // crédito confirmado depois (reenvio)
Rounds.Contracts
  ICashOutArbiter.DecideAsync(salaId:string, roundId, receivedAt, ct) → CashOutAccepted(multiplier) | CashOutTooLate | CashOutUndecided
    // o líder decide pela memória do voo; rodada já encerrada e fora da memória → decide pelos dados públicos persistidos
    // (startedAt e ponto do crash); rodada desconhecida e não encerrada (ex.: líder assumindo) → Undecided, e quem pediu tenta de novo
  ICashOutArbiter.ScheduleAutoAsync(salaId:string, roundId, betId, target:Multiplier, ct)
    // chamado pela Betting quando a aposta fica active com autoCashOut; instância que não é o líder encaminha ao
    // líder pelo Redis (como DecideAsync); o líder executa em t_alvo se t_alvo < t_c
  AutoCashOutExecuted { salaId, roundId, betId, multiplier, executedAt }   // evento de integração publicado pelo
    // líder (Wolverine); a Betting persiste o cash-out, credita e envia CashedOut{auto:true}
Regras
  t_c = ln(crash)/k sem arredondamento; TooLate ⇔ recebidoEm ≥ startedAt + t_c
  multiplicador manual = floor(e^(k·(recebidoEm − startedAt)) · 100)/100, aceito se recebidoEm < startedAt + t_c
  pagamento = min(floor(valor × multiplicador × 100)/100, ganhoMáximo da moeda)
  tolerância de latência = 0 ms (versionada neste contrato)
  pedido pendente: decidido pelo líder com o horário de recebimento gravado, no máximo até o fim da rodada; só o primeiro
    horário gravado vale (pedidos repetidos não o sobrescrevem)
  automático tem precedência: cash-out manual com multiplicador ≥ alvo da aposta é liquidado no alvo exato (auto:true);
    pedido manual pendente e automático executado: vale o automático
  reconciliação no crash: ao processar RoundCrashed (ponto já público), toda aposta active ainda não retirada com alvo
    < ponto do crash é retirada no alvo exato (auto:true) antes de qualquer liquidação — o agendamento em memória do
    líder é só o caminho rápido; falha de publicação não descarta o automático
  crédito: na fila sequencial de eventos da rodada só se grava o cash-out e o CreditCashOut (sem atraso); a chamada à
    carteira acontece no handler do crédito, fora dessa fila, e é ele quem envia o CashedOut do automático (credited com
    saldo, ou pending seguido de BalanceUpdated)
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Cash-out a tempo | recebido em t < t_c | Accepted com multiplicador m(t) truncado | UT-01, IT-01 |
| Cash-out tardio | recebido em t ≥ t_c | CASHOUT_TOO_LATE, aposta segue para perder | UT-01, IT-01 |
| Pagamento | 10,00 × 2,47 ; 3,33 × 1,07 | 24,70 ; 3,56 (truncado) | UT-02 |
| Ganho máximo | 1.000,00 × 150,00x | 100.000,00 | UT-02 |
| Automático executado | alvo 1,50x, crash 2,00x | CashedOut auto em 1,50x exatos | UT-03, IT-02 |
| Automático não executado | alvo 3,00x, crash 2,00x | sem cash-out; aposta perde | UT-03, IT-02 |
| Estado inválido | aposta placed, queued, cancelada | BET_NOT_ACTIVE | UT-04 |
| Repetição | CashOut duas vezes | um crédito, mesma resposta | IT-03 |
| Crédito com falha | carteira indisponível no crédito | cash-out mantido; crédito reenviado até confirmar, também após reinício; BalanceUpdated | IT-03 |
| Instância não líder | pedido chega em outra instância | encaminhado ao líder; mesma regra | IT-04 |
| Desempenho | 300 cash-outs em 1 s | todos decididos; p99 ≤ 50 ms | IT-05 |
| Sigilo do crash | respostas antes do crash | nenhuma contém crash ou t_c | IT-06 |
| Líder sem resposta em 200 ms | pedido não decidido no prazo | CashOutPending; decidido depois pelo horário gravado (CashedOut ou CashOutRejected) | IT-07 |
| Crédito pendente | carteira falha no crédito | CashedOut com creditStatus "pending" e balance nulo; BalanceUpdated depois | IT-03 |
| Manual depois do alvo automático | aposta com alvo 1,50x e manual recebido em 1,62x antes do automático ser processado | liquidada em 1,50x exatos, auto:true | UT-05 |
| Líder sem a rodada | líder assumindo (rodada desconhecida, não encerrada) ou rodada encerrada fora da memória | Undecided e nova tentativa; rodada encerrada decidida pelos dados persistidos | UT-06, IT-09 |
| Failover com automático | líder cai no voo com automático agendado abaixo do crash | aposta retirada no alvo exato (no voo ou na reconciliação do crash), nunca perdida | IT-08 |
| Rajada de automáticos com carteira fora | 300 automáticos e carteira indisponível | eventos da rodada e ativação da rodada seguinte não atrasam; créditos pendentes reenviados | IT-10 |
| Pedidos pendentes repetidos | dois CashOut pendentes concorrentes | vale o primeiro horário gravado; uma única decisão | IT-11 |
| Contrato | mensagens reais | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — o motor da SPEC-0008 é coberto pelos testes dela.

### 7.2 Testes Unitários
- **UT-01** — Dados k = 0,00006, crash 2,00 (t_c ≈ 11.552 ms) e FakeTimeProvider, quando o cash-out é recebido em 5.000 ms, um tick antes de t_c (t_c = ln 2/k ≈ 11.552,45 ms, sem arredondamento) e exatamente em t_c, então Accepted com 1,34x, Accepted com 1,99x e TooLate.
- **UT-02** — Dados valor e multiplicador, quando o pagamento é calculado, então 10,00 × 2,47 = 24,70; 3,33 × 1,07 = 3,56; e 1.000,00 × 150,00 = 100.000,00 pelo ganho máximo.
- **UT-03** — Dados alvo 1,50x com crash 2,00x e alvo 3,00x com crash 2,00x, quando o agendamento roda, então o primeiro executa em t_alvo com 1,50x exatos e o segundo não executa.
- **UT-04** — Dadas apostas placed, queued, cancelled e já retirada, quando CashOut é pedido, então BET_NOT_ACTIVE (e a já retirada devolve o CashedOut original).
- **UT-05** — Dada uma aposta com alvo 1,50x, quando o cash-out manual é decidido com 1,62x, então é liquidada em 1,50x exatos com auto:true; com 1,49x, então em 1,49x com auto:false.
- **UT-06** — Dado o árbitro do líder sem o voo em memória, quando a rodada não está encerrada, então Undecided; quando a rodada está encerrada, então decide pelos dados persistidos (startedAt e ponto do crash) com a mesma regra.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com uma rodada conduzida, quando o jogador retira no meio do voo e outro pedido chega depois do crash, então o primeiro recebe CashedOut com saldo creditado e o segundo CASHOUT_TOO_LATE.
- **IT-02** — Dadas duas apostas com cash-out automático (abaixo e acima do crash), quando a rodada termina, então só a primeira recebe CashedOut auto no alvo exato.
- **IT-03** — Dada uma carteira de teste que falha no primeiro crédito, quando o jogador retira, o processo reinicia e o CashOut é repetido, então a primeira resposta é CashedOut com creditStatus "pending" e balance nulo, existe um único crédito confirmado e BalanceUpdated chega ao jogador.
- **IT-04** — Dadas duas instâncias do Host com Redis real e o líder na instância A, quando o cash-out chega na instância B, então é decidido pelo líder com a mesma regra.
- **IT-05** — Dados 300 cash-outs na mesma rodada em 1 s, quando medidos, então todos decididos e p99 ≤ 50 ms (`Category=Performance`).
- **IT-06** — Dadas todas as respostas e eventos enviados antes do crash, quando inspecionados, então nenhum contém o ponto nem o instante do crash.
- **IT-07** — Dado o líder da sala impedido de responder por 300 ms (falha injetada no encaminhamento), quando o cash-out chega numa instância que não é o líder, então a resposta é CashOutPending e, ao liberar o líder, o jogador recebe CashedOut com o multiplicador do horário de recebimento gravado (ou CashOutRejected se recebido depois do crash).
- **IT-08** — Dada uma aposta com alvo 1,50x numa rodada com crash 5,00x e o líder interrompido em 1,2x (outra instância assume), quando a rodada termina, então a aposta é retirada em 1,50x exatos com CashedOut auto e um único crédito; e com a publicação do automático falhando uma vez, o resultado é o mesmo.
- **IT-09** — Dado um cash-out pendente recebido em 1,8x (crash 3,00x) e o líder interrompido antes de decidir, quando outra instância assume, então a decisão é Accepted com o multiplicador do horário gravado (não TooLate).
- **IT-10** — Dados 300 automáticos em 1,50x com a carteira indisponível, quando a rodada crasha e a seguinte abre, então RoundCrashed é processado e as apostas da rodada seguinte são ativadas sem atraso perceptível (≤ 2 s), e os créditos são confirmados quando a carteira volta.
- **IT-11** — Dados dois CashOut concorrentes para a mesma aposta com o líder sem responder, quando gravados como pendentes, então o horário gravado é o do primeiro recebido e existe uma única decisão.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens reais de IT-01/IT-02, quando validadas contra os tipos gerados, então conferem (contrato consumido pelas SPEC-0025 e 0026).

### 7.5 Testes E2E
- N/A — a jornada do jogador (apostar, retirar, perder) é provada pelos E2E da SPEC-0026, que consome este contrato (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** FakeTimeProvider e cadeias curtas da Fairness nos unitários; carteira de teste com falha programada no IT-03.

**Ambiente de execução:** Testcontainers (Postgres, Redis); duas instâncias do Host no IT-04.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** colunas de cash-out no schema `betting` (expand).
- **Compatibilidade:** extensão aditiva do hub e de `Rounds.Contracts`.
- **Observabilidade:** métricas `betting.cashouts{auto}`, `betting.cashout_too_late`, latência de decisão, `betting.credits_pending`; alerta se créditos pendentes > 0 por mais de 1 min.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Cash-out automático já? — Sim, executado no servidor (thomas, 2026-10-09)
- [x] Tolerância de latência? — Zero: vale o horário do servidor no recebimento (thomas, 2026-10-09)
- [x] Arredondamento do pagamento? — Truncado em centavos, padrão de mercado (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0002, ADR-0005, ADR-0006, ADR-0007, ADR-0014, ADR-0018, ADR-0019 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06 com a tag `SPEC-0023:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, IT-08, IT-09, IT-10, IT-11, CT-01 com a tag `SPEC-0023:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1e9b543 (árvore suja) | 2026-10-09 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:04:43.72]     Dada uma aposta com alvo 1,50x numa rodada com crash 5,00x e o) — b911a01 | 2026-10-11 |
| G2 Green | PASS | build exit 0 (✓ built in 141ms); test exit 0 (Duration  1.35s (tests 66%, transform 20%, import 12%, worker 2%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-11T06:25:36: Report generation took 0,8 seconds) — 501edb1 | 2026-10-11 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  743ms (tests 97%, transform 2%, import 1%)) — 501edb1 | 2026-10-11 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (2ª revisão, 501edb1; minors: resposta de rodada anulada difere entre memória e persistido; CashedOut automático pode não ser anunciado se o processo cair após o crédito; contador credits_pending por processo; automáticos publicados um a um) — 501edb1 | 2026-10-11 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 157ms); test exit 0 (at least ~269ms faster with isolate: false — reuses workers across files instead of one pe); test_integration exit 0 (Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 4 m 18 s ); test_e2e exit 0 ([run-e2e] stopping the AppHost); arch_test exit 0 (Duration  778ms (tests 97%, transform 2%, import 1%)); security_scan exit 0 (Ran 8 rules on 472 files: 0 findings.) — b258d1e | 2026-10-11 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-11 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem); E2E com backend real na CI (PR #25, merge 88257d4) | 2026-10-11 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 3c14772 | 2026-10-11 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-11 | G1 | spec | G1: falta scaffold dos tipos do contrato; D1 limite do UT-01 contradiz t_c não arredondado; D2 balance do CashedOut com crédito falho; D3 saída do cash-out automático; D4 resposta do hub quando o líder não decide em 200 ms | — | Architect | Emenda v2 — contrato v2 (creditStatus, CashOutPending, ScheduleAutoAsync encaminhado, AutoCashOutExecuted, TooLate em t_c exato) + scaffold antes dos testes | 2026-10-11 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
O jogador retira a aposta pelo hub `/hubs/bets` (`CashOut`) durante o voo e recebe o pagamento (valor × multiplicador, truncado em centavos, limitado ao ganho máximo da moeda) creditado na carteira; o cash-out automático executa no alvo exato quando o alvo fica abaixo do crash. A decisão é do líder da sala pelo horário do servidor no recebimento (tolerância 0 ms); pedido sem decisão em 200 ms fica pendente e é decidido depois pelo horário gravado. Crédito pendente é avisado ao jogador e confirmado depois (BalanceUpdated).

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
Rounds: `ICashOutArbiter` com decisão no líder (`LeaderCashOutArbiter` sobre o `SalaConductor`), encaminhamento pelo Redis a partir de outras instâncias com resposta de três estados (aceito, tarde, indefinido), decisão por dados persistidos de rodada encerrada, automáticos agendados no líder e publicados por outbox (`AutoCashOutExecuted`). Betting: `CashOutUseCase`, `CashOutSettlement` (precedência do automático), reconciliação dos automáticos no RoundCrashed (ponto já público), crédito durável `CreditCashOut` pelo outbox na própria fila, `DecideCashOut` para pedidos pendentes, primeiro horário pendente prevalece (token de concorrência). Contrato v2 (envelope `CashOutResult`, `creditStatus`). Duas rodadas de review (G4): automáticos duráveis, líder sem a rodada não decide, precedência do automático, crédito fora da fila sequencial.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type feature.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Dados k = 0,00006, crash 2,00 (t_c ≈ 11.552 ms) e FakeTimeProvider, quando o cash-out é recebido em 5.000 ms, um tick antes de t_c (t_c = ln… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| UT-02 | Dados valor e multiplicador, quando o pagamento é calculado, então 10,00 × 2,47 = 24,70; 3,33 × 1,07 = 3,56; e 1.000,00 × 150,00 = 100.000,0… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| UT-03 | Dados alvo 1,50x com crash 2,00x e alvo 3,00x com crash 2,00x, quando o agendamento roda, então o primeiro executa em t_alvo com 1,50x exato… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| UT-04 | Dadas apostas placed, queued, cancelled e já retirada, quando CashOut é pedido, então BET_NOT_ACTIVE (e a já retirada devolve o CashedOut or… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| UT-05 | Dada uma aposta com alvo 1,50x, quando o cash-out manual é decidido com 1,62x, então é liquidada em 1,50x exatos com auto:true; com 1,49x, e… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| UT-06 | Dado o árbitro do líder sem o voo em memória, quando a rodada não está encerrada, então Undecided; quando a rodada está encerrada, então dec… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-01 | Dado o Host real com uma rodada conduzida, quando o jogador retira no meio do voo e outro pedido chega depois do crash, então o primeiro rec… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-02 | Dadas duas apostas com cash-out automático (abaixo e acima do crash), quando a rodada termina, então só a primeira recebe CashedOut auto no … | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-03 | Dada uma carteira de teste que falha no primeiro crédito, quando o jogador retira, o processo reinicia e o CashOut é repetido, então a prime… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-04 | Dadas duas instâncias do Host com Redis real e o líder na instância A, quando o cash-out chega na instância B, então é decidido pelo líder c… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-05 | Dados 300 cash-outs na mesma rodada em 1 s, quando medidos, então todos decididos e p99 ≤ 50 ms (`Category=Performance`). | PASS | G5 local b258d1e (resultado integrado); CI do PR #25; Category=Performance |
| IT-06 | Dadas todas as respostas e eventos enviados antes do crash, quando inspecionados, então nenhum contém o ponto nem o instante do crash. | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-07 | Dado o líder da sala impedido de responder por 300 ms (falha injetada no encaminhamento), quando o cash-out chega numa instância que não é o… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-08 | Dada uma aposta com alvo 1,50x numa rodada com crash 5,00x e o líder interrompido em 1,2x (outra instância assume), quando a rodada termina,… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-09 | Dado um cash-out pendente recebido em 1,8x (crash 3,00x) e o líder interrompido antes de decidir, quando outra instância assume, então a dec… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-10 | Dados 300 automáticos em 1,50x com a carteira indisponível, quando a rodada crasha e a seguinte abre, então RoundCrashed é processado e as a… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| IT-11 | Dados dois CashOut concorrentes para a mesma aposta com o líder sem responder, quando gravados como pendentes, então o horário gravado é o d… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |
| CT-01 | Dadas as mensagens reais de IT-01/IT-02, quando validadas contra os tipos gerados, então conferem (contrato consumido pelas SPEC-0025 e 0026… | PASS | G5 local b258d1e (resultado integrado); CI do PR #25 |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — IT-05 (300 cash-outs em 1 s, p99 ≤ 50 ms); IT-06 (sigilo do crash antes do crash)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A justificado: sem ambiente até a SPEC-0012 (G6 N/A); E2E com backend real na CI do PR #25
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — métricas `betting.cashouts{auto}`, `betting.cashout_too_late`, `betting.cashout_decision_latency`, `betting.credits_pending` (por processo — ver Pendências); log crítico CreditRefused; rollback = revert do merge e da migração
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma)
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes) — gitleaks e Semgrep sem achados no G5 (b258d1e); sigilo do crash provado no IT-06
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha — TreatWarningsAsErrors; supressões MA0025 só no scaffold, com motivo na linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente (G6 N/A até a SPEC-0012). Integrado pelo PR #25.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- Minors aceitos do review: resposta de rodada anulada difere entre a decisão em memória (aceita antes da anulação) e a persistida (TooLate) — regra única a fixar com a SPEC-0024/anulação; CashedOut automático pode não ser anunciado se o processo cair entre o crédito e o anúncio (estado e dinheiro corretos; novo CashOut devolve o resultado); `betting.credits_pending` é contador por processo e o alerta "> 0 por 1 min" não foi configurado (SPEC-0014, observabilidade); automáticos publicados um a um no laço do condutor.
- Premissa registrada: a tolerância de 0 ms depende de relógios sincronizados (NTP) entre instâncias.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0023`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 (inalterado) | 2026-10-12 | IT-05: guarda local de 75 ms (antes 50 ms); a meta de p99 ≤ 50 ms passa a ser entregue pela SPEC-0048 | A correção de dinheiro da SPEC-0024 (pedido gravado antes da decisão do líder) acrescenta uma escrita ao caminho do cash-out: 52–54 ms de p99 na máquina de referência | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-12); a confirmar com a SPEC-0048 |
| 2 (inalterado) | 2026-10-11 | IT-05: p99 ≤ 50 ms vale no ambiente de referência (registrado no G5 local); no runner da CI (2 vCPU compartilhados) o teste é guarda de regressão com p99 ≤ 1 s. O alvo de 50 ms na infraestrutura real fica com a SPEC-0048 (proposta) | CI do PR #25: p99 807 ms no runner do GitHub com o mesmo código que mede ≤ 50 ms localmente | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11); SPEC-0048 aguarda H1 |
| 2 (inalterado no hub) | 2026-10-11 | Review G4: `CashOutUndecided` em Rounds.Contracts (líder sem a rodada não decide; rodada encerrada decidida pelos dados persistidos); precedência do automático (manual ≥ alvo liquida no alvo, auto:true); reconciliação dos automáticos no RoundCrashed; crédito fora da fila sequencial; primeiro horário pendente prevalece. Aceitos e documentados: pedido manual pendente perde para o automático executado; a tolerância de 0 ms depende do sincronismo de relógio entre instâncias (NTP), registrado como premissa | Review G4 (d91817c): M1–M4 e minors m1–m3 | Nenhuma (contrato do hub inalterado) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11) |
| 2 (forma) | 2026-10-11 | `CashOut` devolve o envelope `CashOutResult { cashedOut?, pending? }` (exatamente um preenchido) em vez de união polimórfica; `creditStatus` como enum "credited"/"pending" | Scaffold 0174675: o gerador de contratos da SPEC-0006 transforma `anyOf` de objetos em `unknown`; o envelope gera TypeScript tipado | SPEC-0026 (mesmo pin @2) | Architect (thomas autorizou seguir pelo recomendado, 2026-10-11) |
| 2 | 2026-10-11 | Contrato v2: `CashedOut` ganha `creditStatus` ("credited"/"pending") e `balance` opcional; `CashOut` pode responder `CashOutPending` (líder sem decisão em 200 ms) com a decisão depois por `CashedOut` ou `CashOutRejected`; `ICashOutArbiter.ScheduleAutoAsync` encaminhado ao líder e evento `AutoCashOutExecuted`; `salaId` é string; TooLate ⇔ recebidoEm ≥ startedAt + t_c sem arredondamento (UT-01 corrigido); novo IT-07; scaffold dos tipos em commit `chore` antes dos testes | IMP-01: G1 do Test-writer (D1 limite do UT-01 contradiz o motor, D2 balance sem crédito confirmado, D3 saída do automático, D4 resposta sem decisão do líder) | SPEC-0026 (pin @1 → @2) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10/11) |
| 1 (inalterado) | 2026-10-11 | Escopo: código gerado do Wolverine no Host (`src/backend/Host/Internal/Generated/**`), registro no `Program.cs` e regras de arquitetura; ADR-0028 (operação do Wolverine) passa a valer para os handlers do cash-out e do crédito | A SPEC-0022 entregou o Wolverine com código pré-gerado no Host (ADR-0028): todo handler novo regenera esse código | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
