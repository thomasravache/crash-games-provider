---
id: SPEC-0006
title: "Contratos gerados do backend para o front-end"
tier: full
type: foundation
user_facing: false
status: approved
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
contract_version: 1
touches: ["src/backend/BuildingBlocks/Contracts/**", "src/backend/Host/OpenApi/**", "src/backend/Modules/Backoffice/Backoffice.Features/Meta/**", "src/frontend/packages/contracts/**", "tools/contracts/**", "tests/backend/Contracts.Tests/**"]
adrs: [ADR-0008, ADR-0004]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0006 — Contratos gerados do backend para o front-end

## 1. Visão Geral
Gera automaticamente, a partir do backend, os tipos e clientes TypeScript da API REST (OpenAPI) e dos hubs SignalR em `packages/contracts`, com verificação de divergência e convenção de dinheiro/multiplicador como string decimal.

## 2. Motivação & Escopo
**Motivação:** duas linguagens com tipos duplicados à mão divergem; o backend é a fonte única (ADR-0008).

**Objetivos (dentro do escopo):**
- Documento OpenAPI gerado no build em `artifacts/openapi/v1.json`.
- Tipos REST com **openapi-typescript** e cliente **openapi-fetch** (MIT); tipos dos hubs a partir das interfaces fortemente tipadas dos hubs: **TypedSignalR.Client.TypeScript** se compatível com .NET 10, senão gerador próprio por reflexão em `tools/contracts` (decisão registrada no G2 com a evidência de compatibilidade).
- Conversores JSON: `decimal` monetário e multiplicadores serializados como string decimal; tipos marcados `Money` e `Multiplier` no TS.
- Comandos `contracts:generate` e `contracts:check`.
- Endpoint de exemplo `GET /api/v1/meta` (versão da API e horário do servidor) para provar o ciclo.

**Não-objetivos (fora do escopo):**
- Contratos de domínio (cada spec publica os seus usando este mecanismo).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — Host e pacote `contracts`.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0008 e ADR-0004 (`packages/contracts` só gerado).

**Decisão:** geração em dois passos (build .NET → artefatos de esquema → geradores TS), com saída versionada no repositório e verificação por `git diff --exit-code`.

**Justificativa:** quebra de contrato vira erro de compilação no front e falha no CI.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** NSwag/Kiota para TS (mais pesados, cliente gerado verboso); esquema neutro escrito à mão (ADR-0008).

**ADRs:** ADR-0008, ADR-0004.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** `contracts:generate` ≤ 60 s — medido em IT-01.
- **Segurança:** OpenAPI publicado só em Development (sem expor documentação em produção sem decisão) — verificado por IT-03.
- **Privacidade e dados pessoais:** N/A — só esquemas.
- **Disponibilidade e resiliência:** N/A
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** comandos de geração + convenção de tipos (contrato v1 consumido pela SPEC-0004 e pelos apps).

```text
pnpm -C src/frontend contracts:generate  → reescreve packages/contracts/src/{rest,hubs}/**
pnpm -C src/frontend contracts:check     → exit 0 se idêntico ao gerado; exit 1 + diff caso contrário
Convenções:
  decimal (dinheiro)   → JSON "10.50"  → TS Money (string marcada)
  multiplicador        → JSON "2.47"   → TS Multiplier (string marcada)
  DateTimeOffset       → JSON ISO-8601 UTC → TS string
  enums                → strings em camelCase
GET /api/v1/meta → 200 {"apiVersion":"1.0.0","serverTime":"2026-10-07T22:00:00.000Z"}
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Dinheiro como string | DTO com `decimal Amount = 10.5m` | JSON `"10.50"`; TS `Money` | UT-01 |
| Multiplicador como string | `Multiplier 2.47` | JSON `"2.47"`; TS `Multiplier` | UT-01 |
| Anulável | propriedade `string?` | TS `string \| null` | UT-02 |
| Geração completa | `contracts:generate` | arquivos REST e hubs gerados e compiláveis | IT-01 |
| Divergência detectada | DTO alterado sem regenerar | `contracts:check` exit 1 com diff | IT-02 |
| Documento só em Development | ambiente Production | `/openapi/v1.json` → 404 | IT-03 |
| Resposta real conforme o esquema | `GET /api/v1/meta` | corpo válido contra o OpenAPI gerado | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um DTO com `decimal` monetário 10.5 e um multiplicador 2.47, quando serializado pelos conversores, então gera `"10.50"` e `"2.47"`, e a desserialização devolve os mesmos valores exatos.
- **UT-02** — Dado um DTO com `string?`, quando o esquema é gerado, então a propriedade é anulável no OpenAPI e `string | null` no TS.

### 7.3 Testes de Integração
- **IT-01** — Dado o backend compilado, quando `contracts:generate` roda, então gera os arquivos e `pnpm -C src/frontend typecheck` passa.
- **IT-02** — Dado um campo novo num DTO sem regenerar, quando `contracts:check` roda, então sai com código 1 e mostra o diff.
- **IT-03** — Dado o Host em ambiente Production (WebApplicationFactory), quando `GET /openapi/v1.json`, então 404; em Development, 200.

### 7.4 Testes de Contrato
- **CT-01** — Dado o Host em execução, quando `GET /api/v1/meta` é chamado, então a resposta valida contra o esquema do OpenAPI gerado.

### 7.5 Testes E2E
- N/A — sem jornada de usuário; só geração de tipos (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** DTOs-fixture no projeto de testes.

**Ambiente de execução:** local e CI.

## 8. Plano de Rollout
- **Estratégia:** merge direto.
- **Dados/schema:** N/A
- **Compatibilidade:** v1 inicial; mudança incompatível exige nova versão (emenda).
- **Observabilidade:** N/A
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0008, ADR-0004 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0006:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0006:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a spec SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-07 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0006`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
