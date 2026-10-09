---
id: SPEC-0011
title: "Animação da cena da Pipa"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0010]
consumes_contract: [SPEC-0010@1]
contract_version: 1
touches: ["src/frontend/packages/scene-kit/**", "src/frontend/apps/game-pipa/src/scene/**", "tests/e2e/pipa-visual/**", "tests/frontend/pipa/scene/**", "src/frontend/playwright.config.ts", ".github/workflows/visual-baselines.yml"]
adrs: [ADR-0004, ADR-0019, ADR-0020, ADR-0021]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0011 — Animação da cena da Pipa

## 1. Visão Geral
Substitui a animação básica do esqueleto (SPEC-0010) por uma cena **bonita e cinematográfica** da Pipa: pipa balançando ao vento, linha com tensão, rabiola com física, céu que escurece até a noite estrelada conforme o multiplicador sobe, nuvens em camadas, rastro brilhante e um crash dramático (linha arrebenta, clarão, pipa rodopiando). Tudo em código, a 60 fps, respeitando movimento reduzido e protegido por regressão visual aprovada pelo usuário.

## 2. Motivação & Escopo
**Motivação:** o usuário quer animação bonita; no lobby do cassino o visual decide o clique, e a emoção do crash vem da escalada de tensão (ADR-0021).

**Objetivos (dentro do escopo) — roteiro da rodada:**
| Fase | O que acontece | Duração / suavização |
|---|---|---|
| **Apostas** (PREPARE SUA PIPA) | pipa baixa sobre a silhueta da orla do Rio com morros ao fundo, balançando ao vento; rabiola ondulando; partículas leves de vento (a contagem fica no HUD — Emenda) | durante a fase; balanço senoidal 2–3 s |
| **Decolagem** | puxão: pipa sobe com aceleração, linha estica, rabiola chicoteia | 600 ms, `power3.out` |
| **Voo** | câmera sobe suavemente (o cenário desce), 3 camadas de nuvens em paralaxe cada vez mais rápidas; céu dia → pôr do sol (≥ 5x) → noite (≥ 25x) com estrelas surgindo (≥ 10x) e lua (≥ 50x); rastro brilhante da curva; vento e balanço aumentam com o multiplicador; pulso do multiplicador nos marcos 2x, 5x, 10x, 25x, 50x e 100x, com brilho dourado a partir de 10x | contínuo, guiado pelo multiplicador |
| **Crash** (PIPA CORTADA!) | a linha arrebenta no ponto de tensão com recuo; clarão vermelho curto; leve tremor de tela; a pipa rodopia e cai com arrasto, saindo do quadro (o tremor do texto PIPA CORTADA! é do HUD: SPEC-0026 — Emenda) | impacto ≤ 300 ms; queda 1,5 s |
| **Recomeço** | esmaecimento suave para o cenário inicial; nova pipa entra | 600 ms, `sine.inOut` |

- Arte **vetorial desenhada em código** (pipa de 4 cores do design do Stitch, nuvens, estrelas, lua, silhueta da orla com morros), sem arquivos de arte externos.
- Física própria: corda e rabiola por integração Verlet com passo fixo.
- Filtros: brilho (pixi-filters `GlowFilter` na pipa a partir de 10x) e rastro brilhante em traços em camadas, com teto; partículas com teto. Bloom fica para a arte final (Emenda).
- Modo **movimento reduzido**.
- Tetos de desempenho, pausa com a aba oculta.
- Imagens de referência dos quadros-chave aprovadas pelo usuário.

**Não-objetivos (fora do escopo):**
- Animação de vitória ou cash-out (épico de apostas).
- Arte final ilustrada (Scenario), Spine, som e música (spec futura "Arte, som e animação final da Pipa").

## 3. Dependências
- **Implementações necessárias:** SPEC-0010 — app, store, gancho de teste e `GameAnimator` básico sobre os quais esta spec evolui.
- **Contratos consumidos:** SPEC-0010@1 — interface `GameAnimator` (`prepare`, `takeoff`, `setMultiplier`, `crash`, `reset`) e gancho `window.__APOGEU_TEST__`.
- **Pré-requisitos externos:** pacotes `gsap` e `pixi-filters` 6.x previstos no ADR-0021 (adicionados ao `package.json` do `scene-kit` por esta spec; o `pnpm-lock.yaml` muda junto).

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0004 (animação só via `GameAnimator`), ADR-0021 (direção de animação), ADR-0019 (desempenho), ADR-0020 (acessibilidade).

