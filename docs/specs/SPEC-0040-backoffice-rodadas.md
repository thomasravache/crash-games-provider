---
id: SPEC-0040
title: "Backoffice: rodadas e auditoria provably fair"
tier: full
type: feature
user_facing: true
status: proposed
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0036, SPEC-0024]
consumes_contract: [SPEC-0024@1]
contract_version: 1
touches: ["src/frontend/apps/backoffice/src/rodadas/**", "tests/frontend/backoffice/rodadas/**", "tests/e2e/backoffice/rodadas/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**", "src/backend/Modules/Rounds/Rounds.Contracts/**", "src/backend/Modules/Rounds/Rounds.Features/Queries/**", "src/backend/Modules/Betting/Betting.Contracts/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0040 — Backoffice: rodadas e auditoria provably fair

## 1. Visão Geral
Histórico de rodadas com filtros e o painel de auditoria provably fair da rodada selecionada: compromisso, semente revelada, salt, ponto de crash recalculado, selo "Verificado" e download da prova em JSON.

## 2. Motivação & Escopo
**Motivação:** auditoria é argumento de venda e exigência de compliance; hoje só existe a verificação do jogador.

**Objetivos (dentro do escopo):**
- Lista paginada (cursor) de rodadas por sala, período, número e faixa de crash, com apostas/apostado/pago por rodada (resumo da SPEC-0024) e status (encerrada/anulada).
- Detalhe com verificação recalculada no servidor (SPEC-0007) e prova JSON assinável por terceiros.

**Não-objetivos (fora do escopo):**
- Reprocessar ou anular rodadas.

## 3. Dependências
- **Implementações necessárias:** SPEC-0036, SPEC-0024.
- **Contratos consumidos:** SPEC-0024@1
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** shell, papéis e auditoria da SPEC-0036; módulos donos dos dados expõem consultas por `Contracts` (ADR-0001). Referência: endpoints e testes do módulo Operators (SPEC-0020) e a tela/shell da SPEC-0036.

**Decisão:** o endpoint de backoffice fica no módulo Backoffice e compõe as consultas dos módulos donos por contratos de leitura; nenhum módulo lê o banco de outro.

**Justificativa:** respeita as fronteiras do monólito modular e mantém o escopo por operador num só lugar.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** endpoint direto em cada módulo (espalha papel e escopo); consulta SQL atravessando schemas (quebra ADR-0001/0010).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** lista ≤ 300 ms p95 com 100 mil rodadas.
- **Segurança:** leitura para admin, financeiro e suporte; operador vê só os próprios valores. Escopo por operador (ADR-0026) — IT de escopo.
- **Privacidade e dados pessoais:** identificador do jogador é o do operador; apelido sempre anonimizado na tela.
- **Disponibilidade e resiliência:** N/A — leitura.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST + porta de leitura.

```text
IRoundHistoryQuery (Rounds.Contracts).List(filter, cursor) → [{ roundId, number, salaId, startedAt, crashPoint?, status }]
GET /api/backoffice/rounds?salaId&from&to&number&crashMin&crashMax&cursor → { items:[{ roundId, number, startedAt, crashPoint, bets, wagered, paid, status }], next }
  | 400 INVALID_FILTER
GET /api/backoffice/rounds/{roundId}/audit → { commitment, revealedSeed, salt, recomputedCrash, verified:true|false, algorithm:"v1" }
GET /api/backoffice/rounds/{roundId}/proof.json → prova (mesmo formato da verificação pública da SPEC-0007/0008)
Rodada em andamento → audit 409 ROUND_NOT_FINISHED
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", tela "Rodadas e auditoria".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Lista filtrada | sala, período, faixa de crash | só as rodadas do filtro, em ordem | IT-01 |
| Paginação | 120 rodadas | cursor estável sem repetir | IT-01 |
| Filtro inválido | crashMin > crashMax | 400 INVALID_FILTER | UT-01 |
| Auditoria | rodada encerrada | verified:true e crash igual ao da rodada | IT-02 |
| Rodada em andamento | audit | 409 ROUND_NOT_FINISHED | IT-02 |
| Prova | download | JSON que o verificador público aceita | CT-01 |
| Escopo | operador vê rodadas | dados de apostas só do próprio operador | IT-03 |
| Tela | suporte audita | selo Verificado e botão de download | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados filtros válidos e inválidos (crashMin > crashMax, from > to), quando validados, então só os válidos passam.

### 7.3 Testes de Integração
- **IT-01** — Dadas 120 rodadas reais no PostgreSQL, quando listadas com filtros e paginação, então os resultados batem e o cursor não repete nem pula.
- **IT-02** — Dada uma rodada encerrada e uma em voo, quando a auditoria é pedida, então a encerrada é verificada com o mesmo ponto de crash e a em voo responde 409.
- **IT-03** — Dado um `operador-leitura`, quando lista rodadas, então os totais de apostas por rodada são só do próprio operador.

### 7.4 Testes de Contrato
- **CT-01** — Dada a prova JSON baixada, quando validada pelo verificador TS da SPEC-0010 e pelo C# da SPEC-0007, então ambos a aceitam.

### 7.5 Testes E2E
- **E2E-01** — Dado o suporte no backoffice após algumas rodadas, quando abre Rodadas e auditoria e seleciona uma rodada, então vê o selo Verificado com o mesmo ponto de crash do histórico do jogo e baixa a prova; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** rodadas reais geradas pelo motor com relógio acelerado.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A — leitura.
- **Compatibilidade:** endpoints novos sob `/api/backoffice/*`.
- **Observabilidade:** tempo por rota.
- **Rollback:** reverter o merge commit.
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
| G0 Spec | PASS | validate: 0 erro(s) — 32c27d1 | 2026-10-10 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0040`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
