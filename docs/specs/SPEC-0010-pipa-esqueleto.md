---
id: SPEC-0010
title: "Pipa demo: esqueleto andante do cliente"
tier: full
type: foundation
user_facing: true
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0005, SPEC-0006]
consumes_contract: [SPEC-0007@2, SPEC-0008@3, SPEC-0009@1]
contract_version: 1
touches: ["src/frontend/apps/game-pipa/**", "src/frontend/packages/game-sdk/**", "src/frontend/packages/game-ui/**", "src/frontend/packages/scene-kit/**", "tests/e2e/pipa/**", "tests/frontend/pipa/**", "tests/frontend/integration/pipa/**", "tests/frontend/arch/pipa-*", "src/frontend/package.json", "src/frontend/vitest.workspace.ts", "src/backend/AppHost/Program.cs", "src/backend/Host/Program.cs", ".github/workflows/ci.yml", "tools/ci/run-e2e.sh"]
adrs: [ADR-0004, ADR-0006, ADR-0008, ADR-0019, ADR-0020]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0010 — Pipa demo: esqueleto andante do cliente

## 1. Visão Geral
Entrega o cliente do jogo Pipa em modo demo de ponta a ponta: o jogador entra pelo lançamento, vê as rodadas ao vivo (aguardando → em voo → pipa cortada) vindas do servidor, com a cena em PixiJS, o HUD em React (a partir da tela do Stitch) e a verificação provably fair calculada no próprio navegador. O painel de aposta aparece desabilitado ("Apostas em breve"): apostas são o épico seguinte.

## 2. Motivação & Escopo
**Motivação:** é o *walking skeleton*: prova a arquitetura inteira (rede → estado → cena/HUD) e a jornada verificacao-fairness antes de colocar dinheiro (mesmo demo) no fluxo.

**Objetivos (dentro do escopo):**
- `game-sdk`: troca do launchToken por sessão, conexão SignalR com reconexão, desvio de relógio (`GetServerTime`), store do estado da rodada espelhando o servidor, interpolação `m(t)`, verificador provably fair em TS (aritmética inteira, `BigInt`).
- `game-ui`: HUD a partir do projeto Stitch "Pipa — Crash Game" (histórico, multiplicador, estados *PREPARE SUA PIPA / EM VOO / PIPA CORTADA!*, selo DEMO, saldo demo fixo, painel de aposta desabilitado, janela "Comprovadamente Justo"), tokens HUD Base + tema Pipa, anúncios `aria-live` só nas mudanças de estado.
- `scene-kit`: app PixiJS, interface `GameAnimator` e implementação em código (pipa subindo, linha, céu do dia à noite conforme o multiplicador, linha cortada e pipa rodopiando).
- Gancho de teste `window.__APOGEU_TEST__` só em builds de teste.
- Tela de erro para sessão inválida ou expirada.

**Não-objetivos (fora do escopo):**
- Apostar, cash-out e carteira (épico "Apostas e cash-out em demo").
- Arte final (Scenario) e Spine; chat; apostas ao vivo de outros jogadores; i18n além de pt-BR.

## 3. Dependências
- **Implementações necessárias:** SPEC-0005 (apps servidos pelo Aspire) e SPEC-0006 (pacote `contracts`).
- **Contratos consumidos:** SPEC-0007@2 (algoritmo e vetores para o verificador TS); SPEC-0008@3 (hub e REST de rodadas); SPEC-0009@1 (lançamento e sessão).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0004 (rede → estado → apresentação; cena só lê o estado), ADR-0008 (contratos gerados), ADR-0019 (interpolação e metas de carregamento).

**Decisão:** store sem framework (assinável) no `game-sdk`, consumido pelo React via `useSyncExternalStore` e pela cena via assinatura direta; a cena chama só `GameAnimator`; o verificador TS reproduz o algoritmo v1 com `BigInt` e é testado com os mesmos vetores do C#.

