---
id: SPEC-0052
title: Limites locais dos testes de desempenho configuráveis por máquina
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-13
parent: SPEC-0019
depends_on: []
consumes_contract: []
touches: ["tests/backend/TestKit/Performance/**", "tests/backend/TestKit/TestKit.csproj", "tests/backend/Modules/Betting.Tests/Integration/CashOutLatencyTests.cs", "tests/backend/Modules/Betting.Tests/Integration/PlaceBetLatencyTests.cs", "tests/backend/Modules/Betting.Tests/Integration/CashOutResilienceTests.cs", "tests/backend/Modules/Betting.Tests/Support/CashOutLatencyBudget.cs", "tests/backend/Modules/Betting.Tests/Unit/CashOutLatencyBudgetTests.cs", "tests/backend/Modules/Rounds.Tests/Integration/LeadershipTests.cs", "tests/backend/Modules/Rounds.Tests/Integration/ResumeTests.cs", "tests/backend/Modules/Fairness.Tests/Integration/FairnessRestartTimingTests.cs", "tests/backend/Modules/Wallet.Tests/Integration/DebitPerformanceTests.cs", "tests/backend/Security.Tests/Integration/LaunchPerformanceTests.cs", "CLAUDE.md"]
adrs: []
external: []
size: S
approved_by: thomas (opção escolhida no chat após o 3º FAIL do G2 da SPEC-0046; pré-aprovação da jornada do jogador)
approved_at: 2026-10-13
---

# SPEC-0052 — Limite local da rajada de cash-out configurável por máquina

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
O teste de rajada de cash-out (`CashOutLatencyTests`, da SPEC-0023) exige p99 ≤ 75 ms fora da CI. Esse limite foi calibrado na máquina de referência anterior, onde o p99 medido era 52–54 ms.
- **Ambiente atual:** a VM de desenvolvimento (OpenStack/KVM, 4 vCPU AMD EPYC, 7,8 GB).
- **Reprodução:** `dotnet test tests/backend/Modules/Betting.Tests -m:1 --filter "FullyQualifiedName~CashOutLatencyTests"` dá p99 de 145–315 ms, inclusive na branch de integração, sem nenhuma mudança nova.
- **Impacto:** todo G2/G5 local reprova (SPEC-0046 IMP-01, 3 FAILs seguidos) sem que haja regressão, e a onda 1 fica travada.
- **Esperado:** cada máquina declara o próprio limite local, e o teste continua pegando regressões nela.

## 2. Causa Raiz
`CashOutLatencyTests.P99Budget` só distingue CI (`GITHUB_ACTIONS=true` → 3 s) de "local" (75 ms fixo), e trata toda máquina local como se fosse a de referência.

Diagnóstico desta VM (2026-10-13):
- não é o disco: com `fsync=off` e `synchronous_commit=off`, o p99 deu 205 ms;
- não é o pool de threads: com `DOTNET_ThreadPool_ForceMinWorkerThreads=64`, deu 179–244 ms;
- não é CPU roubada: steal 0.

É falta de fôlego: Host, Postgres, Redis e 150 clientes SignalR disputam 4 vCPUs no segundo da rajada. A meta real de 50 ms em infraestrutura própria continua com a SPEC-0048.

**Correção do diagnóstico (2026-10-14):** a hipótese de "falta de fôlego da VM" estava errada. As medições de 2026-10-13 aconteceram com outros projetos rodando na mesma VM, com load de 23 a 79 em 4 vCPU. Medida com a máquina ociosa (load < 3, 4f9a124), a suíte `Category=Performance` inteira passa com o fator 1: cash-out p99 45,8 ms / 75; aposta 5,7 / 100; semente 5,4 / 20; troca de líder 4,58 s / 6. O fator continua útil para máquinas compartilhadas, mas o valor desta VM é **1** (sem a variável), e os gates pesados passam a rodar só com load < 3.

## 3. Mudança Proposta
**Emenda v2 (2026-10-13):** em vez de uma variável só para o cash-out, um **fator único por máquina**. A linha de base desta VM mostrou mais 4 testes de tempo reprovando sem mudança nova: aposta p99, resiliência do automático, semente do Fairness e troca de líder.
- **Muda:** um resolvedor compartilhado em `tests/backend/TestKit/Performance/` lê `APOGEU_PERF_BUDGET_SCALE` com estas regras:
  1. `GITHUB_ACTIONS=true` → fator 1, ignorando a variável; cada teste mantém o próprio limite de CI, e ninguém afrouxa a CI por ambiente;
  2. fora da CI, sem a variável → 1, a máquina de referência;
  3. número decimal > 0 em cultura invariante (ex.: `5`, `1.5`) → esse fator;
  4. valor inválido (não numérico, ≤ 0) → exceção que nomeia a variável e o valor recebido, nunca um limite silencioso.
