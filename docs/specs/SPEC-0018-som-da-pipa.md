---
id: SPEC-0018
title: "Som da Pipa"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-09
parent: SPEC-0001
depends_on: [SPEC-0011]
consumes_contract: [SPEC-0010@1]
contract_version: 1
touches: ["src/frontend/packages/scene-kit/src/audio/**", "src/frontend/apps/game-pipa/src/audio/**", "src/frontend/apps/game-pipa/public/audio/**", "src/frontend/packages/game-ui/src/sound/**", "tests/e2e/pipa-audio/**", "tests/frontend/pipa/audio/**", "src/frontend/packages/scene-kit/src/index.ts", "src/frontend/packages/game-ui/src/index.ts", "src/frontend/packages/game-ui/src/hud.tsx", "src/frontend/apps/game-pipa/src/App.tsx", "src/frontend/apps/game-pipa/src/main.tsx", "src/frontend/.dependency-cruiser.cjs"]
adrs: [ADR-0004, ADR-0019, ADR-0020, ADR-0021, ADR-0024]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0018 — Som da Pipa

## 1. Visão Geral
Dá som à rodada da Pipa com **custo zero**: vento que cresce com o multiplicador, a linha "cantando" de tensão, um toque a cada marco (2x, 5x, 10x, 25x, 50x, 100x) com tom subindo, o puxão da decolagem, o estalo do corte no crash e um sinal calmo no recomeço. Sons sintetizados no navegador (Web Audio) e, quando a síntese não bastar, efeitos de domínio público (CC0). Inclui botão de som acessível, preferência lembrada e respeito à política de autoplay dos navegadores.

## 2. Motivação & Escopo
**Motivação:** no cassino o som pesa muito na sensação de qualidade e na tensão do crash; o usuário quer o jogo com som para demonstrar a operadores antes da arte final ilustrada (ADR-0023), sem custo na fase de testes.

**Objetivos (dentro do escopo) — roteiro sonoro da rodada:**
| Fase | Som | Comportamento |
|---|---|---|
| **Apostas** | vento leve em loop (ruído filtrado) | volume baixo e constante (o tique da contagem fica para a SPEC-0026, que conhece o prazo das apostas — Emenda) |
| **Decolagem** | puxão/whoosh curto | ≤ 600 ms, acompanha o `takeoff` da cena |
| **Voo** | vento cresce com o multiplicador; tom de tensão da linha sobe com log(m) | contínuo; marcos tocam um toque cuja altura sobe a cada marco, uma vez por rodada |
| **Crash** | estalo seco da linha + queda descendente; corta o vento e a tensão | estalo ≤ 150 ms; queda ≤ 1,5 s; depois silêncio até o recomeço |
| **Recomeço** | sinal curto e calmo | ≤ 600 ms |

- Motor `PipaAudio` implementando o contrato `GameAudio` (abaixo), alimentado pela mesma sequência de estados do `GameAnimator` (SPEC-0010@1).
- Botão de som no `game-ui` (ligar/desligar e volume), operável por teclado e leitor de tela, com a preferência lembrada no navegador.
- Nenhum som antes do primeiro gesto do jogador (política de autoplay).
- Arquivos CC0 (se usados) com registro de origem e licença ao lado do arquivo.

**Não-objetivos (fora do escopo):**
- Música de fundo e trilha (spec de arte final, ADR-0023).
- Sons de aposta, cash-out e vitória (épico de apostas).
- Sons gerados por IA ou bibliotecas pagas.

## 3. Dependências
- **Implementações necessárias:** SPEC-0011 — roteiro da cena e funções puras (`wind(m)`, marcos) que o som acompanha, e o `PipaAnimator` com o qual o áudio é orquestrado.
- **Contratos consumidos:** SPEC-0010@1 — sequência de estados e interface `GameAnimator` (`prepare`, `takeoff`, `setMultiplier`, `crash`, `reset`) e o gancho `window.__APOGEU_TEST__`.
- **Pré-requisitos externos:** Nenhum (Web Audio é nativa dos navegadores; nenhum pacote novo).

