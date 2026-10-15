---
id: SPEC-0062
title: "Abas de apostas: robustez e testes"
tier: lite
type: fix
user_facing: true
status: proposed
created: 2026-10-15
parent: SPEC-0061
depends_on: [SPEC-0031]
consumes_contract: []
touches: ["src/frontend/packages/game-sdk/**", "src/frontend/packages/game-ui/**", "src/frontend/apps/game-pipa/src/hud/**", "tests/frontend/pipa/bets-tabs/**"]
adrs: [ADR-0009, ADR-0019]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0062 — Abas de apostas: robustez e testes

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Pontos do review (G4) da SPEC-0031: (1) um lote atrasado da rodada anterior, chegado depois do `RoundBetting` da nova rodada, reexibe as apostas antigas em "Todas as apostas"; (2) quando uma aposta termina com "Minhas apostas" aberta, só a página 1 é relida e as páginas carregadas somem; (3) o cancelamento de uma aposta não atualiza o histórico; (4) "Tentar de novo" depois de falha do "Carregar mais" relê a página 1 em vez da página que falhou; (5) faltam testes da nova entrada na sala ao reconectar, da falha do `JoinSalaBets` sem derrubar o `start()` e do descarte de página antiga por geração; (6) o UT-02 procura "disabled" na string de atributos e casa com classes do Tailwind; (7) o `tabpanel` vazio não recebe foco.

## 2. Causa Raiz
`state/live-bets.ts` (case `liveBatch`) aceita lote de qualquer rodada e redefine o `roundId`; `hud/pipa-bets-tabs.tsx` só relê a página 1 e não trata cancelamento; `my-bets-tab.tsx` liga "Tentar de novo" ao `loadMyBets`; o teste usa `attributesOf(...).not.toContain("disabled")`.

## 3. Mudança Proposta
Muda: lote de rodada diferente da atual (definida pelo último `RoundBetting`) é ignorado; ao terminar ou cancelar uma aposta com "Minhas apostas" aberta, a primeira página é mesclada sem perder as já carregadas; "Tentar de novo" repete o cursor que falhou; testes novos para os casos sem cobertura; o UT-02 passa a conferir o atributo `disabled` do botão; `tabpanel` com `tabIndex=0` quando vazio.

Não muda: o contrato SPEC-0025@4, os painéis de aposta e o visual (ajustes visuais só após a aprovação de thomas).

**Padrão seguido:** stores e `BetsClient` da SPEC-0031 em `game-sdk`; componentes de abas em `game-ui`

**Rollback:** revert dos commits (só cliente e UI)

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0062:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — UT-01 e UT-02 da SPEC-0031 cobrem o comportamento atual.
- **UT-01** — Dados um `RoundBetting` da rodada N+1 e depois um lote atrasado da rodada N, quando aplicados ao store, então "Todas as apostas" continua vazia; dada uma reconexão, então o cliente entra de novo na sala; dada a falha do `JoinSalaBets`, então o `start()` conclui; dada uma página antiga que chega depois de uma releitura, então é descartada.
- **UT-02** — Dadas duas páginas carregadas em "Minhas apostas", quando uma aposta termina ou é cancelada, então a primeira página é mesclada e as demais continuam; dada a falha do "Carregar mais", quando o jogador clica "Tentar de novo", então a mesma página (mesmo cursor) é pedida; o botão é conferido pelo atributo `disabled`.
- Integração/E2E: N/A — o E2E-01 da SPEC-0031 continua cobrindo a jornada; os defeitos estão no estado do cliente.

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
