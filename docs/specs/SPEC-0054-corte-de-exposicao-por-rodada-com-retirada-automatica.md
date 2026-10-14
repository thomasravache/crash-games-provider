---
id: SPEC-0054
title: Corte de exposição por rodada com retirada automática
tier: full
type: feature
user_facing: "false"
status: approved
created: 2026-10-13
parent: SPEC-0019
depends_on: [SPEC-0053, SPEC-0025]
consumes_contract: [SPEC-0053@1, SPEC-0023@3, SPEC-0025@4]
contract_version: 1
touches: [src/backend/Modules/Betting/**, tests/backend/Modules/Betting.Tests/**, src/backend/Modules/Rounds/**, tests/backend/Modules/Rounds.Tests/**, src/backend/Host/Internal/Generated/**, src/frontend/packages/contracts/**, artifacts/openapi/**]
adrs: [ADR-0001, ADR-0005, ADR-0007, ADR-0014, ADR-0019]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0054 — Corte de exposição por rodada com retirada automática

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Garante que nenhum operador pague mais que o seu `MaxRoundWin` (SPEC-0053) numa rodada. A Betting calcula, para cada operador, o **multiplicador de corte** da rodada: o maior multiplicador em que a soma do que já foi pago com o que as apostas abertas receberiam ainda cabe no teto. Esse corte vira o alvo de retirada automática de todas as apostas abertas do operador, executado pelo líder como qualquer automático. O jogador recebe `CashedOut` com `reason:"round_limit"`. Ninguém perde por causa do limite; só deixa de ganhar acima do corte.

## 2. Motivação & Escopo
**Motivação:** a chance de o crash passar de *x* é 0,97/*x*. Como todos voam no mesmo multiplicador, uma rodada alta com sala cheia pode custar ao operador milhões, e o ganho máximo por aposta não impede isso. É o padrão do mercado ("ganho máximo por rodada"). Escolhas do thomas (2026-10-13): retirada automática ao atingir o teto e aviso ao jogador.

**Objetivos (dentro do escopo):**
- Cálculo puro e determinístico do multiplicador de corte, no `Betting.Domain`.
- Aplicação quando o voo começa:
  - o corte é calculado com as apostas ativas do operador;
  - cada aposta passa a ter o alvo efetivo `min(alvo próprio, corte)`, persistido;
  - o líder agenda esse alvo.
- Recálculo depois de cada cash-out do operador na rodada. O corte só pode subir; as apostas com alvo de limite recebem o novo alvo.
- Reconciliação no crash: o alvo efetivo persistido vale como alvo (regra existente da SPEC-0023).
- Aceitação de aposta: recusa com `ROUND_LIMIT_REACHED` se, com a nova aposta, o corte da rodada ficasse abaixo de 1,01x.
- `CashedOut` e `LiveBet` ganham `reason?` (`"round_limit"`); `GetLimits` do hub ganha `maxRoundWin`.
- Métricas de cortes por rodada e do valor no corte.

**Não-objetivos (fora do escopo):**
- Aviso visual e regras na Pipa (SPEC-0055).
- Teto global da sala (descartado pelo thomas).
- Alterar o ponto do crash: o crash é comprometido antes da rodada e nunca muda (ADR-0014, provably fair).

## 3. Dependências
- **Implementações necessárias:** SPEC-0053 (o teto existe nos limites); SPEC-0025 (apostas ao vivo, que esta spec estende com `reason`).
- **Contratos consumidos:** SPEC-0053@1, SPEC-0023@2 (árbitro e `CashedOut`), SPEC-0025@3 (`LiveBet`).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:**
- ADR-0014: o crash só é conhecido pelo líder.
- ADR-0005: o instante do cash-out é o horário do servidor.
- ADR-0007: créditos com outbox.
- ADR-0019: 5.000 jogadores por sala.
- Referência: o cash-out automático da SPEC-0023 (`AutoCashOutHandler`, `ICashOutArbiter.ScheduleAutoAsync`, reconciliação no `RoundCrashed`) e o pagamento `Bet.PayoutOf` (`min(floor(valor × m × 100)/100, maxWin)`).

**Decisão:**
- **O corte reaproveita o automático.**
  - O corte de um operador é o maior `m` (em centésimos, ≥ 1,01) com `pago + Σ PayoutOf(valor_i, m, maxWin) ≤ MaxRoundWin` sobre as apostas ativas do operador na rodada.
  - O alvo efetivo de cada aposta é `min(alvo próprio, corte)`, com origem `round_limit` quando vem do corte.
  - O líder executa o automático em `ln(alvo)/k` se for antes do crash. Se o crash vier antes, nada é pago acima do que já seria.
- **Momento do cálculo:** o corte é calculado quando o voo começa (as apostas da rodada ficam fixas) e recalculado depois de cada cash-out do operador. O que foi pago fica abaixo do corte vigente, então o corte novo nunca é menor.
- **Nova chamada a `ScheduleAutoAsync` para a mesma aposta substitui o alvo anterior** (hoje a semântica não é definida). É uma extensão do contrato do árbitro da SPEC-0023 (Emenda v3 dela).
- **Persistência:** o alvo efetivo e a origem ficam na aposta (`limit_target`), para a reconciliação no crash e o failover do líder.

**Justificativa:**
- Não toca no crash: a rodada continua verificável.
- Reaproveita o caminho de automático já testado sob failover e rajada.
- É determinístico e auditável: o corte é função pura dos dados da rodada.

**Desvio do padrão existente:** Nenhum. Extensões aditivas dos contratos da SPEC-0023 e da SPEC-0025.

**Alternativas descartadas:**
- Reduzir o crash da rodada: quebra o provably fair.
- Ratear o pagamento acima do teto: confuso e retroativo; o thomas escolheu a retirada automática.
- Checar só na aceitação da aposta (`Σ valor × teto da sala`): bloquearia salas cheias sem necessidade.

**ADRs:** ADR-0001, ADR-0005, ADR-0007, ADR-0014, ADR-0019.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** com 5.000 apostas de um operador, o cálculo do corte leva ≤ 5 ms (busca binária em centésimos sobre valores ordenados), e o cash-out não perde o p99 da SPEC-0023 — UT-02 e IT-04 (`Category=Performance`).
- **Segurança:** o corte nunca é enviado ao cliente antes de ser executado. Ele revela só o próprio limite, não o crash — IT-01.
- **Privacidade e dados pessoais:** N/A
- **Disponibilidade e resiliência:** o líder cai no voo → o alvo persistido é executado na reconciliação do crash, nunca perdido — IT-03.
- **Acessibilidade (UI):** N/A — sem interface (SPEC-0055).
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** hub `/hubs/bets` (extensão) + `Rounds.Contracts.ICashOutArbiter` (semântica) + `Betting.Domain`.

```text
Betting.Domain
  RoundExposure.CutOff(paid:Money, active:[(amount:Money)], maxWin:Money, maxRoundWin:Money) → Multiplier?
    // maior m em centésimos, 1,01 ≤ m ≤ teto da sala, com paid + Σ min(floor(amount·m·100)/100, maxWin) ≤ maxRoundWin;
    // null quando nem 10.000x (teto da sala) estoura o limite (sem corte)
Regras
  corte calculado ao iniciar o voo (RoundStarted) por operador e moeda; alvo efetivo = min(autoCashOut, corte)
  recálculo após cada cash-out do operador na rodada; novo corte ≥ anterior; apostas com origem round_limit são reagendadas
  ScheduleAutoAsync para a mesma aposta e rodada: substitui o alvo (Emenda v3 da SPEC-0023)
  reconciliação no crash (SPEC-0023): usa o alvo efetivo persistido
  aceitação de aposta: ROUND_LIMIT_REACHED se, incluída a nova aposta, CutOff < 1,01 (o operador não comporta a rodada)
Hub /hubs/bets
  CashedOut { ..., auto:true, reason?:"round_limit" }        // reason ausente nos demais casos
  PlaceBet → erro ROUND_LIMIT_REACHED (mesmo envelope de erro dos outros códigos)
  GetLimits → BetLimitsResponse { currency, minBet, maxBet, maxWin, maxRoundWin }
  LiveBetsBatch.cashedOut[] { betRef, multiplier, payout, reason?:"round_limit" }   // extensão da SPEC-0025
Persistência: betting.bets.limit_target numeric(12,2) null
Métricas: betting.round_limit_cuts (contador por operador), betting.round_limit_cutoff (histograma do multiplicador)
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Cálculo do corte | 10 apostas de 1.000,00, teto 500.000, maxWin 100.000 | corte 50,00x (10 × 50.000 = 500.000) | UT-01 |
| Rodada que não cabe | apostas de 1.000,00 com teto 500.000 | até a 495ª o corte fica ≥ 1,01x (495 × 1.010 = 499.950); a 496ª é recusada | UT-01, UT-03 |
| Sem corte | 3 apostas de 1.000,00: no teto da sala 3 × 100.000 cabe | `null`, nenhuma aposta reagendada | UT-01 |
| Recálculo | jogador retira manual abaixo do corte | corte novo ≥ anterior; demais reagendadas | UT-02, IT-02 |
| Alvo próprio menor | aposta com auto 2,00x e corte 5,00x | executa em 2,00x sem `reason` | UT-02 |
| Execução no voo | crash acima do corte | todas as abertas retiradas no corte com `reason:"round_limit"`; total pago ≤ teto | IT-01 |
| Crash antes do corte | crash abaixo do corte | nada muda; perdidas como sempre | IT-01 |
| Failover | líder cai depois do agendamento | retiradas no corte na reconciliação do crash | IT-03 |
| Recusa | aposta que deixaria o corte < 1,01x | `ROUND_LIMIT_REACHED`, saldo não debitado | IT-02 |
| Operadores isolados | operador A no corte, B não | só as apostas de A são cortadas | IT-01 |
| Desempenho | 5.000 apostas de um operador | cálculo ≤ 5 ms; p99 do cash-out mantido | UT-02, IT-04 |
| Contrato | mensagens reais | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — o automático e a reconciliação da SPEC-0023 têm testes próprios, que continuam valendo.

### 7.2 Testes Unitários
- **UT-01** — Dados `maxWin` 100.000,00 e `maxRoundWin` 500.000,00, quando o corte é calculado para:
  - (a) 3 apostas de 1.000,00 → `null`: no teto da sala, 3 × 100.000 = 300.000 cabe;
  - (b) 10 apostas de 1.000,00 → 50,00x (10 × 50.000 = 500.000), e 50,01x estouraria;
  - (c) `paid` 450.000,00 com 2 apostas de 1.000,00 → 25,00x;
  - (d) 496 apostas de 1.000,00 → abaixo de 1,01 (sinaliza recusa), e 495 → 1,01x;
  - então os valores são exatos em centésimos, com truncamento igual ao de `Bet.PayoutOf`.
- **UT-02** — Dado o corte 50,00x de UT-01 (b), quando uma aposta é retirada manualmente em 10,00x (paga 10.000,00) e o corte é recalculado, então o novo corte é 54,44x (10.000 + 9 × floor(1.000 × m) ≤ 500.000 → m = 54,44) e é ≥ anterior. Uma aposta com auto 2,00x mantém 2,00x sem `reason`. Com 5.000 apostas, o cálculo leva ≤ 5 ms (mediana de 20 execuções).
- **UT-03** — Dada uma rodada do operador em que a próxima aposta deixaria o corte < 1,01x, quando a aposta é validada, então `ROUND_LIMIT_REACHED`; e a anterior, que mantinha ≥ 1,01x, é aceita.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com crash fixo 100,00x, `maxRoundWin` 20.000,00 para o operador A e o padrão para o B, e apostas de 1.000,00 (5 de A, 2 de B), quando o voo passa de 4,00x, então as 5 de A são retiradas em 4,00x com `CashedOut{auto:true, reason:"round_limit"}`, total pago a A = 20.000,00 ≤ teto, e as de B continuam no voo. Com crash fixo 3,00x, então ninguém é retirado pelo limite. Nenhuma mensagem antes do corte revela o corte ou o crash.
- **IT-02** — Dado o cenário de IT-01, quando um jogador de A retira manualmente em 2,00x antes do corte, então o corte das demais sobe para 4,50x (2.000 + 4 × 1.000 × m ≤ 20.000) e elas são retiradas nele. E quando uma aposta nova de A deixaria o corte < 1,01x, então `ROUND_LIMIT_REACHED` sem débito na carteira.
- **IT-03** — Dado o cenário de IT-01 com duas instâncias, quando o líder cai depois do agendamento e antes do corte, então as apostas de A são retiradas no corte pela reconciliação do crash (`limit_target` persistido), nunca perdidas.
- **IT-04** — Dada a rajada de cash-out da SPEC-0023 (300 cash-outs em 1 s) com o corte ativo e recalculado a cada cash-out, quando medida, então o p99 fica dentro do limite local/CI (SPEC-0052) (`Category=Performance`).

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens reais de IT-01/IT-02 (`CashedOut` com `reason`, `ROUND_LIMIT_REACHED`, `GetLimits` com `maxRoundWin`, `LiveBetsBatch` com `reason`), quando validadas contra os tipos gerados, então conferem.

### 7.5 Testes E2E
- N/A — o efeito visível (aviso e regras) é verificado nos E2E da SPEC-0055; esta spec não tem interface (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** crash fixo (`FixedCrashFairness`), carteira programável, limites por operador via fixture.

**Ambiente de execução:** Testcontainers (Postgres, Redis); Host real; duas instâncias para o IT-03.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local). Antes da produção, com o teto configurado por operador (SPEC-0053/0039).
- **Dados/schema:** coluna `limit_target` anulável em `betting.bets`; aditiva.
- **Compatibilidade:** campos opcionais novos (`reason`, `maxRoundWin`) e um código de erro novo; clientes antigos ignoram.
- **Observabilidade:** `betting.round_limit_cuts`, `betting.round_limit_cutoff`, log por corte (operador, rodada, corte, total pago).
- **Rollback:** reverter o merge commit; a coluna fica sem uso.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] O que acontece com as apostas abertas ao atingir o teto? — Retirada automática no multiplicador do corte (thomas, 2026-10-13)
- [x] Como o jogador fica sabendo? — `CashedOut` com `reason:"round_limit"`, aviso na tela e regras (SPEC-0055) (thomas, 2026-10-13)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

**Fase 1: Cálculo do corte (domínio)**
- [ ] Red: UT-01, UT-02 e UT-03 com a tag `SPEC-0054:<ID>`, falhando pelo motivo certo
- [ ] Green: `RoundExposure.CutOff` e a regra de aceitação, seguindo `Bet.PayoutOf`

**Fase 2: Aplicação no voo, failover e contrato**
- [ ] Red: IT-01, IT-02, IT-03, IT-04 e CT-01 com dependências reais, falhando pelo motivo certo
- [ ] Green: alvo efetivo persistido, agendamento/reagendamento no líder, `reason`, `ROUND_LIMIT_REACHED`, métricas
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte afetada + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 91dd6a5 | 2026-10-13 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0054`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