**Decisão:** `PipaAnimator` implementa `GameAnimator` e é composto por subsistemas independentes e testáveis: `Sky` (paleta por multiplicador), `Camera` (altitude por multiplicador), `CloudLayers` (paralaxe), `KiteRig` (pipa + linha + rabiola com Verlet), `Trail`, `Effects` (clarão, tremor, partículas, filtros) e `Timeline` (GSAP por fase). Um `MotionProfile` (`full` | `reduced`) é lido de `matchMedia('(prefers-reduced-motion: reduce)')` e passado a todos os subsistemas. Para os testes visuais, o gancho de teste permite **congelar o relógio** e posicionar a cena num instante exato.

**Justificativa:** subsistemas puros (entrada: multiplicador/tempo → saída: parâmetros visuais) são testáveis sem renderizar; a renderização é provada por regressão visual.

**Desvio do padrão existente:** Nenhum. Evolui a implementação de `GameAnimator` da SPEC-0010 sem mudar a interface.

**Alternativas descartadas:** Rive/Spine na cena (ADR-0021); motor de física completo (Matter.js) — exagero para corda e rabiola.

**ADRs:** ADR-0004, ADR-0019, ADR-0020, ADR-0021.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** tempo de quadro p95 ≤ 16,7 ms (Chromium no CI) e ≤ 33 ms com CPU 4x mais lenta (aproximação de Android médio); ≤ 300 partículas; ≤ 3 filtros ativos; densidade de pixels ≤ 2; acréscimo ao pacote ≤ 150 KB gzip — E2E-03 e orçamento de bundle.
- **Segurança:** N/A — só apresentação, sem dados novos.
- **Privacidade e dados pessoais:** N/A — sem dados.
- **Disponibilidade e resiliência:** perda do contexto WebGL é recuperada recriando a cena sem recarregar a página — IT-02.
- **Acessibilidade (UI):** `prefers-reduced-motion` respeitado; ≤ 3 clarões/s e opacidade do clarão ≤ 25% (WCAG 2.3.1) — UT-05, UT-06, E2E-02.
- **Custo:** N/A — bibliotecas gratuitas.

## 6. Artefato A — Contrato
**Interface:** implementação de `GameAnimator` (contrato da SPEC-0010@1, inalterado) + parâmetros visuais derivados.

```text
GameAnimator (inalterado): prepare() | takeoff(startedAt) | setMultiplier(m) | crash(point) | reset()
Mapeamentos (funções puras):
  sky(m)        → paleta: dia [1,5) · pôr do sol [5,25) · noite ≥25 (interpolação contínua entre faixas)
  stars(m)      → opacidade 0 em <10 → 1 em ≥25
  moon(m)       → visível ≥50
  camera(m)     → altitude = log2(m) · 1 tela, limitada a 12 telas
  wind(m)       → intensidade 0,2 + 0,8 · min(1, log10(m)/2)
  milestones    → {2,5,10,25,50,100} disparam pulso uma vez por rodada
Crash: snap(0 ms) → flash(0–150 ms, opacidade ≤0,25) → shake(0–300 ms, ≤ 8 px) → tumble(300–1800 ms) → fadeOut
MotionProfile reduced: shake=0, flash=0, parallax=0, transições = crossfade 300 ms
Gancho de teste (só build de teste): __APOGEU_TEST__.scene.freeze(t, state) | .effectsLog() | .frameTimes()
```

