---
id: SPEC-0042
title: "Backoffice: relatórios e fechamento mensal"
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0036, SPEC-0038, SPEC-0024]
consumes_contract: [SPEC-0024@1]
contract_version: 1
touches: ["src/frontend/apps/backoffice/src/relatorios/**", "tests/frontend/backoffice/relatorios/**", "tests/e2e/backoffice/relatorios/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**", "src/backend/Modules/Betting/Betting.Contracts/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0042 — Backoffice: relatórios e fechamento mensal

## 1. Visão Geral
Geração de relatórios em CSV: fechamento mensal por operador (apostado, pago, GGR, percentual de comissão vigente e valor da comissão), resumo por rodada, apostas por jogador e auditoria provably fair; histórico das gerações.

## 2. Motivação & Escopo
**Motivação:** o fechamento mensal é como a Apogeu fatura o operador (revenue share — decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- Quatro relatórios em CSV (UTF-8, separador `;`, decimal com vírgula, cabeçalho em pt-BR), gerados em streaming.
- Histórico de gerações (quem, quando, parâmetros) a partir da trilha de auditoria.
- Gestor do operador gera os relatórios do próprio operador.

**Não-objetivos (fora do escopo):**
- PDF (exige biblioteca e ADR; fica para spec futura). Envio por e-mail.

## 3. Dependências
- **Implementações necessárias:** SPEC-0036, SPEC-0038, SPEC-0024.
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
- **Desempenho e escala:** fechamento de um mês de 3 operadores ≤ 2 s; geração em streaming sem carregar tudo em memória.
- **Segurança:** financeiro, admin e gestor do operador (próprio). Escopo por operador (ADR-0026) — IT de escopo.
- **Privacidade e dados pessoais:** identificador do jogador é o do operador; apelido sempre anonimizado na tela.
- **Disponibilidade e resiliência:** N/A — leitura.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST.

```text
GET /api/backoffice/reports/monthly-closing.csv?month=2026-10&operatorId? → linhas por operador e moeda: operador; moeda; apostado; pago; GGR; comissão %; comissão R$
GET /api/backoffice/reports/rounds.csv?from&to&salaId → resumo por rodada
GET /api/backoffice/reports/player-bets.csv?operatorId&playerId&from&to
GET /api/backoffice/reports/fairness.csv?from&to → rodada; compromisso; semente; salt; crash; verificado
GET /api/backoffice/reports/history?cursor → gerações anteriores
Regras: comissão = GGR × percentual vigente no mês (SPEC-0038); GGR negativo → comissão 0; arredondamento half-even em centavos
  | 400 INVALID_PERIOD (mês futuro; intervalo > 366 dias) | 403 OPERATOR_SCOPE
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", tela "Relatórios".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Fechamento | outubro com 12,5% | comissão = GGR × 12,5%, centavos half-even | UT-01, IT-01 |
| GGR negativo | mês com pago > apostado | comissão 0 | UT-01 |
| Troca de percentual | vigência muda no meio do mês | usa o vigente no último dia do mês (regra do ADR-0026: vigente no mês) | UT-02 |
| Período inválido | mês futuro | 400 INVALID_PERIOD | IT-02 |
| Escopo | gestor do operador | só o próprio | IT-03 |
| Formato | CSV gerado | UTF-8, `;`, vírgula decimal, cabeçalho pt-BR | CT-01 |
| Histórico | após gerar | geração listada com quem e parâmetros | IT-01 |
| Tela | financeiro gera o fechamento | download do CSV | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados GGR positivo, zero e negativo e percentuais com 2 casas, quando a comissão é calculada, então bate com o esperado em half-even e é 0 para GGR ≤ 0.
- **UT-02** — Dado um percentual que muda no dia 15, quando o fechamento do mês é calculado, então usa o percentual vigente no último dia do mês.

### 7.3 Testes de Integração
- **IT-01** — Dados resumos de rodada reais de outubro para 2 operadores com comissões diferentes, quando o fechamento é gerado, então os valores conferem e a geração aparece no histórico.
- **IT-02** — Dado o Host real, quando se pede um mês futuro, então 400 INVALID_PERIOD.
- **IT-03** — Dado um `operador-gestor`, quando gera o fechamento, então só o próprio operador aparece; pedir outro dá 403.

### 7.4 Testes de Contrato
- **CT-01** — Dado o CSV real do IT-01, quando lido por um parser CSV padrão com `;`, então as colunas e o formato numérico pt-BR conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o financeiro no backoffice, quando gera o fechamento de outubro, então o download do CSV acontece e a geração aparece em "Relatórios gerados"; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** resumos de rodada e comissões por fixture.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A — leitura; histórico vem da trilha de auditoria.
- **Compatibilidade:** endpoints novos sob `/api/backoffice/*`.
- **Observabilidade:** tempo por relatório.
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
- [ ] Red: escrever UT-01, UT-02, IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0042:<ID>` e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — e46312a | 2026-10-10 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0042`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
