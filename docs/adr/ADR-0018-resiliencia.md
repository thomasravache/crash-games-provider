---
id: ADR-0018
title: "Resiliência: rodada sempre termina, idempotência e recuperação"
status: accepted
origin: decision
date: 2026-10-07
pillars: [resiliencia]
baseline: resiliencia@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes de integração: queda do líder no meio do voo, timeout e duplicata da carteira, reinício do processo com outbox pendente"
---

# ADR-0018 — Resiliência: rodada sempre termina, idempotência e recuperação

## Contexto e Problema
Falhas no meio de uma rodada (queda do processo líder, Redis indisponível, carteira do operador lenta) não podem deixar apostas em limbo nem pagar em dobro. Backups e recuperação ficam para quando houver hospedagem, mas o comportamento do sistema sob falha precisa estar decidido já.

## Direcionadores da Decisão
- Dinheiro nunca some nem duplica.
- Regulador e laboratório exigem que toda rodada tenha desfecho auditável.

## Opções Consideradas
- Rodada determinística retomável por novo líder + liquidação idempotente + anulação com estorno como último recurso
- Anular sempre a rodada interrompida (mais simples, pior experiência)

## Resultado da Decisão
**Opção escolhida:** **retomada determinística**. O ponto de crash é pré-definido (ADR-0006) e o início do voo é persistido; um novo líder retoma a rodada no ponto correto, e os cash-outs são julgados pelo horário do servidor. Se a retomada não ocorrer dentro de um prazo, a rodada é **anulada com estorno** de todas as apostas pendentes. O prazo e a regra de anulação são **questão em aberto** a confirmar com a regulação.

**Regras (verificáveis):**
- Backup automático dos dados com restauração testada a cada trimestre — **ativa com a spec de hospedagem** (banco gerenciado com recuperação para um instante no tempo).
- RPO ≤ 5 min e RTO ≤ 1 h para produção (metas iniciais, revisadas com a hospedagem).
- Chamadas externas com timeout e retry com backoff; nunca espera indefinida (carteira do operador: timeout 2 s, estorno automático em resultado incerto, ADR-0007).
- Operações repetíveis são idempotentes (aposta, cash-out, débito, crédito, estorno, liquidação).
- Falha de dependência não crítica degrada a funcionalidade em vez de derrubar o sistema (ex.: chat e apostas ao vivo podem cair; aposta e cash-out não).
- Toda rodada atinge um estado terminal (liquidada ou anulada com estorno); rodada parada além do prazo gera alerta.
- O Redis não guarda nada que não possa ser reconstruído do PostgreSQL.

### Consequências
- **Boa**, porque uma queda no meio do voo não gera disputa com jogador nem com operador.
- **Ruim**, porque testar failover de líder e reprocessamento exige testes de integração mais elaborados.

### Confirmação (G3)
Testes de integração de falha (líder, Redis, carteira, outbox); teste de restauração registrado após a hospedagem.

## Prós e Contras das Opções
| Critério (peso) | Retomada determinística | Anular sempre |
|---|---|---|
| Experiência do jogador (4) | ✅ | ❌ |
| Simplicidade (3) | ⚠️ | ✅ |
| Auditabilidade (5) | ✅ | ✅ |

## Mais Informações
Referências neutras: Google SRE; Well-Architected (pilar de confiabilidade). Regras regulatórias para rodada interrompida (SPA/MF, GLI): **não verificadas nesta sessão**.
