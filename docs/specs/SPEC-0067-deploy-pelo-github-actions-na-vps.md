---
id: SPEC-0067
title: Deploy pelo GitHub Actions na VPS com aprovação e chave restrita
tier: lite
type: feature
user_facing: false
status: approved
created: 2026-10-15
parent: SPEC-0012
depends_on: [SPEC-0066]
consumes_contract: []
touches: [".github/workflows/deploy-vps.yml", "tools/deploy/**", "infra/vps/**", "docs/runbooks/**", "tests/ci/Ci.Tests/**", "tests/deploy/**"]
adrs: [ADR-0029, ADR-0013]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-15
---

# SPEC-0067 — Deploy pelo GitHub Actions na VPS com aprovação e chave restrita

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Depois da SPEC-0066 cada deploy é manual (script na VPS com a confirmação de thomas). thomas quer o deploy disparado pelo GitHub Actions, "de forma segura sem expor nada de senha ou segredo", na abordagem mais usada no mercado para VPS (decisão dele, 2026-10-15: opção "Action faz deploy por SSH").

## 2. Causa Raiz
Não existe workflow de deploy; o GitHub não tem como chegar à VPS. O padrão de mercado é a Action entrar por SSH com uma chave em GitHub Secrets; o risco dessa chave é contido restringindo o que ela pode fazer e exigindo aprovação humana no GitHub antes de usá-la.

## 3. Mudança Proposta
Muda:
- Workflow `.github/workflows/deploy-vps.yml`: dispara só quando a CI conclui com sucesso num push na `main` (`workflow_run`) ou manualmente (`workflow_dispatch` com um SHA da `main`); nunca em `pull_request`. Usa o *Environment* `production`, com thomas como aprovador obrigatório: o GitHub espera o "Approve" antes de cada deploy (é a confirmação por release do ADR-0013). Permissões mínimas (`contents: read`), Actions fixadas por SHA de commit, `concurrency` para um deploy por vez.
- Único segredo no GitHub: `VPS_DEPLOY_SSH_KEY` (chave Ed25519 dedicada) e `VPS_SSH_KNOWN_HOSTS`, no Environment `production`. Nenhuma senha da aplicação vai para o GitHub (continuam no `.env` da VPS).
- Na VPS (`infra/vps/`): usuário `deploy` sem sudo nem shell interativo; a chave entra no `authorized_keys` com `command="/opt/apogeu/deploy-gate",no-pty,no-port-forwarding,no-agent-forwarding,no-X11-forwarding`; o `deploy-gate` aceita só `deploy <sha de 40 hex da main>` e `rollback`, e chama `tools/deploy/vps-deploy`/`vps-rollback`.
- O resultado do smoke volta para o log da Action; falha de saúde volta sozinha à versão anterior (SPEC-0066).

Não muda: o deploy, o rollback e o `.env` da SPEC-0066; a possibilidade de rodar o deploy direto na VPS.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0067:<ID>`. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — código novo.
- **UT-01** — Dado o `deploy-vps.yml`, quando lido, então só dispara por `workflow_run` da CI com sucesso na `main` ou por `workflow_dispatch`, nunca por `pull_request`; usa o Environment `production`; tem `permissions: contents: read`; toda Action está fixada por SHA de 40 caracteres; os segredos só aparecem no passo de SSH; e há `concurrency` de deploy.
- **UT-02** — Dado o `deploy-gate`, quando recebe `deploy <sha válido>`, `rollback`, um SHA inválido, comandos extras (`;`, `&&`, espaço, outro verbo) ou nenhum comando, então só os dois primeiros chamam os scripts e os demais são recusados com código de saída ≠ 0, sem executar nada.
- **IT-01** — Dado um servidor SSH de teste em contêiner com o usuário `deploy` e a chave restrita, quando o cliente pede `deploy <sha>`, então o `deploy-gate` roda o deploy de teste; quando pede um shell ou outro comando, então é recusado; port forwarding também é recusado.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] O deploy automático pode rodar sem a sua confirmação a cada versão? — Não: cada deploy espera o "Approve" de thomas no Environment `production` do GitHub (opção 1 escolhida por thomas, 2026-10-15)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-15). Cada fase começa pelos testes. -->

**Fase 1: Workflow e chave restrita**
- [ ] Red: escrever UT-01, UT-02 e IT-01 com a tag `SPEC-0067:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: `deploy-vps.yml`, `deploy-gate`, usuário `deploy` e `authorized_keys` restrito (`infra/vps/`)
- [ ] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase 2: Ativação (com a autorização de thomas: credencial)**
- [ ] Chave Ed25519 dedicada, secrets e Environment `production` com thomas como aprovador

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Primeiro deploy pela Action aprovado por thomas (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 0fd4773 (árvore suja) | 2026-10-15 |
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
