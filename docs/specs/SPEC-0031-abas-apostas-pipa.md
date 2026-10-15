---
id: SPEC-0031
title: Abas de apostas da Pipa
tier: lite
type: feature
user_facing: true
status: in-progress
created: 2026-10-09
parent: SPEC-0019
depends_on: [SPEC-0025, SPEC-0026]
consumes_contract: [SPEC-0025@4]
touches: ["src/frontend/packages/game-sdk/**", "src/frontend/packages/game-ui/**", "src/frontend/apps/game-pipa/src/hud/**", "src/frontend/apps/game-pipa/src/App.tsx", "tests/frontend/pipa/bets-tabs/**", "tests/e2e/pipa/bets-tabs/**", "tests/frontend/integration/pipa/bets-tabs/**"]
adrs: [ADR-0004, ADR-0008, ADR-0019]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0031 — Abas de apostas da Pipa

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Pela Emenda v2 da SPEC-0026, o painel de aposta da Pipa sai sem as abas "Todas as apostas" (apostas ao vivo da sala) e "Minhas apostas" (histórico do jogador), previstas no design "Pipa Crash" do Stitch. O jogador aposta e retira, mas não vê o que os outros apostam nem consulta as próprias apostas anteriores.

## 2. Causa Raiz
As abas dependem da SPEC-0025 (`JoinSalaBets`/`LiveBetsSnapshot`, lotes de apostas ao vivo e `GET /api/v1/me/bets`). Foram separadas do painel para que o painel (SPEC-0026) rode na mesma onda que a SPEC-0025 (decisão de thomas, 2026-10-09).

## 3. Mudança Proposta
Muda: o `game-sdk` ganha o cliente das apostas ao vivo e do histórico (paginação por cursor), e o `game-ui` ganha as duas abas abaixo dos painéis, conforme o design do Stitch: "Todas as apostas" com apelido anonimizado, valor, multiplicador e ganho, e totais da rodada; "Minhas apostas" com as apostas do jogador (status cashed_out, lost, cancelled, refunded) e "carregar mais".

Não muda: os painéis de aposta, o saldo e o contrato da SPEC-0025.

**Padrão seguido:** store e cliente do hub de apostas da SPEC-0026 em `game-sdk`; componentes do painel em `game-ui`.

**Rollback:** revert dos commits (só cliente e UI).

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0031:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — código novo; os painéis são cobertos pela SPEC-0026.
- **UT-01** — Dado um `LiveBetsSnapshot` seguido de lotes, quando aplicados ao store, então a aba "Todas as apostas" mostra as apostas da rodada com apelido anonimizado e os totais; numa nova rodada a lista é zerada.
- **UT-02** — Dadas duas páginas de `GET /api/v1/me/bets`, quando o jogador clica "carregar mais", então a segunda página é anexada pelo cursor, sem duplicar, e o botão some na última página.
- **CT-01** — Dadas as mensagens reais de `LiveBetsSnapshot`, dos lotes e de `GET /api/v1/me/bets`, quando validadas contra os tipos gerados, então conferem (SPEC-0025@4). Roda no projeto vitest `integration` contra o Host real (`pnpm test:integration`), no padrão do CT-01 da SPEC-0026, em `tests/frontend/integration/pipa/bets-tabs/` (Emenda v1).
- **E2E-01** — Dado o jogo aberto pelo lançamento demo, quando o jogador aposta R$ 10,00, retira no voo e depois perde outra aposta, então "Todas as apostas" mostrou a aposta durante a rodada e "Minhas apostas" lista as duas (cashed_out com o ganho e lost); axe sem violações nas duas abas.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, UT-02, CT-01 com a tag `SPEC-0031:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 2: Jornada E2E**
- [ ] Red: E2E-01 falhando pelo motivo certo
- [ ] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — f79c3c6 | 2026-10-09 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: ⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[5/5]⎯) — 76732c5 | 2026-10-15 |
| G2 Green | PASS | build exit 0 (✓ built in 270ms); test exit 0 ($ pnpm -C src/frontend test); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:src/frontend/coverage/lcov.info" -targetdir:artifacts/c) — 88cdf0c | 2026-10-15 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.02s (tests 96%, transform 2%, import 1%)) — 88cdf0c | 2026-10-15 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (sem bloqueantes). Minors: lote atrasado de rodada anterior pode reexibir apostas até o próximo lote (ordem entre hubs); conclusão de aposta com Minhas apostas aberta relê só a página 1; cancelamento não relê o histórico; Tentar de novo após falha do Carregar mais relê a página 1; teste UT-02 frágil ao procurar 'disabled' nas classes; tabpanel sem tabIndex; sem teste de re-join na reconexão, de falha do JoinSalaBets e de descarte por geração; ordem das apostas novas não definida pela spec — 88cdf0c | 2026-10-15 |
| G5 Integração & CI | PASS | MANUAL: MANUAL: CI do PR #34 verde (run 38717248741, commit 197fa3e): backend (suíte .NET completa + Performance), frontend, contracts, e2e (Chromium, WebKit e Firefox), security e sdd; e2e verde na 2ª tentativa após 2 falhas de tempo no webkit-mobile em testes da SPEC-0026 (E2E-02 aposta perdida, E2E-04 aposta dupla: a aposta volta a APOSTAR antes de virar RETIRAR), o mesmo padrão intermitente já visto no Firefox na onda 1; local em 197fa3e: build 0 avisos/0 erros, contracts:check sem diferença, gitleaks sem vazamentos, NuGet/pnpm sem vulnerabilidades | 2026-10-15 |
| H2 Integração aprovada | PASS | aprovado por thomas — H2 auto-on-green (CI verde do PR #34, run 38717248741); decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) | 2026-10-15 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem e deploy) | 2026-10-15 |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-15 | G1 | spec | CT-01 exige mensagens reais do Host, mas o touches não tem tests/frontend/integration/pipa/bets-tabs/** (padrão do CT-01 da SPEC-0026, projeto vitest integration); CT-01 cita SPEC-0025@1 em vez de @4; sinal de nova rodada que zera as listas não definido | test-writer leu a spec, o CT-01 da SPEC-0026 e a config do vitest; parou antes de escrever | Architect | Emenda v1 — CT-01 no projeto integration, touches ampliado, contrato @4, sinal de nova rodada = RoundBetting | 2026-10-15 |
| IMP-02 | 2026-10-15 | G2 | spec | O Hud só é montado em apps/game-pipa/src/App.tsx, fora do touches; sem ligar as abas ali o E2E-01 não passa | Implementer isolou a mudança de 2 linhas no commit 13f7a59 | architect | Emenda v2: App.tsx no touches | 2026-10-15 |

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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 | 2026-10-15 | CT-01 roda no projeto vitest `integration` contra o Host (padrão do CT-01 da SPEC-0026), com `tests/frontend/integration/pipa/bets-tabs/**` no `touches`; versão do contrato corrigida para SPEC-0025@4; o sinal de nova rodada que zera "Todas as apostas" é o `RoundBetting` de outra rodada vindo do store de rodadas | Test-writer (SPEC_DEFECT, IMP-01): mensagens reais exigem o Host e o caminho de integração estava fora do `touches` | Nenhuma | decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) |
| 2 | 2026-10-15 | `src/frontend/apps/game-pipa/src/App.tsx` entra no `touches` (só o import e o slot `betsTabs` do `Hud`) | Implementer (IMP-02): o `Hud` só é montado no `App.tsx`; sem ligar as abas ali o E2E-01 não passa | Nenhuma | decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) |
