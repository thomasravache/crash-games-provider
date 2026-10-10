---
id: SPEC-0021
title: "Carteira demo, livro-razão e contrato da carteira do operador"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0009, SPEC-0020]
consumes_contract: [SPEC-0020@2]
contract_version: 4
touches: ["src/backend/Modules/Wallet/**", "tests/backend/Modules/Wallet.Tests/**", "docs/integration/**"]
adrs: [ADR-0007, ADR-0001, ADR-0002, ADR-0010, ADR-0014, ADR-0015, ADR-0018]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-09
---

# SPEC-0021 — Carteira demo, livro-razão e contrato da carteira do operador

## 1. Visão Geral
Implementa o módulo Wallet: a porta `IOperatorWallet` (débito, crédito, estorno e saldo, idempotentes), o livro-razão de partidas dobradas e o adaptador **demo**, com R$ 3.000,00 fictícios por jogador e operador e recarga quando o saldo não cobre a aposta mínima. Publica também o contrato da carteira transparente que cassinos reais vão implementar.

## 2. Motivação & Escopo
**Motivação:** dinheiro, mesmo de demonstração, precisa de idempotência e trilha imutável (ADR-0007); o usuário pediu que plugar uma carteira real seja fácil (2026-10-09). O modo demo usa exatamente o caminho que um operador real vai usar.

**Objetivos (dentro do escopo):**
- `Wallet.Contracts`: `IOperatorWallet` e tipos; seleção do adaptador por operador (`demo` agora; `http` e agregadores depois).
- Livro-razão no schema `wallet`: lançamentos só de inserção, partidas dobradas somando zero, contas do jogador e da casa por operador e moeda.
- `DemoWallet`: saldo inicial R$ 3.000,00 por jogador + operador + moeda, persistente entre sessões; recarga para R$ 3.000,00 só quando o saldo for menor que a aposta mínima da moeda (SPEC-0020).
- REST do jogador (sessão da SPEC-0009): saldo e recarga demo.
- Documento público `docs/integration/operator-wallet-api.md`: contrato HTTP da carteira transparente (saldo, débito, crédito, estorno), assinatura HMAC e idempotência — o adaptador HTTP real fica para depois.

**Não-objetivos (fora do escopo):**
- Adaptador HTTP para cassino real e adaptadores de agregador (épico futuro).
- Dinheiro real (bloqueado pela SPEC-0009/ADR-0020).
- Conciliação diária com relatório do operador (só faz sentido com operador real).

## 3. Dependências
- **Implementações necessárias:** SPEC-0009 — sessão do jogador e operador demo.
- **Contratos consumidos:** N/A (a aposta mínima da SPEC-0020 é lida pela porta `IOperatorLimits` quando disponível; nos testes, dublê pelo contrato da SPEC-0020@2).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0007 (carteira transparente + livro-razão + porta com adaptadores). Implementação de referência: módulo Fairness (EF Core por schema, DI, métricas) e Operators (slices REST com sessão).

**Decisão:** porta `IOperatorWallet` em `Wallet.Contracts`; o livro-razão é a fonte da verdade do provedor em qualquer adaptador (no demo ele também é o saldo do jogador; com operador real ele registra o espelho das transações e o operador guarda o saldo). ID de transação determinístico por operação (`bet:{betId}:debit|credit|rollback`); repetir devolve o resultado original.

**Justificativa:** trocar demo por real é trocar o adaptador; aposta, cash-out e liquidação não mudam.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** saldo demo só em memória/Redis (perde a trilha e o comportamento real); carteira de transferência (ADR-0007).

**ADRs:** ADR-0007, ADR-0001, ADR-0002, ADR-0010, ADR-0014, ADR-0015, ADR-0018.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** débito demo p99 ≤ 20 ms com Postgres local (dentro do orçamento de 100 ms da aposta, ADR-0019) — IT-05.
- **Segurança:** saldo e recarga exigem sessão de jogador; um jogador nunca lê nem altera o saldo de outro — IT-04.
- **Privacidade e dados pessoais:** só ID pseudônimo do jogador; nenhum dado pessoal no livro-razão.
- **Disponibilidade e resiliência:** idempotência por ID de transação, inclusive sob concorrência — IT-02, IT-03.
- **Acessibilidade (UI):** N/A — sem interface (botão de recarga é da SPEC-0026).
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** `Wallet.Contracts` (v2) + REST do jogador + documento público da carteira do operador. `OperatorId`, `PlayerId`, `Currency` e `Money` vêm de `BuildingBlocks.Contracts` (criados pela SPEC-0020); `RoundId` de `Rounds.Contracts`; `BetId` é `Guid`. A aposta mínima vem de `IOperatorLimits` (SPEC-0020@2).

