---
id: ADR-0028
title: "Operação do Wolverine: geração de código, ciclo de vida e armazenamento"
status: accepted
origin: decision
date: 2026-10-10
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "CI roda `codegen test` do Wolverine (código gerado atualizado); teste de arquitetura: só o Host configura o Wolverine e nenhum módulo para/inicia seus hosted services; IT de arranque com banco indisponível"
---

# ADR-0028 — Operação do Wolverine: geração de código, ciclo de vida e armazenamento

<!-- Formato MADR (Markdown Architectural Decision Records). status: proposed | accepted | rejected | deprecated | superseded — vira accepted quando a spec que o introduz é aprovada (H1); superseded exige superseded_by. origin: decision (recomendada: compare 2–3 opções e cite fontes com data, ou declare "não verificado") | user (escolhida pelo usuário: respeite, sem reabrir a discussão, e registre riscos e custo) | as-is (documenta um padrão que já existe no projeto). enforced_by: teste de arquitetura que garante a decisão (G3), ou "N/A — motivo". -->

## Contexto e Problema
A SPEC-0022 fez a primeira ligação do Wolverine (ADR-0002) e, para funcionar, criou padrões sem decisão registrada: código de handlers pré-gerado dentro do módulo Betting, `ServiceLocationPolicy.AlwaysAllowed` global, o módulo Rounds assumindo o ciclo de vida do Wolverine e o schema de mensagens criado só em Development. O review independente (G4) pediu um ADR.

## Direcionadores da Decisão
- WolverineFx 6.48.1 não traz mais o compilador em tempo de execução (exigiria o pacote WolverineFx.RuntimeCompilation, fora do Directory.Packages.props).
- Outbox transacional obrigatório (ADR-0002) e Host que precisa subir mesmo com o banco ainda indisponível (testes, geração do OpenAPI no build).
- Módulos não podem depender do ciclo de vida de outro módulo (ADR-0001).

## Opções Consideradas
- **A — Código pré-gerado num assembly neutro do Host, ciclo de vida do Wolverine no Host (ServiceDefaults), checagem `codegen test` na CI.**
- **B — Compilação em tempo de execução** (adicionar WolverineFx.RuntimeCompilation).
- **C — Manter como está** (código no Betting, ciclo de vida no Rounds).

## Resultado da Decisão
**Opção escolhida:** **A**, pela autorização de thomas para seguir pelo recomendado (2026-10-10): sem pacote novo, arranque previsível e nenhum módulo dono do ciclo de vida de outro.

**Regras (verificáveis):**
- O código gerado dos handlers fica em `src/backend/Host/Internal/Generated/**` (TypeLoadMode.Static, `ApplicationAssembly` = Host); regenerado com `dotnet run --project src/backend/Host -- codegen write`; a CI roda `codegen test` e falha se estiver desatualizado.
- O Host (por extensão em ServiceDefaults/BuildingBlocks) é o único dono do ciclo de vida do Wolverine: inicia a mensageria quando o PostgreSQL responde e expõe `IMessagingReadiness` aos módulos; nenhum módulo para ou substitui hosted services de outro.
- `ServiceLocationPolicy`: `AllowedButWarn` com lista explícita dos serviços resolvidos por fábrica, em vez de `AlwaysAllowed`.
- Schema de mensagens (`wolverine`): criado automaticamente só em Development; nos demais ambientes por passo de migração do pipeline — item da SPEC-0012.

### Consequências
- **Boa**, porque o próximo handler (cash-out, liquidação) segue um padrão único, verificado na CI.
- **Ruim**, porque mudar um handler exige regenerar o código (a CI avisa).

### Confirmação (G3)
`codegen test` no job backend da CI; teste em Architecture.Tests: somente o assembly do Host referencia a configuração do Wolverine e nenhum módulo manipula `IHostedService` de outro assembly.

## Prós e Contras das Opções
<!-- Decisão de tecnologia: matriz com critérios ponderados pelo contexto (references/greenfield.md). as-is: "N/A". -->
| Critério | A | B | C |
|---|---|---|---|
| Sem pacote novo | ✅ | ❌ | ✅ |
| Fronteira dos módulos (ADR-0001) | ✅ | ✅ | ❌ |
| Arranque previsível | ✅ | ⚠️ compila no arranque | ⚠️ |

## Mais Informações
<!-- Fontes oficiais consultadas, versão estável/LTS e janela de suporte, com a data da verificação. Se não foi possível verificar, diga explicitamente. Links para specs relacionadas. -->
Documentação do Wolverine sobre geração de código estática e `codegen write/test` — não verificado nesta sessão; comportamento observado pelo Implementer da SPEC-0022 com WolverineFx 6.48.1 (2026-10-10).
