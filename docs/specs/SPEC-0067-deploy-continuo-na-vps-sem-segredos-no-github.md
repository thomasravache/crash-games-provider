---
id: SPEC-0067
title: Deploy contínuo na VPS sem segredos no GitHub
tier: lite
type: feature
user_facing: false
status: proposed
created: 2026-10-15
parent: SPEC-0012
depends_on: [SPEC-0066]
consumes_contract: []
touches: ["tools/deploy/**", "infra/vps/**", "docs/runbooks/**", "tests/deploy/**"]
adrs: [ADR-0029, ADR-0013]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0067 — Deploy contínuo na VPS sem segredos no GitHub

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Depois da SPEC-0066 cada deploy é manual (script na VPS com a confirmação de thomas). thomas quer o deploy disparado a partir do GitHub, "de forma segura sem expor nada de senha ou segredo" (2026-10-15).

## 2. Causa Raiz
Um deploy empurrado pelo GitHub Actions exigiria uma chave SSH da VPS como secret num repositório público. A alternativa sem segredo é puxar: a VPS consulta a API pública do GitHub e só publica commits da `main` com a CI verde.

## 3. Mudança Proposta
Muda: um timer do systemd na VPS (`infra/vps/`) consulta a cada 5 min o último commit da `main` com o workflow de CI concluído com sucesso (API pública, sem token) e, se for novo, roda `tools/deploy/vps-deploy <sha>`; falha volta sozinha à versão anterior e registra o motivo. Uma tag ou variável (`deploy: hold`) pausa o deploy automático.

Não muda: nenhum segredo vai para o GitHub; o deploy e o rollback continuam os da SPEC-0066.

**Padrão seguido:** `tools/deploy/` da SPEC-0066

**Rollback:** desligar o timer (`systemctl disable`) e reverter o commit

**Outras ocorrências:** Nenhuma.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0067:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — código novo.
- **UT-01** — Dadas respostas gravadas da API do GitHub (CI verde, vermelha, em andamento, commit já publicado), quando o verificador decide, então só um commit novo com a CI verde dispara o deploy, e com `deploy: hold` nada dispara.
- **IT-01** — Dado o projeto compose de teste da SPEC-0066 e um commit novo com CI verde simulado, quando o timer roda, então a versão nova sobe; com o smoke falhando, a anterior volta.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [ ] O deploy automático pode rodar sem a sua confirmação a cada versão, já que produção hoje exige confirmação humana por release (ADR-0013)? (thomas)

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
