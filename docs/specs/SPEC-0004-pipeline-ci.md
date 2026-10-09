---
id: SPEC-0004
title: "Pipeline de CI no GitHub Actions"
tier: full
type: foundation
user_facing: false
status: implemented
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0002, SPEC-0003]
consumes_contract: [SPEC-0006@1]
contract_version: 3
touches: [".github/workflows/**", ".github/dependabot.yml", ".gitleaks.toml", ".semgrep/**", "tools/ci/**", "tests/ci/**", "src/backend/AppHost/AppHost.csproj", "src/backend/AppHost/packages.lock.json", "tests/backend/AppHost.Tests/AppHost.Tests.csproj", "tests/backend/AppHost.Tests/packages.lock.json", "tests/backend/Architecture.Tests/Architecture.Tests.csproj", "tests/backend/Architecture.Tests/packages.lock.json", "tests/backend/AppHost.Tests/Integration/EnvironmentStartupTests.cs", "tests/backend/Modules/Fairness.Tests/Integration/FairnessRestartTimingTests.cs", "tests/backend/Modules/Fairness.Tests/Unit/CrashPointPerformanceTests.cs"]
adrs: [ADR-0011, ADR-0012, ADR-0013, ADR-0016]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0004 — Pipeline de CI no GitHub Actions

## 1. Visão Geral
Cria o pipeline no GitHub Actions que protege a branch principal: build, lint, testes (unitários, integração, arquitetura, E2E), segurança (Semgrep, gitleaks, auditoria de dependências), verificação de contratos gerados, imagens de container e o job SDD (`validate` + `pr-check`), com filtros por caminho. Inclui criar o repositório no GitHub. A proteção da branch no servidor fica para a SPEC-0015 (emenda v2).

## 2. Motivação & Escopo
**Motivação:** a CI é o portão final que vale para humanos e agentes (mandatos 6 e 7); sem ela, nada garante que a main fique verde.

**Objetivos (dentro do escopo):**
- Repositório **privado** `thomasravache/crash-games-provider` criado. Proteção da `main` no servidor fora do escopo (emenda v2 → SPEC-0015): no GitHub Free privado ela é indisponível; o controle é de processo — CI em todo PR e merge só pelo Architect após G5 verde.
- Workflows: `ci.yml` (jobs `backend`, `frontend`, `e2e`, `security`, `contracts`, `sdd`, com filtro de caminhos), `images.yml` (no merge, gera as imagens dos recursos do AppHost — API por `dotnet publish -t:PublishContainer`, apps Vite por `docker build` — como artefato, sem publicar em registro; o modo publish do Aspire fica para a SPEC-0012), `nightly.yml` (simulação de RTP e mutação).
- Dependabot (NuGet, npm, GitHub Actions, Docker) semanal.
- Comandos `ci` e `security_scan` no `sdd-config.yml`.

**Não-objetivos (fora do escopo):**
- Deploy em staging/produção (spec "Hospedagem e deploy", futura — ADR-0013).
- Publicação de imagens em registro.

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 (comandos de build e lint) e SPEC-0003 (comandos de teste).
- **Contratos consumidos:** SPEC-0006@1 — comando `contracts:check` (exit ≠ 0 em divergência).
- **Pré-requisitos externos:** autorização explícita do usuário para criar o repositório no GitHub, fazer o primeiro push e abrir os PRs de prova dos testes live (ação visível externamente); a proteção da branch é da SPEC-0015; `gh` autenticado na conta `thomasravache`.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0011 (fluxo por PR), ADR-0012 (qualidade), ADR-0013 (entrega), ADR-0016 (dependências).

**Decisão:** um workflow principal com filtro de caminhos por job; o job `sdd` roda sempre; cache de NuGet, pnpm e navegadores do Playwright; todos os jobs aplicáveis são checks de todo PR para a `main` (informativos; obrigatórios no servidor com a SPEC-0015).

**Justificativa:** CI por caminho mantém o feedback rápido sem abrir mão do portão SDD.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** um workflow por stack (duplica o portão SDD e complica os checks obrigatórios).

**ADRs:** ADR-0011, ADR-0012, ADR-0013, ADR-0016.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** PR só de front ≤ 8 min; PR completo ≤ 20 min — medido nas execuções de IT-01/IT-02.
- **Segurança:** permissões mínimas (`permissions: contents: read` por padrão); ações de terceiros fixadas por SHA; segredos só via GitHub Secrets — verificado por UT-01.
- **Privacidade e dados pessoais:** N/A — sem dados pessoais no CI.
- **Disponibilidade e resiliência:** N/A — serviço do GitHub.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** dentro da cota gratuita do GitHub Actions; minutos medidos nas primeiras execuções (cota não verificada — ADR-0013).

## 6. Artefato A — Contrato
**Interface:** checks do PR e comandos do sdd-config.

