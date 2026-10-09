---
id: SPEC-0016
title: Mutação com Stryker.NET e xUnit v3
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-08
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: ["tests/ci/**", ".github/workflows/nightly.yml", "stryker-config.json", ".config/dotnet-tools.json", "tools/ci/stryker-targets", "tests/backend/Modules/Fairness.Tests/Fairness.Tests.csproj", "Directory.Packages.props", "tests/backend/Modules/Fairness.Tests/Mutation/**"]
adrs: [ADR-0011]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0016 — Mutação com Stryker.NET e xUnit v3

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Rodar `dotnet stryker` em `Fairness.Domain` (meta ≥ 85%, SPEC-0007 §7.6) dá 8% com "test coverage capture failed": nenhum mutante aparece coberto por teste, embora os vetores públicos exercitem `SeedChain` e `FairnessVerifier`. O comando padrão também falha porque `stryker-config.json` lista `Wallet.Tests`, que ainda não existe. Sem isso o §7.6 da SPEC-0007 não tem evidência e o G7 dela não fecha.

## 2. Causa Raiz
Provável incompatibilidade entre o runner vstest do Stryker.NET 5.0.0 e os testes em xUnit v3 (`xunit.v3.mtp-off`); o runner `mtp` encontrou 0 testes. Config estática aponta para projeto inexistente (SPEC-0003).

## 3. Mudança Proposta
Diagnóstico do Reviewer da SPEC-0007: runner `mtp` do Stryker com o projeto de teste em MTP (ou `coverage-analysis: off` com filtro tirando o UT-06), `mutate` restrito a `Fairness.Domain` e `break` = 85. Configurar o Stryker para capturar cobertura com xUnit v3 (runner/opções corretos, ou ajuste do projeto de teste), tirar da config fixa os projetos que não existem (o `tools/ci/stryker-targets` do nightly já filtra) e registrar o score real do `Fairness.Domain`. Não muda código de produção nem os testes da SPEC-0007.

**Padrão seguido:** `stryker-config.json` (SPEC-0003) e `tools/ci/stryker-targets` (SPEC-0004).

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0016:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — ferramenta de teste, sem comportamento de produção.
- **UT-01** — Dado `Fairness.Tests` com os vetores públicos, quando `dotnet stryker` roda em `Fairness.Domain`, então o relatório JSON mostra testes cobrindo os mutantes de `SeedChain.cs` e o score ≥ 85% (evidência do SPEC-0007 §7.6). Mutantes que o Stryker marca como `Ignored` pelo filtro de bloco não contam como descobertos (o próprio Stryker os exclui do score).
- **UT-02** — (guarda: passa antes da mudança) Dados os pontos de entrada do `Fairness.Domain` (`CrashPointCalculator`, `FairnessVerifier`, `SeedChain.Build/Advance/Next/LinkIndexOfRound/CheckpointOf`), quando chamados com entradas válidas, inválidas e de borda, então devolvem os resultados do algoritmo v1 e lançam as exceções do contrato (InvalidRtp, InvalidCap, InvalidSeed, argumentos nulos/fora de faixa) — testes unitários novos em `Fairness.Tests/Mutation/`, sem alterar os testes da SPEC-0007, para que a mutação mate os mutantes de guarda e da cadeia.
- Integração/E2E: N/A — a verificação é a própria execução do Stryker, local e no job `mutation` do nightly.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN (2026-10-09). -->

