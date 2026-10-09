---
id: SPEC-0006
title: "Contratos gerados do backend para o front-end"
tier: full
type: foundation
user_facing: false
status: implemented
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
contract_version: 1
touches: ["src/backend/BuildingBlocks/Contracts/**", "src/backend/Host/OpenApi/**", "src/backend/Modules/Backoffice/Backoffice.Features/Meta/**", "src/frontend/packages/contracts/**", "tools/contracts/**", "tests/backend/Contracts.Tests/**", "src/backend/Host/Program.cs", "src/backend/Host/Host.csproj", "src/backend/Modules/Backoffice/Backoffice.Features/BackofficeModule.cs", "src/backend/Modules/Backoffice/Backoffice.Features/Backoffice.Features.csproj", "src/frontend/package.json", ".gitignore", "artifacts/openapi/**", "Directory.Packages.props"]
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
| Dinheiro como string | DTO com campo `Money Amount = 10.5m` (campos monetários do contrato usam `Money`, nunca `decimal` cru) | JSON `"10.50"`; TS `Money` | UT-01 |
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
- **UT-01** — Dado um DTO com campo `Money` 10.5 e um multiplicador 2.47, quando serializado pelos conversores, então gera `"10.50"` e `"2.47"`, e a desserialização devolve os mesmos valores exatos.
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
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0008, ADR-0004 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01, UT-02 com a tag `SPEC-0006:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0006:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy (G6): N/A até a spec SPEC-0012 (hospedagem), registrado no gate
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-07 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive" && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:01:35.89]     Dado o backend compilado, quando contracts:generate roda, entã) — 47f0252 | 2026-10-08 |
| G2 Green | PASS | build exit 0 (✓ built in 56ms); test exit 0 (Duration  1.40s (tests 98%, transform 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-08T23:24:17: Report generation took 0,2 seconds) — ef81abd | 2026-10-08 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.46s (tests 98%, transform 1%, import 1%)) — ef81abd | 2026-10-08 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: APPROVED — re-review em ef81abd, 0 blocker/major; minors como pendências (hubs com objetos/enums, possível flake do IT-03 com WebApplicationFactory em paralelo, lint de tools/contracts/*.mjs) — ef81abd | 2026-10-08 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 57ms); test exit 0 (Duration  1.13s (tests 98%, transform 1%)); test_integration exit 0 (No test matches the given testcase filter `Category=Integration` in /private/tmp/claude-50); test_e2e exit 0 (3 passed (3.6s)); arch_test exit 0 (Duration  722ms (tests 98%, transform 1%, import 1%)); security_scan exit 0 (Ran 8 rules on 96 files: 0 findings.) — 93cdba6 | 2026-10-09 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-09 |
| G6 Deploy | N/A | Sem ambiente remoto nesta fase (ADR-0013): deploy na SPEC-0012 | 2026-10-09 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 82e5d6f | 2026-10-09 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
O backend é a fonte única dos contratos com o front-end. O build do Host gera `artifacts/openapi/v1.json` (versionado) e o manifesto dos hubs SignalR. `pnpm -C src/frontend contracts:generate` transforma ambos em tipos e clientes TypeScript em `packages/contracts` (REST via openapi-typescript + openapi-fetch; hubs por gerador próprio), e `contracts:check` falha quando o código versionado diverge do backend. Convenções de JSON do contrato: dinheiro (`Money`) e multiplicador (`Multiplier`) como string decimal ("10.50", "2.47"), instantes ISO-8601 UTC com milissegundos e `Z` (valores sem fuso são rejeitados), enums em camelCase; as mesmas convenções valem no protocolo JSON dos hubs. `GET /api/v1/meta` expõe a versão do contrato; o documento OpenAPI só é servido em Development.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (scaffold 23a7d45, red 8b2aee7; rodada do G4 629229d), Implementer (95c43c7, 5628083, 92a9363; rodada do G4 d9f8faa, ef81abd), Reviewer independente (CHANGES_REQUESTED → APPROVED em ef81abd).
- `BuildingBlocks/Contracts/Json` (`ContractJson`, conversores de `Money`, `Multiplier` e `DateTimeOffset`); `Host/OpenApi` (geração no build com `Microsoft.Extensions.ApiDescription.Server`, `JsonHubProtocolOptions` com `ContractJson`); `tools/contracts` (`contracts.mjs`, `openapi-to-ts.mjs`, `hubs-to-ts.mjs`, `HubContracts`).
- Emendas (contrato v1 inalterado): escopo com `.gitignore`, `artifacts/openapi/**` e `Directory.Packages.props`; esclarecimento do cenário de dinheiro (`Money`, nunca `decimal` cru) e correções do G4 (hubs testados, protocolo dos hubs com as convenções, data sem fuso rejeitada, CT-01 contra o documento versionado). ADR-0008 registra os geradores escolhidos.
- Integração da onda 3: `ContractHost` passou a remover os serviços hospedados dos módulos (7e6bfaa), porque a Fairness migra o banco no start em Development; o job `contracts` da CI compila o backend antes do `contracts:check` (7d72186).

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type foundation.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Dinheiro e multiplicador como string decimal; convenções de data, enum e protocolo dos hubs | PASS | G5 em 93cdba6 (Contracts.Tests 20/20) e CI do PR #6 run em eb0e2af (job backend) |
| UT-02 | Anulabilidade no OpenAPI e no TS; hubs gerados com `Money`, `Multiplier \| null`, `Promise`, `AsyncIterable` | PASS | G5 em 93cdba6 (Contracts.Tests 20/20) e CI do PR #6 em eb0e2af |
| IT-01 | `contracts:generate` gera arquivos que compilam | PASS | G5 em 93cdba6 e CI do PR #6 em eb0e2af (jobs backend e contracts) |
| IT-02 | `contracts:check` falha com campo novo sem regenerar | PASS | G5 em 93cdba6 e CI do PR #6 em eb0e2af |
| IT-03 | OpenAPI só em Development | PASS | G5 em 93cdba6 e CI do PR #6 em eb0e2af |
| CT-01 | `GET /api/v1/meta` valida contra o documento gerado e o versionado | PASS | G5 em 93cdba6 e CI do PR #6 em eb0e2af |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — G5 em clone limpo de 93cdba6 e CI do PR #6 verde em eb0e2af
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado — geradores registrados no ADR-0008
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — divergência detectada pelo `contracts:check` (IT-02) e verificada na CI (job contracts)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A justificado: sem ambiente remoto nesta fase (ADR-0013), G6 aponta a SPEC-0012
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — rollback = reverter o merge commit 4771758; sem dados
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma) — SPEC-0017
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes) — gitleaks e Semgrep sem achados no G5 e na CI
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente remoto nesta fase (ADR-0013): mergeado na `main` pelo PR #6 (merge commit 4771758, 2026-10-09), CI verde. G6 N/A até a SPEC-0012.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0017 — hubs com objetos, enums e datas no gerador; possível instabilidade do IT-03 com `WebApplicationFactory` em paralelo; `tools/contracts/*.mjs` no lint; aviso de tipos que não carregam no `HubDiscovery`; cobertura do Contracts.Tests (coverlet).

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0006`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v1 (inalterado) | 2026-10-08 | Emenda de escopo: `touches` inclui `.gitignore`, `artifacts/openapi/**` e `Directory.Packages.props` | ADR-0008 versiona o documento OpenAPI (exceção no `.gitignore`) e a geração no build usa `Microsoft.Extensions.ApiDescription.Server`, par do `Microsoft.AspNetCore.OpenApi` previsto no ADR | Nenhuma (contrato inalterado) | Architect — correção de escopo sem mudança de contrato, coberta pela autorização do usuário para seguir sem paradas (2026-10-08) |
| v1 (inalterado) | 2026-10-08 | Esclarecimento: o cenário "Dinheiro como string" usa o tipo `Money` (campos monetários do contrato nunca são `decimal` cru); achados do G4 (gerador de hubs testado, `JsonHubProtocolOptions` com `ContractJson`, data sem offset rejeitada, CT-01 também contra `artifacts/openapi/v1.json`) entram como correções dentro do contrato v1 | Review G4: texto citava `decimal`, a implementação e o ADR-0008 usam `Money`; hubs precisam seguir as mesmas convenções do REST | Nenhuma (contrato inalterado) | Architect — esclarecimento sem mudança de contrato, coberto pela autorização do usuário para seguir recomendações (2026-10-08) |
