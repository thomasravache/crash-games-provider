---
id: ADR-0007
title: Carteira do operador e livro-razão
status: accepted
origin: decision
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes de integração (Testcontainers PostgreSQL + servidor de carteira falso) para idempotência, estorno e reprocessamento; teste de arquitetura: só Wallet escreve no livro-razão"
---

# ADR-0007 — Carteira do operador e livro-razão

## Contexto e Problema
O saldo do jogador fica no operador; o provedor debita a aposta e credita o prêmio por API. Rede falha, timeouts acontecem e requisições se repetem: sem idempotência e registro imutável, há dinheiro duplicado ou perdido, e a conciliação fica impossível.

## Direcionadores da Decisão
- Dinheiro: consistência forte, idempotência, trilha imutável.
- Modelo B2B: integrar vários operadores e agregadores com um núcleo só.
- Modo demo sem dinheiro real usando o mesmo caminho.

## Opções Consideradas
- Carteira transparente (seamless wallet) do operador + livro-razão interno de partidas dobradas + adaptadores por operador/agregador
- Carteira de transferência (saldo mantido no provedor)
- Chamar a carteira do operador sem livro-razão interno (descartado: sem conciliação)

## Resultado da Decisão
**Opção escolhida:** **carteira transparente + livro-razão interno de partidas dobradas**, com uma **porta `IOperatorWallet`** e adaptadores: `DemoWallet` (fase pessoal), API própria do provedor (contrato publicado para operadores) e, depois, um adaptador por agregador.

**Regras (verificáveis):**
- Toda operação de carteira (débito, crédito, estorno) carrega um ID de transação único e determinístico; repetir a chamada nunca duplica o efeito, nem do nosso lado nem do operador.
- O livro-razão é só de inserção: lançamentos nunca são alterados nem apagados; correção é um lançamento novo.
- Cada lançamento tem partidas dobradas que somam zero.
- Aposta só é aceita na rodada depois de débito confirmado; débito com resultado incerto (timeout) gera estorno automático com nova tentativa até confirmação.
- Créditos com falha são reenviados (outbox) até confirmação; nenhum prêmio é descartado.
- Só o módulo Wallet escreve no livro-razão.
- Conciliação diária por operador entre livro-razão e relatório do operador.

### Consequências
- **Boa**, porque cada centavo é rastreável, com conciliação e auditoria prontas para o laboratório e para os operadores.
- **Boa**, porque o modo demo usa o mesmo fluxo, só com outro adaptador.
- **Ruim**, porque estorno e reenvio aumentam a complexidade e exigem testes de falha (timeouts, duplicatas).

### Confirmação (G3)
Testes de integração com PostgreSQL real e servidor de carteira falso, injetando timeout, erro e resposta duplicada; teste de arquitetura sobre quem escreve no schema `wallet`.

## Prós e Contras das Opções
| Critério (peso) | Seamless + livro-razão | Transferência | Seamless sem livro-razão |
|---|---|---|---|
| Padrão de mercado B2B (5) | ✅ | ⚠️ menos comum | ✅ |
| Conciliação/auditoria (5) | ✅ | ✅ | ❌ |
| Custo de implementação (3) | ⚠️ | ⚠️ | ✅ |

## Mais Informações
- Padrões: idempotency key, double-entry bookkeeping, transactional outbox.
- Formatos de API de agregadores (SoftSwiss, EveryMatrix etc.): **não verificados**, a tratar na spec de cada adaptador.
- Verificado em 2026-10-07.
