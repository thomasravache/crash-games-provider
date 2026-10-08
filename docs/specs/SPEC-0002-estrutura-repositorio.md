---
id: SPEC-0002
title: "Estrutura do repositório e qualidade de código"
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: []
consumes_contract: []
contract_version: 1
touches: ["src/backend/**", "src/frontend/**", "tests/backend/Repository.Tests/**", "Directory.Build.props", "Directory.Packages.props", "global.json", "Apogeu.slnx", "nuget.config", ".editorconfig", ".gitignore", ".gitattributes", ".githooks/**", ".github/pull_request_template.md", ".github/CODEOWNERS", "tools/sdd/**", "README.md", "CLAUDE.md", "AGENTS.md", "CHANGELOG.md"]
adrs: [ADR-0001, ADR-0002, ADR-0004, ADR-0011, ADR-0012, ADR-0016, ADR-0022]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0002 — Estrutura do repositório e qualidade de código

## 1. Visão Geral
Cria o esqueleto do repositório único: solução .NET 10 com os seis módulos vazios (cada um com seu projeto `Contracts`), o Host e os BuildingBlocks; workspace pnpm com os apps e pacotes do front; e toda a configuração de qualidade de código (formatadores, analisadores, linters, TypeScript estrito), além do controle SDD (hooks, template de PR, CODEOWNERS, CHANGELOG, bloco nos AGENTS/CLAUDE).

## 2. Motivação & Escopo
**Motivação:** todas as outras specs precisam de um lugar padronizado para viver e de regras de qualidade bloqueantes desde o primeiro commit (ADR-0012), para humanos e agentes.

**Objetivos (dentro do escopo):**
- `Apogeu.slnx`, `global.json` (SDK 10.0.x), `Directory.Build.props` (Nullable, TreatWarningsAsErrors, AnalysisLevel/Mode, Meziantou.Analyzer) e `Directory.Packages.props` (Central Package Management + lock files) **já com as versões de todos os pacotes previstos pelos ADRs da fundação**, para que as specs seguintes só referenciem pacotes sem disputar esse arquivo (pacote não previsto exige ADR e emenda).
- Projetos: `src/backend/Host`, `src/backend/BuildingBlocks`, `src/backend/Modules/{Rounds,Fairness,Betting,Wallet,Operators,Backoffice}/<Modulo>.{Contracts,Features,Domain?,Infrastructure}` vazios (Domain só em Rounds, Fairness, Betting e Wallet), já registrados no Host por chamadas explícitas `Add<Modulo>Module()` e `Map<Modulo>Endpoints()`.
- `src/frontend` com `pnpm-workspace.yaml`, `apps/{game-pipa,backoffice,demo-site}` (Vite + React + TS, página mínima) e `packages/{contracts,game-sdk,game-ui,scene-kit}`; `tsconfig.base.json` estrito; ESLint flat (typescript-eslint strictTypeChecked, react-hooks, jsx-a11y); Prettier; `.nvmrc` e `packageManager`.
- `vendor --agents --pr-template github --hooks --changelog --codeowners` do SDD; README e CLAUDE.md com os comandos.
- Comandos reais no `sdd-config.yml`: `build`, `lint`.
- Projeto de teste mínimo `tests/backend/Repository.Tests` (xUnit v3) para os testes desta spec; o harness completo é a SPEC-0003.

**Não-objetivos (fora do escopo):**
- Código de domínio, endpoints ou telas reais.
- CI (SPEC-0004), Aspire (SPEC-0005), harness completo (SPEC-0003).

## 3. Dependências
- **Implementações necessárias:** N/A
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** .NET SDK 10.0.x, Node.js LTS e pnpm instalados na máquina; Docker (para specs seguintes).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; padrão definido nos ADRs de fundação (ADR-0001, 0002, 0004, 0011, 0012, 0016).

**Decisão:** estrutura por módulo com projeto `Contracts` público e vertical slices em `Features`; front em workspace pnpm com a direção de dependência do ADR-0004; regras de qualidade centralizadas em `Directory.Build.props` e na configuração ESLint/Prettier/tsconfig da raiz do front.

**Justificativa:** aplica diretamente as regras dos ADRs; centralizar a configuração impede que um projeto novo nasça sem as regras.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** Biome no lugar de ESLint+Prettier (ADR-0012); `.sln` clássico no lugar de `.slnx` (formato XML padrão do .NET 10, mais fácil de revisar).

**ADRs:** ADR-0001, ADR-0002, ADR-0004, ADR-0011, ADR-0012, ADR-0016.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** N/A — sem código de execução.
- **Segurança:** nenhum segredo no repositório; `.gitignore` cobre `appsettings.*.local.json`, `.env*`, `secrets.json` — verificado por IT-06.
- **Privacidade e dados pessoais:** N/A — sem dados.
- **Disponibilidade e resiliência:** N/A — sem serviço em execução.
- **Acessibilidade (UI):** jsx-a11y ativo no lint desde já — verificado por IT-04.
- **Custo:** N/A — ferramentas gratuitas.

## 6. Artefato A — Contrato
**Interface:** estrutura de pastas + comandos de desenvolvimento.

