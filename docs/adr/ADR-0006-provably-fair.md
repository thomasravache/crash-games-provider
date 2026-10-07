---
id: ADR-0006
title: "Justiça do resultado: provably fair sobre gerador criptográfico"
status: accepted
origin: decision
date: 2026-10-07
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes unitários com vetores conhecidos (Fairness.Tests) + simulação de RTP no CI noturno; teste de arquitetura: Fairness.Domain sem I/O"
---

# ADR-0006 — Justiça do resultado: provably fair sobre gerador criptográfico

## Contexto e Problema
O ponto de crash precisa ser imprevisível, auditável por laboratório (GLI/BMM, exigência SPA/MF) e, para o público cripto, verificável pelo próprio jogador. O resultado nunca pode depender das apostas da rodada.

## Direcionadores da Decisão
- Certificação do gerador e do algoritmo por laboratório credenciado.
- Verificação independente pelo jogador (padrão de mercado em crash games).
- Determinismo para testes e auditoria.

## Opções Consideradas
- Provably fair (cadeia de hashes HMAC-SHA256) com semente gerada por gerador criptográfico seguro
- Apenas gerador certificado, sem verificação pelo jogador
- Gerador comum com semente (descartado: previsível)

## Resultado da Decisão
**Opção escolhida:** **provably fair sobre gerador criptográfico**: por sala, uma cadeia de sementes do servidor gerada com `RandomNumberGenerator` e encadeada por hash, consumida do fim para o começo. O hash da semente da próxima rodada é publicado antes das apostas; o ponto de crash = f(HMAC-SHA256(semente do servidor, semente pública da rodada), RTP da sala). O algoritmo exato fica na spec do módulo Fairness e é congelado como contrato.

**Regras (verificáveis):**
- O ponto de crash é função pura de (semente do servidor, semente pública, RTP); mesmas entradas → mesmo resultado, sempre.
- O compromisso (hash) da semente é publicado antes da abertura das apostas; a semente é revelada depois do crash.
- Nenhum dado de aposta, jogador ou saldo entra no cálculo.
- Sementes do servidor só são geradas por `RandomNumberGenerator` (nunca `System.Random`).
- Sementes não reveladas são segredo: nunca aparecem em log, trace ou resposta de API.
- O RTP efetivo, medido por simulação de milhões de rodadas, fica dentro da tolerância definida na spec do configurado.
- O verificador público (página/rotina de verificação) usa o mesmo algoritmo, testado com os mesmos vetores.

### Consequências
- **Boa**, porque atende o laboratório e o jogador cripto com o mesmo mecanismo.
- **Boa**, porque vetores de teste conhecidos tornam o algoritmo auditável e à prova de regressão.
- **Ruim**, porque trocar o algoritmo depois exige nova certificação e nova cadeia; é decisão de alto custo.
- **Ruim**, porque a guarda das sementes vira requisito de segurança (pilar segredos-dados).

### Confirmação (G3)
Testes unitários com vetores fixos; simulação de RTP; teste de arquitetura (`Fairness.Domain` sem I/O, proibição de `System.Random` no módulo); varredura de logs nos testes de integração para garantir que as sementes não vazam.

## Prós e Contras das Opções
| Critério (peso) | Provably fair + gerador seguro | Só gerador certificado | Gerador comum |
|---|---|---|---|
| Certificável (5) | ✅ | ✅ | ❌ |
| Verificável pelo jogador (4) | ✅ | ❌ | ❌ |
| Imprevisibilidade (5) | ✅ | ✅ | ❌ |
| Complexidade (2) | ⚠️ cadeia de sementes | ✅ | ✅ |

## Mais Informações
- `System.Security.Cryptography.RandomNumberGenerator` e `HMACSHA256` (.NET 10, documentação Microsoft Learn).
- Exigências específicas do laboratório (GLI-19/GLI-11 ou equivalente) e da SPA/MF: **não verificadas nesta sessão**, a confirmar na spec de conformidade.
- Verificado em 2026-10-07.
