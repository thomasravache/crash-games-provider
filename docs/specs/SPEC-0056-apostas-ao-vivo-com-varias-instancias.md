---
id: SPEC-0056
title: Apostas ao vivo com várias instâncias
tier: full
type: feature
user_facing: "false"
status: proposed
created: 2026-10-13
parent: SPEC-0019
depends_on: [SPEC-0025]
consumes_contract: [SPEC-0025@4, SPEC-0023@3]
contract_version: 1
touches: [src/backend/Modules/Betting/**, tests/backend/Modules/Betting.Tests/**, src/backend/Host/Internal/Generated/**, src/frontend/packages/contracts/**, artifacts/openapi/**]
adrs: [ADR-0005, ADR-0014, ADR-0019]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0056 — Apostas ao vivo com várias instâncias

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Faz a lista de apostas ao vivo (SPEC-0025) funcionar com várias instâncias do Host: **uma única projeção e um único emissor de lotes por sala**, no líder da sala, alimentados pelos eventos de aposta e de cash-out de todas as instâncias. O apelido anonimizado passa a ser persistido na aposta. O lote ganha a lista de apostas removidas (cancelamentos). A trava passa a ser por sala, fora do caminho crítico do cash-out.

## 2. Motivação & Escopo
**Motivação:** o review G4 da SPEC-0025 encontrou um MAJOR (M1). Com 2 ou mais instâncias atrás do backplane Redis (ADR-0005, ADR-0019), cada instância difunde a própria projeção. O resultado:
- N lotes por janela;
- totais contraditórios;
- cash-outs decididos pelo líder fora da lista.

A Emenda v4 da SPEC-0025 restringiu aquela entrega a uma instância (demo local). Esta spec é **obrigatória antes de qualquer lançamento com mais de uma instância** (decisão do thomas, 2026-10-13).

**Objetivos (dentro do escopo):**
- Projeção e difusão no líder da sala:
  - as instâncias que não são o líder encaminham "aposta aceita/ativada/cancelada" e "cash-out" pelo mesmo canal Redis do `ICashOutArbiter`;
  - o `JoinSalaBets` lê o snapshot do líder.
- Troca de líder: o novo líder reconstrói a projeção da rodada corrente a partir do banco (apostas da rodada).
- Apelido anonimizado gravado na aposta, no momento em que ela é aceita. Isso resolve a corrida, o vazamento e a perda no reinício (m3 do review).
- `LiveBetsBatch.removed?:[betRef]` para cancelamentos (m1).
- Trava por sala, índice `roundId → sala` e top-100 mantido de forma incremental, sem ordenar dentro da trava (m4).
- Uma moeda por sala (m2): totais na moeda da sala; aposta em outra moeda recusada com `CURRENCY_MISMATCH`.

**Não-objetivos (fora do escopo):**
- Mudar a anonimização, o histórico REST ou o ritmo dos lotes (SPEC-0025).
- O "Top" e os rankings.

## 3. Dependências
- **Implementações necessárias:** SPEC-0025 (lista ao vivo em uma instância).
- **Contratos consumidos:** SPEC-0025@4, SPEC-0023@3 (canal do árbitro no líder).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:**
- ADR-0005: backplane Redis e anonimização entre operadores.
- ADR-0014: liderança da sala.
- ADR-0019: banda constante com 5.000 jogadores por sala.
- Referências: o encaminhamento ao líder do `ICashOutArbiter` (SPEC-0023) e a transmissão agrupada do `RoundSync` (SPEC-0008).

**Decisão:**
- O líder da sala é o único dono da projeção e do emissor.
- Os eventos chegam até ele pelo canal de pedidos ao líder que já existe.
- Na troca de líder, a projeção é reconstruída do banco.
- O apelido anonimizado é persistido na aposta.

**Justificativa:**
- Reaproveita a liderança e o canal Redis já testados (failover, rajada).
- Um único emissor garante o limite de banda do §5 da SPEC-0025, independentemente do número de instâncias.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:**
- Projeção compartilhada no Redis com emissor eleito: duplica a liderança que já existe.
- Cada instância deduplicar por `betRef`: não corrige os totais nem os cash-outs ausentes.

**ADRs:** ADR-0005, ADR-0014, ADR-0019.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** com 2 instâncias e 1.000 apostas por rodada, no máximo 5 lotes por segundo por sala **no cliente**, cada lote ≤ 100 itens; o p99 do cash-out não piora — IT-02 (`Category=Performance`).
- **Segurança:** N/A além da SPEC-0025, sem dado novo exposto.
- **Privacidade e dados pessoais:** só o apelido **anonimizado** é persistido, nunca o completo — UT-01.
- **Disponibilidade e resiliência:** troca de líder no meio da rodada sem perder apostas nem totais — IT-03.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** hub `/hubs/bets` (extensão aditiva do contrato da SPEC-0025) + canal interno ao líder.

```text
LiveBetsBatch { ..., removed?:[betRef] }   // apostas canceladas desde o último lote (≤ 100)
Regras
  um único emissor por sala: o líder; não líderes não difundem
  JoinSalaBets em qualquer instância → snapshot do líder (pedido/resposta pelo canal do líder, prazo 200 ms;
    sem resposta → snapshot vazio da rodada corrente e o cliente recebe os lotes seguintes)
  troca de líder: projeção da rodada corrente reconstruída do banco antes do primeiro lote do novo líder
  apelido: betting.bets.nickname_masked (ex.: "Ma***") gravado na aceitação; nunca o apelido completo
  moeda: cada sala tem uma moeda (configuração da sala); totais na moeda da sala
  aposta com moeda da sessão ≠ moeda da sala → erro CURRENCY_MISMATCH (mesmo envelope dos outros códigos), sem débito
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Apelido persistido | aposta aceita com `nick` "Marcos" | coluna com "Ma***", nunca "Marcos"; reinício mantém | UT-01, IT-01 |
| Um emissor | 2 instâncias, apostas nas duas | cliente recebe um lote por janela, totais consistentes | IT-01 |
| Cash-out do líder | aposta aceita na instância B, cash-out decidido no líder A | aparece em `cashedOut` | IT-01 |
| Cancelamento | aposta cancelada depois de enviada | `removed` traz o `betRef` | UT-02, IT-01 |
| Banda | 2 instâncias, 1.000 apostas | ≤ 5 lotes/s, ≤ 100 itens | IT-02 |
| Troca de líder | líder cai no meio da rodada | novo líder reconstrói e continua os totais | IT-03 |
| Trava por sala | duas salas com rajada | uma não espera a outra; top-100 sem ordenação dentro da trava | UT-03 |
| Moeda da sala | sessão em USD numa sala BRL | `CURRENCY_MISMATCH`, sem débito; totais só em BRL | UT-04, IT-01 |
| Contrato | mensagens reais | conferem com os tipos gerados | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — os testes da SPEC-0025 cobrem o comportamento de uma instância e continuam valendo.

### 7.2 Testes Unitários
- **UT-01** — Dados os apelidos "Marcos" e ausente, quando a aposta é aceita, então o valor persistido é "Ma***" e "Jogador***", e o apelido completo não aparece em nenhum campo gravado.
- **UT-02** — Dada uma aposta enviada num lote e depois cancelada, quando o próximo lote é montado, então `removed` contém o `betRef` e os totais caem.
- **UT-03** — Dadas duas salas com 5.000 apostas cada, quando uma recebe uma rajada de cash-outs, então a outra monta o lote sem esperar a trava da primeira, e o top-100 sai sem ordenar a rodada inteira.

- **UT-04** — Dada uma sala BRL, quando chega uma aposta de sessão USD, então `CURRENCY_MISMATCH` e a projeção não muda; uma aposta BRL é aceita e entra nos totais.

### 7.3 Testes de Integração
- **IT-01** — Dadas duas instâncias do Host com Redis e Postgres reais, quando jogadores apostam nas duas e um cash-out é decidido no líder para uma aposta aceita na outra, então os clientes das duas instâncias recebem os mesmos lotes (um emissor), com totais consistentes, o cash-out presente e o cancelamento em `removed`; uma aposta de sessão USD na sala BRL é recusada com `CURRENCY_MISMATCH` sem débito. Depois de reiniciar uma instância, o apelido continua anonimizado e correto.
- **IT-02** — Dadas duas instâncias e 1.000 apostas por rodada, quando a difusão é observada por 5 s num cliente, então em cada segundo há no máximo 5 lotes e cada um tem ≤ 100 itens (`Category=Performance`).
- **IT-03** — Dadas duas instâncias, quando o líder cai no meio da rodada, então o novo líder reconstrói a projeção do banco e os lotes seguintes trazem os totais corretos.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as mensagens reais de IT-01 (com `removed`), quando validadas contra os tipos gerados, então conferem.

### 7.5 Testes E2E
- N/A — sem interface; a lista é verificada na SPEC-0031.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operadores e jogadores por fixture; `CashOutCluster` e `MultiInstanceTests` (SPEC-0022/0023) como referência de duas instâncias.

**Ambiente de execução:** Testcontainers (Postgres, Redis); duas instâncias do Host.

## 8. Plano de Rollout
- **Estratégia:** merge direto; obrigatória antes de produção com mais de uma instância.
- **Dados/schema:** coluna `nickname_masked` anulável em `betting.bets`; apostas antigas mostram "Jogador***".
- **Compatibilidade:** `removed` opcional; os clientes antigos ignoram o campo.
- **Observabilidade:** `betting.live_batches` por sala, emitido só pelo líder; log na reconstrução da projeção.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Totais da sala com apostas em moedas diferentes: totais por moeda no lote, ou uma moeda por sala garantida na configuração? — Uma moeda por sala; aposta em outra moeda é recusada (thomas, 2026-10-13)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 545ccaa (árvore suja) | 2026-10-13 |
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

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0056`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
