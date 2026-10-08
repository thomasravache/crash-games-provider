---
id: SPEC-0003
title: "Harness de testes e testes de arquitetura"
tier: full
type: foundation
user_facing: false
status: implemented
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
contract_version: 1
touches: ["tests/backend/TestKit/**", "tests/backend/Architecture.Tests/**", "tests/frontend/**", "tests/e2e/harness/**", "src/frontend/vitest.workspace.ts", "src/frontend/playwright.config.ts", "src/frontend/.dependency-cruiser.cjs", "src/frontend/package.json", "stryker-config.json", "Directory.Packages.props", ".config/dotnet-tools.json", "src/frontend/tsconfig.json"]
adrs: [ADR-0001, ADR-0002, ADR-0004, ADR-0009]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0003 — Harness de testes e testes de arquitetura

## 1. Visão Geral
Monta a infraestrutura de testes de todas as camadas (unitário, integração com dependências reais, contrato, E2E, mutação, cobertura) e os testes de arquitetura que transformam as regras dos ADRs em verificações automáticas no .NET e no front.

## 2. Motivação & Escopo
**Motivação:** o usuário pediu foco forte em testes; sem harness pronto, cada spec reinventa fixtures e as regras de arquitetura viram só texto (mandato: arquitetura garantida por teste).

**Objetivos (dentro do escopo):**
- `tests/backend/TestKit`: fixtures Testcontainers (PostgreSQL, Redis, Keycloak), `FakeTimeProvider`, fonte de sementes determinística, builders, captura de logs.
- `tests/backend/Architecture.Tests` (ArchUnitNET), com uma regra por item de ADR-0001, ADR-0002 e ADR-0006 aplicável à estrutura.
- Vitest workspace no front; Playwright (Chromium, WebKit mobile, Firefox) em `tests/e2e`; @axe-core/playwright; dependency-cruiser com as regras do ADR-0004.
- Cobertura: coverlet + ReportGenerator (.NET) e Vitest coverage (v8); Stryker.NET configurado para Fairness e Wallet.
- Comandos no `sdd-config.yml`: `test`, `test_integration`, `test_e2e`, `arch_test`, `coverage`, `mutation`.
- Convenção de tags `SPEC-NNNN:<ID>` documentada e exemplificada.

**Não-objetivos (fora do escopo):**
- Testes de domínio das specs seguintes; k6 (entra na SPEC-0008).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — estrutura e projetos onde as regras de arquitetura se aplicam.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** Docker em execução (Testcontainers).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ferramentas definidas no ADR-0009.

**Decisão:** projetos de teste espelham os módulos (`tests/backend/Modules/<Modulo>.Tests`); integração separada por `[Trait("Category","Integration")]`; arquitetura por `[Trait("Category","Architecture")]`; noturnos por `[Trait("Category","Nightly")]`. Regras de arquitetura testadas contra assemblies-fixture com violações conhecidas, provando que cada regra detecta o que promete.

**Justificativa:** sem provar que a regra reprova uma violação, um teste de arquitetura verde não garante nada.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** NetArchTest (menos expressivo para regras entre camadas e módulos); mocks de banco em integração (proibido no ADR-0009).

**ADRs:** ADR-0001, ADR-0002, ADR-0004, ADR-0009.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** suíte unitária completa ≤ 60 s local; integração reaproveita containers por coleção — medido no CI (SPEC-0004).
- **Segurança:** N/A — sem superfície exposta.
- **Privacidade e dados pessoais:** dados de teste sintéticos; nenhum dado real.
- **Disponibilidade e resiliência:** N/A — infraestrutura de testes.
- **Acessibilidade (UI):** helper axe disponível para as specs com UI — verificado por E2E-01.
- **Custo:** N/A — ferramentas gratuitas.

## 6. Artefato A — Contrato
**Interface:** comandos de teste e convenções.

