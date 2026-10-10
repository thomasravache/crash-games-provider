---
id: ADR-0025
title: Identidade visual Apogeu e design systems derivados
status: proposed
origin: decision
date: 2026-10-10
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Tokens de cor e tipografia num pacote único do front (fonte única por produto); teste de contraste WCAG 2.2 AA dos tokens; revisão visual no G4 contra a tela aprovada no Stitch"
---

# ADR-0025 — Identidade visual Apogeu e design systems derivados

<!-- Formato MADR (Markdown Architectural Decision Records). status: proposed | accepted | rejected | deprecated | superseded — vira accepted quando a spec que o introduz é aprovada (H1); superseded exige superseded_by. origin: decision (recomendada: compare 2–3 opções e cite fontes com data, ou declare "não verificado") | user (escolhida pelo usuário: respeite, sem reabrir a discussão, e registre riscos e custo) | as-is (documenta um padrão que já existe no projeto). enforced_by: teste de arquitetura que garante a decisão (G3), ou "N/A — motivo". -->

## Contexto e Problema
A Apogeu vai a mercado como provedor B2B, mas ainda não tem identidade visual: o único design system de backoffice no Stitch nasceu como rascunho ("Apex Engine", verde-esmeralda neon) e até o ícone da marca herdou esse verde. Backoffice, site demo de cassino e materiais comerciais precisam de uma base comum, profissional, antes de serem desenhados — senão cada tela puxa para um lado. Cada jogo continua com tema próprio (ex.: design system "Pipa Crash" da SPEC-0010).

## Direcionadores da Decisão
- Vai a mercado: o comprador B2B (compliance, finanças, risco) avalia o backoffice numa demo comercial; precisa parecer software financeiro maduro.
- A cor da marca não pode colidir com as cores semânticas (verde = positivo/ok, vermelho = erro): o backoffice é cheio de valores e status.
- Acessibilidade WCAG 2.2 AA (ADR-0020) e modos claro e escuro.
- Custo zero até o lançamento: desenho no Stitch (gratuito), fontes livres (Inter, JetBrains Mono).
- O nome: "Apogeu" é o ponto mais alto da trajetória — como o multiplicador do crash.

## Opções Consideradas
- **A — Azul-noite + dourado**: base azul-marinho profundo, acento dourado; verde/vermelho/âmbar/ciano só para status.
- **B — Grafite + esmeralda** (evolução do rascunho "Apex Engine", sem neon).
- **C — Índigo + turquesa** (SaaS moderno).

## Resultado da Decisão
**Opção escolhida:** a definir por thomas após comparar a mesma tela (catálogo e limites) nas direções A e B no Stitch. Recomendação do Architect: **A**, porque o dourado conta a história do nome (o sol no apogeu), o azul-noite passa a confiança de fintech que o comprador B2B espera, e o acento não colide com as cores de status.

**Regras (verificáveis), valem para qualquer opção:**
- Uma identidade Apogeu (paleta, tipografia, regras) e três aplicações derivadas: backoffice (sóbrio, denso, claro e escuro), lobby do site demo de cassino (neutro, destaca os jogos) e materiais comerciais. Cada jogo mantém tema próprio.
- A cor da marca é usada só para ação principal, marca e navegação ativa; nunca para indicar valor positivo.
- Cores semânticas (sucesso, erro, alerta, informação/provably fair) são reservadas a status e sempre acompanhadas de ícone ou texto.
- Tipografia: Inter (interface) e JetBrains Mono (valores, multiplicadores, hashes, IDs); valores em formato pt-BR.
- Contraste mínimo WCAG 2.2 AA em todo texto; sem neon nem glow no backoffice.
- Toda tela nova de backoffice ou site demo nasce no Stitch com o design system derivado e é aprovada por thomas antes da spec (como a Pipa).

### Consequências
- **Boa**, porque backoffice, site demo e materiais saem coerentes, com cara de produto pronto para venda, e as decisões de tela deixam de ser refeitas a cada spec.
- **Ruim**, porque o rascunho "Apex Engine" e o ícone atual são descartados e o ícone/logo precisa ser refeito na nova paleta.

### Confirmação (G3)
Quando as specs de backoffice e site demo forem implementadas: tokens num pacote único do front com teste de contraste AA; regra de lint/arquitetura proibindo cor literal fora dos tokens; revisão visual no G4 contra a tela aprovada no Stitch.

## Prós e Contras das Opções
<!-- Decisão de tecnologia: matriz com critérios ponderados pelo contexto (references/greenfield.md). as-is: "N/A". -->
| Critério (peso) | A — azul-noite + dourado | B — grafite + esmeralda | C — índigo + turquesa |
|---|---|---|---|
| Percepção B2B / confiança (3) | ✅ fintech premium | ⚠️ técnico, lembra "cassino" no neon | ✅ SaaS |
| Não colide com cores de status (3) | ✅ dourado ≠ verde/vermelho | ❌ marca = verde de "positivo" | ✅ |
| Conta a história do nome (2) | ✅ o sol no apogeu | ❌ | ❌ genérico |
| Reaproveita o que existe (1) | ⚠️ componentes sim, cores não | ✅ | ⚠️ |
| Contraste AA claro/escuro (2) | ✅ (dourado só sobre azul-escuro ou com texto escuro) | ✅ | ✅ |

## Mais Informações
<!-- Fontes oficiais consultadas, versão estável/LTS e janela de suporte, com a data da verificação. Se não foi possível verificar, diga explicitamente. Links para specs relacionadas. -->
Projeto Stitch "Crash Provider — Plataforma" (13535251118740279031): design system "Apogeu — Backoffice (A: azul-noite + dourado)" (assets/2261923743028585933, criado em 2026-10-10) e o rascunho "Apex Engine Backoffice" (direção B). https://www.w3.org/TR/WCAG22/ — critérios 1.4.3 (contraste de texto) e 1.4.11 (contraste de componentes) — não verificado nesta sessão (2026-10-10). Relacionadas: ADR-0020 (acessibilidade), ADR-0021 (animação), design system "Pipa Crash" (SPEC-0010).
