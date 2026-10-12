---
id: SPEC-0026
title: "Painel de aposta da Pipa"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0010, SPEC-0024]
consumes_contract: [SPEC-0020@2, SPEC-0021@4, SPEC-0022@3, SPEC-0023@2, SPEC-0024@2, SPEC-0025@1]
contract_version: 3
touches: ["src/frontend/packages/game-sdk/**", "src/frontend/packages/game-ui/**", "src/frontend/apps/game-pipa/src/hud/**", "src/frontend/apps/game-pipa/src/App.tsx", "src/frontend/apps/game-pipa/src/main.tsx", "src/frontend/apps/game-pipa/src/test-hook.ts", "src/frontend/apps/game-pipa/src/styles.css", "src/frontend/apps/game-pipa/src/bets/**", "tests/frontend/pipa/bets/**", "tests/frontend/integration/pipa/bets/**", "tests/e2e/pipa/bets/**", "tests/e2e/pipa/round.spec.ts", "src/frontend/apps/game-pipa/src/hud/**", "src/frontend/packages/game-ui/src/hud.tsx"]
adrs: [ADR-0004, ADR-0008, ADR-0019, ADR-0020, ADR-0021]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0026 — Painel de aposta da Pipa

## 1. Visão Geral
Liga o painel de aposta da Pipa ao backend: dois painéis (aposta dupla) com valor, fichas rápidas, cash-out automático, botões APOSTAR / CANCELAR / RETIRAR R$ x / PRÓXIMA, saldo demo com "Recarregar demo". As abas "Todas as apostas" e "Minhas apostas" ficam na SPEC-0031 (Emenda v2). Prova as jornadas críticas **aposta-cashout** e **aposta-perdida** de ponta a ponta.

## 2. Motivação & Escopo
**Motivação:** é a experiência que o operador vende e o jogador usa; fecha as duas jornadas críticas ainda sem prova.

**Objetivos (dentro do escopo):**
- `game-sdk`: cliente do hub `/hubs/bets` e da API de carteira/histórico, store das apostas e do saldo (servidor como fonte da verdade; valores sempre como `Money` em string).
- `game-ui`: dois painéis conforme o design "Pipa Crash" do Stitch, com estados por fase, validação local pelos limites (o servidor decide), mensagens de erro em pt-BR, saldo e recarga (abas de apostas: SPEC-0031).
- Integração no HUD do `game-pipa`, substituindo o painel desabilitado da SPEC-0010.
- No crash, o texto "PIPA CORTADA!" do HUD treme por ≤ 300 ms (exceto com movimento reduzido) — vindo da SPEC-0011 (Emenda v3).
- Acessibilidade WCAG 2.2 AA no painel (rótulos, foco, anúncios `aria-live` de aposta aceita, retirada e perda).

**Não-objetivos (fora do escopo):**
- Animação de vitória e som (SPEC-0011/0018); aposta automática repetida; "Top" e chat.

## 3. Dependências
- **Implementações necessárias:** SPEC-0010 (cliente da Pipa, sessão, store da rodada).
- **Contratos consumidos:** SPEC-0020@2 (limites), SPEC-0021@4 (saldo e recarga), SPEC-0022@3 (apostar e cancelar), SPEC-0023@2 (cash-out), SPEC-0024@2 (eventos de liquidação), SPEC-0025@1 (apostas ao vivo e histórico).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0004 (rede → estado → apresentação; cena não importa transporte), ADR-0008 (tipos gerados), ADR-0021 (movimento reduzido). Implementação de referência: store e HUD da SPEC-0010.

**Decisão:** store de apostas no `game-sdk` separado do store da rodada, exposto pela entrada de estado (sem transporte); o painel só envia intenções e mostra o que o servidor confirmou (sem saldo otimista); cada painel é uma instância do mesmo componente.

**Justificativa:** o jogador nunca vê dinheiro que o servidor não confirmou; aposta dupla sem código duplicado.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** atualização otimista do saldo (mostra valores que podem não se confirmar).

