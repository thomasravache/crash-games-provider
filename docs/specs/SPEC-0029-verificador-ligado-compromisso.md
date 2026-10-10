---
id: SPEC-0029
title: Verificador da Pipa ligado ao compromisso exibido ao vivo
tier: lite
type: fix
user_facing: true
status: approved
created: 2026-10-09
parent:
depends_on: []
consumes_contract: []
touches: ["src/frontend/packages/game-sdk/**", "src/frontend/packages/game-ui/**", "tests/frontend/pipa/**", "tests/e2e/pipa/**", "tests/backend/Host.Tests/**"]
adrs: [ADR-0006, ADR-0004, ADR-0020]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0029 — Verificador da Pipa ligado ao compromisso exibido ao vivo

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão (G4) da SPEC-0010: (1) o verificador do navegador confere SHA256(semente) só contra o `previousSeed` da própria resposta, então dados autoconsistentes do provedor sempre mostram "Verificado" — não liga ao compromisso exibido ao jogador na fase de apostas nem à semente revelada da rodada anterior, enfraquecendo o "conferir sem confiar no provedor" (ADR-0006); (2) a resposta de verificação não usa o tipo gerado; (3) axe não roda em todos os estados do HUD (diálogo, Reconectando…, sessão expirada); (4) a política CORS do Host não tem teste .NET.

## 2. Causa Raiz
`fairness-dialog.tsx`/`verifier.ts` comparam só com dados da mesma resposta; `rounds-api.ts` usa tipo escrito à mão.

## 3. Mudança Proposta
O store guarda o `commitment` recebido no `RoundBetting` e as sementes reveladas vistas ao vivo; o diálogo confere SHA256(semente revelada) contra o compromisso visto antes do voo e contra a semente revelada da rodada anterior quando disponível, mostrando "Verificado" só nesse caso e explicando quando a rodada não foi acompanhada ao vivo. Não muda: o algoritmo v1 e o contrato do servidor.

**Padrão seguido:** `src/frontend/packages/game-sdk/src/fairness/**` e `game-ui/src/fairness-dialog.tsx` (SPEC-0010)

**Rollback:** revert do commit

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0029:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — coberto pelos testes da SPEC-0010
- **UT-01** — Dado um compromisso visto ao vivo e uma resposta de verificação autoconsistente porém com semente diferente, quando o diálogo verifica, então NÃO mostra "Verificado" (mostra divergência com o compromisso).
- **E2E-01** — Dado o jogo aberto, quando uma rodada acompanhada ao vivo é verificada, então mostra "Verificado" ligado ao compromisso; axe sem violações no diálogo, em "Reconectando…" e em "Sessão expirada".

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01 com a tag `SPEC-0029:<ID>` e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — 0fd8c11 | 2026-10-09 |
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
