---
id: SPEC-0055
title: Aviso de limite da rodada e regras na Pipa
tier: lite
type: feature
user_facing: "true"
status: approved
created: 2026-10-13
parent: SPEC-0019
depends_on: [SPEC-0054]
consumes_contract: [SPEC-0054@1]
touches: [src/frontend/apps/game-pipa/src/bets/**, src/frontend/apps/game-pipa/src/hud/**, src/frontend/packages/game-ui/src/**, tests/frontend/pipa/bets/**, tests/frontend/pipa/rules/**, tests/e2e/pipa/bets/**, tests/e2e/pipa/rules/**]
adrs: [ADR-0004, ADR-0020, ADR-0025]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-13
---

# SPEC-0055 — Aviso de limite da rodada e regras na Pipa

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Com o corte por exposição (SPEC-0054), uma aposta pode ser retirada automaticamente pelo limite da rodada. Hoje a Pipa não diferencia isso de nenhum outro cash-out e não tem tela de regras. O jogador veria o ganho parar sem explicação, e não teria onde consultar o RTP, o multiplicador máximo e os limites de ganho. Reguladores costumam exigir isso, e o thomas pediu aviso na tela e nas regras (2026-10-13).

**Esperado:**
- **Aviso:** um `CashedOut` com `reason:"round_limit"` mostra "Retirada automática: limite de ganho da rodada atingido" junto do valor ganho.
- **Botão "Regras":** abre um painel com o RTP, o multiplicador máximo, o ganho máximo por aposta, o limite de ganho por rodada e uma frase explicando o corte.

## 2. Causa Raiz
Funcionalidade nova, sem defeito no código atual.
- O painel de aposta (`apps/game-pipa/src/bets/**`) trata `CashedOut` sem olhar `reason`, que ainda não existe.
- A HUD (`packages/game-ui/src/hud.tsx`) não tem entrada de regras.
- Os dados já chegam ao cliente:
  - `sala.rtp` e `sala.cap` pelo hub da rodada;
  - `maxWin` e `maxRoundWin` pelo `GetLimits` (SPEC-0054).

## 3. Mudança Proposta
- No tratamento de `CashedOut`, quando `reason === "round_limit"`, o aviso usa o texto do limite, anunciado em `aria-live` como os outros avisos do painel.
- **Botão "Regras" na HUD**, abrindo um painel modal acessível (foco preso, `Esc` fecha, rótulos). O painel mostra:
  - RTP em % (0,97 → "97%");
  - multiplicador máximo (`cap`);
  - ganho máximo por aposta;
  - limite de ganho por rodada, em Money formatado na moeda;
  - o texto: "Se a soma dos ganhos da rodada atingir o limite do operador, todas as apostas abertas são retiradas automaticamente no multiplicador em que o limite é atingido."
- **Não muda:** o motor, os valores, o backend, nem o comportamento do cash-out (SPEC-0054).

**Padrão seguido:**
- avisos do painel e formatação de Money da SPEC-0026 (`apps/game-pipa/src/bets/**`);
- componentes acessíveis de `packages/game-ui` (HUD);
- E2E em `tests/e2e/pipa/bets/**`.

**Rollback:** revert do merge commit.

**Outras ocorrências:** Nenhuma. A Pipa é o único jogo hoje; um jogo novo herda a HUD do `game-ui`.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — os avisos atuais de cash-out são cobertos pelos testes da SPEC-0026, que continuam passando.
- **UT-01** — Dados um `CashedOut{auto:true, reason:"round_limit", payout:"4.000,00"}` e um `CashedOut{auto:true}` sem `reason`, quando o painel processa cada um, então o primeiro mostra "Retirada automática: limite de ganho da rodada atingido" com R$ 4.000,00 e o segundo mostra o aviso de automático atual. Dados `rtp "0.97"`, `cap "10000.00"`, `maxWin "100000.00"` e `maxRoundWin "500000.00"` em BRL, quando o painel de regras é montado, então mostra "97%", "10.000,00x", "R$ 100.000,00" e "R$ 500.000,00".
- **E2E-01** — Dado o ambiente E2E com crash fixo e limite de rodada baixo para o operador de teste (fixture da SPEC-0054), quando o jogador aposta e o voo passa do corte, então a tela mostra o aviso de limite da rodada e o saldo atualizado; e quando abre "Regras" pelo teclado, então vê os quatro valores e fecha com `Esc`, com o foco voltando ao botão. Roda em Chromium e Firefox, como os E2E da Pipa.

## 5. Questões em Aberto
- [x] Como o jogador fica sabendo do corte? — Aviso na tela e regras do jogo (thomas, 2026-10-13)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Aviso e regras**
- [ ] Red: UT-01 e E2E-01 com a tag `SPEC-0055:<ID>`, falhando pelo motivo certo
- [ ] Green: aviso de `round_limit` e painel "Regras" acessível
- [ ] Validar: build, lint, typecheck, testes do front e E2E (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
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