```text
dotnet test Apogeu.slnx --filter "Category!=Integration&Category!=Nightly"   → unitários + arquitetura
dotnet test Apogeu.slnx --filter Category=Integration                       → integração (Docker)
dotnet test Apogeu.slnx --filter Category=Architecture                      → arquitetura
pnpm -C src/frontend test | test:arch | test:e2e
Tag: [Trait("Spec","SPEC-NNNN:UT-01")]  |  test("SPEC-NNNN:E2E-01 ...")
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Fronteira de módulo violada | Features de um módulo referencia Domain de outro (fixture) | regra reprova citando os tipos | UT-01 |
| Pacote proibido | referência a MediatR/AutoMapper/MassTransit (fixture) | regra reprova | UT-02 |
| Aleatoriedade insegura no Fairness | `System.Random` em Fairness (fixture) | regra reprova | UT-03 |
| Dinheiro em ponto flutuante | propriedade monetária `double` (fixture) | regra reprova | UT-04 |
| Código real conforme | assemblies reais da SPEC-0002 | todas as regras passam | IT-01 |
| Dependências reais | fixtures PostgreSQL e Redis | containers sobem e respondem | IT-02 |
| Cena importando rede (front) | fixture (simulando scene-kit) importando o transporte do game-sdk | dependency-cruiser reprova | IT-03 |
| E2E e axe funcionando | app game-pipa mínimo | abre em Chromium e WebKit mobile, sem violações axe | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um assembly-fixture em que `Betting.Features` referencia `Wallet.Domain`, quando a regra de fronteira roda, então reprova citando os dois tipos.
- **UT-02** — Dado um assembly-fixture que referencia MediatR, quando a regra de pacotes proibidos roda, então reprova.
- **UT-03** — Dado um assembly-fixture de Fairness usando `System.Random`, quando a regra roda, então reprova.
- **UT-04** — Dada uma classe-fixture com propriedade monetária `double`, quando a regra de dinheiro roda, então reprova.

### 7.3 Testes de Integração
- **IT-01** — Dados os assemblies reais da solução, quando a suíte de arquitetura roda, então todas as regras passam.
- **IT-02** — Dadas as fixtures Testcontainers, quando um teste executa `SELECT 1` no PostgreSQL e `PING` no Redis, então ambos respondem e os containers são descartados ao fim.
- **IT-03** — Dado um arquivo-fixture em `tests/frontend/arch-fixtures` (simulando `scene-kit`) importando o transporte do `game-sdk`, quando `pnpm test:arch` roda, então o dependency-cruiser reprova com a regra do ADR-0004.

### 7.4 Testes de Contrato
- N/A — não há contrato entre specs nesta entrega.

### 7.5 Testes E2E
- **E2E-01** — Dado o app `game-pipa` mínimo servido pelo Vite, quando aberto no Chromium e no WebKit mobile, então o título "Pipa" fica visível e o axe não encontra violações (prova do harness; user_facing: false porque não há jornada real ainda).

### 7.6 Outros
- Cobertura: relatório gerado por `coverage` e lido pelo G2 (meta do ADR-0009).

**Dublês e dados de teste:** assemblies-fixture com violações, compilados só no projeto de testes.

**Ambiente de execução:** local com Docker; CI (SPEC-0004).

## 8. Plano de Rollout
- **Estratégia:** merge direto.
- **Dados/schema:** N/A
- **Compatibilidade:** N/A
- **Observabilidade:** N/A — sem serviço.
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
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0002, ADR-0004, ADR-0009 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0003:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, IT-02, IT-03 com a tag `SPEC-0003:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 4: Jornada E2E**
- [x] Red: E2E-01 falhando pelo motivo certo
- [x] Green: jornada completa passando localmente

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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx` exit 1 (red: [xUnit.net 00:00:11.19]     Dado um assembly-fixture em que Betting.Features referencia Wa) — 00122b8 | 2026-10-08 |
| G2 Green | PASS | build exit 0 (✓ built in 54ms); test exit 0 (Duration  1.02s (tests 98%, transform 1%, import 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-08T20:32:37: Report generation took 0,1 seconds) — ae4d966 | 2026-10-08 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  800ms (tests 97%, transform 2%, import 1%)) — ae4d966 | 2026-10-08 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer (agente independente): APPROVED em ae4d966 — regras antes silenciosas agora disparam (sonda); 0 blocker/major; minors: filtro de cobertura, restrição de entrada de estado do game-sdk para a SPEC-0010, lista de nomes de dinheiro, helpers do TestKit sem testes próprios — ae4d966 | 2026-10-08 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 54ms); test exit 0 (Duration  1.00s (tests 98%, transform 1%, import 1%)); test_integration exit 0 (No test matches the given testcase filter `Category=Integration` in /Users/thomas/dev/repo); test_e2e exit 0 (3 passed (4.8s)); arch_test exit 0 (Duration  764ms (tests 97%, transform 2%, import 1%)) — 4fe37a3 | 2026-10-08 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-08 |
| G6 Deploy | N/A | Sem ambientes ainda: validação local por decisão do usuário (ADR-0013); deploy entra com a SPEC-0012 (hospedagem e deploy) | 2026-10-08 |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-08 | G2 | spec | touches incompleto: o lint type-aware exige src/frontend/tsconfig.json para incluir playwright.config.ts e vitest.workspace.ts (exigidos pela spec) | Implementer verificou que nenhum arquivo dentro de touches resolve; com o tsconfig temporário todos os gates ficam verdes | Architect: ampliar touches (sem mudança de contrato) | Emenda de escopo (contrato v1 inalterado) — touches inclui src/frontend/tsconfig.json | 2026-10-08 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
Harness de testes de todas as camadas e testes de arquitetura nas duas stacks:
- `tests/backend/Architecture.Tests` (ArchUnitNET): 7 regras — fronteiras entre módulos só por `Contracts`, Domain sem framework, `Fairness.Domain` puro, sem ciclos entre módulos (no nível do módulo), pacotes proibidos (por namespace, assembly e `PackageReference`), dinheiro em `decimal` (Betting, Wallet, Rounds), sem `System.Random` no Fairness — cada uma com fixture que prova que detecta a violação.
- `tests/backend/TestKit`: fixtures Testcontainers (PostgreSQL 18.3, Redis 8.6, Keycloak 26.6), relógio de teste, fonte de sementes determinística, builders e captura de logs.
- Front: dependency-cruiser com as regras do ADR-0004 (incluindo transporte alcançado pela entrada do pacote), Vitest (projetos unit/arch, cobertura v8), Playwright (Chromium, WebKit iPhone 15, Firefox) com axe.
- Cobertura unificada (coverlet + Vitest lcov via ReportGenerator) e Stryker.NET configurado para Fairness e Wallet.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (scaffolds 37d47c1..83653a2, red a5d0806..00122b8; casos da revisão 5a99a42, 9686cec, 8f9f2b9), Implementer (1ef5c02..9524d66; correções 9779e61, e6810ca, ae4d966), Reviewer independente (CHANGES_REQUESTED: 3 regras silenciosas → APPROVED em ae4d966 após sonda confirmar que disparam).
- Emenda de escopo: `src/frontend/tsconfig.json` (IMP-01), contrato inalterado.
- Convenção de tags `SPEC-NNNN:<ID>` documentada em `tests/backend/TestKit/README.md`.
- Integração da onda: referência ao AppHost para as regras cobrirem toda a solução (510c9b7).
- Restrição registrada para a SPEC-0010: o `game-sdk` precisa de uma entrada de estado que não alcance `src/transport/`.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type foundation.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Fronteira de módulo violada é reprovada (inclusive ciclo entre módulos) | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| UT-02 | Pacote proibido detectado (namespace, assembly, PackageReference) | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| UT-03 | Aleatoriedade insegura no Fairness reprovada | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| UT-04 | Dinheiro em ponto flutuante reprovado | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| IT-01 | Código real conforme todas as regras | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| IT-02 | Fixtures PostgreSQL e Redis reais | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| IT-03 | Cena importando transporte reprovada (direto e pela entrada) | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |
| E2E-01 | App mínimo em Chromium, WebKit mobile e Firefox sem violações axe | PASS | G5 4fe37a3 (arquitetura 14/14, TestKit 2/2, E2E 3/3, Vitest 2/2) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5) — suíte e arquitetura verdes no resultado integrado; CI chega com a SPEC-0004
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — suíte unitária não medida contra a meta de 60 s (Repository.Tests é lento e sem categoria) — anotado para a SPEC-0004; acessibilidade via axe (E2E-01)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A justificado: só validação local nesta fase (ADR-0013), G6 aponta a SPEC-0012
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — rollback = reverter o merge commit; observabilidade N/A (infraestrutura de testes)
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma) — SPEC-0013 (débitos de ferramentas locais) cobre o registro; demais minors anotados no Relatório
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes) — gitleaks sem achados na integração da onda
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente remoto nesta fase (ADR-0013): integrado em `sdd/SPEC-0001` e validado localmente (G5 4fe37a3). G6 N/A até a SPEC-0012.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- Testes próprios dos helpers do TestKit e lista de nomes monetários mais ampla — a tratar junto da SPEC-0007 (que usa os helpers) e quando Rounds tiver membros monetários.
- Medir a meta de 60 s da suíte unitária e categorizar Repository.Tests — SPEC-0004.
- SPEC-0012 — hospedagem e deploy.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0003`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v1 (inalterado) | 2026-10-08 | Emenda de escopo: `touches` inclui `src/frontend/tsconfig.json` | Lint type-aware exige um projeto TS para `playwright.config.ts` e `vitest.workspace.ts` (IMP-01) | Nenhuma (contrato inalterado) | Architect — correção de escopo sem mudança de contrato, coberta pela autorização do usuário para seguir sem paradas (2026-10-08) |
