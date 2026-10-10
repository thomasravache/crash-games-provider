---
id: SPEC-0041
title: "Backoffice: busca de apostas"
tier: full
type: feature
user_facing: true
status: proposed
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0036, SPEC-0024]
consumes_contract: [SPEC-0024@1, SPEC-0021@4]
contract_version: 1
touches: ["src/frontend/apps/backoffice/src/apostas/**", "tests/frontend/backoffice/apostas/**", "tests/e2e/backoffice/apostas/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**", "src/backend/Modules/Betting/Betting.Contracts/**", "src/backend/Modules/Betting/Betting.Features/Queries/**", "src/backend/Modules/Wallet/Wallet.Contracts/**", "src/backend/Modules/Wallet/Wallet.Features/Queries/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0041 — Backoffice: busca de apostas

## 1. Visão Geral
Busca de apostas por operador, jogador, rodada, status e período, com detalhe: linha do tempo (aceita, ativa, retirada/perdida/cancelada/estornada) e as transações da carteira da aposta.

## 2. Motivação & Escopo
**Motivação:** suporte e operadores precisam investigar reclamações de jogadores ("meu cash-out não pagou").

**Objetivos (dentro do escopo):**
- Lista paginada (cursor) com filtros; jogador mostrado pelo id do operador e apelido anonimizado.
- Detalhe com eventos da aposta (Betting) e lançamentos `bet:{id}:*` da carteira (Wallet).

**Não-objetivos (fora do escopo):**
- Estornar ou ajustar apostas manualmente.

## 3. Dependências
- **Implementações necessárias:** SPEC-0036, SPEC-0024.
- **Contratos consumidos:** SPEC-0024@1, SPEC-0021@4
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** shell, papéis e auditoria da SPEC-0036; módulos donos dos dados expõem consultas por `Contracts` (ADR-0001). Referência: endpoints e testes do módulo Operators (SPEC-0020) e a tela/shell da SPEC-0036.

**Decisão:** o endpoint de backoffice fica no módulo Backoffice e compõe as consultas dos módulos donos por contratos de leitura; nenhum módulo lê o banco de outro.

**Justificativa:** respeita as fronteiras do monólito modular e mantém o escopo por operador num só lugar.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** endpoint direto em cada módulo (espalha papel e escopo); consulta SQL atravessando schemas (quebra ADR-0001/0010).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** busca ≤ 300 ms p95 com 1 milhão de apostas (índices por jogador, rodada e data).
- **Segurança:** leitura para admin, financeiro e suporte; operador só as próprias. Escopo por operador (ADR-0026) — IT de escopo.
- **Privacidade e dados pessoais:** identificador do jogador é o do operador; apelido sempre anonimizado na tela.
- **Disponibilidade e resiliência:** N/A — leitura.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST + portas de leitura.

```text
IBetQuery (Betting.Contracts).Search(filter, cursor) / Timeline(betId)
IWalletLedgerQuery (Wallet.Contracts).ByBet(betId) → [{ transactionId, type:"debit"|"credit"|"rollback", amount, status, at }]
GET /api/backoffice/bets?operatorId&playerId&roundNumber&status&from&to&cursor → { items:[{ betId, playerId, nickname:"ma***", operatorId, roundNumber, amount, cashOutAt?, payout, status, placedAt }], next }
  | 400 INVALID_FILTER (período > 31 dias sem playerId nem roundNumber)
GET /api/backoffice/bets/{betId} → { ...item, timeline:[{ event, at, multiplier? }], walletTransactions:[...] } | 404 BET_NOT_FOUND
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", tela "Apostas".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Busca | por jogador | apostas do jogador, mais recentes primeiro | IT-01 |
| Filtro amplo demais | 60 dias sem jogador nem rodada | 400 INVALID_FILTER | UT-01 |
| Detalhe | aposta retirada | linha do tempo e débito/crédito OK | IT-02 |
| Inexistente | betId desconhecido | 404 BET_NOT_FOUND | IT-02 |
| Escopo | operador busca aposta de outro operador | não encontra (404) | IT-03 |
| Anonimização | apelido "marcos" | "ma***" | UT-02 |
| Tela | suporte busca e abre detalhe | linha do tempo e transações | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados filtros (período > 31 dias sem jogador nem rodada; válidos), quando validados, então só os válidos passam.
- **UT-02** — Dados apelidos "marcos", "a" e vazio, quando anonimizados, então "ma***", "a***" e "jogador***".

### 7.3 Testes de Integração
- **IT-01** — Dadas apostas reais de 2 jogadores, quando busca por jogador com paginação, então só as dele, em ordem, sem repetir.
- **IT-02** — Dada uma aposta retirada e um id inexistente, quando o detalhe é pedido, então linha do tempo e transações conferem com Betting e Wallet, e o inexistente dá 404.
- **IT-03** — Dado `operador-leitura` do operador A, quando pede uma aposta do operador B, então 404 (não revela existência).

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais dos IT, quando validadas contra o OpenAPI gerado, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o suporte após uma aposta com cash-out no jogo, quando busca pelo jogador e abre a aposta, então vê aceita → ativa → retirada e o débito e o crédito com status OK; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** apostas reais geradas pelo jogo nos IT.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** índices de busca no schema `betting` (expansão).
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
| G0 Spec | PASS | validate: 0 erro(s) — bf0342a | 2026-10-10 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0041`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