**Justificativa:** mantém a cena desacoplada da rede (troca de animação barata) e garante paridade exata entre servidor e verificador.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** Redux/Zustand (dependência desnecessária para um store pequeno); verificação só no servidor (o jogador precisa conferir sem confiar no provedor).

**ADRs:** ADR-0004, ADR-0006, ADR-0008, ADR-0019, ADR-0020.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** bundle inicial ≤ 1,5 MB gzip; interativo ≤ 3 s no perfil mobile do Lighthouse; 60 fps em Android médio — 7.6.
- **Segurança:** launchToken removido da URL após a troca (`history.replaceState`); sessionToken só em memória — E2E-01.
- **Privacidade e dados pessoais:** só o apelido é exibido; nada pessoal em logs do navegador — revisão no G4.
- **Disponibilidade e resiliência:** reconexão automática com ressincronização do estado — UT-03, E2E-04.
- **Acessibilidade (UI):** WCAG 2.2 AA no HUD; axe sem violações; estados anunciados por `aria-live` — E2E-02 e 7.6.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** tela do jogo `/?lt={launchToken}` + store público do `game-sdk`.

```text
Fluxo de tela:
  /?lt=…  → carregando → (sessão ok) jogo  |  (token inválido/expirado/usado) tela "Sessão expirada. Volte ao cassino para jogar."
Estados do HUD:  Betting → "PREPARE SUA PIPA" + contagem | Flying → "EM VOO" + multiplicador | Crashed → "PIPA CORTADA!" + crash final
                 Annulled → "Rodada anulada" | Reconnecting → selo "Reconectando…"
Store (game-sdk):  RoundState { phase, roundId, roundNumber, multiplier, crashPoint?, history[], serverOffsetMs, connection }
Verificador:      verifyRound(data) → "valid" | "chain-mismatch" | "crash-mismatch"
GameAnimator:     prepare(), takeoff(startedAt), setMultiplier(m), crash(point), reset()
Gancho de teste:  window.__APOGEU_TEST__ = { state(): RoundState }  (somente build de teste)
```

**Design:** Stitch — projeto "Pipa — Crash Game" (16371353209282165204), tela "Pipa Crash - Jogo da Pipa" (d2193929d1454eefa1794cdc14a51c79) e design system "Pipa Crash".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Entrada válida | `/dev/launch-demo` → `/?lt=…` | jogo carrega, selo DEMO, histórico visível, `lt` some da URL | E2E-01, CT-03 |
| Rodada completa | rodada ao vivo | PREPARE SUA PIPA → EM VOO (multiplicador subindo) → PIPA CORTADA! com o crash do servidor; histórico atualizado | E2E-02, CT-02 |
| Verificação | rodada encerrada → "Comprovadamente Justo" | verificador no navegador mostra "Verificado" e o mesmo crash | E2E-03, CT-01 |
| Sessão inválida | `lt` usado ou expirado | tela "Sessão expirada…" | E2E-04 |
| Queda de conexão | rede cai e volta | selo "Reconectando…" e depois estado ressincronizado | UT-03, E2E-04 |
| Interpolação | `startedAt`, k, desvio de relógio | multiplicador exibido = m(t) do servidor (± 1 tique) | UT-01 |
| Mensagens fora de ordem | Sync após Crashed; duplicadas | ignoradas sem regressão de estado | UT-02 |
| Verificador TS | vetores e adulterações | valid / chain-mismatch / crash-mismatch | UT-04, CT-01 |
| Céu por multiplicador | 1x, 10x, 100x | cores de dia, pôr do sol e noite | UT-05 |
| Animações pelo adaptador | sequência de estados | `GameAnimator` chamado na ordem certa, cena sem import de rede | UT-06 |
| Painel de aposta | qualquer estado | botão desabilitado com "Apostas em breve" e nome acessível | E2E-02 |
| SDK contra servidor real | backend do Aspire | sessão, entrada na sala e sequência de mensagens | IT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dados `startedAt`, k = 0,00006 e desvio de relógio de +120 ms, quando o multiplicador é calculado em t local, então é igual a m(t − desvio) do servidor com 2 casas.
- **UT-02** — Dada a store em Crashed, quando chega um `RoundSync` atrasado ou um `RoundCrashed` duplicado, então o estado não muda.
- **UT-03** — Dada uma reconexão, quando o `RoundSnapshot` chega, então a store é substituída pelo snapshot e `connection` volta a `connected`.
- **UT-04** — Dados os vetores `fairness-v1.json`, quando `verifyRound` roda, então todos são "valid"; com semente alterada → "chain-mismatch"; com crash alterado → "crash-mismatch".
- **UT-05** — Dados multiplicadores 1, 10 e 100, quando o céu é calculado, então retorna as paletas de dia, pôr do sol e noite do tema Pipa.
- **UT-06** — Dado um `GameAnimator` falso, quando a store passa por Betting → Flying → Crashed, então são chamados `prepare`, `takeoff`, `setMultiplier`(n vezes) e `crash`, nessa ordem.