```text
Comandos (raiz do repositório):
  dotnet build Apogeu.slnx                       → 0 avisos, 0 erros
  dotnet format Apogeu.slnx --verify-no-changes  → exit 0 se formatado; ≠0 caso contrário
  pnpm -C src/frontend install --frozen-lockfile
  pnpm -C src/frontend build | lint | typecheck | format:check
Estrutura:
  src/backend/{Host,BuildingBlocks,Modules/<Modulo>/<Modulo>.<Camada>}
  src/frontend/{apps/<app>,packages/<pkg>}
  tests/backend/<Projeto>.Tests, tests/e2e
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Clone limpo compila | `dotnet build` e `pnpm build` após clone | sucesso, zero avisos | IT-01 |
| Configuração estrita aplicada | qualquer projeto .NET ou pacote TS | Nullable/TreatWarningsAsErrors/AnalysisMode e TS strict ativos | UT-01, UT-02 |
| Código C# não formatado | arquivo com indentação errada | `dotnet format --verify-no-changes` falha | IT-02 |
| Aviso de compilação C# | variável não usada / nulidade | build falha | IT-03 |
| Violação de lint TS | `any` explícito ou falta de `alt` em `<img>` | `pnpm lint` falha | IT-04 |
| Commit fora do padrão | mensagem sem Conventional Commits ou sem `Refs: SPEC-NNNN` | hook `commit-msg` rejeita | IT-05 |
| Segredo local ignorado | criar `appsettings.Development.local.json` / `.env` | `git status` não o lista | IT-06 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o `Directory.Build.props`, quando suas propriedades são lidas, então `Nullable=enable`, `TreatWarningsAsErrors=true`, `AnalysisMode=Recommended` e a referência a Meziantou.Analyzer estão presentes.
- **UT-02** — Dado o `tsconfig.base.json`, quando lido, então `strict`, `noUncheckedIndexedAccess` e `exactOptionalPropertyTypes` são `true` e todos os pacotes o estendem.

### 7.3 Testes de Integração
- **IT-01** — Dado um clone limpo, quando `dotnet build` e `pnpm build` rodam, então ambos terminam com sucesso e zero avisos.
- **IT-02** — Dado um arquivo C# fora do formato numa cópia temporária, quando `dotnet format --verify-no-changes` roda, então sai com código ≠ 0.
- **IT-03** — Dado um projeto temporário com variável não usada, quando compilado com o `Directory.Build.props`, então o build falha.
- **IT-04** — Dado um arquivo TSX temporário com `any` explícito e `<img>` sem `alt`, quando `pnpm lint` roda, então falha apontando as duas regras.
- **IT-05** — Dado o hook `commit-msg` ativo, quando se tenta commitar "ajustes", então o commit é rejeitado; com "chore(repo): estrutura\n\nRefs: SPEC-0002" é aceito.
- **IT-06** — Dados arquivos `.env` e `appsettings.Development.local.json` criados, quando `git status --porcelain` roda, então nenhum aparece.

### 7.4 Testes de Contrato
- N/A — não há contrato entre specs (estrutura e comandos são consumidos como ambiente, não como contrato versionado).

### 7.5 Testes E2E
- N/A — nenhuma jornada de usuário existe ainda; esta spec só cria a estrutura (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** arquivos temporários criados e apagados por teste.

**Ambiente de execução:** local e CI (a partir da SPEC-0004).

## 8. Plano de Rollout
- **Estratégia:** merge direto (não há produção).
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
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0002, ADR-0004, ADR-0011, ADR-0012, ADR-0016 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0002:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06 com a tag `SPEC-0002:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS — 7a812e0 | 2026-10-07 |
| G2 Green | PASS | build exit 0 (✓ built in 58ms); test exit 0 (Passed!  - Failed:     0, Passed:    21, Skipped:     0, Total:    21, Duration: 50 s - Ap); lint exit 0 (All matched files use Prettier code style!) — 9009df1 | 2026-10-08 |
| G3 Arquitetura | N/A | Testes de arquitetura ainda não existem: são entregues pela SPEC-0003 (harness e testes de arquitetura), que roda logo após esta onda | 2026-10-08 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer (agente independente): APPROVED em 358c0bd — 0 blocker/major; 2 minor no format-staged (caminhos com espaço, cópia não-staged) aceitos como débito — 358c0bd | 2026-10-08 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-08 | G4 | decisão | Dependências com licenças fora da lista permitida do ADR-0016: Npgsql (PostgreSQL License), minimatch (BlueOak-1.0.0), language-subtag-registry (CC0-1.0), caniuse-lite (CC-BY-4.0, dado usado só no build) | Reviewer confirmou que todas são permissivas e entram por ferramentas já decididas (EF Core/PostgreSQL, Vite, jsx-a11y); nenhuma troca de código resolve sem perder essas ferramentas | thomas: aceitar as licenças (ADR que amplia a lista) ou restringir a lista a dependências de runtime | ADR-0022 — opção A do usuário: lista de licenças ampliada (PostgreSQL, BlueOak-1.0.0, CC0-1.0; CC-BY-4.0 só para dados de build) | 2026-10-08 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0002`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
