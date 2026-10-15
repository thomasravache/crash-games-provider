---
id: SPEC-0039
title: "Backoffice: catálogo e limites por operador"
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0036, SPEC-0020]
consumes_contract: [SPEC-0020@3]
contract_version: 1
touches: ["src/frontend/apps/backoffice/src/catalogo/**", "tests/frontend/backoffice/catalogo/**", "tests/e2e/backoffice/catalogo/**", "src/backend/Modules/Operators/**", "tests/backend/Modules/Operators.Tests/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0039 — Backoffice: catálogo e limites por operador

## 1. Visão Geral
Tela para habilitar/desabilitar jogos por operador e editar os limites por moeda (aposta mínima, máxima e ganho máximo), com auditoria; operadores veem, só o admin altera.

## 2. Motivação & Escopo
**Motivação:** o catálogo e os limites da SPEC-0020 hoje só mudam por seed.

**Objetivos (dentro do escopo):**
- Ver jogos do operador com RTP, ganho máximo e status; habilitar/desabilitar (admin).
- Ver e editar limites por moeda; adicionar moeda (admin).
- Versão do catálogo **persistida** (corrige a dívida da SPEC-0020: o cache por processo passa a ver escritas de outra instância).

**Não-objetivos (fora do escopo):**
- Edição de RTP/cap da sala (fica para spec própria com fluxo de aprovação).

## 3. Dependências
- **Implementações necessárias:** SPEC-0036, SPEC-0020.
- **Contratos consumidos:** SPEC-0020@2
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** shell, papéis e auditoria da SPEC-0036; módulos donos dos dados expõem consultas por `Contracts` (ADR-0001). Referência: endpoints e testes do módulo Operators (SPEC-0020) e a tela/shell da SPEC-0036.

**Decisão:** o endpoint de backoffice fica no módulo Backoffice e compõe as consultas dos módulos donos por contratos de leitura; nenhum módulo lê o banco de outro.

**Justificativa:** respeita as fronteiras do monólito modular e mantém o escopo por operador num só lugar.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** endpoint direto em cada módulo (espalha papel e escopo); consulta SQL atravessando schemas (quebra ADR-0001/0010).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** leitura de limites continua ≤ 1 ms com cache (SPEC-0020 UT-03).
- **Segurança:** escritas só admin. Escopo por operador (ADR-0026) — IT de escopo.
- **Privacidade e dados pessoais:** identificador do jogador é o do operador; apelido sempre anonimizado na tela.
- **Disponibilidade e resiliência:** N/A — leitura.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST.

```text
GET /api/backoffice/operators/{id}/games → [{ code, name, enabled, rtp, maxMultiplier, currencies }]
PUT /api/backoffice/operators/{id}/games/{code} (admin) { enabled } | 404 GAME_NOT_FOUND
GET /api/backoffice/operators/{id}/limits → [{ currency, minBet, maxBet, maxWin, updatedAt }]
PUT /api/backoffice/operators/{id}/limits/{currency} (admin) { minBet, maxBet, maxWin }
  | 400 INVALID_LIMITS (min ≤ 0, min > max, maxWin < max, mais de 2 casas) | 400 CURRENCY_NOT_SUPPORTED (fora de ISO 4217 suportadas)
Versão do catálogo persistida: toda escrita incrementa operators.catalog_version; o cache relê quando a versão no banco muda (verificação a cada ≤ 1 s)
Toda escrita → IAuditTrail (antes/depois)
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", telas "Catálogo e limites por operador" (escuro) e "Catálogo e limites por operador — Claro".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ver jogos | gestor do operador | jogos com status | IT-01 |
| Habilitar/desabilitar | admin desabilita a Pipa | lançamento recusa GAME_NOT_AVAILABLE | IT-02 |
| Gestor não altera | gestor tenta PUT | 403 | IT-01 |
| Editar limites | admin muda BRL para 2/500/50.000 | aposta passa a usar o novo limite | IT-03 |
| Limites inválidos | min > max etc. | 400 INVALID_LIMITS | UT-01 |
| Moeda não suportada | "XYZ" | 400 CURRENCY_NOT_SUPPORTED | UT-01 |
| Outra instância | escrita na instância A | instância B vê em ≤ 1 s | IT-04 |
| Auditoria | escrita | antes/depois na trilha | IT-02, IT-03 |
| Tela | admin edita limite | valor salvo e exibido | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados limites válidos e inválidos (min 0, min > max, maxWin < max, 3 casas, moeda XYZ), quando validados, então só os válidos passam com os códigos certos.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real, quando gestor do operador demo lê jogos e limites e tenta alterar, então lê e recebe 403 na alteração.
- **IT-02** — Dado admin, quando desabilita a Pipa para o operador demo, então o lançamento responde GAME_NOT_AVAILABLE e a trilha guarda antes/depois; reabilitar volta a lançar.
- **IT-03** — Dado admin, quando altera os limites BRL, então `IOperatorLimits` devolve os novos valores e a trilha registra a mudança.
- **IT-04** — Dadas duas instâncias do Host no mesmo banco, quando a A altera um limite, então a B devolve o novo limite em ≤ 1 s.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais dos IT, quando validadas contra o OpenAPI gerado, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o admin no backoffice, quando abre Catálogo e limites do Cassino Demo e altera a aposta máxima BRL, então o novo valor aparece na tabela e no painel de aposta da Pipa (limite novo); axe sem violações nos temas claro e escuro.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** operador demo e catálogo semeados.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** coluna/tabela de versão do catálogo no schema `operators` (expansão).
- **Compatibilidade:** endpoints novos sob `/api/backoffice/*`.
- **Observabilidade:** eventos de auditoria e métrica de recarga do cache.
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
- [ ] Red: escrever UT-01, IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0039:<ID>` e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — b0c9fde | 2026-10-10 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0039`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
