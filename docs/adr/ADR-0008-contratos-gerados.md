---
id: ADR-0008
title: Contratos gerados do backend para o front-end
status: accepted
origin: decision
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Job de CI regenera os contratos e falha se houver diferença (git diff --exit-code); testes de contrato (CT) por spec"
---

# ADR-0008 — Contratos gerados do backend para o front-end

## Contexto e Problema
Com C# no servidor e TypeScript no navegador, tipos duplicados à mão divergem silenciosamente, e o jogo quebra em produção numa mensagem de rodada ou de aposta.

## Direcionadores da Decisão
- Fonte única da verdade no backend.
- Divergência detectada no CI, não em produção.
- Cobrir REST (operadores, backoffice) e tempo real (SignalR).

## Opções Consideradas
- Gerar a partir do backend: OpenAPI (ASP.NET Core 10 nativo) → cliente TS; hubs SignalR → tipos TS por gerador
- Esquema neutro escrito à mão (JSON Schema/AsyncAPI) gerando os dois lados
- Tipos escritos à mão nos dois lados (descartado)

## Resultado da Decisão
**Opção escolhida:** **gerar a partir do backend**. REST: documento OpenAPI gerado no build (`Microsoft.AspNetCore.OpenApi`) → cliente/tipos TypeScript em `src/frontend/packages/contracts`. Tempo real: interfaces fortemente tipadas dos hubs → tipos TS por gerador. A ferramenta exata de cada gerador será escolhida na spec de fundação de contratos, comparando 2 opções e registrando a escolha neste ADR por emenda.

**Regras (verificáveis):**
- `packages/contracts` é 100% gerado; o CI regenera e falha se houver diferença.
- O documento OpenAPI e os tipos de hub são versionados; mudança incompatível exige nova versão de contrato (emenda na spec).
- A API pública para operadores tem versão na rota (`/v1/...`) e changelog próprio.
- Valores monetários e multiplicadores trafegam como **string decimal** no JSON (ex.: `"10.50"`, `"2.47"`), nunca como número, e viram tipos marcados (`Money`, `Multiplier`) no TypeScript; o cliente só formata, nunca calcula dinheiro.
- Testes de contrato (CT) validam respostas reais contra o esquema publicado.

### Consequências
- **Boa**, porque quebra de contrato vira erro de compilação no front e falha de CI.
- **Ruim**, porque o pipeline ganha um passo de geração, e o gerador de hubs SignalR é menos padronizado que o de OpenAPI.

### Confirmação (G3)
Job `contracts-drift` no CI; CT nas specs que publicam ou consomem contrato.

## Prós e Contras das Opções
| Critério (peso) | Gerar do backend | Esquema neutro à mão | À mão nos dois lados |
|---|---|---|---|
| Fonte única (5) | ✅ código | ✅ esquema | ❌ |
| Esforço contínuo (4) | ✅ baixo | ⚠️ manter esquema | ❌ alto |
| Cobertura de tempo real (3) | ⚠️ gerador de hub | ✅ AsyncAPI | ❌ |

## Mais Informações
- OpenAPI nativo no ASP.NET Core — https://learn.microsoft.com/aspnet/core/fundamentals/openapi/overview — geradores específicos (cliente TS e hubs SignalR) **não verificados nesta sessão**, a decidir na spec de contratos.
- Verificado em 2026-10-07.
