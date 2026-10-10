---
id: SPEC-0020
title: "Catálogo de jogos e limites por operador e moeda"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0009]
consumes_contract: []
contract_version: 1
touches: ["src/backend/Modules/Operators/**", "tests/backend/Modules/Operators.Tests/**", "src/backend/BuildingBlocks/Contracts/**"]
adrs: [ADR-0001, ADR-0002, ADR-0004, ADR-0005, ADR-0015, ADR-0020]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0020 — Catálogo de jogos e limites por operador e moeda

## 1. Visão Geral
Cria o catálogo de jogos do provedor e os limites de aposta por operador e moeda. Um jogo passa a ser um registro (código, nome, app do cliente, tema e configuração padrão de sala), e o lançamento só aceita jogos habilitados para o operador. É a base para o segundo jogo ser praticamente configuração.

## 2. Motivação & Escopo
**Motivação:** "uma engine, vários jogos" (ADR-0004) precisa de um lugar no backend que diga quais jogos existem e com que regras; as apostas (SPEC-0022) precisam de limites por operador e moeda (decisão do usuário, 2026-10-09).

**Objetivos (dentro do escopo):**
- Catálogo no módulo Operators: jogo `pipa` semeado (código, nome, app `game-pipa`, tema `pipa`, padrão de sala: RTP 0,97, limite 10.000x, `growthK` 0,00006, apostas 6 s, pausa 3 s).
- Habilitação de jogo por operador; o operador demo tem `pipa` habilitado.
- Limites por operador **e por moeda**: aposta mínima, aposta máxima e ganho máximo por aposta. Padrão BRL: R$ 1,00 / R$ 1.000,00 / R$ 100.000,00. Só BRL habilitado neste épico, com o modelo pronto para multimoeda.
- Contrato interno `Operators.Contracts` (`IGameCatalog`, `IOperatorLimits`) consumido por Betting e pelo cliente.
- Lançamento (SPEC-0009) passa a recusar jogo inexistente ou não habilitado.

**Não-objetivos (fora do escopo):**
- Telas de backoffice para editar catálogo e limites (épico de backoffice); a configuração vem de seed e configuração.
- Outras moedas além de BRL (só o modelo).

## 3. Dependências
- **Implementações necessárias:** SPEC-0009 — módulo Operators, operador demo e lançamento.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; segue a implementação de referência do módulo Operators (SPEC-0009) e do Fairness (SPEC-0007): slices em `Features`, EF Core no schema do módulo, registro explícito no Host.

**Decisão:** catálogo e limites vivem no módulo Operators (ADR-0001 já atribui a ele "limites e moedas"). A configuração de sala usada pelo motor continua no módulo Rounds; o catálogo fornece o **padrão** para criar salas novas de um jogo. Valores monetários são `Money` (decimal) sempre acompanhados de `Currency` (código ISO 4217).

**Justificativa:** mantém a fronteira dos módulos e deixa o próximo jogo como dado, não código.

**Desvio do padrão existente:** Nenhum. Extensão compatível do contrato da SPEC-0009: o lançamento ganha uma resposta de erro nova (`GAME_NOT_AVAILABLE`).

**Alternativas descartadas:** módulo novo "Catalog" (multiplica fronteiras para pouco código); limites dentro do Betting (o ADR-0001 dá limites ao Operators).

**ADRs:** ADR-0001, ADR-0002, ADR-0004, ADR-0005, ADR-0015, ADR-0020.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** leitura de limites ≤ 1 ms com cache em memória invalidado por versão (usada a cada aposta) — UT-03.
- **Segurança:** `GET /api/v1/games` exige assinatura HMAC de operador (SPEC-0009) — IT-03.
- **Privacidade e dados pessoais:** N/A — sem dados de jogador.
- **Disponibilidade e resiliência:** N/A — dados de configuração locais.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** `Operators.Contracts` (v1) + REST. Os tipos de valor compartilhados `OperatorId`, `PlayerId` e `Currency` (ISO 4217) ficam em `BuildingBlocks.Contracts`, ao lado de `Money`, porque Carteira e Apostas também os usam.

