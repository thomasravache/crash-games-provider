---
id: SPEC-0023
title: "Cash-out manual e automático"
tier: full
type: feature
user_facing: false
status: in-progress
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
  ICashOutArbiter.DecideAsync(salaId:string, roundId, receivedAt, ct) → CashOutAccepted(multiplier) | CashOutTooLate
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
  pedido pendente: decidido pelo líder com o horário de recebimento gravado, no máximo até o fim da rodada
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
| Contrato | mensagens reais | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — o motor da SPEC-0008 é coberto pelos testes dela.

### 7.2 Testes Unitários
- **UT-01** — Dados k = 0,00006, crash 2,00 (t_c ≈ 11.552 ms) e FakeTimeProvider, quando o cash-out é recebido em 5.000 ms, um tick antes de t_c (t_c = ln 2/k ≈ 11.552,45 ms, sem arredondamento) e exatamente em t_c, então Accepted com 1,34x, Accepted com 1,99x e TooLate.
- **UT-02** — Dados valor e multiplicador, quando o pagamento é calculado, então 10,00 × 2,47 = 24,70; 3,33 × 1,07 = 3,56; e 1.000,00 × 150,00 = 100.000,00 pelo ganho máximo.
- **UT-03** — Dados alvo 1,50x com crash 2,00x e alvo 3,00x com crash 2,00x, quando o agendamento roda, então o primeiro executa em t_alvo com 1,50x exatos e o segundo não executa.
- **UT-04** — Dadas apostas placed, queued, cancelled e já retirada, quando CashOut é pedido, então BET_NOT_ACTIVE (e a já retirada devolve o CashedOut original).

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com uma rodada conduzida, quando o jogador retira no meio do voo e outro pedido chega depois do crash, então o primeiro recebe CashedOut com saldo creditado e o segundo CASHOUT_TOO_LATE.
- **IT-02** — Dadas duas apostas com cash-out automático (abaixo e acima do crash), quando a rodada termina, então só a primeira recebe CashedOut auto no alvo exato.
- **IT-03** — Dada uma carteira de teste que falha no primeiro crédito, quando o jogador retira, o processo reinicia e o CashOut é repetido, então a primeira resposta é CashedOut com creditStatus "pending" e balance nulo, existe um único crédito confirmado e BalanceUpdated chega ao jogador.
- **IT-04** — Dadas duas instâncias do Host com Redis real e o líder na instância A, quando o cash-out chega na instância B, então é decidido pelo líder com a mesma regra.
- **IT-05** — Dados 300 cash-outs na mesma rodada em 1 s, quando medidos, então todos decididos e p99 ≤ 50 ms (`Category=Performance`).
- **IT-06** — Dadas todas as respostas e eventos enviados antes do crash, quando inspecionados, então nenhum contém o ponto nem o instante do crash.
- **IT-07** — Dado o líder da sala impedido de responder por 300 ms (falha injetada no encaminhamento), quando o cash-out chega numa instância que não é o líder, então a resposta é CashOutPending e, ao liberar o líder, o jogador recebe CashedOut com o multiplicador do horário de recebimento gravado (ou CashOutRejected se recebido depois do crash).

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
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0002, ADR-0005, ADR-0006, ADR-0007, ADR-0014, ADR-0018, ADR-0019 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0023:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, CT-01 com a tag `SPEC-0023:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:03:50.96]     Dado o contrato v2 do cash-out no hub /hubs/bets gerado (SPEC-) — 3df9de0 | 2026-10-11 |
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
| IMP-01 | 2026-10-11 | G1 | spec | G1: falta scaffold dos tipos do contrato; D1 limite do UT-01 contradiz t_c não arredondado; D2 balance do CashedOut com crédito falho; D3 saída do cash-out automático; D4 resposta do hub quando o líder não decide em 200 ms | — | Architect | Emenda v2 — contrato v2 (creditStatus, CashOutPending, ScheduleAutoAsync encaminhado, AutoCashOutExecuted, TooLate em t_c exato) + scaffold antes dos testes | 2026-10-11 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0023`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 (forma) | 2026-10-11 | `CashOut` devolve o envelope `CashOutResult { cashedOut?, pending? }` (exatamente um preenchido) em vez de união polimórfica; `creditStatus` como enum "credited"/"pending" | Scaffold 0174675: o gerador de contratos da SPEC-0006 transforma `anyOf` de objetos em `unknown`; o envelope gera TypeScript tipado | SPEC-0026 (mesmo pin @2) | Architect (thomas autorizou seguir pelo recomendado, 2026-10-11) |
| 2 | 2026-10-11 | Contrato v2: `CashedOut` ganha `creditStatus` ("credited"/"pending") e `balance` opcional; `CashOut` pode responder `CashOutPending` (líder sem decisão em 200 ms) com a decisão depois por `CashedOut` ou `CashOutRejected`; `ICashOutArbiter.ScheduleAutoAsync` encaminhado ao líder e evento `AutoCashOutExecuted`; `salaId` é string; TooLate ⇔ recebidoEm ≥ startedAt + t_c sem arredondamento (UT-01 corrigido); novo IT-07; scaffold dos tipos em commit `chore` antes dos testes | IMP-01: G1 do Test-writer (D1 limite do UT-01 contradiz o motor, D2 balance sem crédito confirmado, D3 saída do automático, D4 resposta sem decisão do líder) | SPEC-0026 (pin @1 → @2) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10/11) |
| 1 (inalterado) | 2026-10-11 | Escopo: código gerado do Wolverine no Host (`src/backend/Host/Internal/Generated/**`), registro no `Program.cs` e regras de arquitetura; ADR-0028 (operação do Wolverine) passa a valer para os handlers do cash-out e do crédito | A SPEC-0022 entregou o Wolverine com código pré-gerado no Host (ADR-0028): todo handler novo regenera esse código | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
