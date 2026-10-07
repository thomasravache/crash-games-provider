---
id: ADR-0020
title: "Acessibilidade e conformidade regulatória"
status: accepted
origin: decision
date: 2026-10-07
pillars: [acessibilidade-conformidade]
baseline: acessibilidade-conformidade@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "@axe-core/playwright no CI para HUD e backoffice; checklist de conformidade no G4; spec de conformidade obrigatória antes de dinheiro real"
---

# ADR-0020 — Acessibilidade e conformidade regulatória

## Contexto e Problema
O produto opera em mercado regulado (Brasil: Lei 14.790/2023 e portarias da SPA/MF), trata dados pessoais (LGPD) e tem interfaces usadas por pessoas reais (HUD do jogo e backoffice). Na fase demo não há dinheiro real, mas a arquitetura precisa estar pronta para certificação.

## Direcionadores da Decisão
- Certificação por laboratório credenciado antes de qualquer operação com dinheiro real.
- Acessibilidade das interfaces (HUD em DOM, ADR-0004).
- Separar o que vale já (demo) do que vale antes do dinheiro real.

## Opções Consideradas
- WCAG 2.2 AA no HUD e no backoffice + mapeamento regulatório numa spec de conformidade antes do dinheiro real
- Tratar conformidade só na fase de mercado (descartado: retrabalho de arquitetura)

## Resultado da Decisão
**Opção escolhida:** **WCAG 2.2 AA** no HUD e no backoffice desde já. A cena em canvas é decorativa: os estados da rodada (aguardando, em voo, crash e valor final) são anunciados no DOM por `aria-live`, sem anunciar cada tique. Uma **spec "Conformidade regulatória"** fica obrigatória antes de qualquer operação com dinheiro real.

**Normas aplicáveis (a detalhar na spec de conformidade):**
| Norma | Como é atendida |
|---|---|
| Lei 14.790/2023 e portarias SPA/MF | certificação do jogo e do gerador por laboratório credenciado; trilha de auditoria (ADR-0006, ADR-0007); requisitos específicos a mapear |
| LGPD (Lei 13.709/2018) | dados mínimos e pseudônimos, papéis definidos (ADR-0014) |
| Jogo responsável | suporte às ferramentas do operador (limites, pausas, autoexclusão) e exibição de tempo de sessão, conforme exigência a mapear |
| WCAG 2.2 AA | axe automático + revisão manual das jornadas críticas |

**Regras (verificáveis):**
- Interfaces atendem WCAG 2.2 nível AA nas jornadas do produto (HUD do jogo, verificação de rodada, backoffice).
- Verificação automática de acessibilidade no CI mais revisão manual das jornadas críticas.
- Normas legais e do setor que se aplicam estão listadas, com como cada uma é atendida.
- Termos, consentimento e direitos do titular dos dados implementados quando exigidos.
- Auditoria ou evidência de conformidade registrada quando o setor exigir.
- Modo demo sempre identificado visualmente; nenhuma operação com dinheiro real sem a spec de conformidade implementada.

### Consequências
- **Boa**, porque a certificação encontra uma arquitetura já auditável.
- **Ruim**, porque os requisitos exatos da SPA/MF e do laboratório ainda não foram mapeados; podem gerar emendas.

### Confirmação (G3)
@axe-core/playwright no CI; checklist de conformidade no G4; trava de "dinheiro real" ligada à spec de conformidade.

## Prós e Contras das Opções
| Critério (peso) | Já agora + spec antes do dinheiro real | Só na fase de mercado |
|---|---|---|
| Risco de retrabalho (5) | ✅ baixo | ❌ alto |
| Esforço agora (2) | ⚠️ | ✅ |

## Mais Informações
Referências neutras: WCAG 2.2; LGPD. Conteúdo das portarias SPA/MF e dos padrões GLI aplicáveis: **não verificado nesta sessão; levantar na spec de conformidade com assessoria regulatória**.
