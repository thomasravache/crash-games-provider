---
id: SPEC-0030
title: Medição de desempenho do cliente Pipa (Lighthouse mobile e 60 fps)
tier: lite
type: fix
user_facing: false
status: proposed
created: 2026-10-09
parent:
depends_on: []
consumes_contract: []
touches: ["tests/e2e/pipa/performance/**", "src/frontend/playwright.config.ts", "src/frontend/package.json", ".github/workflows/ci.yml", "tools/ci/run-e2e.sh"]
adrs: [ADR-0019]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0030 — Medição de desempenho do cliente Pipa (Lighthouse mobile e 60 fps)

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
A SPEC-0010 (§5 e §7.6) fixou para o cliente da Pipa: interativo ≤ 3 s no perfil mobile e 60 fps em Android médio. O orçamento de bundle (≤ 1,5 MB gzip) é verificado no build, mas as outras duas metas foram entregues **sem medição**: não há Lighthouse CI nem amostragem de quadros. Uma regressão de desempenho (ex.: cena pesada da SPEC-0011, som da SPEC-0018) passaria despercebida.

## 2. Causa Raiz
O plano de testes da SPEC-0010 citou "Lighthouse CI", que não está previsto em nenhum ADR (exigiria pacote e ADR novos); a implementação ficou só com o orçamento de bundle em `src/frontend/apps/game-pipa/vite.config.ts` e o Review (G4) não bloqueou a ausência. Achado no fechamento (G7) da SPEC-0010.

## 3. Mudança Proposta
Duas medições automáticas, só no Chromium (usam o Chrome DevTools Protocol do Playwright, já no projeto — sem pacote novo), num projeto Playwright próprio `pipa-performance` rodado em passo separado do job `e2e` (mesma convenção dos testes de tempo do backend, `Category=Performance`):

- **Tempo até interativo:** perfil mobile emulado (viewport do iPhone 15, CPU 4× mais lenta e rede "Fast 3G" via CDP), do `goto` do lançamento até o HUD exibir o estado da sala com o botão principal habilitado. Meta ≤ 3 s.
- **Quadros por segundo:** com CPU 4× mais lenta, amostra `requestAnimationFrame` durante 5 s de voo. Meta: mediana ≥ 55 fps e p5 ≥ 30 fps (aproximação de 60 fps em Android médio, aceita como proxy).

**Desvio declarado:** troca "Lighthouse CI" por medição com Playwright + CDP para não adicionar dependência; a métrica "interactive" do Lighthouse é substituída pelo critério observável acima. Não muda código de produção, a cena, o HUD nem o orçamento de bundle.

**Padrão seguido:** `tests/e2e/pipa/round.spec.ts` e `tests/e2e/pipa/support/pipa.ts` (lançamento demo e esperas do HUD); `tools/ci/run-e2e.sh` (backend real via Aspire).

**Rollback:** revert dos commits (só testes e configuração de CI).

**Outras ocorrências:** As metas do backoffice e do site demo não têm medição; ficam para as specs dessas telas quando existirem.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0030:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — não altera comportamento, só mede.
- Unitário: N/A — a medição só tem sentido no navegador real.
- **E2E-01** — Dado o perfil mobile emulado (CPU 4×, Fast 3G) e o backend real, quando o jogador abre o lançamento demo, então o HUD fica interativo em ≤ 3 s (falha hoje por não existir a medição; com um atraso artificial de 4 s injetado na rota do bundle, o teste reprova).
- **E2E-02** — Dada uma rodada em voo com CPU 4× mais lenta, quando se amostram os quadros por 5 s, então a mediana é ≥ 55 fps e o p5 ≥ 30 fps; o resultado é anexado ao relatório do Playwright.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma — a troca de Lighthouse CI por Playwright + CDP está declarada em §3 para o H1.

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