- **Testes que passam a usar o fator** (limite local × fator, com a origem na mensagem de falha):
  - `CashOutLatencyTests` (75 ms; continua com 3 s na CI);
  - `PlaceBetLatencyTests` (100 ms);
  - `CashOutResilienceTests` (o prazo de tempo que reprova nesta VM);
  - `LeadershipTests` (6 s);
  - `FairnessRestartTimingTests` (20 ms);
  - `DebitPerformanceTests` (20 ms);
  - `LaunchPerformanceTests` (100 ms);
  - `ResumeTests` (tolerância de 250 ms).
- **Sai** a variável `APOGEU_CASHOUT_P99_BUDGET_MS` da v1 (ainda não mergeada); o resolvedor do cash-out passa a usar o fator.
- O `CLAUDE.md` documenta `APOGEU_PERF_BUDGET_SCALE`. Esta VM usa o fator 1 (sem a variável), conforme a correção do diagnóstico acima.
- **Não muda:** o cenário de nenhum teste, os limites da CI, as metas reais de produção (SPEC-0048) e o código de produção.

**Padrão seguido:** o resolvedor da v1 (`CashOutLatencyBudget`), generalizado; os helpers compartilhados de `tests/backend/TestKit/`.

**Rollback:** revert do merge commit; tirar a variável do perfil da máquina.

**Outras ocorrências:** todas as ocorrências conhecidas de limite local fixo em testes `Category=Performance` estão listadas acima. Os limites de minutos (subida do AppHost, rajada de entrada) passam nesta VM e ficam como estão.

## 4. Plano de Testes (TDD)
<!-- Tag no código: `SPEC-0052:<ID>`. -->
- Caracterização: N/A — os testes de desempenho existentes cobrem os cenários e continuam inalterados.
- **UT-01** (Emenda v2; substitui o UT-01 da v1, da variável do cash-out) — Dado o ambiente com `GITHUB_ACTIONS=true` (com ou sem `APOGEU_PERF_BUDGET_SCALE=5`), sem nenhuma das duas, com só `APOGEU_PERF_BUDGET_SCALE=5`, com `1.5`, e com `abc`, `0` ou `-1`, quando o limite local de 75 ms é resolvido (com CI de 3 s), então 3 s, 75 ms, 375 ms, 112,5 ms, e exceção nomeando a variável e o valor. A origem informada é `CI`, `reference machine` ou `APOGEU_PERF_BUDGET_SCALE=5`. O ambiente é passado por parâmetro.
- Integração/E2E: N/A — os oito testes de desempenho listados passam a usar o resolvedor; o G2 desta VM os executa com o fator 1 (máquina ociosa; ver Correção do diagnóstico).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Seguir como, depois de 3 FAILs do G2 da SPEC-0046? — Limite local configurável por variável de ambiente; esta VM usa 400 ms; CI e SPEC-0048 inalteradas (thomas, 2026-10-13). Superada pela Emenda v2 e pela correção do diagnóstico de 2026-10-14: fator único, 1 nesta VM

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Regressão**
- [x] Red: UT-01 com a tag `SPEC-0052:UT-01` falhando pelo motivo certo (resolvedor ainda sem a variável)
- [x] Emenda v2 — Red: UT-01 reescrito para o fator, com a tag `SPEC-0052:UT-01`, falhando pelo motivo certo
- [x] Emenda v2 — Green: resolvedor no TestKit e os oito testes usando o fator; `CLAUDE.md`
- [x] Green: resolvedor + `CashOutLatencyTests` usando-o; `CLAUDE.md` documenta a variável
- [x] Validar: build + suíte afetada + arquitetura (G2/G3), com a máquina ociosa e fator 1

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy (G6): N/A até a SPEC-0012
- [x] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — c6f9b08 | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:00.72]     Dado um ambiente sem GITHUB_ACTIONS nem APOGEU_PERF_BUDGET_SCA) — 3a5f57f | 2026-10-14 |
| G2 Green | PASS | build exit 0 (✓ built in 254ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — b2ccf8b | 2026-10-14 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.00s (tests 95%, transform 3%, import 1%)) — b2ccf8b | 2026-10-14 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINORS (Emenda v2, b2ccf8b). Minors: fator enorme (1e300) gera OverflowException sem nomear a variável e faltam NaN/Infinity/1,5 no UT-01; GITHUB_ACTIONS com distinção de caixa; static readonly embrulha erro de configuração (TypeInitializationException); prazo (b) do IT-10 sem fator; docstring do PerformanceBudget cita fator 5; UT-01 com nome de cash-out. Testes alterados fora de commit red justificados: só passaram a ler o limite pelo resolvedor. Minor de spec (texto desatualizado) corrigido pelo Architect. — b2ccf8b | 2026-10-14 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #31 verde (run 38676137955, commit 32cbe33): backend (suíte .NET completa + Performance), frontend, contracts, e2e (Chromium, WebKit e Firefox), security e sdd; backend verde na 3ª tentativa e e2e Firefox na 2ª, após falhas de tempo conhecidas sem relação com a onda (LateDebitReusedClientBetIdTests, E2E Firefox de aposta perdida/dupla, troca de rodada no teste do líder impedido, alerta de conciliação verificado sem espera em SettlementRaceTests; os dois últimos passam 3/3 localmente); local em 54f8b85: build 0 avisos, arquitetura 19/19 + front, gitleaks sem vazamentos, NuGet/pnpm sem vulnerabilidades | 2026-10-15 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-15 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem e deploy) | 2026-10-15 |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue
Os limites locais dos testes de desempenho são multiplicados pelo fator da máquina `APOGEU_PERF_BUDGET_SCALE` (decimal > 0; sem a variável vale 1, a máquina de referência; ignorada na CI). Oito testes de tempo usam o resolvedor compartilhado, e a mensagem de falha diz de onde veio o limite.