**Design:** Stitch — projeto "Pipa — Crash Game" (16371353209282165204), design system "Pipa Crash" (paleta do céu `#FACC15 → #F97316 → #EC4899 → #7C3AED`, pipa losango de 4 cores, rastro laranja brilhante, clarão `rgba(239,68,68,0.25)`).

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Corda estável | 10.000 passos de simulação | comprimento ± 2%, sem NaN | UT-01 |
| Física independe dos fps | 1 s a 30 fps vs 120 fps | posições finais iguais (± 1 px) | UT-02 |
| Escalada visual | m = 1, 5, 10, 25, 50, 100 | céu, estrelas, lua, câmera e vento nas faixas definidas e monotônicos | UT-03 |
| Marcos do multiplicador | cruzar 2x…100x; ressincronização para trás | pulso uma vez por marco; sem repetir | UT-04 |
| Movimento reduzido | `MotionProfile=reduced` | sem tremor, clarão e paralaxe; transições em esmaecimento | UT-05, E2E-02 |
| Roteiro do crash | `crash(point)` | ordem snap → flash → shake → tumble → fade; clarão ≤ 25% e único | UT-06 |
| Tetos de desempenho | qualquer fase | ≤ 300 partículas, ≤ 3 filtros, densidade ≤ 2 | UT-07 |
| Aba oculta | `visibilitychange` hidden/visible | cena pausa; ao voltar, alinha ao estado atual sem "pular" animações antigas | UT-08 |
| Contrato do animador | sequência de estados da SPEC-0010 | `PipaAnimator` passa na suíte de contrato do `GameAnimator` | CT-01 |
| Renderização real | rodada completa com backend real em Chromium (WebGL) | sem erros de console nem perda de contexto | IT-01 |
| Perda de contexto WebGL | `WEBGL_lose_context` forçado | cena recriada e voltando ao estado atual | IT-02 |
| Beleza protegida | 6 quadros-chave com relógio congelado | iguais às imagens de referência aprovadas | E2E-01 |
| Fluidez | rodada completa | p95 de quadro ≤ 16,7 ms; com CPU 4x ≤ 33 ms | E2E-03 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — evolui código novo da SPEC-0010, já coberto pelos testes dela.

### 7.2 Testes Unitários
- **UT-01** — Dada uma corda de 20 segmentos com passo fixo de 1/120 s, quando simulada por 10.000 passos com vento variável, então o comprimento total fica em ± 2% e nenhuma coordenada é NaN.
- **UT-02** — Dada a mesma corda e o mesmo vento, quando 1 s é simulado com quadros de 1/30 s e de 1/120 s, então as posições finais coincidem em ± 1 px.
- **UT-03** — Dados m = 1, 5, 10, 25, 50 e 100, quando `sky`, `stars`, `moon`, `camera` e `wind` são calculados, então ficam nas faixas do contrato e nunca diminuem com m crescente.
- **UT-04** — Dada uma rodada que vai de 1x a 120x com uma ressincronização de 12x para 11,8x, quando os marcos são avaliados, então cada marco dispara exatamente uma vez.
- **UT-05** — Dado `MotionProfile=reduced`, quando decolagem, voo e crash são executados, então tremor = 0, clarão = 0, paralaxe = 0 e transições são esmaecimentos de 300 ms.
- **UT-06** — Dado `crash(3.21)`, quando a linha do tempo é inspecionada, então a ordem é snap → flash → shake → tumble → fade, o clarão dura ≤ 150 ms com opacidade ≤ 0,25 e ocorre uma única vez.
- **UT-07** — Dado o pior caso (100x, vento máximo, crash), quando a cena é configurada, então partículas ≤ 300, filtros ativos ≤ 3 e densidade de pixels ≤ 2.
- **UT-08** — Dado `visibilitychange` para hidden durante o voo e visible 10 s depois já em outra rodada, então o ticker pausa e, ao voltar, a cena vai direto ao estado atual sem reproduzir animações perdidas.

### 7.3 Testes de Integração
- **IT-01** — Dado o backend real (Aspire) e o Chromium com WebGL, quando uma rodada completa é renderizada, então não há erros de console nem evento `webglcontextlost`.
- **IT-02** — Dado o jogo em voo, quando `WEBGL_lose_context.loseContext()` e depois `restoreContext()` são chamados, então a cena é recriada e volta a acompanhar o estado atual em ≤ 1 s.

### 7.4 Testes de Contrato
- **CT-01** — Dada a suíte de contrato do `GameAnimator` da SPEC-0010@1, quando executada contra o `PipaAnimator`, então passa sem alterações.

### 7.5 Testes E2E
- **E2E-01** — Dado o gancho de teste congelando o relógio, quando a cena é posicionada em 6 quadros-chave (apostas; decolagem a 300 ms; voo a 2x; voo a 15x, pôr do sol; voo a 120x, noite com lua; crash a 150 ms), então as capturas (Chromium, 390×844) são iguais às imagens de referência **aprovadas pelo usuário** (diferença ≤ 1% dos pixels).
- **E2E-02** — Dado o navegador com `reducedMotion: 'reduce'`, quando uma rodada completa acontece, então o registro de efeitos não contém tremor, clarão nem paralaxe, e os estados continuam anunciados no `aria-live`.
- **E2E-03** — Dada uma rodada completa no Chromium, quando os tempos de quadro são coletados, então p95 ≤ 16,7 ms; com CPU 4x mais lenta (CDP), p95 ≤ 33 ms.

