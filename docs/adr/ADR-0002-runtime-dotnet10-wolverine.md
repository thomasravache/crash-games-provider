---
id: ADR-0002
title: "Runtime do backend: .NET 10 LTS, ASP.NET Core e Wolverine"
status: accepted
origin: user
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Teste de arquitetura proíbe referências a MediatR, AutoMapper e MassTransit; global.json fixa o SDK 10"
---

# ADR-0002 — Runtime do backend: .NET 10 LTS, ASP.NET Core e Wolverine

## Contexto e Problema
O backend precisa de aritmética monetária exata, WebSocket robusto, gerador criptográfico na biblioteca padrão e desempenho previsível em picos de cash-out. Também precisa de mensageria em processo com outbox entre módulos, sem dependências com licença comercial.

## Direcionadores da Decisão
- Preferência do usuário por .NET (origin: user).
- Produto comercial: bibliotecas com licença permissiva (MIT/Apache).
- Suporte longo (LTS) até a fase de certificação.

## Opções Consideradas
- .NET 10 (escolha do usuário). Para mensageria/handlers: Wolverine (MIT) | interface própria mínima de handler + outbox próprio | MediatR/MassTransit (descartados: licença comercial desde 2025)

## Resultado da Decisão
**Opção escolhida:** .NET 10 LTS + ASP.NET Core Minimal APIs + **Wolverine** para handlers, mensageria em processo e outbox transacional no PostgreSQL, porque é MIT, já resolve o outbox de que os eventos entre módulos precisam e se adapta a vertical slices. Mapeamento manual (sem AutoMapper).

**Regras (verificáveis):**
- SDK fixado por `global.json` na linha 10.0.x; `TreatWarningsAsErrors` e `Nullable` habilitados.
- Valores monetários sempre `decimal` (nunca `double`/`float`) nos módulos Betting, Wallet e Rounds.
- Proibido referenciar MediatR, AutoMapper e MassTransit (licenças comerciais).
- Handlers ficam no slice do caso de uso; nada de pasta "Handlers" global.

### Consequências
- **Boa**, porque `decimal`, SignalR, `RandomNumberGenerator` e HMAC vêm da plataforma.
- **Boa**, porque o outbox do Wolverine garante entrega dos eventos entre módulos mesmo com queda do processo.
- **Ruim**, porque duas linguagens no repositório (C# e TypeScript), mitigado por contratos gerados (ADR-0008).
- **Ruim**, porque o Wolverine traz convenções próprias (descoberta de handlers por convenção) que o time precisa aprender. Se incomodar, a alternativa é uma interface própria mínima, decidida em novo ADR.

### Confirmação (G3)
Teste de arquitetura sobre as referências de pacote e de tipos (`double`/`float` em propriedades monetárias); `global.json` no repositório.

## Prós e Contras das Opções
| Critério (peso) | Wolverine | Handler próprio + outbox próprio | MediatR + MassTransit |
|---|---|---|---|
| Licença para produto comercial (5) | ✅ MIT | ✅ código próprio | ❌ comercial (MediatR ≥13, MassTransit ≥9) |
| Outbox pronto (4) | ✅ PostgreSQL | ❌ escrever e testar | ✅ |
| Simplicidade/curva (3) | ⚠️ convenções próprias | ✅ explícito | ✅ conhecido |
| Manutenção ativa (3) | ✅ 6.22.0 (jul/2026) | ⚠️ por nossa conta | ✅ |

## Mais Informações
- .NET 10 LTS: suporte até 14/11/2028; patch mais recente encontrado 10.0.12 (08/09/2026) — https://dotnet.microsoft.com/platform/support/policy/dotnet-core — verificado em 2026-10-07.
- WolverineFx 6.22.0 (23/07/2026, net10.0) — NuGet; licença MIT segundo fontes secundárias, **confirmar no repositório oficial** antes do H1.
- MediatR dual-licenciado desde 13.0.0 (jul/2025); MassTransit comercial a partir da v9 — fontes secundárias, verificado em 2026-10-07.