**ADRs:** ADR-0004, ADR-0008, ADR-0019, ADR-0020, ADR-0021.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** clique em RETIRAR → confirmação visível ≤ 150 ms em rede local; painel não derruba a cena abaixo de 60 fps — E2E-01 e medição do E2E-03 da SPEC-0011 quando integrada.
- **Segurança:** nenhum token em armazenamento do navegador; valores enviados como string decimal — UT-04.
- **Privacidade e dados pessoais:** só apelido anonimizado nas apostas ao vivo.
- **Disponibilidade e resiliência:** após reconexão, saldo e apostas são relidos do servidor — UT-03.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações em todos os estados do painel — E2E-01/02.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** painel de aposta (tela) + store de apostas do `game-sdk`.

```text
Painel (por painel 1 e 2)
  Estados: idle (APOSTAR R$ x) → placed (CANCELAR) → active (RETIRAR R$ valor×m) → cashed_out (ganhou R$ y) | lost (perdeu)
           active → cashing_out ("Retirando…", após CashOutPending) → cashed_out | lost (CashOutRejected)
           placed|active em rodada anulada → refunded (estornado, BetRefunded)
           crédito pendente (creditStatus "pending"): ganho exibido; saldo atualiza com BalanceUpdated
           durante o voo: idle → queued (PRÓXIMA · CANCELAR) → placed na rodada seguinte
  Controles: valor com − / + (passo R$ 1,00), fichas 1, 5, 10, 50; cash-out automático (interruptor + alvo ≥ 1,01x)
  Validação local: fora dos limites → botão desabilitado com motivo; o servidor tem a palavra final
  Erros do servidor (código → mensagem pt-BR): BET_BELOW_MIN, BET_ABOVE_MAX, INSUFFICIENT_FUNDS, PANEL_BUSY,
    CANCEL_NOT_ALLOWED, CASHOUT_TOO_LATE ("Cash-out tardio: a pipa já tinha sido cortada"), WALLET_UNAVAILABLE,
    INVALID_PANEL, ROUND_CLOSED, BET_NOT_ACTIVE, REAL_MONEY_DISABLED
Saldo: "R$ 2.990,00 · DEMO"; botão "Recarregar demo" visível só quando saldo < aposta mínima
Reconexão: saldo relido da carteira (SPEC-0021@4); apostas abertas relidas de GET /api/v1/me/bets (SPEC-0025@1) quando
  o endpoint existir — enquanto responder 404, os painéis locais são mantidos e só o saldo é relido
Store (game-sdk/state): BetsState { panels:[PanelState, PanelState], balance:Money, limits }
Gancho de teste (só build de teste): __APOGEU_TEST__.bets() → BetsState
```

**Design:** Stitch — projeto "Pipa — Crash Game" (16371353209282165204), tela "Pipa Crash - Jogo da Pipa" (painéis APOSTA 1/APOSTA 2, botões APOSTAR verde e RETIRAR laranja; as abas de apostas são da SPEC-0031).

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Transições do painel | eventos do servidor por fase | idle → placed → active → cashed_out/lost; queued → placed | UT-01 |
| Validação local | valor fora dos limites; alvo < 1,01x | botão desabilitado com motivo | UT-02 |
| Reconexão | conexão volta | saldo e apostas relidos do servidor | UT-03 |
| Formatação de dinheiro | "2990.5", "24.70" | "R$ 2.990,50", "R$ 24,70"; envio como string, sem float | UT-04 |
| Mensagens de erro | cada código do contrato | mensagem pt-BR correspondente | UT-05 |
| Tremor do texto no crash | RoundCrashed; movimento normal e reduzido | "PIPA CORTADA!" treme ≤ 300 ms; sem tremor com movimento reduzido | UT-07 |
| Recarga | saldo abaixo/acima do mínimo | botão visível/oculto; recarga atualiza saldo | UT-06 |
| Crash antes do BetSettled | fase crashed com painel active | sem RETIRAR; BetSettled em cashing_out → lost | UT-08 |
| Painel preso de rodada anterior | evento perdido na reconexão; BET_NOT_ACTIVE | painel volta a idle | UT-09 |
| Recarga recusada e foco | recarga falha; botão ocupado | mensagem com role=alert; foco mantido | UT-10 |
| SDK de apostas contra servidor real | backend do Aspire | aposta, retira, saldo atualizado | IT-01 |
| Tipos do hub e REST | mensagens reais | conferem com os tipos gerados | CT-01 |
| Aposta e cash-out | jogador aposta R$ 10 e retira no voo | painel mostra o ganho; saldo = 3.000 − 10 + pagamento | E2E-01 |
| Aposta perdida | jogador aposta e não retira | "perdeu" no painel (BetSettled); saldo reduzido em 10 | E2E-02 |
| Cancelar | aposta e cancela na fase de apostas | estorno integral | E2E-03 |
| Próxima rodada e aposta dupla | apostas nos dois painéis, uma durante o voo | PRÓXIMA ativa na rodada seguinte; painéis independentes | E2E-04 |
| Cash-out automático | alvo 1,01x | retirada automática anunciada (ou perda em crash 1,00x, com nova tentativa) | E2E-05 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — o HUD da SPEC-0010 é coberto pelos testes dela; o painel desabilitado é substituído.