**Fase 1: Regressão**
- [x] Red: escrever UT-01 com a tag `SPEC-0016:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: corrigir a causa seguindo o padrão existente
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1ddb579 (árvore suja) | 2026-10-08 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive" && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:04.14]     Dado o stryker-config.json, quando lido, então o runner não é ) — aa62e89 | 2026-10-09 |
| G2 Green | PASS | build exit 0 (✓ built in 58ms); test exit 0 (Duration  3.24s (tests 97%, import 1%, transform 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-09T13:43:32: Report generation took 0,3 seconds) — a75c4df | 2026-10-09 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  824ms (tests 97%, transform 1%, import 1%)) — a75c4df | 2026-10-09 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer (agente independente): APPROVED em a75c4df — 0 blocker/major; minors: variável do Stryker dentro do teste, concorrência 1 sem teste estático, relatório HTML do noturno, dois nits de documentação — a75c4df | 2026-10-09 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 140ms); test exit 0 (Duration  1.31s (tests 81%, transform 13%, import 4%, worker 1%)); test_integration exit 0 (Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 3 m 49 s ); test_e2e exit 0 ([run-e2e] stopping the AppHost); arch_test exit 0 (Duration  816ms (tests 97%, transform 2%, import 1%)); security_scan exit 0 (Ran 8 rules on 250 files: 0 findings.) — 7392d3f | 2026-10-09 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-09 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem); validação local via Aspire e E2E na CI (PR #20, merge 0a33c92) | 2026-10-09 |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-09 | G2 | spec | Mutação do Fairness.Domain em 37,84% (28/74): SeedChain só é exercida por testes de integração e nenhuma validação de entrada tem teste; e o UT-01 conta como não cobertos os mutantes que o Stryker 5 sempre ignora (filtro de bloco) | Implementer confirmou ativação dos mutantes, config correta e teto de ~73% sem testes novos; nenhuma opção do Stryker desliga o filtro de bloco | Architect: emenda — testes unitários novos do Fairness.Domain (guardas e SeedChain) e UT-01 desconsiderando mutantes Ignored; meta de 85% mantida | Emenda 2 (2026-10-09) — UT-01 ignora mutantes Ignored; UT-02 com testes unitários do Fairness.Domain; meta 85% mantida | 2026-10-09 |

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
O teste de mutação do `Fairness.Domain` voltou a funcionar e a medir: o Stryker.NET 5 roda com xUnit v3, o score real é 91,89% (meta 85%) e o job `mutation` do nightly falha se cair abaixo da meta.

### Como foi feito
O runner do Stryker passou a usar o modo VSTest do xUnit v3 com a variável de controle de mutante (`STRYKER_MUTANT_ID_CONTROL_VAR`), e o teste `Category=Mutation` em `tests/ci/Ci.Tests` executa o Stryker e lê o relatório JSON. Emenda 1: escopo (`tests/ci/**`, `nightly.yml`), Stryker uma vez por noite. Emenda 2 (IMP-01): mutantes `Ignored` pelo filtro de bloco não contam, e o score inicial de 37,84% foi corrigido com testes unitários novos do domínio em `Fairness.Tests/Mutation/` (guardas e cadeia de sementes), mantendo a meta de 85%.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
- **Antes:** o UT-01 falhou nos commits red b4a8ce4/aa62e89 — primeiro o runner incompatível com xUnit v3, depois, já rodando, "The final mutation score is 37.84 %" abaixo do limite de 85%.
- **Depois:** correção em e39a949 (fix) e testes do UT-02 em 30fc52f; no código integrado (1868480, após o merge 0a33c92) `dotnet test tests/ci/Ci.Tests --filter Category=Mutation` passou: "The final mutation score is 91.89 %" (74 mutantes testados, 18 ignorados pelo filtro de bloco), 9,3 min.

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Stryker em Fairness.Domain com score ≥ 85% (Ignored fora do score) | PASS | local em 1868480: score 91,89%, Passed 1; G2 a75c4df; CI 37971276684 |
| UT-02 | testes unitários das guardas e da cadeia do Fairness.Domain | PASS | CI 37971276684 (PR #20); G5 local 7392d3f |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção — UT-01 vermelho em b4a8ce4/aa62e89 (37,84%), verde em 1868480 (91,89%)
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5) — G2 a75c4df, G3, G5 7392d3f e CI 37971276684 verdes
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — N/A justificado: sem ambiente até a SPEC-0012 (G6 N/A)
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [x] Outras ocorrências registradas como novas specs (ou nenhuma) — minors do review na SPEC-0017
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes) — gitleaks sem achados (CI 37971276684)
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha — TreatWarningsAsErrors; sem supressões novas

### Deploy
Sem ambiente (G6 N/A até a SPEC-0012). Integrado na `main` pelo PR #20 (merge 0a33c92); roda no nightly às 03:00 UTC.

### Pendências
Minors do review (G4) incluídos na SPEC-0017 (Emenda de escopo, UT-04), que ainda não começou: variável do Stryker definida dentro do teste, concorrência 1 sem teste estático, relatório HTML no artefato do nightly.

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 | 2026-10-09 | UT-01 desconsidera mutantes `Ignored` (filtro de bloco do Stryker 5); novo UT-02 com testes unitários do `Fairness.Domain` em `tests/backend/Modules/Fairness.Tests/Mutation/**` (escopo ampliado); meta de 85% mantida | Score real 37,84%: guardas sem teste e `SeedChain` só coberta por integração; filtro de bloco não pode ser desligado (IMP-01) | Nenhuma | Architect — opção recomendada (manter a meta e fortalecer os testes), pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09); a confirmar no resumo |
| 1 | 2026-10-09 | Escopo: `tests/ci/**` (onde vive o UT-01) e `.github/workflows/nightly.yml`; o teste noturno de mutação leva `Category=Nightly` + `Category=Mutation`, o job `rtp-simulation` filtra `Category=Nightly&Category!=Mutation` e o job `mutation` roda o teste `Category=Mutation` (Stryker uma vez por noite) | SPEC_DEFECT do Test-writer: teste fora de `touches` e Stryker rodando duas vezes na noite | Nenhuma | Architect — pela autorização do usuário para seguir recomendações (2026-10-09) |
