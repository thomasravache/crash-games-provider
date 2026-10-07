---
id: ADR-0005
title: Tempo real e salas de rodada
status: accepted
origin: decision
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes de integração (Testcontainers Redis) para liderança e cash-out com horário do servidor; teste de arquitetura: o hub não contém regra de negócio"
---

# ADR-0005 — Tempo real e salas de rodada

## Contexto e Problema
Uma rodada de crash é um evento compartilhado em tempo real: todos os jogadores de uma sala veem o mesmo multiplicador, e o cash-out precisa ser decidido de forma justa sob latência. É preciso decidir também se a rodada é global (todos os operadores) ou por operador.

## Direcionadores da Decisão
- Justiça e certificação: o servidor decide; o cliente só exibe.
- Escala horizontal sem duas rodadas divergentes.
- Flexibilidade comercial: começar isolado por operador e poder juntar operadores depois.
- RTP pode variar por jurisdição ou operador.

## Opções Consideradas
- **Salas configuráveis** (rodada pertence a uma sala; operadores são associados a salas)
- Rodada global fixa
- Rodada fixa por operador

## Resultado da Decisão
**Opção escolhida:** **salas configuráveis**, transmitidas por **SignalR com backplane Redis**, com **um líder por sala** (trava distribuída no Redis) conduzindo o relógio da rodada. Global ou por operador vira configuração: começa com uma sala por operador; salas compartilhadas reúnem operadores com a mesma configuração (jurisdição, RTP, moedas).

**Regras (verificáveis):**
- Cada rodada pertence a exatamente uma sala; uma sala tem uma única configuração de RTP, jurisdição e moedas.
- No máximo uma rodada ativa por sala, conduzida por um único líder; perda de liderança encerra a condução sem duplicar a rodada.
- O instante do cash-out é o horário do servidor no recebimento do comando, comparado ao instante do crash; nunca o horário informado pelo cliente.
- A tolerância de latência do cash-out é um parâmetro de contrato versionado, não um valor solto no código.
- O hub SignalR só traduz mensagens para comandos e consultas dos módulos; não contém regra de negócio.
- Jogadores de operadores diferentes na mesma sala aparecem anonimizados nas apostas ao vivo.

### Consequências
- **Boa**, porque compartilhar ou isolar salas é configuração, sem reescrita.
- **Boa**, porque cada sala tem RTP único e trilha de auditoria por operador.
- **Ruim**, porque liderança distribuída e recuperação de rodada interrompida exigem testes de integração cuidadosos (pilar resiliência).
- **Ruim**, porque uma sala muito cheia concentra difusão de mensagens num líder; a extração de Rounds para processo próprio é a saída prevista (ADR-0001).

### Confirmação (G3)
Testes de integração com Redis real (liderança, failover, cash-out no limite do crash); teste de arquitetura para o hub.

## Prós e Contras das Opções
| Critério (peso) | Salas configuráveis | Global fixa | Por operador fixa |
|---|---|---|---|
| Sala cheia / prova social (4) | ✅ quando compartilhada | ✅ | ❌ |
| RTP por jurisdição (5) | ✅ uma sala por config | ❌ RTP único | ✅ |
| Isolamento de falha/dados (4) | ✅ configurável | ❌ | ✅ |
| Complexidade (3) | ⚠️ abstração extra pequena | ✅ | ✅ |

## Mais Informações
- ASP.NET Core SignalR com backplane Redis — https://learn.microsoft.com/aspnet/core/signalr/redis-backplane — versão acompanha o .NET 10 (ADR-0002); detalhes do backplane no .NET 10 **não verificados nesta sessão**. Verificado em 2026-10-07.
