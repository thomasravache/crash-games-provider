---
id: SPEC-0036
title: "Backoffice: shell, login, papéis e trilha de auditoria"
tier: full
type: feature
user_facing: true
status: approved
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0009]
consumes_contract: []
contract_version: 1
touches: ["src/frontend/apps/backoffice/**", "src/frontend/packages/game-ui/src/brand/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**", "tests/frontend/backoffice/**", "tests/e2e/backoffice/**", "src/backend/AppHost/keycloak/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0036 — Backoffice: shell, login, papéis e trilha de auditoria

## 1. Visão Geral
Base do backoffice: app React com login SSO (Keycloak), menu lateral, cabeçalho com seletor de operador e período, modos claro e escuro e o logo Arco (ADR-0025); no backend, os papéis do ADR-0026, o escopo por operador e a trilha de auditoria imutável que todas as telas usam.

## 2. Motivação & Escopo
**Motivação:** as telas do épico SPEC-0034 precisam de uma casca comum, de regras de acesso únicas e de auditoria antes de existirem (decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- Shell do app `backoffice`: menu (Painel, Operadores, Catálogo e limites, Rodadas e auditoria, Apostas, Relatórios, Configurações, Sair), seletor de operador (só para papéis Apogeu) e de período, alternância claro/escuro persistida.
- Tokens de cor e tipografia do ADR-0025 num pacote único (`game-ui/src/brand`), com logo Arco em SVG (horizontal, ícone, monocromático).
- Papéis no realm `apogeu` (`apogeu-admin`, `apogeu-financeiro`, `apogeu-suporte`, `operador-gestor`, `operador-leitura`) com usuários de teste no realm de desenvolvimento.
- `GET /api/backoffice/me` (papéis, operador do escopo, nome) e políticas de autorização por papel; escopo por operador vindo do token.
- Trilha de auditoria: `IAuditTrail.RecordAsync(...)` em `Backoffice.Contracts`, tabela `backoffice.audit_events` só de inserção (trigger), `GET /api/backoffice/audit` (só admin).
- Página de login com o botão "Entrar com SSO" e o selo de ambiente.

**Não-objetivos (fora do escopo):**
- O conteúdo de cada tela (specs SPEC-0037 a SPEC-0042). Gestão de usuários do Keycloak pela tela (feita no Keycloak).

## 3. Dependências
- **Implementações necessárias:** SPEC-0009 — login Keycloak do backoffice e realm `apogeu`.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** app `backoffice` esqueleto (SPEC-0002/0009) e módulo Backoffice vazio (`BackofficeModule.cs`). Implementação de referência: política de sessão e erros problem+json do módulo Operators (SPEC-0009/0020); trigger de só-inserção do livro-razão da SPEC-0021.

**Decisão:** o módulo Backoffice concentra papéis, escopo e auditoria; os outros módulos expõem consultas por contratos e o Backoffice monta as respostas (ADR-0001). Os tokens visuais ficam num pacote compartilhado para o app e as telas futuras.

**Justificativa:** uma regra de acesso e uma trilha para todas as telas, testadas uma vez.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** auditoria por módulo (espalha a regra); papéis no banco da Apogeu (duplica o Keycloak).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `/me` ≤ 50 ms p95 local — IT-01.
- **Segurança:** todo endpoint `/api/backoffice/*` exige papel; operador só do token — IT-02, IT-03; auditoria imutável — IT-04.
- **Privacidade e dados pessoais:** a trilha guarda o identificador do usuário do backoffice (sem senha nem token) — IT-04.
- **Disponibilidade e resiliência:** N/A — sem estado crítico além da auditoria.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01, E2E-02.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST do backoffice + `Backoffice.Contracts` + shell da UI.

```text
Papéis (realm apogeu): apogeu-admin | apogeu-financeiro | apogeu-suporte | operador-gestor | operador-leitura
Claims usadas: sub, name, roles, op (só papéis operador-*)
GET /api/backoffice/me → 200 { userId, name, roles:[...], operatorScope: "op_demo" | null }  | 401 sem sessão
Escopo: papel operador-* + parâmetro operatorId diferente do token → 403 OPERATOR_SCOPE
IAuditTrail.RecordAsync(AuditEvent { Action, TargetType, TargetId, Before?, After? })   // quem/papel/quando vêm do contexto
GET /api/backoffice/audit?from&to&action&cursor (apogeu-admin) → 200 { items:[{ at, userId, role, action, target, before, after }], next }
backoffice.audit_events: só inserção (trigger BEFORE UPDATE OR DELETE lança erro)
UI: tema claro/escuro (preferência salva), menu por papel (itens sem permissão não aparecem)
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", telas "Login - Apogeu Games" e "Painel — Apogeu Games Backoffice" (shell e menu).

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Login | usuário sem sessão abre o backoffice | redireciona ao Keycloak; volta autenticado | E2E-01 |
| Quem sou | `GET /me` com sessão | papéis e escopo | IT-01 |
| Sem sessão | `GET /me` sem token | 401 | IT-01 |
| Escopo de operador | operador-leitura pede outro operatorId | 403 OPERATOR_SCOPE | IT-02 |
| Política obrigatória | qualquer endpoint `/api/backoffice/*` | declara papel | UT-01 |
| Menu por papel | operador-leitura | sem Operadores nem Catálogo editável; sem seletor de operador | UT-02, E2E-02 |
| Auditoria gravada | escrita de teste | evento com quem, papel, antes e depois | IT-03 |
| Auditoria imutável | UPDATE/DELETE direto | recusado pelo banco | IT-04 |
| Auditoria só admin | financeiro lê `/audit` | 403 | IT-03 |
| Tema | alterna claro/escuro e recarrega | preferência mantida, contraste AA nos dois | UT-03, E2E-02 |
| Tokens | cores usadas nas telas | só tokens do ADR-0025 | UT-04 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados todos os endpoints mapeados sob `/api/backoffice/*`, quando inspecionados, então cada um declara uma política de papel (teste de arquitetura).
- **UT-02** — Dados os papéis `operador-leitura` e `apogeu-admin`, quando o menu é montado, então o primeiro não vê Operadores, nem o seletor de operador, nem ações de edição; o segundo vê tudo.
- **UT-03** — Dados os tokens claro e escuro, quando os pares texto/fundo do ADR-0025 são medidos, então o contraste é ≥ 4,5:1 (texto) e ≥ 3:1 (componentes).
- **UT-04** — Dado o código do app `backoffice`, quando analisado, então não há cor literal fora do pacote de tokens (regra de lint/teste).

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com Keycloak de teste, quando `GET /api/backoffice/me` com token de `operador-gestor` e sem token, então 200 com `operatorScope:"op_demo"` e 401.
- **IT-02** — Dado um usuário `operador-leitura` do operador demo, quando chama um endpoint de teste com `operatorId` de outro operador, então 403 OPERATOR_SCOPE; sem parâmetro, o escopo é o do token.
- **IT-03** — Dado o Host real, quando um admin faz uma escrita de teste e o financeiro lê `/audit`, então o evento tem quem, papel, ação, antes e depois, e o financeiro recebe 403.
- **IT-04** — Dado um evento gravado, quando se tenta UPDATE/DELETE com a conexão do módulo, então o banco recusa.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais de IT-01 e IT-03, quando validadas contra o OpenAPI gerado, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o ambiente Aspire, quando um usuário admin abre o backoffice, então é levado ao Keycloak, entra e vê o shell com o logo Arco e o menu completo; axe sem violações.
- **E2E-02** — Dado um usuário `operador-leitura`, quando entra e alterna para o tema claro e recarrega, então vê o menu restrito, sem seletor de operador, e o tema claro mantido; axe sem violações nos dois temas.

### 7.6 Outros
- Revisão visual no G4 contra as telas do Stitch citadas.

**Dublês e dados de teste:** usuários de teste por papel no realm de desenvolvimento; endpoint de escrita de teste só em Development.

**Ambiente de execução:** Testcontainers (Postgres, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** schema `backoffice` novo (tabela de auditoria com trigger).
- **Compatibilidade:** endpoints novos; login existente da SPEC-0009 inalterado.
- **Observabilidade:** métrica `backoffice.requests{route,role,status}`; a própria trilha de auditoria.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0036:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 2: Jornada E2E**
- [ ] Red: E2E-01, E2E-02 falhando pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — 0707fc7 | 2026-10-10 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0036`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