## 4. Decisão Arquitetural
**Contexto:** ADR-0004 (a cena não lê a rede; efeitos por interfaces do `scene-kit`), ADR-0021 (roteiro por fase, tempo vem do servidor), ADR-0024 (Web Audio nativa, CC0).

**Decisão:** `GameAudio` é uma interface irmã do `GameAnimator` no `scene-kit`; o app chama os dois a partir do mesmo assinante de estado do `game-sdk`. `PipaAudio` compõe subsistemas puros e testáveis: `AudioMix` (mapeamentos multiplicador → ganho/altura), `CueScheduler` (marcos uma vez por rodada, alinhado ao relógio do servidor) e `Voices` (síntese: ruído filtrado, osciladores, envelopes). Um `AudioGate` só cria/retoma o `AudioContext` após `unlock()` chamado num gesto do jogador. Volume mestre único com rampa (sem cliques).

**Justificativa:** mesma forma da SPEC-0011 — funções puras testáveis sem tocar som; a integração é provada por um registro de sinais (`cueLog`) no gancho de teste e por `OfflineAudioContext` nos unitários.

**Desvio do padrão existente:** Nenhum. Segue o `GameAnimator` da SPEC-0010/0011; nenhuma dependência nova.

**Alternativas descartadas:** Howler.js e Tone.js (ADR-0024); arquivos de áudio para tudo (pesa no pacote e exige licenças).

**ADRs:** ADR-0004, ADR-0019, ADR-0020, ADR-0021, ADR-0024.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** áudio ligado não piora o tempo de quadro da SPEC-0011 (p95 ≤ 16,7 ms no Chromium do CI) — E2E-04; acréscimo ao pacote ≤ 40 KB gzip de código e ≤ 150 KB de arquivos de áudio, carregados sob demanda.
- **Segurança:** N/A — só apresentação, sem dados novos.
- **Privacidade e dados pessoais:** preferência de som guardada só no navegador (`localStorage`, com falha silenciosa), sem enviar ao servidor — UT-04.
- **Disponibilidade e resiliência:** sem `AudioContext` disponível (ou bloqueado), o jogo segue mudo sem erro — UT-03.
- **Acessibilidade (UI):** botão de som com nome acessível e `aria-pressed`, operável por teclado, sem violações axe; picos limitados (ganho mestre ≤ 0,8, sem saltos acima de 6 dB entre sinais consecutivos); o estado do jogo continua anunciado no `aria-live` sem depender do som — UT-05, E2E-03.
- **Custo:** zero — APIs nativas e arquivos CC0.

## 6. Artefato A — Contrato
**Interface:** `GameAudio` no `scene-kit` + mapeamentos sonoros + controles do `game-ui`.

```text
GameAudio: unlock() | prepare() | takeoff(startedAt) | setMultiplier(m) | crash(point) | reset()
           | setMuted(muted) | setVolume(v ∈ [0,1]) | dispose()
Antes de unlock(): nenhum AudioContext em execução; chamadas de estado só atualizam o estado interno (sem enfileirar sons).
Mapeamentos (funções puras):
  windGain(m)     → 0,15 + 0,45 · min(1, log10(m)/2)        (apostas: 0,15)
  tensionHz(m)    → 220 · 2^(min(log2(m), 6)/3)  limitado a [220, 880] Hz
  milestoneHz(i)  → 523,25 · 2^(2i/12) (sobe um tom por marco) para os marcos {2,5,10,25,50,100} (i = 0..5), uma vez por rodada
Crash: snap(0–150 ms) → fall(150–1500 ms, tom descendente) → silence; vento e tensão com rampa a 0 em ≤ 100 ms
Mestre: ganho = volume · (muted ? 0 : 1) · 0,8; mudanças com rampa de 50 ms
Aba oculta: AudioContext suspenso; ao voltar, retoma no estado atual sem tocar sinais perdidos
Preferência: localStorage "apogeu.sound" = {"muted":bool,"volume":number}; padrão {"muted":false,"volume":0,7}
UI (game-ui): <SoundToggle> botão "Som" com aria-pressed + controle de volume; o primeiro gesto em qualquer
  controle do jogo chama unlock()
Gancho de teste (só build de teste): __APOGEU_TEST__.audio.cueLog() | .state() → "locked" | "running" | "suspended" | "muted"
```