```text
IGameCatalog
  GameDefinition? Find(GameCode code)
  bool IsEnabledFor(OperatorId op, GameCode code)
GameDefinition { Code:"pipa", Name:"Pipa", ClientApp:"game-pipa", Theme:"pipa",
                 DefaultSala:{ Rtp:0.97, Cap:Multiplier 10000.00, GrowthK:0.00006, BettingSeconds:6, PauseSeconds:3 },
                 Currencies:["BRL"] }
IOperatorLimits
  BetLimits Get(OperatorId op, Currency currency)       // erro CURRENCY_NOT_ENABLED se a moeda não estiver habilitada
BetLimits { Currency, MinBet:Money, MaxBet:Money, MaxWin:Money }
REST (HMAC de operador, SPEC-0009)
  GET /api/v1/games → 200 [{ code, name, currencies:[...] }]   // só jogos habilitados para o operador
POST /api/v1/operator/launch (SPEC-0009) — extensão compatível:
  400 GAME_NOT_AVAILABLE  (gameCode inexistente ou não habilitado para o operador)
  400 CURRENCY_NOT_ENABLED (moeda sem limites configurados)
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Jogo encontrado | `Find("pipa")` | definição com padrão de sala e moedas | UT-01 |
| Jogo inexistente | `Find("aviao")` | nulo | UT-01 |
| Limites padrão BRL | operador demo, BRL | 1,00 / 1.000,00 / 100.000,00 | UT-02 |
| Moeda não habilitada | operador demo, USD | erro CURRENCY_NOT_ENABLED | UT-02 |
| Cache de limites | 10.000 leituras | média ≤ 1 ms, atualiza quando a versão muda | UT-03 |
| Catálogo persistido | seed aplicado no Postgres real | `pipa` habilitado para o operador demo | IT-01 |
| Lançamento de jogo indisponível | `gameCode:"aviao"` ou jogo desabilitado | 400 GAME_NOT_AVAILABLE | IT-02 |
| Lançamento com moeda não habilitada | `currency:"USD"` | 400 CURRENCY_NOT_ENABLED | IT-02 |
| Lista de jogos | GET /api/v1/games assinado / sem assinatura | 200 com `pipa` / 401 | IT-03 |
| Contrato publicado | respostas reais | conferem com o OpenAPI gerado | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — módulo Operators coberto pelos testes da SPEC-0009.

### 7.2 Testes Unitários
- **UT-01** — Dado o catálogo semeado, quando `Find("pipa")` e `Find("aviao")`, então retorna a definição completa da Pipa e nulo.
- **UT-02** — Dado o operador demo, quando `Get(op, BRL)` e `Get(op, USD)`, então BRL traz 1,00/1.000,00/100.000,00 e USD lança CURRENCY_NOT_ENABLED.
- **UT-03** — Dadas 10.000 leituras de limites, quando medidas, então a média é ≤ 1 ms e uma mudança de versão é refletida na leitura seguinte.

### 7.3 Testes de Integração
- **IT-01** — Dado PostgreSQL real com as migrações, quando o seed roda duas vezes, então existe um único `pipa` habilitado para o operador demo com os limites BRL padrão.
- **IT-02** — Dado o Host real, quando o lançamento recebe `gameCode:"aviao"`, um jogo desabilitado e `currency:"USD"`, então 400 GAME_NOT_AVAILABLE, 400 GAME_NOT_AVAILABLE e 400 CURRENCY_NOT_ENABLED.
- **IT-03** — Dado o Host real, quando `GET /api/v1/games` é chamado com e sem assinatura, então 200 com `[{code:"pipa",…}]` e 401.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais de IT-02 e IT-03, quando validadas contra o OpenAPI gerado (SPEC-0006), então conferem (contrato v1 consumido pelas SPEC-0022 e SPEC-0026).

### 7.5 Testes E2E
- N/A — o efeito visível (jogo abre ou recusa) é coberto pelas jornadas da SPEC-0026; esta spec só altera respostas da API de operador já cobertas por IT/CT (user_facing: false — a jornada do jogador é provada pelos E2E da SPEC-0026, que consome este contrato).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operador demo e jogo desabilitado de teste criados por fixture.

**Ambiente de execução:** Testcontainers (Postgres, Redis).

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** tabelas novas no schema `operators` (expand), seed idempotente.
- **Compatibilidade:** lançamento ganha duas respostas de erro novas; chamadas válidas não mudam.
- **Observabilidade:** contador `operators.launch_rejected{reason=GAME_NOT_AVAILABLE|CURRENCY_NOT_ENABLED}`.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Limites de aposta? — Mínimo R$ 1,00, máximo R$ 1.000,00, ganho máximo R$ 100.000,00 por aposta, configuráveis por operador (thomas, 2026-10-09)
- [x] Moedas? — Só BRL neste épico, com modelo pronto para multimoeda (thomas, 2026-10-09)
- [x] Catálogo de jogos neste épico? — Sim (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0002, ADR-0004, ADR-0005, ADR-0015, ADR-0020 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0020:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0020:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1e9b543 (árvore suja) | 2026-10-09 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:01:16.17]     Dado um campo novo num DTO sem regenerar, quando contracts:che) — 37cbc72 | 2026-10-09 |
| G2 Green | PASS | build exit 0 (✓ built in 155ms); test exit 0 (Duration  1.38s (tests 79%, transform 14%, import 6%, worker 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-09T17:14:09: Writing report file 'artifacts/coverage/Summary.txt') — 17d1bc9 | 2026-10-09 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  3.63s (tests 99%, transform 1%)) — 17d1bc9 | 2026-10-09 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer (agente independente): APPROVED em 17d1bc9 — 0 blocker/major; minors: GET /games sem anti-replay e rate limit, contador operators.launch_rejected sem teste e em dobro, isolamento entre operadores sem IT, versão do cache por processo, numeric(18,2), Load síncrono — 38acd95 | 2026-10-09 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 153ms); test exit 0 (Duration  1.33s (tests 67%, transform 20%, import 11%, worker 1%)); test_integration exit 0 (Passed!  - Failed:     0, Passed:    15, Skipped:     0, Total:    15, Duration: 3 m 36 s ); test_e2e exit 0 ([run-e2e] stopping the AppHost); arch_test exit 0 (Duration  817ms (tests 97%, transform 2%, import 1%)); security_scan exit 0 (Ran 8 rules on 294 files: 0 findings.) — 38a1eb3 | 2026-10-10 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-10 |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
Catálogo de jogos e limites por operador e moeda: a Pipa é um jogo do catálogo (definição com RTP, cap, crescimento e tempos padrão da sala), habilitado por operador; cada operador tem limites de aposta mínima, máxima e ganho máximo por moeda. O operador lista os jogos habilitados em `GET /api/v1/games` (assinatura HMAC), e o lançamento recusa jogo inexistente/desabilitado (400 GAME_NOT_AVAILABLE) e moeda sem limites (400 CURRENCY_NOT_ENABLED). Carteira e Apostas leem os limites por `IOperatorLimits`, com cache em memória invalidado por versão.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
No módulo Operators (ADR-0001: limites e moedas são dele), seguindo a SPEC-0009: tabelas `games`, `operator_games` e `operator_limits` no schema `operators` (migração só de expansão, com chaves estrangeiras e checks), `CatalogStore` singleton com escopo de DbContext por operação, `GameCatalog`/`OperatorLimits` sobre um `CatalogCache` recarregado só quando a versão muda, seed idempotente no ambiente de desenvolvimento, endpoint `ListGames` e checagem no lançamento depois da autenticação do operador. Tipos compartilhados `OperatorId`, `PlayerId` e `Currency` em `BuildingBlocks.Contracts` (emenda de escopo, IMP-01 da SPEC-0021). OpenAPI e contratos TypeScript regenerados. Contador `operators.launch_rejected{reason}`.

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A — type feature.

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Find("pipa") devolve a definição completa; Find("aviao") é nulo | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |
| UT-02 | limites BRL 1,00/1.000,00/100.000,00; USD → CURRENCY_NOT_ENABLED | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |
| UT-03 | 10.000 leituras com média ≤ 1 ms; mudança de versão refletida | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |
| IT-01 | seed duas vezes no PostgreSQL real: um único pipa habilitado com limites BRL | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |
| IT-02 | lançamento com aviao, jogo desabilitado e USD → 400 com o código certo | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |
| IT-03 | GET /api/v1/games: 200 assinado com [pipa], 401 sem assinatura | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |
| CT-01 | respostas reais conferem com o OpenAPI gerado | PASS | G5 local 6d7a6e2 (onda 6, resultado integrado); G2 17d1bc9 |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — leitura de limites ≤ 1 ms (UT-03); GET /games exige HMAC (IT-03)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A justificado: sem ambiente até a SPEC-0012 (G6 N/A)
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — contador operators.launch_rejected{reason} (teste na SPEC-0028); rollback = revert do merge e da migração de expansão
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma)
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes) — gitleaks e Semgrep sem achados no G5 (6d7a6e2)
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha — TreatWarningsAsErrors; uma supressão CA1716 com justificativa na linha (nome Get fixado pelo contrato)

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente (G6 N/A até a SPEC-0012). Integrado pela onda 6 (PR #22).

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
- SPEC-0028 — anti-replay e limite de taxa em todo endpoint com assinatura de operador (inclusive `GET /api/v1/games`), teste do contador `operators.launch_rejected` e isolamento entre operadores com um segundo operador (achados do review).
- Dívidas aceitas (decisão do Architect, registradas no review): versão do cache por processo (só o seed escreve hoje; precisa ser persistida ou difundida antes de um backoffice que edite o catálogo); valores `numeric(18,2)` (suficiente para BRL; moedas com 3 casas exigem mudança de escala antes de habilitá-las); `GameDefinition.Currencies` informativo — CURRENCY_NOT_ENABLED depende só dos limites do operador, como diz o contrato; `Load` síncrono só em falta de cache.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0020`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-09 | Escopo: `src/backend/BuildingBlocks/Contracts/**` — `OperatorId`, `PlayerId` e `Currency` ficam junto de `Money`, e não em `Operators.Contracts` | IMP-01 da SPEC-0021: tipos compartilhados por Carteira e Apostas não existiam | SPEC-0021 | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09) |