### Como foi feito
v1 (red 3a364df, green 58546c6) tinha uma variável só para o cash-out; a Emenda v2 generalizou para o fator único `PerformanceBudget` no TestKit (scaffold 0882431, red 3a5f57f, green 4f9a124). O diagnóstico foi corrigido em 2026-10-14: a lentidão vinha de outros projetos na mesma VM, e com a máquina ociosa tudo passa com fator 1.

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
Red: UT-01 em 3a5f57f falhava em 8/8 casos com NotImplementedException (G1 PASS registrado nesse commit). Green: 4f9a124, UT-01 8/8; suíte `Category=Performance` toda verde com fator 1 e a máquina ociosa.

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Dado o ambiente com GITHUB_ACTIONS=true (com ou sem APOGEU_PERF_BUDGET_SCALE=5), sem nenhuma das duas, com só … | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |

### Definição de Pronto
- [x] Teste de regressão falhou antes e passa depois da correção
- [x] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão existente mantido
- [x] Disponível no ambiente-alvo via pipeline (G6) — N/A até a SPEC-0012; em main pelo PR #31
- [x] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [x] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy
Sem ambiente de deploy até a SPEC-0012 (G6 N/A); disponível em main pelo PR #31 (merge 25c6d4e, 2026-10-15).

### Pendências
Minors da 2ª revisão: fator enorme gera OverflowException sem nomear a variável; `GITHUB_ACTIONS` comparado com distinção de caixa; `static readonly` embrulha o erro de configuração; o prazo (b) do IT-10 fica sem fator; o UT-01 tem nome de cash-out. A dependência de ordem do teste de latência ficou registrada na SPEC-0048.

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 2 (inalterado) | 2026-10-14 | Diagnóstico corrigido: a lentidão vinha de outros projetos na mesma VM; com a máquina ociosa tudo passa com fator 1; gates pesados só com load < 3 | Linha de base com a máquina ociosa (4f9a124) | SPEC-0046 (IMP-01: causa real registrada) | thomas (opção "esperar a máquina livre", 2026-10-14) |
| 2 | 2026-10-13 | Fator único `APOGEU_PERF_BUDGET_SCALE` para os limites locais de oito testes de desempenho, no lugar de `APOGEU_CASHOUT_P99_BUDGET_MS`; UT-01 redefinido para o fator; esta VM usa 5 | Linha de base desta VM: mais 4 testes de tempo reprovam sem mudança nova (aposta, resiliência, semente, troca de líder); G2 da SPEC-0025 caiu por eles | SPEC-0025, SPEC-0046 (recebem por merge) | thomas (opção "fator único por máquina", 2026-10-13) |
