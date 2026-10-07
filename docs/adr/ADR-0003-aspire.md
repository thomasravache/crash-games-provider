---
id: ADR-0003
title: Orquestração local e padrões de serviço com Aspire
status: accepted
origin: user
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Teste de integração do AppHost (Aspire.Hosting.Testing) sobe o ambiente e verifica health checks"
---

# ADR-0003 — Orquestração local e padrões de serviço com Aspire

## Contexto e Problema
O ambiente local tem várias peças (PostgreSQL, Redis, API .NET, apps Vite do jogo, backoffice e site demo). Sem um orquestrador, rodar e depurar tudo junto vira um script frágil, e telemetria fica para depois.

## Direcionadores da Decisão
- Escolha do usuário (origin: user).
- Um comando sobe tudo; telemetria (OpenTelemetry) ligada desde o primeiro dia.
- Suporte nativo a apps JavaScript/Vite.

## Opções Consideradas
- Aspire 13.x (escolha do usuário)
- N/A — Docker Compose foi citado só como alternativa e não foi reaberto

## Resultado da Decisão
**Opção escolhida:** Aspire 13.x com AppHost em C# (`src/backend/AppHost`) e `ServiceDefaults` compartilhado.

**Regras (verificáveis):**
- `dotnet run --project src/backend/AppHost` (ou `aspire run`) sobe PostgreSQL, Redis, API e os apps de front via `AddViteApp` com pnpm.
- Todo serviço .NET chama `AddServiceDefaults()`: OpenTelemetry (logs, métricas, traces), health checks `/health` e `/alive`, resiliência de HttpClient.
- Configuração vem do ambiente (parâmetros do AppHost / variáveis), nunca hardcoded.
- Testes de integração de ponta a ponta podem usar `Aspire.Hosting.Testing` para subir o ambiente real.

### Consequências
- **Boa**, porque o ambiente local reflete a topologia de produção e o painel do Aspire mostra traces desde o primeiro dia.
- **Boa**, porque o Aspire gera artefatos de publicação (Dockerfiles, manifestos) a partir do mesmo modelo.
- **Ruim**, porque o deploy de produção ainda precisa de decisão própria (pilar entrega); o Aspire não substitui IaC e pipeline.

### Confirmação (G3)
Teste de integração do AppHost verificando que todos os recursos ficam saudáveis.

## Prós e Contras das Opções
N/A — escolha do usuário.

## Mais Informações
- Aspire 13 (11/11/2025): JavaScript e Python de primeira classe; `AddViteApp` no pacote Aspire.Hosting.JavaScript; `AddNpmApp` removido — https://devblogs.microsoft.com/aspire/aspire13/ — verificado em 2026-10-07.
- Aspire 13.2 (21/04/2026) — https://devblogs.microsoft.com/aspire/aspire-bun-support-and-container-enhancements/
- Aspire para desenvolvedores JavaScript (12/01/2026) — https://devblogs.microsoft.com/aspire/aspire-for-javascript-developers
