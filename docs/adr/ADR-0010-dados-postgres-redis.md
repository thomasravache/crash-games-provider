---
id: ADR-0010
title: PostgreSQL e Redis, schema por módulo e migrações
status: accepted
origin: decision
date: 2026-10-07
pillars: [dados-migracoes]
baseline: dados-migracoes@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Teste de integração aplica todas as migrações num PostgreSQL real (Testcontainers) e roda a versão anterior contra o schema novo; teste de arquitetura: cada DbContext só mapeia o próprio schema"
---

# ADR-0010 — PostgreSQL e Redis, schema por módulo e migrações

## Contexto e Problema
Apostas e livro-razão exigem transações e consistência forte; a rodada ao vivo exige estado de baixa latência e difusão de eventos. Os módulos precisam ter dados isolados para respeitar as fronteiras (ADR-0001).

## Direcionadores da Decisão
- Dinheiro: ACID, trilha imutável.
- Tempo real: estado da rodada, liderança e backplane SignalR.
- Licenças abertas e serviço gerenciado disponível em qualquer nuvem.

## Opções Consideradas
- PostgreSQL + Redis (ou Valkey, compatível)
- SQL Server + Redis
- PostgreSQL só (sem cache)

## Resultado da Decisão
**Opção escolhida:** **PostgreSQL** (fonte da verdade, um schema por módulo, EF Core com migrações por módulo; outbox do Wolverine no mesmo banco) + **Redis** (estado da rodada ao vivo, trava de liderança, backplane SignalR), mantendo compatibilidade com **Valkey** para não ficar preso a licença.

**Regras (verificáveis):**
- Migrações versionadas no repositório e executadas pelo pipeline.
- Toda migração é compatível com a versão anterior da aplicação (expand, migrar, contract).
- Migrações são testadas em testes de integração contra o banco real.
- Cada migração destrutiva tem plano de rollback de dados documentado na spec.
- Cada módulo tem seu próprio schema e DbContext; sem FK nem join entre schemas.
- Redis nunca é fonte da verdade de dinheiro: tudo que afeta saldo ou resultado é persistido no PostgreSQL antes de ser confirmado ao jogador.
- Valores monetários em `numeric` com escala definida na spec; nunca `float`.

### Consequências
- **Boa**, porque PostgreSQL é aberto, gerenciado em todas as nuvens e forte em transações.
- **Boa**, porque perder o Redis não perde dinheiro (só a rodada em andamento, tratada pelo pilar resiliência).
- **Ruim**, porque expand/contract exige disciplina e mais de um deploy por mudança destrutiva.

### Confirmação (G3)
Teste de integração de migração; Plano de Rollout das specs com mudança de schema; teste de arquitetura sobre DbContexts.

## Prós e Contras das Opções
| Critério (peso) | PostgreSQL + Redis | SQL Server + Redis | Só PostgreSQL |
|---|---|---|---|
| Custo/licença (4) | ✅ | ⚠️ licença | ✅ |
| Latência da rodada ao vivo (5) | ✅ | ✅ | ❌ |
| Gerenciado em qualquer nuvem (3) | ✅ | ⚠️ | ✅ |

## Mais Informações
Referências neutras: padrão expand/contract. Versões (PostgreSQL 18, Redis 8 com licença tripla incluindo AGPLv3, Valkey 8): **não verificadas nesta sessão; confirmar nas fontes oficiais antes do H1**. Verificado em 2026-10-07.