```text
Checks de todo PR para a main (informativos; obrigatórios no servidor só com a SPEC-0015):
  ci/backend, ci/frontend, ci/e2e, ci/security, ci/contracts, ci/sdd
  (jobs sem mudanças relevantes reportam sucesso "skipped" para não bloquear)
Merge: só pelo Architect, com merge commit, depois de G5 com todos os checks verdes
sdd-config:
  ci: "gh pr checks --watch"
  security_scan: (mesmos passos do job security) gitleaks --redact; Semgrep pela imagem semgrep/semgrep:1.178.0@sha256:32e459968daabe7ab86968184a29109b9564aa00392401156f9788452b42786b; vulnerabilidades NuGet
    via tools/ci/nuget-vulnerabilities (falha em High/Critical); pnpm audit --audit-level high; tools/ci/check-licenses
Gatilhos: pull_request (main), push (main), schedule (nightly 03:00 UTC)
Saídas verificáveis: gitleaks imprime "leaks found: N" e o RuleID (--verbose --redact); imagens no artefato `container-images`
  (apogeu-api.tar, apogeu-game-pipa.tar, apogeu-backoffice.tar, apogeu-demo-site.tar), retenção de 1 dia
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Workflows válidos e seguros | arquivos em `.github/workflows` | actionlint sem erros; ações fixadas por SHA; permissões mínimas | UT-01 |
| PR só de front | mudança em `src/frontend/**` | jobs frontend, e2e, contracts e sdd executam; backend reporta skipped | IT-01 |
| Teste quebrado | PR com teste falhando | check vermelho e PR com estado de merge não limpo (`mergeStateStatus` ≠ `CLEAN`) | IT-02 |
| Segredo commitado | PR com chave falsa no código | job security falha (gitleaks) | IT-03 |
| Contrato divergente | DTO alterado sem regenerar contratos | job contracts falha | IT-04, CT-01 |
| Merge na main | merge de PR verde | imagens de container geradas como artefato | IT-05 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dados os workflows, quando `actionlint` e o script `tools/ci/check-workflows` rodam, então não há erros, toda ação de terceiros está fixada por SHA e todo workflow declara `permissions`.

### 7.3 Testes de Integração
- **IT-01** — Dado um PR de teste que altera só `src/frontend/**`, quando o CI roda, então os jobs frontend, e2e, contracts e sdd executam e o backend reporta skipped (evidência: link da execução).
- **IT-02** — Dado um PR de teste com um teste unitário falhando, quando o CI roda, então o check fica vermelho e o PR reporta `mergeStateStatus` diferente de `CLEAN` (o bloqueio no servidor é da SPEC-0015).
- **IT-03** — Dado um PR de teste com uma chave AWS de exemplo no código, quando o CI roda, então o job security falha no gitleaks.
- **IT-04** — Dado um PR de teste que altera um DTO sem regenerar `packages/contracts`, quando o CI roda, então o job contracts falha.
- **IT-05** — Dado o merge de um PR verde, quando o workflow `images` roda, então as imagens da API e dos apps são geradas e anexadas como artefato.

### 7.4 Testes de Contrato
- **CT-01** — Dado o contrato SPEC-0006@1, quando o job contracts executa `pnpm -C src/frontend contracts:check` sobre contratos desatualizados, então recebe exit ≠ 0 e falha.

### 7.5 Testes E2E
- N/A — sem jornada de usuário; o CI apenas executa os E2E das outras specs (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** PRs de teste em branches descartáveis, fechados após a evidência.

**Ambiente de execução:** GitHub Actions (runners ubuntu-latest).

## 8. Plano de Rollout
- **Estratégia:** ativar os workflows (informativos), validar IT-01..05; torná-los obrigatórios fica para a SPEC-0015.
- **Dados/schema:** N/A
- **Compatibilidade:** N/A
- **Observabilidade:** histórico de execuções do Actions; tempo por job acompanhado.
- **Rollback:** reverter o workflow; desativar o job afetado temporariamente com registro de impedimento.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] O repositório no GitHub será privado ou público? — Privado; SAST via Semgrep CE, CodeQL só se o repositório virar público ou houver licença (thomas, 2026-10-07)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0011, ADR-0012, ADR-0013, ADR-0016 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01 com a tag `SPEC-0004:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0004:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly" && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:01.16]     Dado um workflow inseguro, quando tools/ci/check-workflows rod) — 367e6a8 | 2026-10-08 |
| G2 Green | PASS | build exit 0 (✓ built in 81ms); test exit 0 (Duration  1.53s (tests 98%, transform 2%, import 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-08T23:31:34: Report generation took 0,1 seconds) — 87317f8 | 2026-10-08 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.05s (tests 97%, transform 2%, import 1%)) — 87317f8 | 2026-10-08 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: APPROVED — re-review em 87317f8, 0 blocker/major; pendências: UT-01 para as regras novas do check-workflows, .semgrepignore, Dependabot sem [no-spec] — 87317f8 | 2026-10-08 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 50ms); test exit 0 (Duration  968ms (tests 98%, transform 1%, import 1%)); test_integration exit 0 (No test matches the given testcase filter `Category=Integration` in /private/tmp/claude-50); test_e2e exit 0 (3 passed (3.6s)); arch_test exit 0 (Duration  745ms (tests 98%, transform 1%, import 1%)); security_scan exit 0 (Ran 8 rules on 96 files: 0 findings.) — 93cdba6 | 2026-10-09 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-09 |
| G6 Deploy | N/A | Sem ambiente remoto nesta fase (ADR-0013): deploy na SPEC-0012 | 2026-10-09 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 38d2b4e | 2026-10-09 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-08 | G2 | decisão | Proteção de branch e rulesets indisponíveis: repositório privado no GitHub Free (API retorna 403 'Upgrade to GitHub Pro or make this repository public'). Sem isso IT-06 (push direto rejeitado) e a parte de bloqueio do IT-02 não podem passar, e os checks obrigatórios do contrato §6 não são aplicados. | Test-writer consultou branches/main/protection e rules/branches/main com o token do dono (admin) | thomas: (a) assinar GitHub Pro, (b) tornar o repositório público, ou (c) emenda: manter privado sem proteção no servidor, CI só informativa e merges só pelo Architect após G5 | Emenda v2 — repositório segue privado; proteção da main e IT-06 movidos para SPEC-0015 (thomas, 2026-10-08) | 2026-10-08 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
Pipeline de CI no GitHub Actions para o repositório privado `thomasravache/crash-games-provider`. O workflow `ci` roda em todo PR e push na `main`, com filtro de caminhos, nos jobs `backend` (build sem avisos, formatação, testes unitários, de integração e de arquitetura com cobertura, e testes cronometrados numa etapa própria), `frontend`, `e2e` (Playwright em 3 navegadores), `security` (gitleaks, Semgrep pela imagem oficial fixada por digest, vulnerabilidades NuGet e npm, licenças), `contracts` (build + `contracts:check`) e `sdd` (`validate` + `pr-check`). O workflow `images` gera no merge as imagens da API e dos três apps como artefato `container-images` (retenção de 1 dia), e o `nightly` roda a simulação de RTP e a mutação às 03:00 UTC, pulando noites sem commits. Dependabot semanal para NuGet, npm, Actions e Docker. Todas as ações estão fixadas por SHA, com permissões só de leitura, verificadas por `tools/ci/check-workflows`.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
- TDD com papéis separados: Test-writer (scaffold e0ff0ea, red 367e6a8; emenda v2 697deef; testes reforçados 3757033; leitura do log por API 64bd811), Implementer (6dd5ffd, 441aea8, 2834c6f, d8f3985, 124a0d2; rodada do G4 6d74e93, 22d6c64, d143247, 3981172, b89a054, 87317f8), Reviewer independente (CHANGES_REQUESTED → APPROVED em 87317f8).
- Emenda v2: o repositório segue privado no GitHub Free, sem proteção de branch no servidor. Os checks são informativos, e o merge acontece só depois do G5; a proteção e o IT-06 foram para a SPEC-0015. Também: imagens sem Aspire publish (o modo publish do AppHost foi para a SPEC-0012).
- Emenda v3: o `security_scan` do sdd-config ficou igual ao job `security`.
- Ajustes do primeiro run real (PR #6):
  - lock files do AppHost sem RID fixo (`RestorePackagesWithLockFile=false` no AppHost e nos testes que o referenciam);
  - build antes do `contracts:check`;
  - Python-2.0 aceita só em dependência de build (emenda do ADR-0022);
  - testes cronometrados com `Category=Performance` numa etapa sem cobertura.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type foundation.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Workflows válidos, ações fixadas por SHA, permissões mínimas | PASS | G5 em 93cdba6 (Ci.Tests 10/10) e CI do PR #6 em eb0e2af |
| IT-01 | PR só de front: backend skipped, demais jobs executam | PASS | live, PR #12 (CI em 1 min 59 s) |
| IT-02 | Teste quebrado: check vermelho e `mergeStateStatus` ≠ CLEAN | PASS | live, PR #10 (CI completa em 15 min 12 s) |
| IT-03 | Segredo commitado: `security` falha no gitleaks (`aws-access-token`) | PASS | live, PR #15 |
| IT-04 | DTO alterado sem regenerar: `contracts` falha com a divergência | PASS | live, PR #16 |
| IT-05 | Merge de PR verde: artefato `container-images` com as 4 imagens | PASS | live, PR #13 (merge e1d0d8a; run de imagens 37895259188) |
| CT-01 | `contracts:check` sobre contrato editado à mão sai ≠ 0 | PASS | live, PR #17 |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado — o push direto na `main` saiu do escopo (emenda v2, SPEC-0015)
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado — exceção de lock file do AppHost e Python-2.0 de build registradas (emendas da SPEC-0004 e do ADR-0022)
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — PR só de front em 1 min 59 s (≤ 8 min, IT-01); PR completo em 15 min 12 s (≤ 20 min, IT-02); storage dentro da cota com retenção de 1 dia
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A justificado: sem ambiente remoto nesta fase (ADR-0013), G6 aponta a SPEC-0012
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — histórico de execuções do Actions; rollback = reverter o workflow
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma) — SPEC-0015, SPEC-0016, SPEC-0017, SPEC-0012
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Workflows ativos na `main` desde o PR #6 (merge 4771758, 2026-10-09). A correção dos testes live (64bd811) entra no próximo PR. G6 N/A até a SPEC-0012.

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0015 — proteção da `main` e IT-06 (parada até haver GitHub Pro ou o repositório ficar público).
- SPEC-0016 — mutação com Stryker e xUnit v3.
- SPEC-0017 — etapas do job `backend`, coverlet em todos os testes, `.semgrepignore`, casos novos do `check-workflows`, PRs do Dependabot no `pr-check`.
- SPEC-0012 — modo publish do AppHost e hospedagem.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0004`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-08 | Proteção da `main` no servidor e IT-06 saem do escopo; IT-02 passa a verificar check vermelho e `mergeStateStatus` ≠ `CLEAN`; checks `ci/*` informativos, merge só pelo Architect após G5 | IMP-01: repositório privado no GitHub Free não tem branch protection nem rulesets (API 403); o humano escolheu manter privado | SPEC-0015 (nova, recebe a proteção e o IT-06); nenhum consumidor do contrato | thomas, 2026-10-08 (chat) |
| 2 (inalterado) | 2026-10-08 | `images.yml` gera as imagens sem Aspire publish (`dotnet publish -t:PublishContainer` para a API, `docker build` para os apps Vite), mesmo artefato `container-images` | SPEC_DEFECT do Implementer: o AppHost quebra no modo publish (`.Single()` em `JavaScriptPackageInstallerAnnotation`) e não tem ambiente de computação; corrigir exige mexer no AppHost e talvez `Aspire.Hosting.Docker` (sem ADR) — decisão natural da SPEC-0012, que escolhe a hospedagem | SPEC-0012 (recebe o modo publish do AppHost) | Architect — mudança de texto sem mudança de contrato, coberta pela autorização do usuário para seguir recomendações (2026-10-08) |
| 2 (inalterado) | 2026-10-09 | Escopo: AppHost, AppHost.Tests e Architecture.Tests restauram sem `packages.lock.json` (`RestorePackagesWithLockFile=false`, motivo no csproj); demais projetos seguem com lock e `--locked-mode` | G5 FAIL no primeiro run real da CI (PR #6): o SDK do Aspire referencia `Aspire.Dashboard.Sdk.<rid>`/`Aspire.Hosting.Orchestration.<rid>` da plataforma da máquina, então o lock gerado no macOS diverge no runner Linux (NU1004). Exceção pontual ao ADR-0016 para o orquestrador local | Nenhuma | Architect — correção de escopo para a CI rodar, coberta pela autorização do usuário para seguir recomendações (2026-10-09) |
| 2 (inalterado) | 2026-10-09 | Testes cronometrados (SPEC-0005 IT-01, SPEC-0007 UT-07/IT-02) com `Category=Performance`, fora da etapa com cobertura; etapa própria no job `backend`, sem instrumentação e um projeto por vez | G5 FAIL no PR #6: no runner de 2 vCPUs com coverlet e assemblies em paralelo o AppHost levou 98 s (meta 90 s) e a mediana do IT-02 deu 28,6 ms (meta 20 ms) — o coverlet instrumenta o laço medido. Os limites das specs não mudam | SPEC-0005, SPEC-0007 (só o trait de categoria) | Architect — ajuste de execução da CI recomendado pelos reviewers do G4, coberto pela autorização do usuário para seguir recomendações (2026-10-09) |
| 3 | 2026-10-09 | `security_scan` do sdd-config igual ao job `security`: Semgrep pela imagem oficial fixada por digest (Docker), vulnerabilidades NuGet bloqueando em High/Critical por `tools/ci/nuget-vulnerabilities` e `tools/ci/check-licenses` | O comando v2 exigia o Semgrep instalado na máquina e o `dotnet list --vulnerable` nunca falhava; o G5 local precisa rodar o mesmo portão da CI | Nenhuma (nenhuma spec consome o contrato da SPEC-0004) | Architect — alinhamento com a CI já revisada no G4, coberto pela autorização do usuário para seguir recomendações (2026-10-09) |
