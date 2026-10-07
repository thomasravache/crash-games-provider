---
id: ADR-0017
title: "Observabilidade com OpenTelemetry e SLOs das jornadas críticas"
status: accepted
origin: decision
date: 2026-10-07
pillars: [observabilidade]
baseline: observabilidade@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "ServiceDefaults em todo serviço (teste de arquitetura); teste de integração do health check; teste de mascaramento de dados em logs"
---

# ADR-0017 — Observabilidade com OpenTelemetry e SLOs das jornadas críticas

## Contexto e Problema
Em um crash game, problemas aparecem como cash-out lento, rodada travada ou crédito não entregue, e custam dinheiro e reputação junto aos operadores. É preciso enxergar cada aposta de ponta a ponta. A hospedagem ainda não foi escolhida.

## Direcionadores da Decisão
- Padrão aberto, sem aprisionamento.
- Funciona local já (painel do Aspire) e em qualquer nuvem depois.
- Rastrear uma aposta do clique ao livro-razão.

## Opções Consideradas
- OpenTelemetry via ServiceDefaults; local no painel do Aspire; produção em backend compatível com OTLP (Grafana LGTM, Grafana Cloud ou o da nuvem) escolhido na spec de hospedagem
- SDK proprietário (Datadog/New Relic) desde já (descartado: custo e aprisionamento)

## Resultado da Decisão
**Opção escolhida:** **OpenTelemetry** (logs, métricas, traces) via `ServiceDefaults` do Aspire, exportando OTLP. Local: painel do Aspire. Produção: backend OTLP escolhido com a hospedagem. Métricas de negócio próprias: rodadas/s, apostas/s, cash-outs/s, latência de cash-out, RTP real por sala, falhas e reenvios de carteira.

**Regras (verificáveis):**
- Logs estruturados com ID de correlação que atravessa as chamadas (HTTP, SignalR, eventos Wolverine) e IDs de rodada, aposta e operador como atributos.
- Métricas de requisição (taxa, erro, duração) e de recurso para cada serviço.
- Traces distribuídos quando houver mais de um processo envolvido numa requisição (inclui chamadas à carteira do operador).
- Health check em cada serviço implantável (`/health`, `/alive`).
- SLI e SLO para as jornadas críticas, com orçamento de erro: aposta-cashout com disponibilidade ≥ 99,9% e latência conforme ADR-0019 (válido a partir da produção).
- Alertas disparam por SLO; cada alerta tem responsável e procedimento curto (runbook).
- Cada Plano de Rollout define o limite numérico que dispara rollback durante a observação do deploy.
- Nenhum dado pessoal, token ou semente em logs, métricas ou traces.
- Alerta de negócio: RTP real da sala fora da faixa esperada após N rodadas.

### Consequências
- **Boa**, porque desde o primeiro dia dá para ver o caminho de uma aposta no painel do Aspire.
- **Ruim**, porque alertas e SLOs só passam a valer com a hospedagem; até lá são definição, não operação.

### Confirmação (G3)
Teste de arquitetura (todo Host chama `AddServiceDefaults`); teste de integração do health check; teste de mascaramento; limite de rollback verificado no G6.

## Prós e Contras das Opções
| Critério (peso) | OpenTelemetry | SDK proprietário |
|---|---|---|
| Aprisionamento (4) | ✅ nenhum | ❌ |
| Custo agora (4) | ✅ zero (local) | ❌ |
| Recursos prontos (2) | ⚠️ montar painéis | ✅ |

## Mais Informações
Referências neutras: OpenTelemetry; Google SRE (SLI, SLO, orçamento de erro). ServiceDefaults do Aspire: https://devblogs.microsoft.com/aspire/aspire13/ (verificado em 2026-10-07). Backend de produção: **a decidir na spec de hospedagem**.
