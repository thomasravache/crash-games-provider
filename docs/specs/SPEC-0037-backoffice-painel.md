---
id: SPEC-0037
title: "Backoffice: painel de desempenho"
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0036, SPEC-0024]
consumes_contract: [SPEC-0024@1]
contract_version: 1
touches: ["src/frontend/apps/backoffice/src/painel/**", "tests/frontend/backoffice/painel/**", "tests/e2e/backoffice/painel/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**", "src/backend/Modules/Betting/Betting.Contracts/**", "src/backend/Modules/Betting/Betting.Features/Summaries/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0037 — Backoffice: painel de desempenho

## 1. Visão Geral
Tela inicial do backoffice: totais do período (apostado, pago, GGR com margem, rodadas), variação contra o período anterior, GGR por dia, desempenho por operador e saúde da plataforma.

## 2. Motivação & Escopo
**Motivação:** é a primeira coisa que o comprador B2B e o operador olham (SPEC-0034).

**Objetivos (dentro do escopo):**
- `GET /api/backoffice/dashboard?operatorId&from&to` montado a partir do resumo imutável por rodada e operador da SPEC-0024 (porta de leitura `IRoundSummaryQuery` em `Betting.Contracts`).
- Cartões, gráfico diário e tabela por operador (só o próprio para papéis de operador).
- Saúde: salas ativas e status da carteira (último resultado por operador).

**Não-objetivos (fora do escopo):**
- Métricas de infraestrutura detalhadas (observabilidade fica para a SPEC-0012/0014).

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
- **Desempenho e escala:** `/dashboard` p95 ≤ 300 ms com 1 ano de resumos de 3 operadores — IT-01 (medido).
- **Segurança:** só papéis com acesso ao painel. Escopo por operador (ADR-0026) — IT de escopo.
- **Privacidade e dados pessoais:** identificador do jogador é o do operador; apelido sempre anonimizado na tela.
- **Disponibilidade e resiliência:** N/A — leitura.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST + porta de leitura.

```text
IRoundSummaryQuery.Aggregate(OperatorId?, Currency, DateRange) → { wagered, paid, ggr, rounds, bets, activePlayers, observedRtp, daily:[{day, ggr}], byOperator:[...] }
GET /api/backoffice/dashboard?operatorId&currency=BRL&from&to → 200 {
  totals:{ wagered, paid, ggr, ggrMargin, rounds, activePlayers, observedRtp },
  previous:{ ...mesmos campos do período anterior de mesmo tamanho },
  daily:[{ day:"2026-10-01", ggr }], byOperator:[{ operatorId, name, wagered, paid, ggr, observedRtp, activePlayers }],
  health:{ activeSalas, walletStatus:[{ operatorId, status:"OK"|"FAILING", lastAt }] } }
  | 400 INVALID_RANGE (from > to ou > 366 dias) | 403 OPERATOR_SCOPE
Regras: GGR = apostado − pago; margem = GGR / apostado; RTP observado = pago / apostado; valores Money em string
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", tela "Painel — Apogeu Games Backoffice".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Totais do período | 10 rodadas semeadas | apostado, pago, GGR, margem e RTP corretos | UT-01, IT-01 |
| Período anterior | período de 7 dias | comparação com os 7 dias anteriores | UT-02 |
| Intervalo inválido | from > to ou > 366 dias | 400 INVALID_RANGE | IT-02 |
| Escopo de operador | operador-leitura | só o próprio operador em totais e tabela | IT-03 |
| Sem dados | período vazio | zeros, sem erro, gráfico vazio | UT-01 |
| Tela | admin abre o painel | cartões, gráfico e tabela com os valores da API | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados resumos de rodada com apostado/pago conhecidos (inclusive vazio), quando agregados, então GGR, margem, RTP e totais batem com o cálculo esperado em decimal exato.
- **UT-02** — Dado um intervalo de 7 dias, quando o período anterior é calculado, então são os 7 dias imediatamente anteriores.

### 7.3 Testes de Integração
- **IT-01** — Dados resumos de rodada reais no PostgreSQL (2 operadores), quando `GET /dashboard` como admin, então totais, série diária e tabela por operador conferem.
- **IT-02** — Dado o Host real, quando `from > to` ou intervalo de 400 dias, então 400 INVALID_RANGE.
- **IT-03** — Dado um usuário `operador-leitura` do operador demo, quando pede o painel, então só vê o operador demo, e pedir outro operatorId dá 403.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais de IT-01, quando validadas contra o OpenAPI gerado, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o ambiente com rodadas e apostas jogadas, quando o admin abre o Painel, então os cartões mostram os mesmos totais da API, o gráfico e a tabela aparecem e os valores estão em texto neutro; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** resumos de rodada semeados por fixture.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A — leitura do resumo da SPEC-0024.
- **Compatibilidade:** endpoints novos sob `/api/backoffice/*`.
- **Observabilidade:** tempo de resposta por rota (`backoffice.requests`).
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, UT-02, IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0037:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 2: Jornada E2E**
- [ ] Red: E2E-01 falhando pelo motivo certo
- [ ] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1cfdc48 | 2026-10-10 |
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0037`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