**Design:** Stitch — projeto "Pipa — Crash Game" (16371353209282165204): botão de som no cabeçalho do jogo, ícone de alto-falante com estado ligado/desligado, nos tokens do design system "Pipa Crash".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Mistura cresce com o risco | m = 1, 2, 5, 10, 25, 50, 100 | `windGain` e `tensionHz` nas faixas do contrato e nunca diminuem | UT-01 |
| Marcos sonoros | cruzar 2x…100x; ressincronização de 12x para 11,8x | cada marco toca uma vez, com altura crescente | UT-02 |
| Autoplay respeitado | estados chegam antes de `unlock()` | nenhum `AudioContext` em execução, nenhum som enfileirado; após `unlock()` toca só o estado atual | UT-03, E2E-02 |
| Sem áudio no navegador | `AudioContext` ausente ou criação falha | jogo segue mudo, sem exceção | UT-03 |
| Mudo e volume | `setMuted(true)`, `setVolume(0,3)`; recarregar | ganho mestre com rampa de 50 ms; preferência lembrada; `localStorage` indisponível não quebra | UT-04, E2E-01 |
| Picos seguros | pior caso (100x, crash) | ganho mestre ≤ 0,8; sem salto > 6 dB entre sinais consecutivos | UT-05 |
| Roteiro do crash | `crash(point)` | ordem snap → fall → silêncio; vento e tensão a 0 em ≤ 100 ms; uma vez | UT-06 |
| Aba oculta | `visibilitychange` hidden/visible | contexto suspende; ao voltar retoma no estado atual sem tocar sinais perdidos | UT-07 |
| Contrato do áudio | sequência de estados da SPEC-0010 | `PipaAudio` passa na suíte de contrato do `GameAudio` | CT-01 |
| Rodada real com som | backend real, Chromium com áudio falso | `cueLog` segue as fases da rodada; sem erros de console | IT-01 |
| Botão de som acessível | teclado e axe | operável por teclado, `aria-pressed` correto, sem violações | E2E-03 |
| Fluidez mantida | rodada completa com som ligado | p95 de quadro ≤ 16,7 ms | E2E-04 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; a cena da SPEC-0011 já é coberta pelos testes dela.

### 7.2 Testes Unitários
- **UT-01** — Dados m = 1, 2, 5, 10, 25, 50 e 100, quando `windGain` e `tensionHz` são calculados, então ficam nas faixas do contrato ([0,15; 0,60] e [220; 880] Hz) e nunca diminuem com m crescente.
- **UT-02** — Dada uma rodada de 1x a 120x com uma ressincronização de 12x para 11,8x, quando os marcos são avaliados, então cada um dos 6 marcos toca exatamente uma vez e as alturas são estritamente crescentes.
- **UT-03** — Dado o `PipaAudio` sem `unlock()`, quando `prepare`, `takeoff` e `setMultiplier(3)` são chamados, então nenhum `AudioContext` é criado nem retomado e nada é enfileirado; após `unlock()`, só o estado atual (voo a 3x) soa; e dado `AudioContext` ausente, então nenhuma chamada lança exceção.
- **UT-04** — Dado `setMuted(true)` e depois `setVolume(0,3)`, quando o ganho mestre é inspecionado num `OfflineAudioContext`, então vai a 0 por rampa de 50 ms e, ao desmutar, a 0,3 · 0,8; a preferência é gravada em `apogeu.sound`, e com `localStorage` lançando exceção o comportamento é o mesmo, sem erro.
- **UT-05** — Dado o pior caso (100x seguido de crash) renderizado num `OfflineAudioContext`, quando o sinal é analisado, então o ganho mestre nunca passa de 0,8 e não há salto > 6 dB entre sinais consecutivos.
- **UT-06** — Dado `crash(3.21)`, quando o agendamento é inspecionado, então a ordem é snap (≤ 150 ms) → fall (até 1500 ms) → silêncio, vento e tensão chegam a 0 em ≤ 100 ms e o crash soa uma única vez.
- **UT-07** — Dado `visibilitychange` para hidden durante o voo e visible 10 s depois já em outra rodada, então o `AudioContext` é suspenso e, ao voltar, retoma no estado atual sem tocar marcos nem crash perdidos.