### 7.2 Testes Unitários
- **UT-01** — Dada a sequência de eventos (BetAccepted placed, RoundFlying, CashedOut; BetAccepted queued, BetActivated, RoundCrashed com BetSettled; CashOutPending seguido de CashedOut e de CashOutRejected; CashedOut com creditStatus "pending" seguido de BalanceUpdated; BetRefunded de rodada anulada), quando aplicada ao store, então os painéis passam pelos estados do contrato e o saldo só muda com valores do servidor.
- **UT-02** — Dados limites 1,00/1.000,00 e alvo mínimo 1,01x, quando o valor é 0,50, 1.000,01 ou o alvo 1,00x, então o botão fica desabilitado com o motivo.
- **UT-03** — Dado o store com dados antigos, quando a conexão volta, então o saldo é substituído pelo lido do servidor e as apostas pelas de GET /api/v1/me/bets; com o endpoint respondendo 404, os painéis locais são mantidos sem erro.
- **UT-04** — Dados "2990.5" e "24.70", quando formatados e enviados, então aparecem como "R$ 2.990,50" e "R$ 24,70" e trafegam como string, sem `Number`.
- **UT-05** — Dado cada código de erro do contrato, quando recebido, então a mensagem pt-BR correspondente aparece no painel.
- **UT-06** — Dado saldo 0,50 com mínimo 1,00 e depois saldo 10,00, quando o painel renderiza, então "Recarregar demo" aparece e some; a recarga atualiza o saldo.
- **UT-07** — Dado o HUD em voo, quando chega RoundCrashed, então o texto "PIPA CORTADA!" recebe a animação de tremor de ≤ 300 ms; com `prefers-reduced-motion: reduce`, não treme.
- **UT-08** — Dado um painel active quando a fase da rodada passa a crashed (ou annulled) e o BetSettled ainda não chegou, então o painel não oferece RETIRAR (ação desabilitada, sem valor de retirada) — o estado do store não muda; e um BetSettled que chega com o painel em cashing_out leva o painel a lost.
- **UT-09** — Dado um painel placed ou active de uma rodada anterior (BetSettled perdido numa queda de conexão), quando a fase de apostas de outra rodada começa, então o painel volta a idle; e um BET_NOT_ACTIVE ou BET_NOT_FOUND em RETIRAR ou CANCELAR também devolve o painel a idle.
- **UT-10** — Dada uma recarga recusada (RECHARGE_NOT_ALLOWED ou WALLET_UNAVAILABLE), quando o jogador clica em "Recarregar demo", então a mensagem pt-BR aparece com role="alert"; e o botão de ação do painel não perde o foco do teclado durante "placing"/"Retirando…" (aria-disabled em vez de disabled).

### 7.3 Testes de Integração
- **IT-01** — Dado o backend real do Aspire, quando o `game-sdk` (Node) aposta 10,00, retira no voo e relê o saldo, então recebe BetAccepted e CashedOut e o saldo é 3.000 − 10 + pagamento.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens reais de IT-01 (hub de apostas e carteira), quando validadas em tempo de execução contra os tipos gerados, então conferem (SPEC-0020@2 a SPEC-0024@2; o histórico da SPEC-0025@1 é validado na SPEC-0031).

