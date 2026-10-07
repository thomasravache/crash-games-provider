---
id: ADR-0009
title: Estratégia e ferramentas de teste
status: accepted
origin: decision
date: 2026-10-07
pillars: [testes]
baseline: testes@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Comandos test, test_integration, test_e2e e coverage do sdd-config, executados pelo gate --run e pelo CI"
---

# ADR-0009 — Estratégia e ferramentas de teste

## Contexto e Problema
Dinheiro real, resultado certificado e tempo real: um bug em cash-out, liquidação ou justiça é perda financeira e regulatória. O usuário pediu foco forte em testes unitários, de integração e E2E.

## Direcionadores da Decisão
- Dependências reais em integração (PostgreSQL, Redis), nunca mocks delas.
- Jornadas críticas (`critical_journeys`) provadas por E2E no navegador real.
- Testes exigidos pelo mercado: vetores de justiça, simulação de RTP, carga de cash-out.
- Determinismo: relógio, aleatoriedade e IDs controlados.

## Opções Consideradas
- Conjunto A: xUnit v3 + Testcontainers, Vitest, Playwright, k6
- Conjunto B: NUnit + Testcontainers, Jest, Cypress, NBomber

## Resultado da Decisão
**Opção escolhida:** xUnit v3 + Testcontainers (.NET), Vitest (front), Playwright (E2E), k6 (carga), Stryker (mutação em Fairness e Wallet). São ferramentas maduras, gratuitas, com boa integração ao CI, e o Playwright cobre Chromium, WebKit (Safari iOS) e Firefox.

**Regras (verificáveis):**
- Níveis e obrigatoriedade conforme a skill: unitário, integração com dependências reais, contrato entre specs e E2E das jornadas de usuário.
- A suíte completa roda no CI em toda solicitação de mudança.
- As jornadas críticas estão em `critical_journeys` no sdd-config e cada uma tem ao menos um E2E.
- Teste instável é defeito: vai para quarentena por no máximo 7 dias com spec aberta para corrigir; nunca retry-até-passar.
- Cobertura das linhas alteradas ≥ 80% (≥ 95% nos módulos Fairness, Wallet e Betting).
- Tempo e aleatoriedade são injetados (`TimeProvider`, fonte de sementes) para testes determinísticos; nada de `Thread.Sleep`/`waitForTimeout` fixo.
- E2E não depende de pixels da cena: valida o HUD (DOM, papéis e rótulos) e o estado da rodada exposto por um gancho de teste só presente em builds de teste.
- Justiça: vetores fixos (semente → ponto de crash) e simulação de RTP com ≥ 10 milhões de rodadas no CI noturno.
- Mutação (Stryker) em Fairness e Wallet com score mínimo definido na spec do harness.
- Tag de rastreabilidade em todo teste: `[Trait("Spec","SPEC-NNNN:UT-01")]` (.NET) e `"SPEC-NNNN:E2E-01 …"` no nome (Vitest/Playwright).

**Ferramenta por nível:**
| Nível | Ferramenta / abordagem | Por quê | Onde roda |
|---|---|---|---|
| Unitário | xUnit v3 + AwesomeAssertions/Shouldly (escolher na spec do harness, ambos com licença aberta) no .NET; Vitest no front | rápidos, padrão da plataforma; Vitest nativo do Vite | local + CI |
| Integração | xUnit v3 + Testcontainers (PostgreSQL e Redis reais em containers) + `WebApplicationFactory`; ambiente completo via `Aspire.Hosting.Testing` | dependências reais, sem mocks da infraestrutura | local + CI |
| Contrato | Validação de esquema: respostas reais contra o OpenAPI gerado e os tipos de hub (ADR-0008) | backend é a fonte única; o CI detecta divergência | CI |
| E2E | Playwright (Chromium, WebKit mobile, Firefox) contra o ambiente subido pelo Aspire, com carteira demo | jornadas reais no navegador, inclusive Safari iOS | local + CI + staging |
| Carga | k6 (WebSocket/SignalR e HTTP): rajada de cash-outs no mesmo instante, N jogadores por sala | metas de desempenho do pilar desempenho-escala | staging (manual/agendado) |
| Acessibilidade | @axe-core/playwright no HUD e no backoffice | interface com pessoas reais; regras do pilar de conformidade | CI |

### Consequências
- **Boa**, porque integração com banco e Redis reais pega os bugs de dinheiro e concorrência que mocks escondem.
- **Boa**, porque vetores fixos e simulação de RTP já produzem a evidência que o laboratório pede.
- **Ruim**, porque o CI fica mais lento (containers, navegadores); mitigado por CI por caminho e cache.
- **Ruim**, porque a cena em canvas exige o gancho de estado para testes.

### Confirmação (G3)
Comandos `test`, `test_integration`, `test_e2e` e `coverage` do sdd-config, executados pelo `gate --run` e pelo CI.

## Prós e Contras das Opções
| Critério (peso) | xUnit v3 | NUnit | Playwright | Cypress | k6 | NBomber |
|---|---|---|---|---|---|---|
| Padrão do ecossistema (4) | ✅ | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| WebKit/Safari iOS (5) | — | — | ✅ | ⚠️ experimental | — | — |
| WebSocket em carga (4) | — | — | — | — | ✅ | ✅ |
| Licença livre (4) | ✅ | ✅ | ✅ | ✅ (runner) | ✅ | ⚠️ comercial em uso corporativo |

## Mais Informações
Referências neutras: Pirâmide de testes; references/testing.md da skill. Versões exatas (xUnit v3, Testcontainers, Playwright, Vitest, k6, Stryker) **não verificadas nesta sessão**: serão fixadas no lockfile pela spec do harness de testes, com fonte e data. Verificado em 2026-10-07.
