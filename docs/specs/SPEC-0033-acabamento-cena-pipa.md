---
id: SPEC-0033
title: Acabamento técnico da cena da Pipa
tier: lite
type: refactor
user_facing: false
status: proposed
created: 2026-10-10
parent:
depends_on: [SPEC-0011]
consumes_contract: []
touches: ["src/frontend/packages/scene-kit/**", "src/frontend/apps/game-pipa/src/scene/**", "tests/frontend/pipa/scene/**", "tests/e2e/pipa-visual/**", ".github/workflows/visual-baselines.yml"]
adrs: [ADR-0004, ADR-0019, ADR-0021]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0033 — Acabamento técnico da cena da Pipa

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores do review (G4) e do G5 da SPEC-0011: (1) `freeze`, `isFrozen`, `effectsLog` e o replay determinístico, só de teste, vão no chunk de produção (inacessíveis, mas pesam); (2) a constante de crescimento `0.00006` (SPEC-0008) está duplicada na cena e no teste; (3) em build de teste, `frameTimes` e o registro de efeitos crescem sem limite (inclusive no Vite de desenvolvimento do Aspire); (4) o workflow `visual-baselines` dispara também por `scene-kit/**` e instala WebKit e Firefox sem usar; (5) o IT-02 (contexto WebGL recriado em ≤ 1 s) falhou uma vez sob carga paralela no GL por software (passou 10/10 isolado e na CI).

## 2. Causa Raiz
`scene-kit/src/kite/pipa-animator.ts` mistura API de teste com a de produção; a constante não vem do `game-sdk`; os buffers do gancho de teste não têm teto; o IT-02 depende de compilar shaders num contexto novo sob carga.

## 3. Mudança Proposta
Separa a API de teste num módulo só de build de teste (tree-shaken em produção); importa a constante de crescimento de uma fonte única do `game-sdk`; limita os buffers de teste (anel de 600 quadros / 200 efeitos); alinha o gatilho do workflow e instala só o Chromium; deixa um renderizador reserva compilado para reduzir a recriação após perda de contexto. Não muda: o visual (as 6 imagens aprovadas continuam valendo), o contrato `GameAnimator`, os testes da SPEC-0011.

**Padrão seguido:** a própria cena da SPEC-0011 (`scene-kit/src/kite`, `scene-kit/src/pixi`) e o gancho de teste da SPEC-0010.

**Rollback:** revert dos commits.

**Outras ocorrências:** Nenhuma. O bloom (fora da SPEC-0011 por decisão de thomas) fica para a arte final antes do lançamento, junto da arte do ADR-0023.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0033:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — a suíte da SPEC-0011 (UT-01..08, CT-01, IT-01/02, E2E-01..03) e as 6 imagens aprovadas fixam o comportamento atual.
- **UT-01** — Dado o build de produção do game-pipa, quando o chunk da cena é inspecionado, então não contém `freeze`, `effectsLog` nem o replay de teste; e com 10.000 quadros no build de teste, `frameTimes()` e `effectsLog()` ficam limitados ao teto.
- **IT-01** — Dado o pipa-visual rodando com 4 workers em paralelo no Chromium com GL por software, quando o IT-02 da SPEC-0011 é repetido 10 vezes, então passa 10/10.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PENDING | | |
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

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
