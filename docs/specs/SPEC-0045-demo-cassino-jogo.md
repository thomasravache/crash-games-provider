---
id: SPEC-0045
title: "Cassino demo: página do jogo com lançamento assinado"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0044]
consumes_contract: [SPEC-0009@1]
contract_version: 1
touches: ["src/demo/DemoCasino/**", "tests/demo/DemoCasino.Tests/**", "src/frontend/apps/demo-site/**", "tests/e2e/demo-site/**"]
adrs: [ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0045 — Cassino demo: página do jogo com lançamento assinado

## 1. Visão Geral
Página do jogo no cassino demo: o BFF chama o lançamento da Apogeu (`POST /api/v1/operator/launch`, assinado) com o jogador e a moeda da sessão e devolve a URL de uso único; a página abre o jogo num iframe com "voltar ao lobby", tela cheia e a coluna "Sobre o jogo".

## 2. Motivação & Escopo
**Motivação:** fecha o fluxo real de integração ponta a ponta (decisão de thomas, 2026-10-10); substitui o `/dev/launch-demo` como forma principal de testar.

**Objetivos (dentro do escopo):**
- `POST /casino/play/{gameCode}` no BFF → URL de lançamento de uso único.
- Página com iframe (sandbox mínimo necessário), tela cheia, "voltar ao lobby" e coluna "Sobre o jogo" (RTP, ganho máximo, provably fair, provedor).
- Expiração: ao voltar ao lobby e abrir de novo, um lançamento novo é feito.

**Não-objetivos (fora do escopo):**
- Saldo do cassino no topo (na fase 1 o saldo é a carteira demo da Apogeu, mostrada dentro do jogo; na fase 2 o cassino mostra o próprio saldo — SPEC-0047).

## 3. Dependências
- **Implementações necessárias:** SPEC-0044.
- **Contratos consumidos:** SPEC-0009@1 (lançamento).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** o `demo-site` é esqueleto (SPEC-0002/0005) e o AppHost já o sobe. O cassino é um cliente externo (ADR-0027): fala só pela API pública de operador (SPEC-0009/0020). Referência de cliente HMAC: a assinatura de referência dos testes da SPEC-0009 (`ReferenceSignature`).

**Decisão:** o BFF chama o lançamento com o corpo do SPEC-0009@1: `playerId`, `nickname` e `currency` da sessão do cassino, `gameCode` da rota, `mode: "demo"` e `language: "pt-BR"` (sem `returnUrl`, que o contrato não tem; a volta ao lobby é o botão da moldura); o front só recebe a URL. A coluna "Sobre o jogo" usa dados fixos por jogo no demo-site (Pipa: RTP 97%, ganho máximo 10.000x, "Comprovadamente justo", provedor Apogeu Games — o padrão do catálogo da SPEC-0020, que o catálogo público não expõe); jogo sem dados fixos mostra só o provedor (Emenda v1).

**Justificativa:** é exatamente o que um cassino real faz.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** redirecionar o navegador para o `/dev/launch-demo` (atalho de teste, não integração).

**ADRs:** ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** do clique ao jogo carregado ≤ 3 s no perfil mobile.
- **Segurança:** lançamento assinado no servidor; iframe com sandbox mínimo; URL de uso único — IT-02.
- **Privacidade e dados pessoais:** só o id fictício do jogador.
- **Disponibilidade e resiliência:** lançamento recusado → mensagem clara e volta ao lobby — IT-03.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** BFF.

```text
POST /casino/play/{gameCode} → 200 { launchUrl } | 401 sem sessão | 404 GAME_NOT_AVAILABLE (repasse) | 502 PROVIDER_UNAVAILABLE
Página /jogo/{gameCode}: iframe(src=launchUrl, sandbox="allow-scripts allow-same-origin allow-popups"), botão tela cheia, "← Voltar ao lobby"
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu — Lobby demo (cassino neutro)", tela "Cassino Demo — Pipa (Refinado)" (moldura; o conteúdo do iframe é o jogo real).

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Lançar | jogador logado abre a Pipa | URL de uso único; jogo carrega no iframe | IT-01, E2E-01 |
| Uso único | mesma URL aberta duas vezes | segunda vez: "Sessão expirada" no jogo | IT-02 |
| Jogo indisponível | Pipa desabilitada | 404 e volta ao lobby com aviso | IT-03 |
| Sem sessão | play sem cookie | 401 | IT-01 |
| Apogeu indisponível | conexão recusada, 5xx ou tempo esgotado no lançamento | 502 PROVIDER_UNAVAILABLE; a página mostra o aviso e volta ao lobby | IT-04 |
| Tela | voltar ao lobby e reabrir | novo lançamento | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dada a sessão do cassino, quando o corpo do lançamento é montado, então traz `playerId`, `nickname` e `currency` da sessão, `gameCode` da rota, `mode: "demo"` e `language: "pt-BR"`, e nenhum outro campo (Emenda v1).

### 7.3 Testes de Integração
- **IT-01** — Dados DemoCasino e Apogeu reais, quando o jogador logado lança a Pipa, então recebe uma `launchUrl` válida que abre a sessão do jogo; sem cookie, 401.
- **IT-02** — Dada uma `launchUrl` já usada, quando aberta de novo, então a Apogeu recusa (sessão expirada).
- **IT-03** — Dada a Pipa desabilitada para o operador demo, quando o jogador tenta jogar, então 404 GAME_NOT_AVAILABLE.
- **IT-04** — Dada a Apogeu indisponível (conexão recusada, 500, 503 ou 504), quando o jogador tenta jogar, então 502 PROVIDER_UNAVAILABLE (Emenda v1).

### 7.4 Testes de Contrato
- **CT-01** — Dada a requisição real de lançamento do DemoCasino (corpo capturado), quando validada de forma estrita contra o esquema `LaunchRequest` do OpenAPI gerado da Apogeu (sem propriedades extras), então confere (Emenda v1).

### 7.5 Testes E2E
- **E2E-01** — Dado o ambiente Aspire, quando o jogador entra no cassino demo, abre a Pipa no lobby, vê uma rodada no iframe, volta ao lobby e reabre, então o jogo carrega nas duas vezes (lançamentos distintos); axe sem violações na moldura.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** operador demo e catálogo semeados.

**Ambiente de execução:** DemoCasino + Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A.
- **Compatibilidade:** nada muda na Apogeu.
- **Observabilidade:** contagem de lançamentos e falhas no DemoCasino.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, IT-01, IT-02, IT-03, IT-04, CT-01 com a tag `SPEC-0045:<ID>` e confirmar que falham pelo motivo certo
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

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 92551a4 | 2026-10-10 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:06.48]     Dada a requisição real de lançamento do DemoCasino, quando val) — ad10f10 | 2026-10-15 |
| G2 Green | FAIL | build exit 0 (✓ built in 225ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 1 ([xUnit.net 00:03:56.32]     Dado um crédito pendente e o processo reiniciado, quando a var) — 7c9409e | 2026-10-15 |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-15 | G1 | spec | returnUrl do lançamento não existe no contrato consumido SPEC-0009@1 (UT-01 e CT-01 sem regra); 502 PROVIDER_UNAVAILABLE sem linha no Mapa nem teste; coluna Sobre o jogo sem fonte de RTP e ganho máximo | test-writer escreveu IT-01..03 e E2E-01 (red em b6b9b87) e parou antes de UT-01/CT-01 | architect | Emenda v1: corpo do SPEC-0009@1 sem returnUrl, CT-01 estrito, IT-04 (502) e Sobre o jogo com dados fixos | 2026-10-15 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0045`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-15 | Lançamento sem `returnUrl` (corpo exato do SPEC-0009@1, com `nickname` da sessão, `mode` demo e `language` pt-BR); UT-01 e CT-01 (estrito contra `LaunchRequest`) reescritos; IT-04 e linha do Mapa para 502 PROVIDER_UNAVAILABLE; "Sobre o jogo" com dados fixos por jogo no demo-site (Pipa: RTP 97%, ganho máximo 10.000x) | Test-writer (SPEC_DEFECT, IMP-01): `returnUrl` fora do contrato consumido, 502 sem teste e RTP/ganho máximo sem fonte | Nenhuma | decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) |
