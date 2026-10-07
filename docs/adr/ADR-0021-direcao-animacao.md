---
id: ADR-0021
title: Direção de animação das cenas dos jogos
status: accepted
origin: decision
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes de regressão visual (Playwright toHaveScreenshot) dos quadros-chave; E2E de movimento reduzido; medição de tempo de quadro; dependency-cruiser: animação só via GameAnimator"
---

# ADR-0021 — Direção de animação das cenas dos jogos

## Contexto e Problema
O usuário quer **animação bonita**: no lobby do cassino, o visual decide se o jogador abre o jogo. O ADR-0004 já define *onde* a animação vive (atrás do `GameAnimator`, em código, Spine depois). Faltava decidir *como* chegar a uma animação de qualidade sem animador nem arte final, mantendo 60 fps em celular médio e acessibilidade (movimento reduzido).

## Direcionadores da Decisão
- Qualidade percebida alta já no demo, sem custo de licença.
- Desempenho (ADR-0019): 60 fps em Android médio, pacote ≤ 1,5 MB.
- Acessibilidade (ADR-0020, WCAG 2.2): respeitar `prefers-reduced-motion` e evitar flashes perigosos.
- Beleza não vira regressão silenciosa: precisa de verificação visual aprovada por humano.

## Opções Consideradas
- **Movimento procedural em código**: GSAP (linhas do tempo e curvas de suavização) + física simples (corda e rabiola por Verlet) + partículas + filtros do pixi-filters (brilho/bloom), com arte vetorial desenhada em código
- **Rive como base da cena** (via pixi-rive, de terceiros)
- **Spine desde já** (licença paga + animador)

## Resultado da Decisão
**Opção escolhida:** **movimento procedural em código**, porque a pipa é um objeto rígido. A beleza vem do *comportamento*: o balanço ao vento, a linha com tensão, a rabiola chicoteando, o céu mudando, a profundidade das camadas, o brilho. Tudo isso se faz melhor com física e linhas do tempo do que com animação quadro a quadro, sem custo e sem dependência de terceiros imaturos.

**Princípios de movimento (valem para todos os jogos):**
1. **Roteiro por fase:** cada fase da rodada (apostas, decolagem, voo, crash, recomeço) tem um roteiro com duração, suavização e efeitos definidos na spec do jogo.
2. **Escalada de tensão:** a intensidade visual (câmera, vento, brilho, cores) cresce com o multiplicador, para o jogador *sentir* o risco subindo.
3. **O crash é o clímax:** ruptura clara (≤ 300 ms de impacto), seguida de queda com física e recomeço calmo.
4. **Física simples e estável:** Verlet com passo fixo; nunca "explode" nem depende da taxa de quadros.
5. **O tempo vem do servidor:** a animação segue o estado e o relógio sincronizado do `game-sdk`; nunca antecipa o crash.

**Regras (verificáveis):**
- Toda animação da cena passa pelo `GameAnimator`; a cena não lê a rede (ADR-0004).
- Com `prefers-reduced-motion: reduce`: sem tremor de tela, sem clarão, sem paralaxe; transições viram esmaecimentos; o multiplicador e os estados continuam visíveis.
- Nenhum clarão acima de 3 por segundo (WCAG 2.3.1); o clarão do crash tem opacidade ≤ 25%.
- Tempo de quadro p95 ≤ 16,7 ms no perfil de referência do CI; número de partículas e de filtros com teto fixo; resolução limitada a 2x de densidade de pixels; a cena pausa com a aba oculta.
- Quadros-chave de cada jogo têm **imagens de referência aprovadas pelo usuário**; mudança visual exige nova aprovação (regressão visual).
- Bibliotecas: **GSAP** (licença padrão gratuita, uso comercial permitido) e **pixi-filters 6.x**; física própria (sem motor de física).

### Consequências
- **Boa**, porque fica bonito sem custo de licença nem animador, e o "jeito Apogeu" de animar vale para os próximos jogos.
- **Boa**, porque a regressão visual com aprovação humana protege a qualidade.
- **Ruim**, porque movimento procedural exige ajuste fino (tuning); as imagens de referência precisam ser reaprovadas a cada mudança intencional.
- **Ruim**, porque personagens (por exemplo, um surfista num jogo futuro) ainda vão precisar de Spine. Este ADR cobre objetos rígidos e efeitos.

### Confirmação (G3)
Playwright `toHaveScreenshot` nos quadros-chave com tempo congelado pelo gancho de teste; E2E com `reducedMotion: 'reduce'`; medição de tempo de quadro; dependency-cruiser (ADR-0004).

## Prós e Contras das Opções
| Critério (peso) | Procedural em código | Rive na cena | Spine já |
|---|---|---|---|
| Beleza para objeto rígido + efeitos (5) | ✅ física e partículas | ⚠️ bom para personagem/UI | ⚠️ exige animador |
| Custo (4) | ✅ zero | ✅ plano grátis | ❌ licença + animador |
| Maturidade da integração com PixiJS (4) | ✅ nativo | ❌ plugin de terceiros | ✅ runtime oficial |
| Desempenho controlável (4) | ✅ tetos explícitos | ⚠️ canvas extra | ✅ |

## Mais Informações
- GSAP gratuito, incluindo plugins e uso comercial (licença padrão vigente desde 30/04/2025) — https://webflow.com/blog/gsap-becomes-free e https://gsap.com/community/standard-license/ — verificado em 2026-10-07. A licença proíbe só ferramentas que concorram com o construtor visual do Webflow, o que não é o nosso caso.
- pixi-filters 6.x compatível com PixiJS v8 (importação por efeito, ex. `pixi-filters/glow`) — https://pixijs.com/8.x/guides/components/filters.md — versão exata **não verificada nesta sessão**.
- WCAG 2.2 — critérios 2.3.1 (três clarões) e 2.3.3 (animação por interação).