### 7.6 Outros
- Revisão visual humana: o usuário aprova as 6 imagens de referência antes do G4 (registrado no Relatório de Entrega).
- Orçamento de pacote: acréscimo ≤ 150 KB gzip.

**Dublês e dados de teste:** relógio congelado e estados posicionados pelo gancho de teste (apenas nos E2E visuais); backend real no IT e E2E-02/03.

**Ambiente de execução:** Vitest (UT, CT); Playwright Chromium com WebGL (IT, E2E) no CI. Imagens de referência geradas no mesmo runner Linux do CI, para evitar diferenças de fonte e renderização.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** N/A
- **Compatibilidade:** interface `GameAnimator` inalterada; o animador básico da SPEC-0010 é substituído.
- **Observabilidade:** métrica de tempo de quadro p95 enviada junto com a telemetria do cliente quando houver hospedagem; por ora, só no relatório do E2E-03.
- **Rollback:** reverter o merge commit (volta ao animador básico).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Qual cenário aparece no chão, de onde a pipa sobe? — (b) Orla do Rio com morros ao fundo, em silhueta contra o degradê do céu (thomas, 2026-10-07)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0004, ADR-0019, ADR-0020, ADR-0021 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06, UT-07, UT-08 com a tag `SPEC-0011:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, CT-01 com a tag `SPEC-0011:<ID>`, backend real e Chromium com WebGL no IT, suíte de contrato do `GameAnimator` no CT, e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 4: Jornada E2E**
- [ ] Red: E2E-01, E2E-02, E2E-03 falhando pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — 8dec4f4 (árvore suja) | 2026-10-07 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:43.72]     Dado um processo que avança 3 rodadas e é reiniciado, quando N) — b867610 | 2026-10-09 |
| G2 Green | PASS | build exit 0 (✓ built in 154ms); test exit 0 (Duration  1.39s (tests 69%, transform 19%, import 11%, worker 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-09T18:48:49: Report generation took 0,3 seconds) — 6cd9827 | 2026-10-09 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  802ms (tests 97%, transform 2%, import 1%)) — 6cd9827 | 2026-10-09 |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0011`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-09 | Escopo reduzido: (1) sem bloom — brilho por `GlowFilter` na pipa a partir de 10x e rastro em traços em camadas; bloom vai para a arte final antes do lançamento; (2) sem anel de contagem regressiva na cena — o `GameAnimator` não recebe o prazo das apostas e o HUD já mostra a contagem; (3) o tremor do texto PIPA CORTADA! sai desta spec e entra na SPEC-0026 (HUD) | Implementação: o bloom estourava a recriação do contexto WebGL (IT-02 ≤ 1 s) no GL por software da CI; anel e texto ficam fora do contrato e do `touches` | SPEC-0026 (tremor do texto) | thomas, 2026-10-09 (chat: "ok nas 3") |
| 1 (inalterado) | 2026-10-09 | Escopo: `.github/workflows/visual-baselines.yml` (novo, `workflow_dispatch` + push em branch `sdd/**` que altere `tests/e2e/pipa-visual/**`) gera as imagens de referência no runner Linux da CI com `run-e2e.sh pipa-visual --project=chromium --update-snapshots=missing` e as publica como artefato; o usuário aprova as 6 imagens e elas são commitadas. Esclarecimentos: `frameTimes()` = duração do trabalho de cada quadro (ms), não o intervalo do rAF; IT-02 provado pelo gancho de teste (contexto vivo, canvas redesenhado e quadros novos em ≤ 1 s, mesma página); `freeze(t)` = ms desde o início da fase | G1: as imagens precisam do mesmo runner da CI (§7.6) e o `ci.yml` é da SPEC-0017; interpretações do Test-writer | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09) |
| 1 (inalterado) | 2026-10-09 | Escopo: `tests/frontend/pipa/scene/**` (UT e CT no Vitest, onde vivem os testes do front) e `src/frontend/playwright.config.ts` (WebGL e imagens de referência dos testes visuais) | Despacho da onda 6: o `touches` não tinha onde colocar os unitários | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09) |
