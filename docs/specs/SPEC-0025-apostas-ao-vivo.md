---
id: SPEC-0025
title: "Apostas ao vivo e histórico pessoal"
tier: full
type: feature
user_facing: false
status: approved
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0024]
consumes_contract: [SPEC-0021@4]
contract_version: 1
touches: ["src/backend/Modules/Betting/**", "tests/backend/Modules/Betting.Tests/**"]
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
- **Desempenho e escala:** com 1.000 apostas por rodada, no máximo 5 lotes por segundo por sala e cada lote ≤ 100 itens — IT-03 (`Category=Performance`).
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
Servidor → grupo da sala (a cada 200 ms, se houver novidade)
  LiveBetsBatch { roundId, placed:[LiveBet], cashedOut:[{ betRef, multiplier, payout }], totalBets, totalPlayers, totalWagered }
LiveBet { betRef, nickname:"Ma***", amount:Money, currency, multiplier?:Multiplier, payout?:Money }
REST (sessão do jogador)
  GET /api/v1/me/bets?limit=1..50&before=<cursor>
    → 200 { items:[{ betId, roundId, roundNumber, panel, amount, currency, autoCashOut?, multiplier?, payout?,
                      status:"placed"|"queued"|"active"|"cashed_out"|"lost"|"cancelled"|"refunded", placedAt }],
            nextCursor? }
    | 400 INVALID_LIMIT | 401
Anonimização: 2 primeiras letras do apelido + "***"; apelido ausente → "Jogador***"
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Anonimização | "Marcos", "A", sem apelido | "Ma***", "A***", "Jogador***" | UT-01 |
| Agrupamento | 250 apostas em 200 ms | um lote com 100 itens e totais com 250 | UT-02 |
| Snapshot e eventos | jogador entra no meio da rodada | snapshot atual + lotes seguintes; sem betId nem apelido completo | IT-01 |
| Histórico | jogador com apostas em vários estados | página ordenada, cursor funciona; só as próprias | IT-02 |
| Limite inválido | limit 0 ou 51 | 400 INVALID_LIMIT | IT-02 |
| Banda | 1.000 apostas por rodada | ≤ 5 lotes/s, ≤ 100 itens cada | IT-03 |
| Contrato | mensagens e respostas reais | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — comportamento novo.

### 7.2 Testes Unitários
- **UT-01** — Dados os apelidos "Marcos", "A" e ausente, quando anonimizados, então "Ma***", "A***" e "Jogador***".
- **UT-02** — Dadas 250 apostas na mesma janela de 200 ms, quando o lote é montado, então traz 100 itens (maiores valores) e totais de 250.

### 7.3 Testes de Integração
- **IT-01** — Dado o Host real com dois jogadores de operadores diferentes na mesma sala, quando um aposta e retira e o outro entra no meio da rodada, então o segundo recebe snapshot e lotes com apelidos anonimizados, `betRef` opaco e sem `betId`.
- **IT-02** — Dado um jogador com apostas cashed_out, lost, cancelled e refunded, quando pagina `GET /api/v1/me/bets` com limit 2, então recebe as próprias apostas em ordem e o cursor leva à página seguinte; limit 0 e 51 → 400; sem sessão → 401.
- **IT-03** — Dadas 1.000 apostas numa rodada, quando a difusão é observada por 5 s, então no máximo 5 lotes por segundo e cada um com ≤ 100 itens (`Category=Performance`).

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
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0005, ADR-0014, ADR-0019, ADR-0008 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0025:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, CT-01 com a tag `SPEC-0025:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1e9b543 (árvore suja) | 2026-10-09 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
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

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Todos os testes do plano passando e listados na Verificação
- [ ] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [ ] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [ ] Requisitos não-funcionais medidos com evidência (ou N/A justificado)
- [ ] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6)
- [ ] Observabilidade e rollback prontos conforme o Plano de Rollout
- [ ] Documentação raiz e CHANGELOG atualizados (G7)
- [ ] Pendências registradas como novas specs (ou nenhuma)
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0025`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