### 7.3 Testes de Integração
- **IT-01** — Dado o backend real subido pelo Aspire, quando o `game-sdk` (Node, sem navegador) troca um launchToken, conecta e acompanha uma rodada, então recebe snapshot, Betting, Flying e Crashed, e a store termina com o `crashPoint` do servidor.

### 7.4 Testes de Contrato
- **CT-01** — Dado o contrato SPEC-0007@2 (vetores), quando o verificador TS recalcula todos os vetores, então os resultados são idênticos aos do C#.
- **CT-02** — Dados os tipos gerados do hub (SPEC-0008@3), quando as mensagens reais de IT-01 são validadas em tempo de execução, então conferem.
- **CT-03** — Dados os tipos gerados de lançamento e sessão (SPEC-0009@1), quando o fluxo de IT-01 roda, então requisições e respostas conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o ambiente Aspire, quando o jogador abre `/dev/launch-demo` (Chromium e WebKit mobile), então o jogo carrega com selo DEMO e histórico, e a URL não contém mais `lt`.
- **E2E-02** — Dado o jogo aberto, quando uma rodada completa acontece, então o HUD mostra PREPARE SUA PIPA, EM VOO com multiplicador crescente e PIPA CORTADA! com o crash igual ao do servidor (via gancho de teste); o histórico ganha a rodada; o botão de aposta está desabilitado com "Apostas em breve"; axe sem violações.
- **E2E-03** — Dada uma rodada encerrada, quando o jogador abre "Comprovadamente Justo" e escolhe essa rodada, então vê "Verificado" e o mesmo ponto de crash [jornada: verificacao-fairness].
- **E2E-04** — Dado um `lt` já usado, quando aberto, então aparece "Sessão expirada. Volte ao cassino para jogar."; dado o jogo aberto e a rede derrubada por 5 s, então aparece "Reconectando…" e o estado volta a acompanhar a rodada.

### 7.6 Outros
- Desempenho: orçamento de bundle verificado no build (≤ 1,5 MB gzip); Lighthouse CI perfil mobile com interativo ≤ 3 s.
- Acessibilidade: @axe-core/playwright em todos os estados do HUD; revisão manual com leitor de tela no G4.

**Dublês e dados de teste:** `GameAnimator` falso nos unitários; backend real no IT e E2E; vetores compartilhados.

**Ambiente de execução:** Vitest local/CI; IT e E2E contra o ambiente Aspire no CI.

