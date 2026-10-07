---
id: ADR-0019
title: "Metas de desempenho e escala"
status: accepted
origin: decision
date: 2026-10-07
pillars: [desempenho-escala]
baseline: desempenho-escala@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Teste de carga k6 com limiares (thresholds) que reprovam a execução; Lighthouse CI com orçamento de bundle no build do jogo"
---

# ADR-0019 — Metas de desempenho e escala

## Contexto e Problema
Hoje é demo, mas o produto precisa nascer com metas, porque elas definem decisões de arquitetura (difusão de mensagens, interpolação no cliente, extração do módulo Rounds). Sem números, "rápido" vira opinião.

## Direcionadores da Decisão
- Cash-out é o momento mais sensível: atraso = jogador perdendo dinheiro.
- Muitos jogadores em celulares médios e redes móveis.
- Fase demo local; metas de produção valem a partir da hospedagem.

## Opções Consideradas
- Metas em duas fases (lançamento/demo e alvo de 12 meses), verificadas por k6 e Lighthouse
- Sem metas até haver clientes (descartado: decisões de arquitetura ficam sem critério)

## Resultado da Decisão
**Opção escolhida:** **metas em duas fases**, com transmissão eficiente: o servidor envia o **início do voo e a função de crescimento**, e o cliente **interpola o multiplicador localmente**, com sincronização a cada ~200 ms. Assim a banda não cresce com a frequência de quadros.

| Métrica | Lançamento (demo) | Alvo 12 meses |
|---|---|---|
| Jogadores simultâneos por sala | ≥ 1.000 | ≥ 5.000 (sala compartilhada) |
| Jogadores simultâneos no total | ≥ 1.000 | ≥ 20.000 |
| Rajada de cash-out | 300 em 1 s sem perda | 2.000 em 1 s sem perda |
| Cash-out: recebimento → confirmação persistida (servidor) | p99 ≤ 50 ms | p99 ≤ 50 ms |
| Aposta: recebimento → aceita (com carteira demo) | p99 ≤ 100 ms | p99 ≤ 100 ms + latência da carteira do operador |
| Evento de crash entregue a todos da sala | p99 ≤ 200 ms | p99 ≤ 200 ms |
| Carregamento do jogo (Android médio, 4G) | interativo ≤ 3 s | interativo ≤ 3 s |
| Bundle inicial do jogo (gzip, sem arte carregada sob demanda) | ≤ 1,5 MB | ≤ 1,5 MB |
| Quadros por segundo | 60 em Android médio, ≥ 30 em básico | idem |
| Backoffice (consultas) | p95 ≤ 500 ms | p95 ≤ 500 ms |

**Regras (verificáveis):**
- Metas de latência e throughput por jornada crítica nos requisitos não-funcionais das specs (tabela acima como referência).
- Teste de carga antes de releases que mudam caminhos críticos, com o resultado registrado (local via Aspire até a hospedagem; staging depois).
- Limites e quotas para proteger o sistema de picos e abuso (por jogador, operador e IP; aposta e cash-out com limite de taxa).
- Cache, filas e escala horizontal só quando uma meta exigir, registrados em ADR.
- Orçamento de bundle verificado no build; regressão acima do orçamento reprova o CI.

### Consequências
- **Boa**, porque os números orientam as decisões (interpolação no cliente, liderança por sala, extração de Rounds).
- **Ruim**, porque testes de carga locais não representam rede real; as metas de 12 meses só se comprovam em staging.

### Confirmação (G3)
k6 com thresholds (cenários: N jogadores por sala via SignalR, rajada de cash-out); Lighthouse CI e orçamento de bundle no build do jogo.

## Prós e Contras das Opções
| Critério (peso) | Metas em duas fases | Sem metas |
|---|---|---|
| Critério para arquitetura (5) | ✅ | ❌ |
| Esforço agora (2) | ⚠️ | ✅ |

## Mais Informações
Referências neutras: métodos RED/USE. As metas são propostas iniciais: benchmarks de mercado **não verificados nesta sessão** e serão revisadas com dados reais do teste de carga.
