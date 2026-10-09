---
id: ADR-0023
title: Recraft para a arte vetorial dos jogos
status: accepted
origin: user
date: 2026-10-09
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Revisão no G4 da spec de arte final (licença do plano e origem de cada asset registradas); regressão visual do ADR-0021 com imagens de referência aprovadas pelo usuário"
---

# ADR-0023 — Recraft para a arte vetorial dos jogos

## Contexto e Problema
A SPEC-0011 desenha a cena da Pipa em vetor no próprio código (pipa, nuvens, orla do Rio com morros). O resultado é limpo, mas não chega a uma ilustração pintada. A arte final ilustrada fica para a spec futura "Arte, som e animação final da Pipa". Falta decidir com que ferramenta gerar essa arte sem ilustrador, mantendo o motor de animação do ADR-0021 (PixiJS + GSAP).

## Direcionadores da Decisão
- Decisão do usuário (2026-10-09): usar o Recraft.
- A cena já é vetorial: o asset ideal é SVG, que entra no lugar do desenho em código sem trocar o motor e mantém o pacote leve (ADR-0019, ≤ 1,5 MB).
- Produto comercial B2B: o asset precisa de direito de uso comercial claro.

## Opções Consideradas
- N/A — escolha do usuário (origin: user). Também foram apresentadas: Scenario (modelo treinado no estilo do jogo), Adobe Firefly (treino em conteúdo licenciado) e Midjourney/Leonardo (exploração de direção de arte).

## Resultado da Decisão
**Opção escolhida:** **Recraft** para gerar a arte vetorial (SVG) dos jogos, começando pela Pipa.

**Regras (verificáveis):**
- Só assets gerados em **plano pago** do Recraft entram no repositório: no plano gratuito as imagens são públicas e sem licença comercial.
- Cada asset gerado é versionado em SVG com um registro de origem (ferramenta, modelo, data, plano, prompt) ao lado do arquivo.
- O SVG passa por otimização (ex.: SVGO) e entra pela cena via `GameAnimator` (ADR-0004, ADR-0021); a animação continua procedural.
- A conta e a chave de API do Recraft são do usuário: credenciais nunca ficam no repositório nem são digitadas pelo agente (segredos, ADR-0014).

### Consequências
- **Boa**, porque gera vetor nativo, que encaixa direto na cena atual e escala sem perder qualidade.
- **Boa**, porque o plano pago dá direito comercial às imagens geradas.
- **Ruim**, porque tem custo recorrente (assinatura ou cobrança por imagem via API) e depende dos termos do fornecedor, que podem mudar.
- **Ruim**, porque arte gerada só por IA pode não ter proteção de direito autoral em algumas jurisdições; retoque humano ajuda se o visual precisar ser exclusivo.

### Confirmação (G3)
Revisão no G4 da spec de arte final (registro de origem e plano de cada asset) e regressão visual do ADR-0021 (imagens de referência aprovadas pelo usuário).

## Prós e Contras das Opções
N/A — escolha do usuário.

## Mais Informações
- Plano gratuito: imagens públicas, sem uso comercial; planos pagos: direitos comerciais completos — blog oficial do Recraft (https://www.recraft.ai/blog/when-the-brief-says-commercial-not-just-beautiful) e review (https://www.toolsforhumans.ai/ai-tools/recraft-ai) — consultados em 2026-10-09. Fonte oficial é material do próprio fornecedor.
- Preços divergem entre fontes (plano Basic citado entre US$ 10 e US$ 20/mês; API em torno de US$ 0,08 por vetor) — **não verificado na página oficial de preços**; confirmar em https://www.recraft.ai/pricing ao assinar.
- Spec que vai aplicar este ADR: "Arte, som e animação final da Pipa" (futura). Relacionados: ADR-0004, ADR-0019, ADR-0021.