### 7.3 Testes de Integração
- **IT-01** — Dado o backend real (Aspire) e o Chromium com dispositivo de áudio falso (`--use-fake-device-for-media-stream`, `--autoplay-policy=no-user-gesture-required` só neste teste), quando uma rodada completa acontece após `unlock()`, então o `cueLog` contém, em ordem, vento, decolagem, marcos atingidos, crash e recomeço, e não há erros de console.

### 7.4 Testes de Contrato
- **CT-01** — Dada a sequência de estados do contrato da SPEC-0010@1 (a mesma da suíte de contrato do `GameAnimator`), quando executada contra o `PipaAudio`, então todas as chamadas são aceitas, o `cueLog` segue o roteiro do §6 e nenhuma lança exceção.

### 7.5 Testes E2E
- **E2E-01** — Dado o jogo aberto, quando o jogador toca no botão "Som" para desligar e recarrega a página, então o botão continua desligado (`aria-pressed="false"`), `audio.state()` é "muted" e nenhum sinal entra no `cueLog`.
- **E2E-02** — Dado o jogo aberto sem nenhum gesto do jogador, quando uma rodada inteira passa, então `audio.state()` é "locked" e o `cueLog` está vazio; após o primeiro toque, passa a "running".
- **E2E-03** — Dado o cabeçalho do jogo, quando o botão de som é operado só por teclado (Tab, Enter, Espaço) e a página é analisada com axe, então alterna o estado, anuncia `aria-pressed` correto e não há violações.
- **E2E-04** — Dada uma rodada completa no Chromium com som ligado, quando os tempos de quadro são coletados, então p95 ≤ 16,7 ms.

### 7.6 Outros
- Revisão auditiva humana: o usuário ouve uma rodada gravada (export do `OfflineAudioContext` em WAV) e aprova antes do G4 (registrado no Relatório de Entrega).
- Orçamento de pacote: código de áudio ≤ 40 KB gzip; arquivos de áudio ≤ 150 KB, carregados sob demanda.
- Licenças: todo arquivo em `public/audio/` tem registro de origem com licença CC0 (verificado no G4 e pelo `check-licenses` se aplicável).

**Dublês e dados de teste:** `OfflineAudioContext` nos unitários (renderização determinística sem alto-falante); relógio do servidor simulado nos UT; backend real no IT e nos E2E.

