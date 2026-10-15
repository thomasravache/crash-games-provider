---
id: SPEC-0064
title: Robustez dos testes intermitentes
tier: lite
type: fix
user_facing: false
status: proposed
created: 2026-10-15
parent: SPEC-0061
depends_on: [SPEC-0047]
consumes_contract: []
touches: ["tests/backend/Modules/Betting.Tests/**", "tests/backend/Modules/Wallet.Tests/**", "tests/backend/TestKit/**", "tests/e2e/pipa/bets/**", "tests/e2e/demo-site/**", "tools/ci/**"]
adrs: [ADR-0009]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0064 — Robustez dos testes intermitentes

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Testes falham de forma intermitente sob carga e passam ao repetir, custando tentativas de gate (G2 local e CI) sem regressão real: `SeamlessCreditRetryTests` (crédito pendente com o processo reiniciado; falhou no G2 da SPEC-0045, passa 3/3 isolado), `LateDebitReusedClientBetIdTests` (débito retido além da varredura; idem), `MultiInstanceCashOutTests` (líder impedido além de 200 ms; troca de rodada no meio do teste, falhou na CI dos PRs #31 e #38), `SettlementRaceTests` (alerta de conciliação conferido sem esperar), os E2E da Pipa SPEC-0026:E2E-02 (aposta perdida) e E2E-04 (aposta dupla: volta a APOSTAR antes de virar RETIRAR, Firefox e webkit-mobile, PRs #31 e #34) e SPEC-0047:E2E-01 (aposta PRÓXIMA não ativada em 190 s, uma vez). Esperado: o mesmo resultado em toda execução, com ou sem carga.

## 2. Causa Raiz
Hipóteses por teste, a confirmar na caracterização: asserções com prazo fixo curto em vez de espera pelo evento (SettlementRace, SeamlessCreditRetry), dependência da fase da rodada sem controlar o relógio (MultiInstanceCashOut, LateDebit: a rodada vira no meio do cenário quando a máquina atrasa) e, nos E2E, ação clicada sem esperar a fase de apostas aberta ou a confirmação do débito. Se a causa estiver no código de produção (por exemplo a ativação da aposta enfileirada), a correção vira uma spec própria (impedimento `trabalho`).

## 3. Mudança Proposta
Muda: os testes passam a esperar eventos/estados em vez de prazos fixos e a controlar o relógio (FakeTimeProvider/TestKit) onde a fase da rodada importa; os E2E esperam a fase e a confirmação antes de agir; um script `tools/ci/stress-flaky` roda os testes N vezes com carga sintética de CPU para provar a estabilidade.

Não muda: o comportamento do produto, os limites de desempenho (SPEC-0052) e as asserções do que cada teste prova.

**Padrão seguido:** esperas por evento já usadas em `tests/backend/TestKit/` e o `FakeTimeProvider` dos testes da SPEC-0044; nos E2E, os helpers de `tests/e2e/pipa/bets/support/bets.ts`

**Rollback:** revert dos commits (só testes e script)

**Outras ocorrências:** O teste de mutação do Fairness (Ci.Tests, Stryker) falhou uma vez localmente durante a SPEC-0047; entra na caracterização para decidir se é do mesmo tipo.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0064:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- **CH-01** — Dado `tools/ci/stress-flaky` com carga sintética (um processo de CPU por núcleo), quando cada teste listado roda 20 vezes no código atual, então a taxa de falha de cada um fica registrada (linha de base que reproduz a intermitência).
- **UT-01** — Dada a mesma carga sintética, quando os testes .NET listados rodam 20 vezes depois da correção, então 20/20 passam e as asserções continuam as mesmas (mesmo resultado esperado em cada teste).
- **E2E-01** — Dada a mesma carga sintética, quando SPEC-0026:E2E-02, SPEC-0026:E2E-04 e SPEC-0047:E2E-01 rodam 10 vezes em Chromium, Firefox e webkit-mobile, então 10/10 passam em cada navegador.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma.

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
