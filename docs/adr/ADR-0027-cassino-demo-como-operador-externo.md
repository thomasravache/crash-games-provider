---
id: ADR-0027
title: Cassino demo como operador externo
status: proposed
origin: decision
date: 2026-10-10
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Teste de arquitetura: o projeto do cassino demo não referencia nenhum projeto da Apogeu (só HTTP); teste de build: nenhum segredo HMAC no bundle do navegador"
---

# ADR-0027 — Cassino demo como operador externo

<!-- Formato MADR (Markdown Architectural Decision Records). status: proposed | accepted | rejected | deprecated | superseded — vira accepted quando a spec que o introduz é aprovada (H1); superseded exige superseded_by. origin: decision (recomendada: compare 2–3 opções e cite fontes com data, ou declare "não verificado") | user (escolhida pelo usuário: respeite, sem reabrir a discussão, e registre riscos e custo) | as-is (documenta um padrão que já existe no projeto). enforced_by: teste de arquitetura que garante a decisão (G3), ou "N/A — motivo". -->

## Contexto e Problema
O site demo de cassino (SPEC-0035) precisa assinar chamadas de operador (HMAC, SPEC-0009) sem expor o segredo no navegador, e deve provar a integração como um cassino real faria.

## Direcionadores da Decisão
- O segredo HMAC nunca pode ir ao navegador.
- O demo não pode usar atalhos internos: se funciona no demo, funciona num cassino real.
- Mesma stack (.NET 10, ADR-0002), sem custo.

## Opções Consideradas
- **A — Projeto .NET mínimo separado (`DemoCasino`) no AppHost**, falando com a Apogeu só por HTTP.
- **B — Rotas do cassino dentro do Host da Apogeu.**
- **C — Assinar no navegador** (descartada: expõe o segredo).

## Resultado da Decisão
**Opção escolhida:** **A** (thomas, 2026-10-10), porque prova a integração de verdade e mantém provedor e cassino separados.

**Regras (verificáveis):**
- `src/demo/DemoCasino` (ASP.NET Core Minimal API) é o backend do `demo-site`; só HTTP para a Apogeu, nenhuma referência a projetos `src/backend/**`.
- Credenciais do operador demo vêm de parâmetros do Aspire/user-secrets, nunca do repositório nem do bundle.
- Carteira em duas fases: (1) carteira demo da Apogeu; (2) o cassino implementa o contrato público da carteira (docs/integration/operator-wallet-api.md) e a Apogeu ganha o adaptador seamless.

### Consequências
- **Boa**, porque o demo é um cliente real do contrato público e serve de referência para integradores.
- **Ruim**, porque é mais um projeto no AppHost para manter.

### Confirmação (G3)
Teste em Architecture.Tests: assemblies do DemoCasino sem referência a `Apogeu.*` exceto contratos públicos gerados; teste de build do demo-site sem segredo no bundle.

## Prós e Contras das Opções
<!-- Decisão de tecnologia: matriz com critérios ponderados pelo contexto (references/greenfield.md). as-is: "N/A". -->
| Critério | A | B | C |
|---|---|---|---|
| Prova a integração real | ✅ | ⚠️ | ❌ |
| Segredo protegido | ✅ | ✅ | ❌ |
| Simplicidade | ⚠️ | ✅ | ✅ |

## Mais Informações
<!-- Fontes oficiais consultadas, versão estável/LTS e janela de suporte, com a data da verificação. Se não foi possível verificar, diga explicitamente. Links para specs relacionadas. -->
Decisão de produto de thomas (2026-10-10) sobre as perguntas Q1–Q3 da SPEC-0035. Sem fonte externa: não verificado (decisão interna de arquitetura).
