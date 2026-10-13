---
id: SPEC-0049
title: Multiplicador da Pipa no topo da cena, sem cobrir a pipa
tier: lite
type: fix
user_facing: true
status: in-progress
created: 2026-10-12
parent: SPEC-0019
depends_on: []
consumes_contract: []
touches: ["src/frontend/packages/game-ui/src/hud-base.css", "src/frontend/packages/game-ui/src/hud.tsx", "tests/e2e/pipa/layout/**"]
adrs: [ADR-0009, ADR-0025]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-12
---

# SPEC-0049 — Multiplicador da Pipa no topo da cena, sem cobrir a pipa

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Atual: durante o voo, o placar (multiplicador "3,42x" + "EM VOO") fica numa caixa escura no centro da cena e cobre a pipa, que a câmera mantém perto do centro (relato do thomas jogando, 2026-10-12, com captura de tela). Esperado: o placar fica no terço de cima da área da cena, sem caixa escura que tape o céu, e a pipa fica visível em todas as fases (apostas, voo, crash). Impacto: o jogador não vê a animação principal do jogo.

## 2. Causa Raiz
O HUD da SPEC-0010 centraliza o bloco do multiplicador sobre a cena (`hud-base.css`), e a cena da SPEC-0011 enquadra a pipa no centro do canvas; as duas specs foram feitas separadas e nenhum teste confere a sobreposição do HUD com a pipa.

## 3. Mudança Proposta
Muda: posição, tamanho e fundo do bloco do multiplicador no HUD, nas três fases: no terço superior da área da cena, centralizado na horizontal, compacto (no máximo 20% da altura e 40% da largura da cena) e sem caixa opaca — só o texto com sombra suave para leitura sobre o céu, de modo que nuvens, sol, rastro e linha continuem visíveis ao redor. Não muda: a cena, a câmera, a pipa, os quadros-chave aprovados (que fotografam só o canvas), os textos e test ids do HUD, o tremor do crash e o painel de aposta.

**Padrão seguido:** `src/frontend/packages/game-ui/src/hud-base.css` e `hud.tsx` (HUD da SPEC-0010); identidade visual do ADR-0025 (valores nunca em dourado).

**Rollback:** revert do commit.

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0049:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — os textos e test ids do HUD já são cobertos pelos E2E da SPEC-0010 e continuam valendo.
Unitário: N/A — posição na tela só é verificável com layout real no navegador (E2E).
- **E2E-01** — Dado o jogo aberto pelo lançamento demo, nas fases de apostas, voo e crash, quando a posição do bloco do multiplicador (`hud-multiplier` e o texto da fase) é medida em relação à área da cena, então a borda de baixo do bloco fica acima de 35% da altura da cena, o centro da cena (onde a pipa é enquadrada) não fica sob o bloco, o bloco ocupa no máximo 20% da altura e 40% da largura da cena e não tem fundo opaco (cor de fundo transparente ou com opacidade ≤ 0,3); em Chromium, WebKit e Firefox.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Como resolver o multiplicador cobrindo a pipa? — Placar no terço de cima da cena, sem caixa escura; a câmera e a pipa não mudam; compacto, sem prejudicar o cenário (thomas, 2026-10-12)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->
**Fase 1: Placar no topo**
- [ ] Red: escrever E2E-01 com a tag `SPEC-0049:E2E-01` e confirmar que falha pelo motivo certo (placar no centro, com caixa opaca)
- [ ] Green: mover e compactar o placar no HUD (`hud-base.css`, `hud.tsx`)
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 7c323ee | 2026-10-12 |
| G1 Red | PASS | MANUAL: verify G1: PASS (red antes do green, tag SPEC-0049:E2E-01 no código); teste de regressão é E2E: tools/ci/run-e2e.sh --project=chromium pipa/layout/ falha em d815910 — borda de baixo do placar a 59–60% da altura da cena (limite 35%), centro da cena sob o placar e fundo com alfa 0,85 (limite 0,3); a suíte do test do sdd-config não roda E2E | 2026-10-12 |
| G2 Green | FAIL | build exit 0 (✓ built in 508ms); test exit 0 (at least ~474ms faster with isolate: false — reuses workers across files instead of one pe); lint exit 0 (All matched files use Prettier code style!); coverage exit 1 ([xUnit.net 00:03:49.10]     Dada uma aposta X cujo débito fica retido até depois da varred) — e379289 | 2026-10-12 |
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

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