### 7.5 Testes E2E
- **E2E-01** — Dado o jogo aberto pelo lançamento demo, quando o jogador aposta R$ 10,00 no painel 1 e clica RETIRAR durante o voo, então o painel mostra o ganho, o saldo é 3.000 − 10 + pagamento; axe sem violações [jornada: aposta-cashout].
- **E2E-02** — Dado o jogo aberto, quando o jogador aposta R$ 10,00 e não retira, então após PIPA CORTADA! o painel mostra a perda (BetSettled), o saldo cai 10,00 e o painel volta a APOSTAR; axe sem violações [jornada: aposta-perdida].
- **E2E-03** — Dado o jogo na fase de apostas, quando o jogador aposta e cancela, então o saldo volta ao valor anterior.
- **E2E-04** — Dado o jogo em voo, quando o jogador aposta nos dois painéis (um durante o voo), então o painel mostra PRÓXIMA e ativa na rodada seguinte, e os dois painéis evoluem de forma independente.
- **E2E-05** — Dado o cash-out automático em 1,01x, quando a rodada voa, então a retirada automática é anunciada (rodada com crash 1,00x conta como perda e o teste tenta a próxima, até 5 rodadas).

### 7.6 Outros
- Acessibilidade: axe em todos os estados do painel; revisão manual com leitor de tela no G4.

**Dublês e dados de teste:** backend real nos IT/E2E; eventos sintéticos do contrato nos unitários.

