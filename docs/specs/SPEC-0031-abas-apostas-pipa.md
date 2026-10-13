---
id: SPEC-0031
title: Abas de apostas da Pipa
tier: lite
type: feature
user_facing: true
status: approved
created: 2026-10-09
parent: SPEC-0019
depends_on: [SPEC-0025, SPEC-0026]
consumes_contract: [SPEC-0025@3]
touches: ["src/frontend/packages/game-sdk/**", "src/frontend/packages/game-ui/**", "src/frontend/apps/game-pipa/src/hud/**", "tests/frontend/pipa/bets-tabs/**", "tests/e2e/pipa/bets-tabs/**"]
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
- **CT-01** — Dadas as mensagens reais de `LiveBetsSnapshot`, dos lotes e de `GET /api/v1/me/bets`, quando validadas contra os tipos gerados, então conferem (SPEC-0025@1).
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
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
