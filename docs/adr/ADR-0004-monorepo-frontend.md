---
id: ADR-0004
title: Repositório único e arquitetura do front-end dos jogos
status: accepted
origin: decision
date: 2026-10-07
pillars: [arquitetura]
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "dependency-cruiser (src/frontend/.dependency-cruiser.cjs) no CI, categoria arquitetura"
---

# ADR-0004 — Repositório único e arquitetura do front-end dos jogos

## Contexto e Problema
O produto tem vários fronts (cada jogo, backoffice, site demo) e um backend .NET. Cada jogo novo deve reaproveitar conexão, estado da rodada, painel de aposta e utilitários de cena, mudando só a cena e o tema. O cliente roda em iframe no cassino, inclusive em celulares fracos, e nunca decide resultado.

## Direcionadores da Decisão
- Uma engine, vários jogos: o custo de um jogo novo é a cena e o tema.
- Carregamento rápido em mobile; cena 2D com animação fluida.
- Interface do painel vinda do Stitch (exporta HTML/Tailwind).
- Começar sem custo de licença de animação, com troca futura para Spine barata.

## Opções Consideradas
- **Cena:** PixiJS 8 | Phaser 4 | Cocos Creator | Unity WebGL
- **HUD:** React + Tailwind no DOM sobre a cena | HUD desenhado na própria cena
- **Animação:** código (GSAP + spritesheets + partículas) atrás de um adaptador, Rive opcional na interface | Rive como base da cena | Spine desde já

## Resultado da Decisão
**Opção escolhida:**
- **Repositório único** (`src/backend`, `src/frontend`, `docs`), CI filtrado por caminho.
- **Front em workspace pnpm** com Vite + TypeScript estrito:
  ```
  src/frontend/
  ├── apps/{game-pipa, backoffice, demo-site}
  └── packages/{contracts, game-sdk, game-ui, scene-kit}
  ```
- **Cena em PixiJS 8**; **HUD em React + Tailwind** sobre a cena; **animação em código** (GSAP + spritesheets + partículas) atrás da interface `GameAnimator`, com Rive opcional para microanimações do HUD; Spine entra depois trocando só a implementação do `GameAnimator`.

Porque PixiJS é só renderização (leve, WebGL/WebGPU), é o padrão entre provedores de jogos HTML5 e tem runtime oficial de Spine. O HUD em DOM aproveita o código do Stitch, é acessível e fácil de traduzir. O adaptador mantém a troca para Spine barata.

**Regras (verificáveis):**
- Fluxo de dependência dentro de um jogo: `contracts → game-sdk (rede + estado) → game-ui / scene-kit → apps`. Nunca no sentido inverso.
- `scene-kit` e a cena dos apps não importam a camada de rede nem os tipos de transporte; só leem o estado exposto pelo `game-sdk`.
- Nenhum app de jogo importa outro app de jogo; código compartilhado vai para `packages/`.
- Nenhum código do cliente calcula ponto de crash, saldo ou resultado de aposta: o cliente só mostra o estado vindo do servidor.
- `packages/contracts` contém só código gerado (ADR-0008), nunca editado à mão.
- Animações da cena são acessadas apenas pela interface `GameAnimator` (`takeoff`, `setMultiplier`, `crash`, `cashout`, …).

### Consequências
- **Boa**, porque um jogo novo = app novo com cena e tema, reaproveitando SDK, HUD e contratos.
- **Boa**, porque a troca de Rive/código por Spine fica restrita à implementação do `GameAnimator`.
- **Ruim**, porque montar estrutura de jogo sobre PixiJS (cenas, loader, ciclo de vida) é trabalho nosso, já que ele não é um motor completo.
- **Ruim**, porque a integração Rive ↔ PixiJS é de terceiros (pixi-rive), por isso o Rive fica fora do núcleo da cena.

### Confirmação (G3)
dependency-cruiser no CI com uma regra por item acima; lint proíbe edição em `packages/contracts` (gerado).

## Prós e Contras das Opções
| Critério (peso) | PixiJS 8 | Phaser 4 | Cocos Creator | Unity WebGL |
|---|---|---|---|---|
| Peso/carregamento mobile (5) | ✅ leve | ⚠️ médio | ⚠️ médio | ❌ pesado |
| Adequação a crash 2D (4) | ✅ | ✅ | ✅ | ⚠️ exagero |
| Ecossistema iGaming/Spine (4) | ✅ runtime oficial Spine | ⚠️ | ⚠️ | ⚠️ |
| Estrutura pronta (2) | ❌ montar | ✅ | ✅ editor | ✅ editor |
| Licença/custo (3) | ✅ MIT | ✅ MIT | ✅ | ⚠️ |

## Mais Informações
- PixiJS v8.16.0: renderizador Canvas experimental e texto com tags — https://pixijs.com/blog/tags/tagged-text — data não confirmada; **conferir a versão no GitHub de releases antes do H1**.
- Phaser v4.0.0 (10/04/2026), v4.2.1 (jul/2026) — https://phaser.io/download/stable, https://gamedev.net/news/4825-phaser-v421-released/
- spine-pixi-v8 exige PixiJS ≥ 8.16 — https://esotericsoftware.com/blog/spine-pixi-v8-runtime-released
- pixi-rive (terceiros) — https://npmjs.com/package/pixi-rive; runtimes oficiais Rive: @rive-app/canvas, @rive-app/webgl.
- GSAP: 100% gratuito, incluindo plugins, desde 2025 (aquisição pela Webflow) — **não verificado nesta sessão; confirmar antes do H1**.
- Verificado em 2026-10-07.