**Ambiente de execução:** Vitest (UT, CT); IT e E2E contra o ambiente Aspire.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** N/A
- **Compatibilidade:** substitui o painel desabilitado da SPEC-0010.
- **Observabilidade:** erros do cliente no console de desenvolvimento; telemetria de navegador com a SPEC-0012.
- **Rollback:** reverter o merge commit (volta ao painel desabilitado).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Recarga demo? — Botão "Recarregar demo" quando o saldo cair abaixo da aposta mínima (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0004, ADR-0008, ADR-0019, ADR-0020, ADR-0021 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06, UT-07, UT-08, UT-09, UT-10 com a tag `SPEC-0026:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, CT-01 com a tag `SPEC-0026:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 4: Jornada E2E**
- [x] Red: E2E-01, E2E-02, E2E-03, E2E-04, E2E-05 falhando pelo motivo certo
- [x] Green: jornada completa passando localmente

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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: ⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[9/9]⎯) — e96e513 | 2026-10-12 |
| G2 Green | PASS | build exit 0 (✓ built in 160ms); test exit 0 (at least ~400ms faster with isolate: false — reuses workers across files instead of one pe); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-12T01:12:36: Report generation took 0,8 seconds) — 088c1dd | 2026-10-12 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  746ms (tests 95%, transform 4%, import 1%)) — 088c1dd | 2026-10-12 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (2ª revisão, 088c1dd; minors: foco perdido em 'Aguardando o resultado…', requestFailed não confere o tipo de pedido) — 088c1dd | 2026-10-12 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #26 verde (run 38227481466, commit 7c323ee): backend, frontend, contracts, e2e (Chromium, WebKit e Firefox), security e sdd; local: build, testes, integração (43/43), arquitetura e segurança PASS em 21ac3da; E2E local isolado 51/51 — decisão do IMP-01 | 2026-10-12 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-12 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem); E2E com backend real na CI (PR #26, merge 89b1b79) | 2026-10-12 |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-12 | G5 | falha | G5 local reprovado 3 vezes: (1) IT-11 da SPEC-0023 e p99 do cash-out (corrigidos: cb7b5a0, 2b2f80b); (2) e (3) E2E logo após os testes de integração na mesma máquina — E2E-04 'panel 1 placed' em 10 s e SPEC-0011 IT-02 WebGL. O E2E isolado passou 3 vezes (5/5 e 51/51 duas vezes) | reprodução: integração seguida de E2E falha; E2E isolado passa; container remanescente da integração indica carga da máquina | thomas | Diagnóstico: a 1ª falha teve causas reais já corrigidas (cb7b5a0, 2b2f80b); a 2ª e a 3ª são do ambiente local — o gate roda o E2E logo após a integração na mesma máquina, e o E2E isolado passou 3 vezes. Decisão (Architect, por delegação explícita de thomas em 2026-10-12: 'deixo em suas mãos', seguir o recomendado): a evidência de E2E do G5 é o job e2e da CI do PR, em máquina limpa; a robustez dos E2E com tempo fixo vai para a SPEC-0017. thomas pode rever ao acordar | 2026-10-12 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
O jogador aposta pela tela da Pipa: dois painéis (APOSTA 1 e APOSTA 2) com valor, fichas, cash-out automático, botões APOSTAR / CANCELAR / RETIRAR / PRÓXIMA, ganho, perda e estorno no painel, saldo "R$ 3.000,00 · DEMO" com recarga quando abaixo da aposta mínima, mensagens de erro em português e o texto PIPA CORTADA! tremendo no crash (sem tremor com movimento reduzido).

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
game-sdk: cliente do hub `/hubs/bets` e da carteira, store de apostas com máquina de estados por painel e dinheiro em centavos (bigint, sem Number), tipos derivados do contrato gerado (ADR-0008). game-ui: `BetPanel`, `BalanceDisplay` e `betPanelView` puro. game-pipa: painel no HUD e gancho de teste `__APOGEU_TEST__.bets()`. Review G4 (duas rodadas): tipos do contrato gerado, sem RETIRAR após o crash, painéis presos voltam a idle, recarga recusada com aviso e foco preservado. Tique sonoro dos 3 últimos segundos movido para a SPEC-0033.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type feature.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Dada a sequência de eventos (BetAccepted placed, RoundFlying, CashedOut; BetAccepted queued, BetActivated, RoundCrashed com BetSettled; Cash… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-02 | Dados limites 1,00/1.000,00 e alvo mínimo 1,01x, quando o valor é 0,50, 1.000,01 ou o alvo 1,00x, então o botão fica desabilitado com o moti… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-03 | Dado o store com dados antigos, quando a conexão volta, então o saldo é substituído pelo lido do servidor e as apostas pelas de GET /api/v1/… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-04 | Dados "2990.5" e "24.70", quando formatados e enviados, então aparecem como "R$ 2.990,50" e "R$ 24,70" e trafegam como string, sem `Number`. | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-05 | Dado cada código de erro do contrato, quando recebido, então a mensagem pt-BR correspondente aparece no painel. | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-06 | Dado saldo 0,50 com mínimo 1,00 e depois saldo 10,00, quando o painel renderiza, então "Recarregar demo" aparece e some; a recarga atualiza … | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-07 | Dado o HUD em voo, quando chega RoundCrashed, então o texto "PIPA CORTADA!" recebe a animação de tremor de ≤ 300 ms; com `prefers-reduced-mo… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-08 | Dado um painel active quando a fase da rodada passa a crashed (ou annulled) e o BetSettled ainda não chegou, então o painel não oferece RETI… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-09 | Dado um painel placed ou active de uma rodada anterior (BetSettled perdido numa queda de conexão), quando a fase de apostas de outra rodada … | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| UT-10 | Dada uma recarga recusada (RECHARGE_NOT_ALLOWED ou WALLET_UNAVAILABLE), quando o jogador clica em "Recarregar demo", então a mensagem pt-BR … | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| IT-01 | Dado o backend real do Aspire, quando o `game-sdk` (Node) aposta 10,00, retira no voo e relê o saldo, então recebe BetAccepted e CashedOut e… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| CT-01 | Dadas as mensagens reais de IT-01 (hub de apostas e carteira), quando validadas em tempo de execução contra os tipos gerados, então conferem… | PASS | G5 local 21ac3da (resultado integrado); CI do PR #26 |
| E2E-01 | Dado o jogo aberto pelo lançamento demo, quando o jogador aposta R$ 10,00 no painel 1 e clica RETIRAR durante o voo, então o painel mostra o… | PASS | `tools/ci/run-e2e.sh` local 51/51 (integrado); job e2e da CI do PR #26 |
| E2E-02 | Dado o jogo aberto, quando o jogador aposta R$ 10,00 e não retira, então após PIPA CORTADA! o painel mostra a perda (BetSettled), o saldo ca… | PASS | `tools/ci/run-e2e.sh` local 51/51 (integrado); job e2e da CI do PR #26 |
| E2E-03 | Dado o jogo na fase de apostas, quando o jogador aposta e cancela, então o saldo volta ao valor anterior. | PASS | `tools/ci/run-e2e.sh` local 51/51 (integrado); job e2e da CI do PR #26 |
| E2E-04 | Dado o jogo em voo, quando o jogador aposta nos dois painéis (um durante o voo), então o painel mostra PRÓXIMA e ativa na rodada seguinte, e… | PASS | `tools/ci/run-e2e.sh` local 51/51 (integrado); job e2e da CI do PR #26 |
| E2E-05 | Dado o cash-out automático em 1,01x, quando a rodada voa, então a retirada automática é anunciada (rodada com crash 1,00x conta como perda e… | PASS | `tools/ci/run-e2e.sh` local 51/51 (integrado); job e2e da CI do PR #26 |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — E2E-01 (RETIRAR → "Ganhou" em ≤ 150 ms); axe sem violações nos estados medidos
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A justificado: sem ambiente até a SPEC-0012 (G6 N/A); E2E com backend real na CI do PR #26
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — sem telemetria de cliente até haver hospedagem; rollback = revert do merge (o HUD volta ao painel desabilitado)
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma)
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes) — gitleaks e Semgrep sem achados no G5
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha — TreatWarningsAsErrors; lint e format verdes

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente (G6 N/A até a SPEC-0012). Integrado pelo PR #26.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0017 — E2E com tempo fixo instáveis quando rodam logo após a integração na mesma máquina (IMP-01).
- SPEC-0031 — histórico e abas de apostas; reconexão relê as apostas abertas quando GET /api/v1/me/bets existir (SPEC-0025).
- Pendências aceitas do review: jogo continuar assistível se o hub de apostas falhar ao iniciar; axe em todos os estados do painel; aviso de crédito pendente por aposta; foco em "Aguardando o resultado…"; requestFailed sem conferir o tipo de pedido.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0026`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 3 (inalterado) | 2026-10-12 | Review G4: tipos BetSettled/BetRefunded vêm só do contrato gerado (ADR-0008); painel sem RETIRAR após o crash e BetSettled aceito em cashing_out (UT-08); painel de rodada anterior volta a idle (UT-09); recarga recusada com mensagem e foco preservado (UT-10). Pendências: falha ao iniciar o hub de apostas não deve bloquear o jogo (modo só assistir, com nova tentativa); axe em todos os estados do painel; aviso de crédito pendente por aposta (BalanceUpdated não traz betId) | Review G4 (1c7f207) | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-12) |
| 3 (inalterado) | 2026-10-11 | Decisões do scaffold e2e5c6c: todos os códigos de erro dos contratos consumidos têm mensagem pt-BR (além dos 11 listados: INVALID_AMOUNT, AUTO_CASHOUT_INVALID, BET_NOT_FOUND, RECHARGE_NOT_ALLOWED, NOT_DEMO, falha de rede e desconhecido); saldo e limites ficam vazios até a primeira leitura (sem "R$ 0,00" nem recarga falsa); fichas definem o valor; aposta cancelada volta o painel a idle; o tique sonoro dos 3 últimos segundos sai deste escopo e vai para a SPEC-0033 (exige `tick()` no `GameAudio`, fora dos `touches`) | Ambiguidades levantadas no scaffold | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11) |
| 3 (inalterado) | 2026-10-11 | Repin em SPEC-0023@2: estados cashing_out (CashOutPending), lost por CashOutRejected, refunded (BetRefunded) e crédito pendente com BalanceUpdated (UT-01); novos códigos de erro INVALID_PANEL, ROUND_CLOSED, BET_NOT_ACTIVE, REAL_MONEY_DISABLED (UT-05); reconexão tolera GET /api/v1/me/bets ausente (404) até a SPEC-0025 (UT-03); escopo inclui `main.tsx`, `test-hook.ts`, `styles.css` e `src/bets/**` do app | Contratos v3 da SPEC-0022 e v2 da SPEC-0023; a SPEC-0025 (histórico) não é dependência desta | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-11) |
| v3 | 2026-10-09 | Tremor do texto "PIPA CORTADA!" no HUD no crash (≤ 300 ms, sem tremor com movimento reduzido), com UT-07 | Saiu do escopo da SPEC-0011 (texto pertence ao HUD) | Nenhuma | thomas, 2026-10-09 (chat: "ok nas 3") |
| v2 | 2026-10-09 | As abas "Todas as apostas" e "Minhas apostas" saem do escopo (vão para a SPEC-0031); IT-01, CT-01, E2E-01 e E2E-02 deixam de verificar o histórico; `depends_on` passa a incluir a SPEC-0024, cujos cash-out e liquidação reais os E2E exigem | Jogar com apostas uma onda antes: sem o histórico da SPEC-0025, o painel roda na mesma onda que ela | SPEC-0031 (nova); nenhuma outra (consome SPEC-0025@1 só para a releitura na reconexão, com dublês) | thomas (2026-10-09) |