**Ambiente de execução:** Vitest com dublê de `OfflineAudioContext` (UT, CT — Emenda IMP-01); Chromium real para o sinal renderizado do UT-05; Playwright Chromium no CI (IT, E2E).

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local); som ligado por padrão após o primeiro gesto, com botão de mudo visível.
- **Dados/schema:** N/A
- **Compatibilidade:** `GameAnimator` e o contrato da SPEC-0010@1 inalterados; `GameAudio` é uma interface nova e opcional (o app funciona sem ela).
- **Observabilidade:** contagem de falhas de criação do `AudioContext` na telemetria do cliente quando houver hospedagem; por ora, só no `cueLog` dos testes.
- **Rollback:** reverter o merge commit; o jogo volta a ser mudo sem outras mudanças.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] O som começa ligado (após o primeiro toque) ou desligado? — Ligado após o primeiro gesto, com botão de mudo visível e preferência lembrada; recomendação do Architect, aceita pela autorização do usuário para seguir as recomendações (thomas, 2026-10-09) — confirmado no H1 (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN (2026-10-09). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0004, ADR-0019, ADR-0020, ADR-0021, ADR-0024 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06, UT-07 com a tag `SPEC-0018:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, CT-01 com a tag `SPEC-0018:<ID>` (IT com backend real no Chromium, CT com `OfflineAudioContext`, conforme §7.6) e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 4: Jornada E2E**
- [ ] Red: E2E-01, E2E-02, E2E-03, E2E-04 falhando pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — bb2da66 | 2026-10-09 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: ⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯⎯[31/31]⎯) — 259ab57 | 2026-10-10 |
| G2 Green | PASS | build exit 0 (✓ built in 169ms); test exit 0 (Duration  1.41s (tests 66%, transform 20%, import 12%, worker 2%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-10T21:20:33: Report generation took 0,4 seconds) — 31d2696 | 2026-10-10 |
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
| IMP-01 | 2026-10-10 | G1 | spec | Sem caminho no touches para os testes do Vitest (raiz tests/frontend) e o §7.6 pede OfflineAudioContext no Vitest, que roda no Node sem Web Audio | Test-writer leu vitest.workspace.ts, tsconfig dos pacotes, Node 24 (OfflineAudioContext indefinido) e as dependências; nenhum commit | Architect: emenda de escopo e do §7.6 | Emenda (2026-10-10): tests/frontend/pipa/audio/** no touches; dublê de OfflineAudioContext no Vitest e sinal renderizado do UT-05 no Chromium | 2026-10-10 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0018`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-10 | Escopo para ligar o som ao jogo: exports de `scene-kit` e `game-ui`, `hud.tsx` (botão de som), `App.tsx`/`main.tsx` (montagem, desbloqueio no primeiro gesto, gancho de teste) e `.dependency-cruiser.cjs` (regra do ADR-0024: só o scene-kit usa AudioContext). O tique da contagem nos 3 últimos segundos sai desta spec (o `GameAnimator`/`GameAudio` não recebe o prazo das apostas, como na SPEC-0011) e vira pendência da SPEC-0026. Esclarecimentos confirmados do contrato do Test-writer: `unlock(): Promise<void>` cria o contexto só no desbloqueio; opções `{createContext?, storage?, visibility?}`; "mudo" prevalece nos estados; nada no `cueLog` antes do desbloqueio, com mudo ou aba oculta; deixas wind/tick/takeoff/tension/milestone/snap/fall/silence/restart; ganho mestre é o único nó ligado ao destino; marco perdido com a aba oculta não é tocado depois; roteiro do crash agendado no relógio de áudio; regra de 6 dB comparando picos de deixas consecutivas (unitário) e RMS de janelas de 50 ms acima de −50 dBFS (renderizado) | G1 do Test-writer: arquivos de integração fora do `touches` e o tique sem prazo no contrato | SPEC-0026 (tique da contagem) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
| 1 (inalterado) | 2026-10-10 | Escopo: `tests/frontend/pipa/audio/**` (UT e CT no Vitest). §7.6: no Vitest (Node, sem Web Audio) os UT/CT usam um dublê escrito à mão de `OfflineAudioContext`/`AudioParam` que registra a automação (curvas de ganho, agendamentos) e o UT-05 verifica a curva agendada (pico ≤ 0,8, nenhum degrau > 6 dB); a verificação do sinal renderizado de verdade roda uma vez no Chromium (`OfflineAudioContext` real via `page.evaluate`) em `tests/e2e/pipa-audio/**`, sob a tag do UT-05. Sem pacote novo (ADR-0024). IT-01 e E2E-04 restritos ao Chromium no próprio arquivo | IMP-01: Test-writer — Vitest descobre só `tests/frontend` e o Node não tem `OfflineAudioContext` | Nenhuma | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
