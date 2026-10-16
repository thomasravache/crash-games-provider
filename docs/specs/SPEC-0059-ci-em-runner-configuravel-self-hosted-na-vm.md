---
id: SPEC-0059
title: CI em runner configurável (self-hosted na VM)
tier: lite
type: fix
user_facing: "false"
status: deprecated
created: 2026-10-15
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: [.github/workflows/**, tests/ci/Ci.Tests/**, README.md]
adrs: []
external: []
size: S
approved_by: thomas (runner na VM escolhido no chat, 2026-10-15)
approved_at: 2026-10-15
---

# SPEC-0059 — CI em runner configurável (self-hosted na VM)

> Deprecated: arquivada por thomas em 2026-10-15 ("pode arquivar"). Com o repositório público a CI roda nos runners do GitHub de graça; um runner self-hosted na VM só volta a fazer sentido se o repositório ficar privado de novo. O trabalho parado está no branch `sdd/SPEC-0059-runner-configuravel` (red d1de362), para retomar se for preciso.

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Desde 2026-10-15 o GitHub Actions não inicia nenhum job do repositório. A mensagem é "recent account payments have failed or your spending limit needs to be increased": a cota gratuita de minutos do repositório privado acabou e o limite de gasto é zero. Sem CI, nenhuma PR fecha o G5 (PR #31, onda 1; SPEC-0001 IMP-01). O custo de infraestrutura deve ser zero até o lançamento (decisão do thomas).

## 2. Causa Raiz
Todos os jobs (`ci.yml`, `nightly.yml`, `images.yml`, `visual-baselines.yml`) declaram `runs-on: ubuntu-latest` fixo, então só rodam em runners hospedados e cobrados. Num repositório privado, o runner próprio (self-hosted) não consome minutos. Além disso, dois passos usam `playwright install --with-deps`, que chama `sudo apt-get` e não deve rodar num runner sem privilégios.

## 3. Mudança Proposta
- **Workflows:** todo job passa a usar em `runs-on` a expressão do GitHub Actions `vars.CI_RUNNER || 'ubuntu-latest'` (entre `${` `{ }}`). A variável de repositório `CI_RUNNER` escolhe o runner sem mudar código:
  - vazia → runner hospedado (comportamento atual);
  - `self-hosted` → o runner da VM.
- **Passos que pedem `sudo`:** o `--with-deps` e o `install-deps` do Playwright (que chamam `sudo apt-get`) só rodam em runner hospedado (`runner.environment == 'github-hosted'`). No runner próprio, os navegadores são instalados sem dependências de sistema, que a VM já tem.
- **Runner na VM** (operação, fora do repositório; decisões do thomas em 2026-10-15):
  - usuário Linux dedicado `gha-runner`, sem sudo, só no grupo `docker`. A CI não enxerga os tokens do gh, as chaves SSH nem os outros projetos do thomas;
  - instalado como serviço systemd: sobe com a VM e se reinicia se cair;
  - registrado só neste repositório, com os rótulos `self-hosted`, `linux`, `x64`.
- **Token de registro:** é credencial. O thomas o gera e o encaminha direto ao `config.sh`, sem que ele apareça no terminal nem no chat.
- **Documentação:** o README ganha uma seção curta "CI em runner próprio": como ligar e desligar pela variável, onde fica o serviço, e o aviso de que a CI disputa CPU com outros projetos da VM.
- **Não muda:** os passos, os gates da CI, os limites de desempenho da CI (ainda detectados por `GITHUB_ACTIONS=true`) e o fluxo de PR.

**Padrão seguido:** os workflows atuais e os testes de política de workflow em `tests/ci/Ci.Tests/Unit/WorkflowSecurityTests.cs` (actionlint fixado, ações por SHA, `permissions` no topo).

**Rollback:** apagar a variável `CI_RUNNER` (volta ao runner hospedado sem código) ou reverter o merge commit; para o runner: `svc.sh stop/uninstall` e remover o registro no GitHub.

**Outras ocorrências:** o mesmo bloqueio de cobrança afeta outros repositórios da conta (por exemplo, ai-dev-brain). Fica fora desta spec; cada repositório decide no próprio processo.

## 4. Plano de Testes (TDD)
- Caracterização: N/A — os testes de política de workflow existentes continuam valendo e passam a rodar sobre os workflows alterados.
- **UT-01** — Dados os quatro workflows, quando cada job é lido, então o `runs-on` de todos é exatamente a expressão `vars.CI_RUNNER || 'ubuntu-latest'`; um job com `ubuntu-latest` fixo reprova, com o arquivo e o job na mensagem.
- **UT-02** — Dados os workflows, quando os passos são lidos, então todo passo que usa `sudo`, `apt-get`, `--with-deps` ou `install-deps` tem a condição `runner.environment == 'github-hosted'`; um passo assim sem a condição reprova.
- Integração: a CI da própria PR desta spec roda no runner da VM, com `CI_RUNNER=self-hosted` e todos os jobs verdes. É a prova de ponta a ponta, registrada no G5 com o link do run.

## 5. Questões em Aberto
- [x] Como seguir sem a cota do GitHub Actions? — Runner próprio nesta VM, custo zero (thomas, 2026-10-15)
- [x] Com qual usuário o runner roda? — Usuário dedicado `gha-runner`, sem acesso aos tokens e chaves do thomas (thomas, 2026-10-15)
- [x] Como o runner fica rodando? — Serviço systemd (thomas, 2026-10-15)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

**Fase 1: Workflows com runner configurável**
- [ ] Red: UT-01 e UT-02 com a tag `SPEC-0059:<ID>`, falhando pelo motivo certo
- [ ] Green: `runs-on` pela variável nos quatro workflows; `--with-deps` e `install-deps` só no runner hospedado; README
- [ ] Validar: build + Ci.Tests + actionlint (G2/G3)

**Fase 2: Runner na VM (operação)**
- [ ] Usuário `gha-runner` (grupo docker, sem sudo) e runner baixado com checksum conferido
- [ ] Registro com o token gerado pelo thomas (sem exibir) e serviço systemd ativo
- [ ] Variável `CI_RUNNER=self-hosted` no repositório

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] CI da PR verde no runner da VM (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012, registrado no gate
- [ ] Relatório de Entrega e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 8a5f520 | 2026-10-15 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:00.66]     Dados os quatro workflows, quando cada job é lido, então o run) — d1de362 | 2026-10-15 |
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
| IMP-01 | 2026-10-15 | G1 | spec | UT-02 e §3 não cobrem 'playwright install-deps', que usa apt-get com sudo por baixo e roda quando o cache dos navegadores é encontrado (ci.yml e visual-baselines.yml) | test-writer seguiu o texto atual da spec (a5d4f15) e reportou o SPEC_DEFECT | Architect | Emenda v1 — UT-02 e §3 incluem install-deps | 2026-10-15 |

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
| 1 | 2026-10-15 | UT-02 e §3 incluem `install-deps` (`playwright install-deps` usa `sudo apt-get` por baixo) | Test-writer (red a5d4f15): o passo 'Install browser system dependencies' escapava da regra e falharia no runner sem sudo | Nenhuma | thomas (runner na VM escolhido no chat, 2026-10-15) |
