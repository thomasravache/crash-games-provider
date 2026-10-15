---
id: SPEC-0025
title: "Apostas ao vivo e histórico pessoal"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0024]
consumes_contract: [SPEC-0021@4]
contract_version: 4
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**", "src/backend/Modules/Operators/Operators.Features/Sessions/**", "tests/backend/Modules/Operators.Tests/**", "tests/backend/Security.Tests/**", "src/backend/Host/Internal/Generated/**", "src/frontend/packages/contracts/**", "artifacts/openapi/**", "src/backend/Host/OpenApi/ContractScalarSchemaTransformer.cs"]
adrs: [ADR-0005, ADR-0014, ADR-0019, ADR-0008]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0025 — Apostas ao vivo e histórico pessoal

## 1. Visão Geral
Mostra para a sala as apostas da rodada em tempo real (apelido anonimizado, valor, multiplicador e ganho de quem retirou) e oferece ao jogador o histórico das próprias apostas — as abas "Todas as apostas" e "Minhas apostas" do padrão de mercado.

## 2. Motivação & Escopo
**Motivação:** é o que faz a sala parecer cheia; padrão do gênero confirmado pelo usuário (2026-10-09). O "Top" fica para o épico de engajamento.

**Objetivos (dentro do escopo):**
- Eventos para o grupo da sala no hub `/hubs/bets`: aposta feita e cash-out, agrupados a cada 200 ms.
- Snapshot das apostas da rodada corrente ao entrar na sala.
- Anonimização para todos: duas primeiras letras do apelido + `***` (ADR-0005 exige anonimizar entre operadores; aplicar a todos simplifica e protege).
- `GET /api/v1/me/bets` com paginação por cursor.

**Não-objetivos (fora do escopo):**
- "Top" e rankings, chat (épico de engajamento).

## 3. Dependências
- **Implementações necessárias:** SPEC-0024 (estados finais das apostas).
- **Contratos consumidos:** SPEC-0021@4 (moeda e valores).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0005 (anonimização entre operadores), ADR-0019 (5.000 jogadores por sala: banda constante). Referência: transmissão agrupada do `RoundSync` da SPEC-0008.

**Decisão:** projeção de leitura no Betting alimentada pelos próprios eventos de aposta; difusão agrupada a cada 200 ms (no máximo 100 itens por lote, o resto entra só nos totais); identificador público `betRef` opaco (não é o `betId`).

**Justificativa:** banda previsível com salas cheias e nenhum identificador interno exposto.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** um evento por aposta (banda proporcional ao número de jogadores); mostrar apelido completo (vaza identidade entre operadores).

**ADRs:** ADR-0005, ADR-0014, ADR-0019, ADR-0008.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** com 1.000 apostas por rodada, no máximo 5 lotes por segundo por sala e cada lote ≤ 100 itens — IT-03 (`Category=Performance`). **Vale para uma instância do Host** (demo local, Emenda v4); várias instâncias são da SPEC-0056, obrigatória antes do lançamento.
- **Segurança:** histórico só da própria sessão; sem sessão → 401 — IT-02.
- **Privacidade e dados pessoais:** apelido sempre anonimizado na sala; `betRef` opaco — UT-01, IT-01.
- **Disponibilidade e resiliência:** reconexão recebe o snapshot atual — IT-01.
- **Acessibilidade (UI):** N/A — sem interface (lista é da SPEC-0026).
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** hub `/hubs/bets` (extensão) + REST.

```text
Hub /hubs/bets
  JoinSalaBets() → LiveBetsSnapshot { roundId, totalBets, totalPlayers, totalWagered:Money,
                                     bets:[LiveBet] (até 100, maiores valores primeiro) }
    (sem argumento: usa a sala da sessão do jogador)
    (sem rodada corrente → snapshot vazio, nunca o da rodada anterior — Emenda v4)
Servidor → grupo da sala (a cada 200 ms, se houver novidade), método do cliente "LiveBetsBatch"
  LiveBetsBatch { roundId, placed:[LiveBet] (≤ 100, maiores valores), cashedOut:[{ betRef, multiplier, payout }] (≤ 100),
                  totalBets, totalPlayers, totalWagered }
  totalPlayers = jogadores distintos com aposta na rodada
LiveBet { betRef, nickname:"Ma***", amount:Money, currency, multiplier?:Multiplier, payout?:Money }
REST (sessão do jogador)
  GET /api/v1/me/bets?limit=1..50&before=<cursor>
    → 200 { items:[{ betId, roundId?, roundNumber?, panel, amount, currency, autoCashOut?, multiplier?, payout?,
                      status:"placed"|"queued"|"active"|"cashed_out"|"lost"|"cancelled"|"refunded", placedAt }],
            nextCursor? }
      ordem: placedAt decrescente, desempate por betId decrescente; cursor opaco; aposta queued sem rodada → roundId e roundNumber nulos
      limit ausente → 20 (Emenda v3); limit não numérico → 400 INVALID_LIMIT no envelope (Emenda v4)
    | 400 { code:"INVALID_LIMIT", … } (mesmo envelope de erro dos outros REST)
    | 400 { code:"INVALID_CURSOR", … } quando `before` não é um cursor emitido pela API (Emenda v3) | 401
Anonimização: 2 primeiras letras do apelido + "***"; apelido ausente → "Jogador***"
Apelido (Emenda v2): claim opcional `nick` no token de sessão do jogador (SPEC-0009 §4), preenchida pelo Operators com o
  apelido do lançamento; o Betting lê a claim na sessão; o apelido nunca vem do cliente
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Anonimização | "Marcos", "A", sem apelido | "Ma***", "A***", "Jogador***" | UT-01 |
| Apelido na sessão | lançamento com apelido "Marcos" / sem apelido | token com claim `nick` = "Marcos" / sem a claim; Betting lê a claim | UT-03 |
| Agrupamento | 250 apostas em 200 ms | um lote com 100 itens e totais com 250 | UT-02 |
| Snapshot e eventos | jogador entra no meio da rodada | snapshot atual + lotes seguintes; sem betId nem apelido completo | IT-01 |
| Histórico | jogador com apostas em vários estados | página ordenada, cursor funciona; só as próprias | IT-02 |
| Limite inválido | limit 0 ou 51 | 400 INVALID_LIMIT | IT-02 |
| Limite não numérico | limit=abc | 400 INVALID_LIMIT no envelope | IT-06 |
| Sala sem rodada corrente | rodada anterior com apostas, nenhuma rodada corrente | snapshot vazio, sem apostas da rodada anterior | IT-05 |
| Cursor inválido e limite padrão | `before` malformado / limit ausente | 400 INVALID_CURSOR / página de até 20 itens | IT-04 |
| Banda | 1.000 apostas por rodada | ≤ 5 lotes/s, ≤ 100 itens cada | IT-03 |
| Contrato | mensagens e respostas reais | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — comportamento novo.

### 7.2 Testes Unitários
- **UT-01** — Dados os apelidos "Marcos", "A" e ausente, quando anonimizados, então "Ma***", "A***" e "Jogador***".
- **UT-03** — Dado um lançamento com apelido "Marcos" e outro sem apelido, quando a sessão do jogador é criada, então o token traz a claim `nick` = "Marcos" no primeiro e nenhuma claim `nick` no segundo, e a sessão do Betting expõe o apelido ou nulo (Emenda v2).
- **UT-02** — Dadas 250 apostas na mesma janela de 200 ms, quando o lote é montado, então traz 100 itens (maiores valores) e totais de 250.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com dois jogadores de operadores diferentes na mesma sala (segundo operador por dublê de teste), com apelido na claim `nick`, quando um aposta e retira e o outro entra no meio da rodada, então o segundo recebe snapshot e lotes com apelidos anonimizados, `betRef` opaco e sem `betId`.
- **IT-02** — Dado um jogador com apostas cashed_out, lost, cancelled e refunded, quando pagina `GET /api/v1/me/bets` com limit 2, então recebe as próprias apostas em ordem e o cursor leva à página seguinte; limit 0 e 51 → 400; sem sessão → 401.
- **IT-04** — Dado um jogador com 25 apostas, quando chama `GET /api/v1/me/bets` sem `limit`, então recebe 20 itens e `nextCursor`; e quando chama com `before` malformado (não base64url ou de tamanho errado), então recebe 400 `INVALID_CURSOR` no envelope de erro (Emenda v3).
- **IT-05** — Dada uma sala cuja rodada anterior teve apostas e que está sem rodada corrente no feed, quando um jogador chama `JoinSalaBets`, então recebe um snapshot vazio (sem apostas nem totais da rodada anterior) (Emenda v4).
- **IT-06** — Dado um jogador com sessão, quando chama `GET /api/v1/me/bets?limit=abc`, então recebe 400 `INVALID_LIMIT` no mesmo envelope de erro dos outros REST (Emenda v4).
- **IT-03** — Dadas 1.000 apostas numa rodada (vários jogadores em paralelo; fase de apostas ampliada pela configuração da sala), quando a difusão é observada por 5 s, então em cada segundo inteiro contado a partir do primeiro lote há no máximo 5 lotes e cada um com ≤ 100 itens (`Category=Performance`).

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens e respostas reais de IT-01/IT-02, quando validadas contra os tipos gerados, então conferem (contrato consumido pela SPEC-0026).

### 7.5 Testes E2E
- N/A — as listas "Todas as apostas" e "Minhas apostas" são verificadas nos E2E da SPEC-0026 (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operadores e jogadores de teste por fixture.

**Ambiente de execução:** Testcontainers (Postgres, Redis); Host real.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** índices de consulta por jogador no schema `betting`.
- **Compatibilidade:** extensão aditiva do hub e REST novo.
- **Observabilidade:** métricas `betting.live_batches`, tamanho dos lotes.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Apostas ao vivo e histórico neste épico? — Sim, padrão de mercado; "Top" fica para depois (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [x] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0005, ADR-0014, ADR-0019, ADR-0008 relidos

**Fase 2: Unitários**
- [x] Red: escrever UT-01, UT-02, UT-03 com a tag `SPEC-0025:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo os ADRs
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [x] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0025:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:06:21.38]     Dado o contrato gerado do hub /hubs/bets, quando o snapshot de) — fb28c49 | 2026-10-13 |
| G2 Green | PASS | build exit 0 (✓ built in 332ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 2abd224 | 2026-10-14 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.04s (tests 96%, transform 3%, import 1%)) — 2abd224 | 2026-10-14 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINORS (2ª revisão, a243e65). m5, m6 e m7 resolvidos; M1 fora do escopo pela Emenda v4 (SPEC-0056). Minors: OpenAPI tipa limit como string (registrado na Emenda v4, nota); ?limit= vazio → 400 (registrado); falta teste dedicado para rodada nova sem snapshot (pendência). Manifestos de teste alterados: só Compile Include de suportes compartilhados (verificado na 1ª revisão); scaffold c3fb967 sem lógica. — 2abd224 | 2026-10-14 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #31 verde (run 38676137955, commit 32cbe33): backend (suíte .NET completa + Performance), frontend, contracts, e2e (Chromium, WebKit e Firefox), security e sdd; backend verde na 3ª tentativa e e2e Firefox na 2ª, após falhas de tempo conhecidas sem relação com a onda (LateDebitReusedClientBetIdTests, E2E Firefox de aposta perdida/dupla, troca de rodada no teste do líder impedido, alerta de conciliação verificado sem espera em SettlementRaceTests; os dois últimos passam 3/3 localmente); local em 54f8b85: build 0 avisos, arquitetura 19/19 + front, gitleaks sem vazamentos, NuGet/pnpm sem vulnerabilidades | 2026-10-15 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-15 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem e deploy) | 2026-10-15 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — 0486474 | 2026-10-15 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-13 | G1 | spec | Betting não tem acesso ao apelido do jogador (token só leva sub/op/sala/cur/mode; apelido só no Operators, sem porta em Operators.Contracts); faltam no touches os contratos gerados (packages/contracts), o código gerado do Wolverine e o scaffold; contrato não fixa nome do método do lote, ordem/cursor do histórico, corpo do INVALID_LIMIT, totalPlayers e teto dos cashedOut | test-writer leu o código (PlayerSessionClaims, BettingSession, LaunchGrantRecord) e parou antes de escrever testes | Architect + thomas | Emenda v2 — claim nick no token, contrato detalhado e touches ampliado (aprovada por thomas) | 2026-10-13 |
| IMP-02 | 2026-10-14 | G2 | trabalho | G2 reprovado por fator externo: CVE-2026-70159 (GHSA-334f-vvp4-7g6p, publicado em 2026-10-13 19:41Z), DoS no SignalR do ASP.NET Core ≤ 10.0.12. Microsoft.AspNetCore.SignalR.Common 10.0.12 (transitivo do SignalR.Client nos testes) gera NU1903 como erro em todo restore limpo (ContractsCheckTests e a CI de qualquer PR). Não é regressão da 0025. | ContractsCheckTests isolado reproduz o NU1903 no restore; dotnet list package --vulnerable aponta Betting.Tests e Rounds.Tests | Architect | SPEC-0058 — ASP.NET Core 10.0.13 e SDK 10.0.402 (CVE-2026-70159), G1–G4 PASS; merge na branch da 0025 (2abd224) | 2026-10-14 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
A sala recebe as apostas da rodada em tempo real (apelido anonimizado, valor, multiplicador e ganho de quem retirou), em lotes a cada 200 ms com no máximo 100 itens, e o snapshot ao entrar. O jogador consulta o histórico das próprias apostas em `GET /api/v1/me/bets`, com paginação por cursor. Vale para uma instância do Host (Emenda v4); várias instâncias ficam com a SPEC-0056.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
Scaffold c3fb967, red fb28c49, green 5741615: projeção por sala (`LiveBetsBatcher`) alimentada pelos eventos de aposta, difusão por `LiveBetsBroadcaster`, claim `nick` no token de sessão (Emenda v2), `betRef` opaco, índice `ix_bets_player_history`, métricas `betting.live_batches` e `betting.live_batch_size`. Emenda v3 (limite padrão 20 e `INVALID_CURSOR`, IT-04 em 1026bd6). Emenda v4 depois do 1º review: snapshot vazio sem rodada, `limit` não numérico com envelope e feed obrigatório (red 90f1062, green a243e65).

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Dados os apelidos "Marcos", "A" e ausente, quando anonimizados, então "Ma", "A" e "Jogador". | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| UT-03 | Dado um lançamento com apelido "Marcos" e outro sem apelido, quando a sessão do jogador é criada, então o toke… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| UT-02 | Dadas 250 apostas na mesma janela de 200 ms, quando o lote é montado, então traz 100 itens (maiores valores) e… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| IT-01 | Dado o Host real com dois jogadores de operadores diferentes na mesma sala (segundo operador por dublê de test… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| IT-02 | Dado um jogador com apostas cashed_out, lost, cancelled e refunded, quando pagina GET /api/v1/me/bets com limi… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| IT-04 | Dado um jogador com 25 apostas, quando chama GET /api/v1/me/bets sem limit, então recebe 20 itens e nextCursor… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| IT-05 | Dada uma sala cuja rodada anterior teve apostas e que está sem rodada corrente no feed, quando um jogador cham… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| IT-06 | Dado um jogador com sessão, quando chama GET /api/v1/me/bets?limit=abc, então recebe 400 INVALID_LIMIT no mesm… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| IT-03 | Dadas 1.000 apostas numa rodada (vários jogadores em paralelo; fase de apostas ampliada pela configuração da s… | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |
| CT-01 | Dadas as mensagens e respostas reais de IT-01/IT-02, quando validadas contra os tipos gerados, então conferem … | PASS | G2 do registro de gates; CI do PR #31 verde (run 38676137955 em 32cbe33 e run 38683588402 em 0bb3111), merge 25c6d4e |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A até a SPEC-0012; em main pelo PR #31
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma)
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente de deploy até a SPEC-0012 (G6 N/A); disponível em main pelo PR #31 (merge 25c6d4e, 2026-10-15).

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
SPEC-0056 (várias instâncias: emissor único no líder, apelido persistido, removidas, trava por sala, uma moeda por sala). Minors da 2ª revisão: o OpenAPI tipa `limit` como string; falta teste dedicado para rodada nova sem snapshot.

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0025`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 4 (nota) | 2026-10-14 | Registro da 2ª revisão: o OpenAPI gerado passa a tipar `limit` como string (o parse do inteiro 1..50 fica no endpoint), e `?limit=` vazio é tratado como "presente e não numérico" → 400 `INVALID_LIMIT`; o consumidor atual (game-sdk) não usa o tipo gerado. Voltar a tipar como inteiro no OpenAPI fica como pendência | Review G4 (2ª, a243e65): minors m-a e m-b | SPEC-0026, SPEC-0031 (sem efeito hoje) | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
| 4 | 2026-10-13 | NFR de banda e totais restritos a uma instância do Host (várias instâncias → SPEC-0056); snapshot vazio sem rodada corrente (IT-05); `limit` não numérico → 400 `INVALID_LIMIT` no envelope (IT-06); `LiveBetsFeed` passa a ser dependência obrigatória | Review G4 (CHANGES REQUESTED, 387ba75): M1 multi-instância, m5, m6, m7; os demais minors (m1–m4) vão para a SPEC-0056 | SPEC-0026, SPEC-0031, SPEC-0054 (repinadas em @4; aditivo), SPEC-0056 | thomas (opção "spec nova para multi-instância", 2026-10-13) |
| 3 (inalterado) | 2026-10-13 | `LiveBetsBatch.cashedOut[]` ganha `reason?:"round_limit"`, definido e implementado pela SPEC-0054 (contrato dela) | Corte de exposição por rodada; a SPEC-0025 está em gates e não muda | SPEC-0026, SPEC-0031 (campo opcional, sem efeito) | thomas (2026-10-13, H1 das SPEC-0053/0054/0055) |
| 3 | 2026-10-13 | `GET /api/v1/me/bets`: `limit` ausente vale 20; `before` malformado → 400 `INVALID_CURSOR` no mesmo envelope; novo IT-04 | Implementer (G2): o contrato não definia o limite padrão nem a resposta a cursor inválido; mudança aditiva, sem efeito no hub nem nos campos já consumidos | SPEC-0026, SPEC-0031 (repinados em @3, sem mudança) | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
| 2 (inalterado) | 2026-10-13 | `touches` inclui `src/backend/Host/OpenApi/ContractScalarSchemaTransformer.cs`: `Money`/`Multiplier` anuláveis passam a usar `oneOf` com null na propriedade, sem tornar o componente compartilhado anulável | Scaffold: o primeiro `Multiplier?` (autoCashOut do histórico) deixava `Multiplier` anulável em todo o contrato REST (crashPoint e cap do Rounds) | Nenhuma | thomas (Emenda v2 aprovada, 2026-10-13) |
| 2 | 2026-10-13 | Claim opcional `nick` no token de sessão (Operators) lida pelo Betting; método do cliente `LiveBetsBatch`; `JoinSalaBets` sem argumento; `cashedOut` ≤ 100 por lote; `totalPlayers` = jogadores distintos; histórico em `placedAt` decrescente com desempate por `betId`, cursor opaco, `roundId`/`roundNumber` nulos em aposta queued; 400 no envelope `code`; IT-03 por segundos inteiros; `touches` com sessões do Operators, testes de Operators/Security, codegen do Wolverine, contratos gerados (ADR-0008) e OpenAPI | Test-writer (IMP-01): o Betting não tinha acesso ao apelido e o contrato deixava detalhes em aberto | SPEC-0026, SPEC-0031 | thomas (2026-10-13) |
