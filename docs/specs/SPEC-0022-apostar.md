---
id: SPEC-0022
title: "Apostar, aposta dupla e cancelamento"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0008, SPEC-0009]
consumes_contract: [SPEC-0020@1, SPEC-0021@3]
contract_version: 1
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**", "src/backend/Modules/Rounds/Rounds.Contracts/**", "src/backend/Modules/Rounds/Rounds.Features/Conductor/**", "src/backend/Host/Program.cs"]
adrs: [ADR-0001, ADR-0002, ADR-0005, ADR-0007, ADR-0008, ADR-0010, ADR-0015, ADR-0019]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0022 — Apostar, aposta dupla e cancelamento

## 1. Visão Geral
Implementa o módulo Betting para aceitar apostas: até duas apostas por jogador e rodada (painéis 1 e 2), aposta "para a próxima rodada" quando o voo já começou, cancelamento com estorno durante a fase de apostas, limites por operador e moeda, e débito na carteira antes de aceitar. Inclui o cash-out automático **como parâmetro** da aposta (a execução é da SPEC-0023).

## 2. Motivação & Escopo
**Motivação:** apostar é o centro do produto; as regras foram decididas pelo usuário em 2026-10-09 (aposta dupla, próxima rodada, cancelamento só na fase de apostas, limites por operador e moeda).

**Objetivos (dentro do escopo):**
- Hub SignalR `/hubs/bets` (mesma autenticação de sessão da SPEC-0009) com `PlaceBet`, `CancelBet` e `GetBetLimits`.
- Ciclo de vida da aposta: `placed` (rodada em apostas) → `active` (voo começou); `queued` (feita durante o voo, vale para a próxima rodada) → `placed` quando abre a fase de apostas; `cancelled`.
- Débito na carteira (`bet:{betId}:debit`) antes de aceitar; incerteza de débito gera estorno automático (ADR-0007).
- Eventos de integração do motor (`RoundBettingOpened`, `RoundFlying`, `RoundCrashed`, `RoundAnnulled`) publicados pelo módulo Rounds via `Rounds.Contracts` (Wolverine, outbox — ADR-0002), consumidos pelo Betting.
- Persistência no schema `betting`.

**Não-objetivos (fora do escopo):**
- Execução do cash-out manual e automático (SPEC-0023); liquidação e estorno de rodada anulada (SPEC-0024); apostas ao vivo e histórico (SPEC-0025); aposta automática repetida (épico futuro).

## 3. Dependências
- **Implementações necessárias:** SPEC-0008 (motor e salas) e SPEC-0009 (sessão do jogador, hub autenticado).
- **Contratos consumidos:** SPEC-0020@1 (`IOperatorLimits`: mínimo, máximo, ganho máximo por moeda); SPEC-0021@3 (`IOperatorWallet`: débito e estorno idempotentes).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0001 (módulos por Contracts), ADR-0005 (salas, horário do servidor), ADR-0007 (débito antes de aceitar). Implementação de referência: módulo Rounds (hub que só traduz, EF por schema, métricas) e Fairness.

**Decisão:**
- Hub próprio `/hubs/bets` no Betting (o hub de rodadas continua do Rounds); o cliente mantém as duas conexões.
- Fronteira de fase pelo **horário do servidor**: aposta recebida antes de `bettingEndsAt` entra na rodada atual; depois disso vira `queued` para a próxima.
- O débito acontece na hora da aposta (inclusive `queued`), e cancelar devolve o valor com `RollbackAsync`.
- Idempotência por `clientBetId` (gerado pelo cliente): repetir devolve o resultado original.
- Valores com no máximo 2 casas decimais; `autoCashOut` entre 1,01x e o limite da sala.

**Justificativa:** debitar já na aposta segue o ADR-0007 e evita aposta aceita sem saldo; hubs por módulo mantêm a fronteira.

**Desvio do padrão existente:** Nenhum. O Rounds ganha eventos de integração em `Rounds.Contracts` (extensão compatível do contrato da SPEC-0008).

**Alternativas descartadas:** métodos de aposta dentro do hub de rodadas (acoplaria Rounds ao Betting); reservar sem debitar (estado intermediário a mais).

**ADRs:** ADR-0001, ADR-0002, ADR-0005, ADR-0007, ADR-0008, ADR-0010, ADR-0015, ADR-0019.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** aposta aceita (recebimento → resposta, carteira demo) p99 ≤ 100 ms — IT-06 (`Category=Performance`).
- **Segurança:** só a própria sessão aposta e cancela; aposta de outro jogador → BET_NOT_FOUND; dinheiro real recusado (modo vem do token) — IT-04.
- **Privacidade e dados pessoais:** só ID pseudônimo.
- **Disponibilidade e resiliência:** débito incerto → estorno automático com nova tentativa; idempotência por `clientBetId` — IT-03.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** hub `/hubs/bets` (v1) + eventos de integração em `Rounds.Contracts`.

```text
Hub /hubs/bets (access_token = sessionToken da SPEC-0009)
  PlaceBet({ clientBetId:uuid, panel:1|2, amount:Money, autoCashOut?:Multiplier })
    → BetAccepted { betId, clientBetId, roundId?, roundNumber?, panel, amount, currency, autoCashOut?,
                    status:"placed"|"queued", balance:Money }
    erros (HubException com o código): INVALID_AMOUNT (≤ 0 ou > 2 casas) | BET_BELOW_MIN | BET_ABOVE_MAX
      | INSUFFICIENT_FUNDS | PANEL_BUSY (já existe aposta não final nesse painel para a mesma rodada/próxima)
      | AUTO_CASHOUT_INVALID (< 1.01 ou > limite da sala) | WALLET_UNAVAILABLE | REAL_MONEY_DISABLED
    repetição do mesmo clientBetId → mesmo BetAccepted
  CancelBet({ betId }) → BetCancelled { betId, refunded:Money, balance:Money }
    erros: CANCEL_NOT_ALLOWED (voo da rodada da aposta já começou) | BET_NOT_FOUND
  GetBetLimits() → { currency, minBet:Money, maxBet:Money, maxWin:Money }
Servidor → jogador (só a própria conexão)
  BetActivated { betId, roundId, roundNumber }      // aposta queued entrou na fase de apostas
Rounds.Contracts (eventos de integração, publicados pelo condutor da sala)
  RoundBettingOpened { salaId, roundId, roundNumber, bettingEndsAt }
  RoundFlying        { salaId, roundId, startedAt, growthK }
  RoundCrashed       { salaId, roundId, crashPoint, crashedAt }
  RoundAnnulled      { salaId, roundId, reason }
Estados: placed → active (RoundFlying) ; queued → placed (RoundBettingOpened) ; placed|queued → cancelled
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Aposta na fase de apostas | 10,00 no painel 1 antes de `bettingEndsAt` | placed, débito de 10,00, saldo 2.990,00 | UT-01, IT-01 |
| Aposta durante o voo | rodada voando | queued; vira placed com BetActivated quando abre a próxima | UT-02, IT-02 |
| Fronteira pelo horário do servidor | recebida em `bettingEndsAt` (relógio do servidor) | queued para a próxima | UT-02 |
| Aposta dupla | painéis 1 e 2 na mesma rodada | duas apostas independentes | UT-03 |
| Painel ocupado | segunda aposta no mesmo painel e rodada | PANEL_BUSY | UT-03 |
| Limites | 0,50 / 1.000,01 / 1,005 / 0 | BET_BELOW_MIN / BET_ABOVE_MAX / INVALID_AMOUNT / INVALID_AMOUNT | UT-04 |
| Cash-out automático inválido | 1,00x ou acima do limite | AUTO_CASHOUT_INVALID | UT-04 |
| Saldo insuficiente | aposta acima do saldo | INSUFFICIENT_FUNDS, nada persistido | IT-01 |
| Idempotência | mesmo clientBetId duas vezes (inclusive em paralelo) | um débito, mesmo BetAccepted | IT-03 |
| Débito incerto | carteira expira após debitar | estorno automático, aposta não aceita (WALLET_UNAVAILABLE) | IT-03 |
| Cancelamento permitido | placed na fase de apostas; queued durante o voo | cancelled, estorno integral | UT-05, IT-02 |
| Cancelamento negado | aposta active | CANCEL_NOT_ALLOWED | UT-05 |
| Isolamento | cancelar aposta de outro jogador; sem sessão | BET_NOT_FOUND; 401 | IT-04 |
| Eventos do motor | rodada real conduzida | Betting recebe aberto/voo/crash/anulada na ordem | IT-05 |
| Desempenho | 300 apostas | p99 ≤ 100 ms | IT-06 |
| Contrato | mensagens reais do hub | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — módulo Betting novo.

### 7.2 Testes Unitários
- **UT-01** — Dada uma rodada em apostas e limites BRL padrão, quando uma aposta de 10,00 chega antes de `bettingEndsAt`, então fica placed na rodada atual.
- **UT-02** — Dado o relógio do servidor em `bettingEndsAt` e depois durante o voo, quando apostas chegam, então ficam queued; quando abre a próxima rodada, então viram placed nela.
- **UT-03** — Dado um jogador com aposta no painel 1, quando aposta no painel 2 e de novo no painel 1, então a do painel 2 é aceita e a outra recebe PANEL_BUSY.
- **UT-04** — Dados os valores 0,50; 1.000,01; 1,005; 0 e autoCashOut 1,00x e 10.000,01x, quando validados, então BET_BELOW_MIN, BET_ABOVE_MAX, INVALID_AMOUNT, INVALID_AMOUNT, AUTO_CASHOUT_INVALID e AUTO_CASHOUT_INVALID.
- **UT-05** — Dadas apostas placed, queued e active, quando canceladas, então as duas primeiras são cancelled com estorno integral e a active recebe CANCEL_NOT_ALLOWED.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real (Postgres, Redis, carteira demo), quando um jogador aposta 10,00 e depois aposta acima do saldo, então saldo 2.990,00 com aposta placed e INSUFFICIENT_FUNDS sem aposta persistida.
- **IT-02** — Dada uma rodada voando, quando o jogador aposta, recebe BetActivated na rodada seguinte, aposta de novo e cancela na fase de apostas, então os estados e o saldo final batem com estornos integrais.
- **IT-03** — Dadas duas chamadas paralelas com o mesmo clientBetId e uma carteira de teste que expira depois de debitar, então existe um único débito para a primeira e a segunda situação termina estornada com WALLET_UNAVAILABLE.
- **IT-04** — Dadas sessões de dois jogadores, quando um cancela a aposta do outro e uma conexão sem token tenta apostar, então BET_NOT_FOUND e 401.
- **IT-05** — Dado o condutor real de uma sala, quando uma rodada passa por apostas, voo e crash (e outra é anulada), então o Betting recebe `RoundBettingOpened`, `RoundFlying`, `RoundCrashed` e `RoundAnnulled` na ordem.
- **IT-06** — Dadas 300 apostas sequenciais, quando medidas do recebimento à resposta, então p99 ≤ 100 ms (`Category=Performance`).

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens reais de IT-01/IT-02, quando validadas contra os tipos gerados do hub (SPEC-0006), então conferem (contrato v1 consumido pelas SPEC-0023, 0025 e 0026).

### 7.5 Testes E2E
- N/A — a jornada do jogador (apostar, retirar, perder) é provada pelos E2E da SPEC-0026, que consome este contrato (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** carteira de teste com falha programada (IT-03); limites e carteira reais nos demais.

**Ambiente de execução:** Testcontainers (Postgres, Redis); Host real.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** schema `betting` novo.
- **Compatibilidade:** Rounds ganha eventos de integração (aditivo).
- **Observabilidade:** métricas `betting.bets{status}`, `betting.rejected{code}`, latência de aceite; trace por betId.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Aposta dupla? — Sim, padrão do mercado (thomas, 2026-10-09)
- [x] Apostar durante o voo? — Sim, como aposta para a próxima rodada (thomas, 2026-10-09)
- [x] Cancelar? — Só na fase de apostas, com estorno integral (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0002, ADR-0005, ADR-0007, ADR-0008, ADR-0010, ADR-0015, ADR-0019 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05 com a tag `SPEC-0022:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 com a tag `SPEC-0022:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:02:00.57]     Dado um campo novo num DTO sem regenerar, quando contracts:che) — f224ab8 | 2026-10-10 |
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0022`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-10 | Esclarecimentos: o "limite da sala" do AUTO_CASHOUT_INVALID é o cap da sala no Rounds (o mesmo valor que o motor usa e persiste por rodada), não o padrão do catálogo; o débito de aposta queued vai com RoundId nulo (SPEC-0021@3); TransactionIds montados como `bet:{betId}:debit|rollback`; o IT-01 de saldo insuficiente usa um segundo jogador com saldo baixo no dublê da carteira (com a carteira real, o caso fica coberto pela SPEC-0021) | G1 do Test-writer: origem do limite da sala e RoundId do débito queued não definidos | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
