---
id: SPEC-0038
title: "Backoffice: operadores, chaves e comissão"
tier: full
type: feature
user_facing: true
status: proposed
created: 2026-10-10
parent: SPEC-0034
depends_on: [SPEC-0036, SPEC-0020]
consumes_contract: []
contract_version: 1
touches: ["src/frontend/apps/backoffice/src/operadores/**", "tests/frontend/backoffice/operadores/**", "tests/e2e/backoffice/operadores/**", "src/backend/Modules/Operators/**", "tests/backend/Modules/Operators.Tests/**", "src/backend/Modules/Backoffice/**", "tests/backend/Modules/Backoffice.Tests/**"]
adrs: [ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026, ADR-0014]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0038 — Backoffice: operadores, chaves e comissão

## 1. Visão Geral
Gestão de operadores: lista, detalhe, criação, rotação da chave de API (mostrada uma única vez), IPs permitidos, URL da carteira do operador, percentual de comissão (revenue share) com vigência e desativação — tudo auditado.

## 2. Motivação & Escopo
**Motivação:** integrar um cassino novo hoje exige seed; a comissão alimenta o fechamento mensal (SPEC-0042).

**Objetivos (dentro do escopo):**
- Lista e detalhe de operadores (modo DEMO/PRODUÇÃO, moedas, jogos habilitados, status da integração da carteira).
- Criar operador (admin), rotacionar chave (o segredo aparece uma vez), IPs permitidos, URL da carteira, desativar/reativar.
- Comissão: percentual do GGR com data de vigência; histórico preservado.
- Gestor do operador vê o próprio operador e pode rotacionar a própria chave.

**Não-objetivos (fora do escopo):**
- Alteração do modo para produção com dinheiro real (bloqueado por REAL_MONEY_DISABLED da SPEC-0009).

## 3. Dependências
- **Implementações necessárias:** SPEC-0036, SPEC-0020.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** shell, papéis e auditoria da SPEC-0036; módulos donos dos dados expõem consultas por `Contracts` (ADR-0001). Referência: endpoints e testes do módulo Operators (SPEC-0020) e a tela/shell da SPEC-0036.

**Decisão:** o endpoint de backoffice fica no módulo Backoffice e compõe as consultas dos módulos donos por contratos de leitura; nenhum módulo lê o banco de outro.

**Justificativa:** respeita as fronteiras do monólito modular e mantém o escopo por operador num só lugar.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** endpoint direto em cada módulo (espalha papel e escopo); consulta SQL atravessando schemas (quebra ADR-0001/0010).

**ADRs:** ADR-0001, ADR-0008, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026, ADR-0014.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** listas ≤ 300 ms p95 local.
- **Segurança:** escritas só admin; o segredo nunca é logado nem auditado. Escopo por operador (ADR-0026) — IT de escopo.
- **Privacidade e dados pessoais:** identificador do jogador é o do operador; apelido sempre anonimizado na tela.
- **Disponibilidade e resiliência:** N/A — leitura.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** REST.

```text
GET  /api/backoffice/operators?search&mode → [{ operatorId, name, mode, currencies, enabledGames, walletStatus, createdAt }]
GET  /api/backoffice/operators/{id} → detalhe { ..., keys:[{ keyId, createdAt, lastUsedAt }], allowedIps, walletUrl, revenueShare:[{ percent, validFrom }], active }
POST /api/backoffice/operators (admin) { name, mode:"demo", currencies } → 201 { operatorId }
POST /api/backoffice/operators/{id}/keys:rotate (admin | operador-gestor do próprio) → 200 { keyId, secret }   // secret só nesta resposta
PUT  /api/backoffice/operators/{id}/allowed-ips (admin) { ips:[cidr] } | 400 INVALID_CIDR
PUT  /api/backoffice/operators/{id}/wallet-url (admin) { url https } | 400 INVALID_URL
POST /api/backoffice/operators/{id}/revenue-share (admin) { percent 0..100 (2 casas), validFrom } | 400 INVALID_PERCENT
POST /api/backoffice/operators/{id}:deactivate | :reactivate (admin) → desativado recusa lançamento (403 OPERATOR_INACTIVE)
Toda escrita → IAuditTrail (o segredo nunca entra na trilha)
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu A — azul-noite + dourado" (escuro) e "Apogeu A — claro", tela "Operadores".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Lista | admin | operadores com modo e status | IT-01 |
| Criar | admin cria operador | 201 e operador demo novo com chave | IT-02 |
| Rotação | admin rotaciona | segredo novo uma vez; chave antiga recusada | IT-03 |
| Rotação pelo gestor | gestor do próprio / de outro | 200 / 403 | IT-03 |
| IP inválido | CIDR malformado | 400 INVALID_CIDR | UT-01 |
| URL inválida | http ou malformada | 400 INVALID_URL | UT-01 |
| Comissão | 12,5% a partir de 01/11 | histórico preserva o valor anterior | UT-02, IT-04 |
| Percentual inválido | 120 ou 3 casas | 400 INVALID_PERCENT | UT-02 |
| Desativar | operador desativado lança jogo | 403 OPERATOR_INACTIVE | IT-05 |
| Auditoria | qualquer escrita | evento sem o segredo | IT-02, IT-03 |
| Tela | admin abre e rotaciona | segredo exibido uma vez com copiar | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados CIDRs e URLs válidos e inválidos, quando validados, então só IPv4/IPv6 CIDR e URLs `https` passam.
- **UT-02** — Dado o histórico de comissão, quando se adiciona 12,50% com vigência futura e se tenta 120% ou 1,234%, então o vigente em cada data é o correto e os inválidos são recusados.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com 2 operadores, quando admin lista, então vê ambos com modo e status.
- **IT-02** — Dado admin, quando cria um operador, então 201, o operador existe com chave e a trilha registra a criação sem segredo.
- **IT-03** — Dado um operador, quando a chave é rotacionada (admin; gestor do próprio; gestor de outro), então o segredo aparece só na resposta, a chave antiga deixa de assinar, e o gestor de outro operador recebe 403.
- **IT-04** — Dado um percentual de comissão com vigência, quando o histórico é lido, então ambos os valores aparecem com as vigências.
- **IT-05** — Dado um operador desativado, quando lança a Pipa, então 403 OPERATOR_INACTIVE; reativado, volta a lançar.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais dos IT, quando validadas contra o OpenAPI gerado, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o admin no backoffice, quando abre Operadores, seleciona o Cassino Demo e rotaciona a chave, então o segredo aparece uma única vez com botão copiar e some ao fechar; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** operadores criados por fixture.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak) e Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** tabelas de chaves (histórico), IPs, URL e comissão no schema `operators` (expansão).
- **Compatibilidade:** endpoints novos sob `/api/backoffice/*`.
- **Observabilidade:** eventos de auditoria por ação.
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
| G0 Spec | PENDING | | |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0038`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
