---
id: ADR-0001
title: Backend em monólito modular com vertical slices
status: accepted
origin: user
date: 2026-10-07
pillars: [arquitetura]
baseline: arquitetura@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes de arquitetura (ArchUnitNET) em tests/Architecture.Tests, categoria Architecture, no CI (G3)"
---

# ADR-0001 — Backend em monólito modular com vertical slices

## Contexto e Problema
Provedor B2B de crash games: um núcleo (rodada, justiça do resultado, apostas, carteira) que será reaproveitado por vários jogos, operado inicialmente por uma pessoa e destinado a certificação por laboratório (GLI/BMM) e à regulação SPA/MF. Sem fronteiras explícitas e verificadas, regras de dinheiro e de resultado se espalham pelo código, ficam difíceis de auditar e caras de mudar.

## Direcionadores da Decisão
- Escolha explícita do usuário: monólito modular (linha Milan Jovanović), vertical slices e Aspire.
- Código de justiça e de dinheiro precisa ser isolado e auditável.
- Equipe pequena: um único deploy é mais barato de operar e testar que microsserviços.
- O módulo de tempo real (Rounds) pode precisar escalar separado no futuro.

## Opções Consideradas
- Monólito modular + vertical slices dentro de cada módulo (escolha do usuário)
- N/A — escolha do usuário registrada sem reabrir alternativas (microsserviços e Clean Architecture em camadas por módulo foram descartados na conversa por custo operacional e cerimônia)

## Resultado da Decisão
**Opção escolhida:** monólito modular com vertical slices, porque o usuário escolheu e o modelo atende os direcionadores: fronteiras verificáveis, um deploy, extração futura barata.

**Módulos iniciais:**
| Módulo | Responsabilidade | Estilo interno |
|---|---|---|
| Rounds | ciclo da rodada (aberta → voo → crash → liquidação), relógio, multiplicador, salas e sua configuração de jogo (RTP, limite máximo, tempos) | domínio rico (máquina de estados) + slices |
| Fairness | sementes, cadeia de hashes, cálculo do ponto de crash, verificação | domínio puro, sem I/O; armazenamento cifrado das sementes isolado em `Fairness.Infrastructure` |
| Betting | apostar, cash-out manual/automático, aposta dupla, limites | slices + regras de domínio |
| Wallet | integração com a carteira do operador, livro-razão, idempotência, conciliação | domínio rico + adaptadores |
| Operators | operadores, limites e moedas, associação operador → sala, chaves de API, lançamento de jogo | slices simples |
| Backoffice | consultas, relatórios, auditoria | slices de leitura |

**Estrutura:** `src/backend/Modules/<Modulo>/<Modulo>.{Domain?,Features,Infrastructure,Contracts}` + `src/backend/Host` + `src/backend/BuildingBlocks`. Dentro de `Features`, uma pasta por caso de uso (`PlaceBet/`, `CashOut/` …) com endpoint, handler, validação.

**Regras (verificáveis):**
- Cada módulo expõe só o projeto `<Modulo>.Contracts` (consultas, comandos públicos, eventos de integração); os demais módulos só referenciam esse projeto.
- Nenhum módulo referencia `Features`, `Domain` ou `Infrastructure` de outro módulo.
- `Fairness.Domain` (cálculo e verificação) não depende de nenhuma infraestrutura (sem EF Core, Redis, HTTP, relógio do sistema); só `Fairness.Infrastructure` persiste sementes.
- `Domain` de qualquer módulo não referencia ASP.NET Core, EF Core nem Wolverine.
- Comunicação assíncrona entre módulos só por eventos de integração publicados com outbox (ADR-0002).
- Cada módulo é dono do seu schema no banco; sem joins nem FKs entre schemas (ADR-0010).
- Não há ciclos de dependência entre módulos.
- Mudar uma fronteira exige um ADR novo (que substitui este).

### Consequências
- **Boa**, porque regras de dinheiro e de resultado ficam isoladas, auditáveis e testáveis sozinhas.
- **Boa**, porque um único deploy simplifica operação, CI e testes de integração.
- **Boa**, porque Rounds pode ser hospedado em processo separado sem reescrever (mesmo código, outro Host).
- **Ruim**, porque disciplina de fronteira custa projetos extras e testes de arquitetura desde o primeiro dia.
- **Ruim**, porque todos os módulos escalam juntos até haver extração.

### Confirmação (G3)
Testes de arquitetura com ArchUnitNET em `tests/Architecture.Tests` (`[Trait("Category","Architecture")]`), rodando no CI, cobrindo cada regra acima.

## Prós e Contras das Opções
N/A — escolha do usuário (origin: user). Riscos registrados em Consequências.

## Mais Informações
- Referências neutras: C4 model; Modular Monolith (Milan Jovanović, Kamil Grzybek).
- Specs relacionadas: épico de fundação.
- Verificado em 2026-10-07.
