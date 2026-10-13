---
id: SPEC-0052
title: Limite local da rajada de cash-out configurável por máquina
tier: lite
type: fix
user_facing: false
status: in-progress
created: 2026-10-13
parent: SPEC-0019
depends_on: []
consumes_contract: []
touches: ["tests/backend/Modules/Betting.Tests/Integration/CashOutLatencyTests.cs", "tests/backend/Modules/Betting.Tests/Support/CashOutLatencyBudget.cs", "tests/backend/Modules/Betting.Tests/Unit/CashOutLatencyBudgetTests.cs", "CLAUDE.md"]
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

## 3. Mudança Proposta
- **Muda:** o limite sai de um resolvedor pequeno (`CashOutLatencyBudget`, no `Support` do Betting.Tests), com estas regras:
  1. `GITHUB_ACTIONS=true` → 3 s, ignorando qualquer outra variável, para que ninguém afrouxe a CI por ambiente;
  2. fora da CI, `APOGEU_CASHOUT_P99_BUDGET_MS` com um inteiro > 0 → esse valor em ms;
  3. sem a variável → 75 ms (máquina de referência);
  4. valor inválido (não inteiro, ≤ 0) → exceção com mensagem clara, nunca um limite silencioso.
- A mensagem de falha do teste passa a dizer de onde veio o limite.
- O `CLAUDE.md` documenta a variável. Esta VM usa 400 ms (pior p99 medido: 315 ms), configurado no perfil do shell da máquina e fora do repositório.
- **Não muda:** o cenário do teste (300 cash-outs em 1 s), o limite da CI, a meta de 50 ms da SPEC-0048 e o código de produção.

**Padrão seguido:** `CashOutLatencyTests` (escolha atual por `GITHUB_ACTIONS`); helpers de teste em `tests/backend/Modules/Betting.Tests/Support/`.

**Rollback:** revert do merge commit; tirar a variável do perfil da máquina.

**Outras ocorrências:** Nenhuma. É o único teste com limite local fixo em ms; os demais testes `Category=Performance` medem proporções ou contagens.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0052:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — o `CashOutLatencyTests` da SPEC-0023 já cobre o cenário e continua inalterado.
- **UT-01** — Dado o ambiente com `GITHUB_ACTIONS=true` (com ou sem `APOGEU_CASHOUT_P99_BUDGET_MS=400`), sem nenhuma das duas, com só `APOGEU_CASHOUT_P99_BUDGET_MS=400`, e com o valor `abc`, `0` ou `-5`, quando o limite é resolvido, então dá 3 s, 75 ms, 400 ms, e exceção nomeando a variável (o resolvedor recebe as variáveis por parâmetro, sem ler o ambiente real).
- Integração/E2E: N/A — o `CashOutLatencyTests` (SPEC-0023) passa a usar o resolvedor, e o G2 desta VM o executa com 400 ms.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Seguir como, depois de 3 FAILs do G2 da SPEC-0046? — Limite local configurável por variável de ambiente; esta VM usa 400 ms; CI e SPEC-0048 inalteradas (thomas, 2026-10-13)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Regressão**
- [ ] Red: UT-01 com a tag `SPEC-0052:UT-01` falhando pelo motivo certo (resolvedor ainda sem a variável)
- [ ] Green: resolvedor + `CashOutLatencyTests` usando-o; `CLAUDE.md` documenta a variável
- [ ] Validar: build + suíte afetada com `APOGEU_CASHOUT_P99_BUDGET_MS=400` + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012
- [ ] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — c6f9b08 | 2026-10-13 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:23.25]     Dado um DTO com string?, quando o esquema é gerado, então a pr) — 3a364df | 2026-10-13 |
| G2 Green | PASS | build exit 0 (✓ built in 295ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 58546c6 | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.16s (tests 96%, transform 3%, import 1%)) — 58546c6 | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINORS (58546c6; minors: Assert.Throws<InvalidOperationException> e teste do Source; casos de borda de espaços/decimais; TypeInitializationException no static readonly; evidência do G1 cita teste de outra suíte — red confirmado no 3a364df com 7/7 NotImplementedException). Testes fora de commit red justificados: o Support é o próprio resolvedor e o IT só passou a consumi-lo. — a212b5a | 2026-10-13 |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