## 8. Plano de Rollout
- **Estratégia:** merge direto (somente local/demo).
- **Dados/schema:** N/A
- **Compatibilidade:** consome contratos v1 das SPEC-0007/0008/0009.
- **Observabilidade:** sem log no console (regra `no-console` do ADR-0012); erros visíveis ao jogador pelas telas de sessão expirada, falha de conexão e "Reconectando…"; telemetria de navegador fica para a spec de hospedagem.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0004, ADR-0006, ADR-0008, ADR-0019, ADR-0020 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06 com a tag `SPEC-0010:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, CT-01, CT-02, CT-03 com a tag `SPEC-0010:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 4: Jornada E2E**
- [ ] Red: E2E-01, E2E-02, E2E-03, E2E-04 falhando pelo motivo certo
- [ ] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a spec SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-07 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive" && pnpm -C src/frontend test` exit 1 (red: ⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[13/45]⎯) — 90d03d6 | 2026-10-09 |
| G2 Green | PASS | build exit 0 (✓ built in 161ms); test exit 0 (Duration  1.36s (tests 82%, transform 12%, import 4%, worker 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-09T14:21:27: Report generation took 0,3 seconds) — 7af2d33 | 2026-10-09 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  789ms (tests 97%, transform 2%, import 1%)) — 7af2d33 | 2026-10-09 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer (agente independente): APPROVED em 6dce3a1 e no delta até 7af2d33 (E2E com backend real, título estável) — 0 blocker/major; minors como pendências — 7af2d33 | 2026-10-09 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 140ms); test exit 0 (Duration  1.35s (tests 81%, transform 13%, import 4%, worker 1%)); test_integration exit 0 (Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 3 m 17 s ); test_e2e exit 0 ([run-e2e] stopping the AppHost); arch_test exit 0 (Duration  814ms (tests 97%, transform 2%, import 1%)); security_scan exit 0 (Ran 8 rules on 250 files: 0 findings.) — 07e8617 | 2026-10-09 |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0010`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v1 (inalterado) | 2026-10-09 | Emenda de escopo: `tools/ci/run-e2e.sh` (sobe o AppHost, espera a saúde da API, exporta `APOGEU_API_URL`, roda `pnpm test:e2e` e derruba tudo) e o job `e2e` do `.github/workflows/ci.yml` passa a usá-lo | Os E2E da Pipa dependem do backend real; sem isso a CI falharia após a integração | Nenhuma (contrato inalterado) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09) |
| v1 (inalterado) | 2026-10-09 | Pin atualizado para SPEC-0008@3: o REST de rodadas (verificação) exige a sessão do jogador; o cliente envia o Bearer e o auxiliar dos E2E também (6dce3a1) | Emenda v3 da SPEC-0008 (lacuna de contrato encontrada na integração) | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09) |
| v1 (inalterado) | 2026-10-09 | Emenda de escopo: `touches` inclui os testes do front (`tests/frontend/pipa/**`, `tests/frontend/integration/pipa/**`, `tests/frontend/arch/pipa-*`), `src/frontend/package.json`, `src/frontend/vitest.workspace.ts`, `src/backend/AppHost/Program.cs` (URL da API para o jogo) e `src/backend/Host/Program.cs` (CORS para a origem do jogo) | Os testes vivem onde o Vitest e o TestKit esperam; o esqueleto andante só funciona no Aspire com a URL da API injetada no jogo e CORS liberado | Nenhuma (contrato inalterado) | Architect — correção de escopo sem mudança de contrato, coberta pela autorização do usuário para seguir pelo recomendado (2026-10-09) |
| v1 (inalterado) | 2026-10-09 | Pins revistos: SPEC-0007@2 (só acrescenta `GetCrashPointAsync`; vetores e algoritmo inalterados) e SPEC-0008@2 (`RoundSnapshot.current` definido; `crashPoint`/`revealedSeed` nulos até o crash; erros em ProblemDetails `code`; `rtp`/`growthK` como string decimal) | Emendas v2 das SPEC-0007 e SPEC-0008 | Nenhuma | Architect — revisão de consumidor, pela autorização do usuário (2026-10-09) |
| v1 (inalterado) | 2026-10-09 | Observabilidade do rollout: sem log no console (regra de lint `no-console`), erros mostrados nas telas do jogo; `@microsoft/signalr` como cliente JS do hub (emenda do ADR-0005) | Relato do Implementer: o texto pedia log no console, proibido pelas regras de código | Nenhuma | Architect — pela autorização do usuário para seguir recomendações (2026-10-09) |
