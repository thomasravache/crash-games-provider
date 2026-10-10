---
id: ADR-0026
title: Papéis, escopo de dados e trilha de auditoria do backoffice
status: accepted
origin: decision
date: 2026-10-10
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Teste de arquitetura: todo endpoint /api/backoffice/* declara política de papel; IT por tela provando que usuário de operador só vê o próprio operador; trigger que recusa UPDATE/DELETE na trilha de auditoria"
---

# ADR-0026 — Papéis, escopo de dados e trilha de auditoria do backoffice

<!-- Formato MADR (Markdown Architectural Decision Records). status: proposed | accepted | rejected | deprecated | superseded — vira accepted quando a spec que o introduz é aprovada (H1); superseded exige superseded_by. origin: decision (recomendada: compare 2–3 opções e cite fontes com data, ou declare "não verificado") | user (escolhida pelo usuário: respeite, sem reabrir a discussão, e registre riscos e custo) | as-is (documenta um padrão que já existe no projeto). enforced_by: teste de arquitetura que garante a decisão (G3), ou "N/A — motivo". -->

## Contexto e Problema
O backoffice (SPEC-0034) será usado pela equipe Apogeu e pelos operadores (cassinos), com dados financeiros e de jogadores. Precisa de papéis claros, de isolamento entre operadores e de trilha de auditoria para compliance. Base existente: login Keycloak do backoffice (SPEC-0009), realm `apogeu`.

## Direcionadores da Decisão
- Um operador nunca pode ver dados de outro (risco comercial e de LGPD).
- Compliance exige saber quem alterou catálogo, limites, chaves e comissões.
- Reusar Keycloak (SPEC-0009), sem custo.

## Opções Consideradas
- **A — Um app, papéis no Keycloak e escopo pelo token** (claim `op` para usuários de operador).
- **B — Dois apps** (backoffice Apogeu e portal do operador).
- **C — Só equipe Apogeu**, relatórios enviados aos operadores por e-mail.

## Resultado da Decisão
**Opção escolhida:** **A** (thomas, 2026-10-10), porque entrega o padrão de mercado (operador com backoffice próprio no provedor) sem duplicar telas.

**Regras (verificáveis):**
- Papéis no realm `apogeu`: `apogeu-admin` (tudo), `apogeu-financeiro` (painel, relatórios, comissões — leitura de operadores), `apogeu-suporte` (leitura de rodadas, apostas e auditoria), `operador-gestor` e `operador-leitura` (sempre restritos ao operador da claim `op`; o gestor gera relatórios e vê a própria chave de API).
- Escopo: para papéis `operador-*`, o operador vem **só do token** — parâmetro de operador na requisição é ignorado ou recusado com 403.
- Escrita de catálogo, limites, operadores, chaves e comissão: **só `apogeu-admin`**.
- Toda escrita grava evento imutável `{quem, papel, quando, ação, alvo, antes, depois}` no schema `backoffice` (trigger recusa UPDATE/DELETE); leitura da trilha só para `apogeu-admin`.
- Comissão: percentual do GGR (revenue share) por operador, com vigência; o fechamento mensal usa o percentual vigente no mês.

### Consequências
- **Boa**, porque um só app e um só lugar de regras de acesso, testado.
- **Ruim**, porque toda tela precisa de testes de escopo por papel.

### Confirmação (G3)
Teste em Architecture.Tests: todo endpoint sob `/api/backoffice/*` declara uma política de papel; IT de escopo em cada spec de tela; trigger testado em IT.

## Prós e Contras das Opções
<!-- Decisão de tecnologia: matriz com critérios ponderados pelo contexto (references/greenfield.md). as-is: "N/A". -->
| Critério | A | B | C |
|---|---|---|---|
| Padrão de mercado | ✅ | ✅ | ❌ |
| Custo de construção | ✅ um app | ❌ dois apps | ✅ |
| Risco de vazamento entre operadores | ⚠️ mitigado por testes | ✅ | ✅ |

## Mais Informações
<!-- Fontes oficiais consultadas, versão estável/LTS e janela de suporte, com a data da verificação. Se não foi possível verificar, diga explicitamente. Links para specs relacionadas. -->
Decisão de produto de thomas (2026-10-10) sobre as perguntas Q1–Q5 da SPEC-0034. Keycloak realm roles — não verificado nesta sessão; segue a configuração da SPEC-0009.