```text
IOperatorWallet
  Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
  Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
  Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)   // estorna um débito anterior pelo TransactionId dele
  Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct)
WalletOperation { TransactionId, Player:PlayerRef{OperatorId, PlayerId}, Currency, Amount:Money (>0), RoundId?, BetId }
  // RoundId é nulo só no débito (e no estorno desse débito) de uma aposta para a próxima rodada (queued); o crédito leva a rodada
WalletRollback  { TransactionId, OriginalTransactionId, Player, Currency, RoundId?, BetId }   // RoundId nulo só ao estornar o débito de aposta queued
WalletResult = Ok(BalanceAfter) | InsufficientFunds(Balance) | Duplicate(OriginalResult) | TransactionNotFound(OriginalTransactionId) | Unavailable(retryable)
  // TransactionNotFound: estorno cujo débito original não existe — nada é lançado; é resultado final para quem estorna
  //   (não há o que devolver; não reenviar). A corrida "débito atrasado depois do estorno" é do adaptador do operador real.
TransactionId determinístico: "bet:{betId}:debit" | "bet:{betId}:credit" | "bet:{betId}:rollback"
Saldo inicial demo (3.000,00 BRL) é implícito: não gera lançamento; o saldo = 3.000,00 + soma das partidas do jogador
IDemoWallet
  Task<Balance> RechargeAsync(PlayerRef player, Currency currency, CancellationToken ct)   // erro tipado RECHARGE_NOT_ALLOWED se saldo ≥ aposta mínima
REST (sessão do jogador, SPEC-0009)
  GET  /api/v1/wallet/balance        → 200 { balance:Money, currency, mode:"demo" }
  POST /api/v1/wallet/demo/recharge  → 200 { balance:Money, currency } | 409 RECHARGE_NOT_ALLOWED | 403 NOT_DEMO   (erros no corpo problem+json `ErrorResponse` com `code`, padrão da SPEC-0009)
Contrato público (docs/integration/operator-wallet-api.md): POST {walletBaseUrl}/apogeu/v1/{balance|debit|credit|rollback},
  corpo JSON com transactionId, playerId, currency, amount (string decimal), roundId (nulo no débito de aposta para a próxima rodada e no estorno desse débito), betId; assinatura HMAC (esquema da SPEC-0009);
  respostas { status:"OK"|"INSUFFICIENT_FUNDS"|"DUPLICATE"|"PLAYER_NOT_FOUND"|"TRANSACTION_NOT_FOUND", balance, currency, transactionId }; idempotente por transactionId.
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Saldo inicial demo | jogador novo, BRL | 3.000,00 | UT-01, IT-01 |
| Débito com saldo | débito de 10,00 | Ok, saldo 2.990,00; lançamento soma zero | UT-02, IT-01 |
| Saldo insuficiente | débito maior que o saldo | InsufficientFunds, saldo inalterado | UT-02 |
| Débito repetido | mesmo TransactionId | Duplicate com o resultado original, sem novo lançamento | UT-03, IT-02 |
| Concorrência | 20 débitos iguais em paralelo | exatamente um efeito | IT-02 |
| Débitos diferentes concorrentes | dois débitos distintos simultâneos que juntos excedem o saldo (aposta dupla) | um Ok e um InsufficientFunds; o saldo nunca fica negativo | IT-07 |
| Crédito e estorno | crédito de prêmio; estorno de débito | saldo correto; estorno repetido não duplica; estorno de débito inexistente → TransactionNotFound, sem lançamento | UT-04, IT-03 |
| Recarga permitida | saldo < aposta mínima | saldo volta a 3.000,00 | UT-05, IT-04 |
| Recarga negada | saldo ≥ aposta mínima | 409 RECHARGE_NOT_ALLOWED | UT-05, IT-04 |
| Isolamento | sessão do jogador A | só vê e recarrega o próprio saldo; sem sessão → 401; sessão de modo real → 403 NOT_DEMO na recarga | IT-04 |
| Livro-razão imutável | UPDATE/DELETE/TRUNCATE com a conexão do próprio módulo | recusado pelo banco (trigger de só inserção) | IT-06 |
| Escala do valor | 10,005 ou 0,001 | recusado, sem lançamento | UT-06 |
| Estorno duplo do mesmo débito | dois rollbacks com ids diferentes | devolve uma única vez | UT-06 |
| Banco indisponível | exceção transitória | Unavailable(retryable) | UT-07 |
| Desempenho do débito | 500 débitos | p99 ≤ 20 ms | IT-05 |
| Contrato publicado | respostas REST | conferem com o OpenAPI gerado | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — módulo novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um jogador sem lançamentos, quando o saldo demo é lido, então é 3.000,00 BRL.
- **UT-02** — Dado saldo 3.000,00, quando débito de 10,00 e depois débito de 5.000,00, então Ok com 2.990,00 e InsufficientFunds com saldo inalterado; cada lançamento soma zero.
- **UT-03** — Dado um débito aplicado, quando o mesmo TransactionId chega de novo, então Duplicate com o resultado original.
- **UT-04** — Dado um débito de 10,00, quando crédito de 25,00 e estorno do débito (duas vezes), então saldo 3.015,00 após o crédito, 3.025,00 após o estorno (+10,00 uma única vez), e estorno de débito inexistente devolve TransactionNotFound sem lançamento.
- **UT-06** — Dados débitos, créditos e estornos com valor de 3 casas (10,005) ou abaixo de um centavo (0,001), quando chegam à porta, então são recusados como argumento inválido (ArgumentOutOfRange), sem lançamento; dois estornos com TransactionIds diferentes para o mesmo débito original devolvem uma única vez (o segundo é Duplicate do primeiro).
- **UT-07** — Dada uma falha transitória do banco (exceção de conexão ou tempo esgotado) durante um débito, quando a operação é executada, então devolve `Unavailable(retryable: true)` e o trace fica com status de erro.
- **UT-05** — Dada aposta mínima 1,00, quando a recarga é pedida com saldo 0,50 e com saldo 1,00, então recarrega para 3.000,00 e recusa com RECHARGE_NOT_ALLOWED.

### 7.3 Testes de Integração
- **IT-01** — Dado PostgreSQL real, quando um jogador novo é debitado em 10,00, então o saldo é 2.990,00 e o schema `wallet` tem um lançamento com duas partidas que somam zero.
- **IT-02** — Dados 20 débitos com o mesmo TransactionId em paralelo, quando executados, então existe um único lançamento e todos recebem o mesmo resultado.
- **IT-03** — Dado débito, crédito e estorno reais, quando repetidos, então o saldo final é o esperado e não há lançamento duplicado.
- **IT-04** — Dado o Host real com sessões de dois jogadores, quando cada um lê o saldo e pede recarga, então cada um só vê o próprio saldo, recarga funciona abaixo do mínimo e retorna 409 acima; sem sessão → 401; com sessão de modo real, a recarga → 403 NOT_DEMO.
- **IT-07** — Dado saldo 15,00 e PostgreSQL real, quando dois débitos com TransactionIds diferentes de 10,00 chegam ao mesmo tempo para o mesmo jogador (repetido 20 vezes), então em cada repetição um recebe Ok e o outro InsufficientFunds, e o saldo nunca fica negativo.
- **IT-05** — Dados 500 débitos sequenciais com Postgres real, quando medidos, então p99 ≤ 20 ms (`Category=Performance`).
- **IT-06** — Dado um lançamento gravado, quando se tenta UPDATE, DELETE ou TRUNCATE direto no banco, com a mesma conexão do módulo usada pela aplicação, então o banco recusa com erro (trigger `BEFORE UPDATE OR DELETE` nas tabelas de lançamentos e partidas — vale também para o dono das tabelas e para o superusuário dos testes).

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais de IT-04, quando validadas contra o OpenAPI gerado, então conferem (contrato v2 consumido pelas SPEC-0022, 0023, 0024, 0025 e 0026).

### 7.5 Testes E2E
- N/A — o saldo e a recarga aparecem para o jogador pela SPEC-0026, cujos E2E cobrem a jornada (user_facing: false — a jornada do jogador é provada pelos E2E da SPEC-0026, que consome este contrato).

### 7.6 Outros
- Revisão do documento `operator-wallet-api.md` no G4 contra o ADR-0007.

**Dublês e dados de teste:** limites pela porta `IOperatorLimits` com dublê do contrato SPEC-0020@2.

**Ambiente de execução:** Testcontainers (Postgres, Redis); Host real via WebApplicationFactory.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** schema `wallet` novo (migração inicial); tabelas de lançamentos e partidas só de inserção, garantidas por trigger `BEFORE UPDATE OR DELETE` que lança erro (privilégios não restringem o dono nem o superusuário).
- **Compatibilidade:** N/A (API nova).
- **Observabilidade:** métricas `wallet.operations{type,result}`, `wallet.duplicates`, `wallet.demo_recharges`; trace por TransactionId.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Saldo inicial da demo? — R$ 3.000,00 por jogador e operador, persistente, com recarga quando abaixo da aposta mínima (thomas, 2026-10-09)
- [x] Carteira real fácil de plugar? — Sim: mesma porta `IOperatorWallet`, troca só o adaptador (thomas, 2026-10-09)
- [x] API de carteira do operador neste épico? — Contrato + adaptador demo; adaptadores reais depois (thomas, 2026-10-09)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0007, ADR-0001, ADR-0002, ADR-0010, ADR-0014, ADR-0015, ADR-0018 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06, UT-07 com a tag `SPEC-0021:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, CT-01 com a tag `SPEC-0021:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:02.01]     Dada uma falha transitória do banco durante um débito, quando ) — 963fe9f | 2026-10-10 |
| G2 Green | PASS | build exit 0 (✓ built in 151ms); test exit 0 (Duration  1.35s (tests 67%, transform 20%, import 11%, worker 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-10T20:31:25: Report generation took 0,5 seconds) — 7e48e0d | 2026-10-10 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  767ms (tests 97%, transform 2%, import 1%)) — 7e48e0d | 2026-10-10 |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-09 | G1 | spec | Contrato v1 incompleto: (D1) estorno de débito inexistente sem resultado em WalletResult nem no contrato público; (D2) OperatorId/Currency e a porta IOperatorLimits não existem e a SPEC-0021 não pode criá-los nem consumir a SPEC-0020; (D3) IT-06 não diz com que papel do banco o UPDATE/DELETE é recusado | Test-writer leu a spec, o contrato da SPEC-0020, ADR-0007, BuildingBlocks.Contracts e os testes de referência; nenhum commit | Architect: emenda da SPEC-0021 | Emenda v2 (2026-10-09): TransactionNotFound/TRANSACTION_NOT_FOUND; tipos compartilhados em BuildingBlocks.Contracts pela SPEC-0020 (emenda de escopo) e dependência da SPEC-0020; IT-06 por trigger com a conexão do módulo; consumidores SPEC-0022..0026 repinados em @2 | 2026-10-09 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0021`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v4 (inalterado) | 2026-10-10 | Correções do review (G4): valores com mais de 2 casas são recusados na porta (UT-06); um débito só pode ser estornado uma vez, mesmo com TransactionIds diferentes (UT-06); falha transitória do banco vira `Unavailable(retryable)` (UT-07); IT-06 cobre TRUNCATE | Review independente: o `numeric(18,2)` arredondava em silêncio e o saldo informado divergia das partidas (major); estorno duplo e exceção de banco (minors de segurança de dinheiro) | Nenhuma (contrato inalterado; esclarece o comportamento já esperado) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
| v4 | 2026-10-10 | `WalletRollback.RoundId` opcional: nulo só ao estornar o débito de uma aposta queued (cancelamento antes da rodada existir ou reversão de débito incerto); documento público atualizado | IMP-01 da SPEC-0022: o estorno de aposta queued não tem rodada | SPEC-0022..SPEC-0026 (pin @3 → @4; só afrouxa um campo) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
| v3 | 2026-10-10 | `WalletOperation.RoundId` opcional: nulo só no débito de uma aposta para a próxima rodada (queued), cuja rodada ainda não existe; crédito e estorno sempre levam a rodada; o documento público reflete isso | G1 da SPEC-0022: o débito acontece na hora da aposta (inclusive queued) e o contrato v2 exigia RoundId | SPEC-0022..SPEC-0026 (pin @2 → @3; só afrouxa um campo) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
| v2 (inalterado) | 2026-10-10 | Correção do UT-04 (3.015,00 após o crédito, 3.025,00 após o estorno); novo IT-07 (dois débitos diferentes simultâneos nunca deixam o saldo negativo — aposta dupla); IT-04 cobre 403 NOT_DEMO. Esclarecimentos confirmados: TransactionId com Guid no formato "D"; `currency` como string ISO; 20 débitos iguais → um WalletOk e 19 WalletDuplicate com o mesmo resultado; estorno repetido → WalletDuplicate(WalletOk); recarga é lançamento balanceado (jogador +x, casa −x); cada lançamento tem exatamente duas partidas | G1 do Test-writer: erro de conta no UT-04, corrida de débitos distintos sem teste (risco de saldo negativo) e código do contrato sem teste | Nenhuma (contrato inalterado) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-10) |
| v2 | 2026-10-09 | (D1) `WalletResult.TransactionNotFound` e status público `TRANSACTION_NOT_FOUND` para estorno sem débito original; (D2) tipos compartilhados em `BuildingBlocks.Contracts` (SPEC-0020) e aposta mínima por `IOperatorLimits` — a spec passa a depender da SPEC-0020 e roda na onda seguinte, junto da SPEC-0022; (D3) livro-razão só de inserção por trigger, testado com a conexão do módulo; esclarecimentos: saldo inicial implícito (sem lançamento), erros REST em problem+json com `code` | IMP-01: SPEC_DEFECT do Test-writer no G1 (contrato v1 sem resultado para estorno recusado, tipos inexistentes, papel do banco indefinido no IT-06) | SPEC-0022, SPEC-0023, SPEC-0024, SPEC-0025, SPEC-0026 (pin @1 → @2; só acréscimo de caso) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09); a confirmar no resumo |
