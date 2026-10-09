---
id: SPEC-0021
title: "Carteira demo, livro-razão e contrato da carteira do operador"
tier: full
type: feature
user_facing: false
status: approved
created: 2026-10-07
parent: SPEC-0019
depends_on: [SPEC-0009]
consumes_contract: []
contract_version: 1
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
- **Contratos consumidos:** N/A (a aposta mínima da SPEC-0020 é lida pela porta `IOperatorLimits` quando disponível; nos testes, dublê pelo contrato da SPEC-0020@1).
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
**Interface:** `Wallet.Contracts` (v1) + REST do jogador + documento público da carteira do operador.

```text
IOperatorWallet
  Task<WalletResult> DebitAsync(WalletOperation op, CancellationToken ct)
  Task<WalletResult> CreditAsync(WalletOperation op, CancellationToken ct)
  Task<WalletResult> RollbackAsync(WalletRollback op, CancellationToken ct)   // estorna um débito anterior pelo TransactionId dele
  Task<Balance> GetBalanceAsync(PlayerRef player, Currency currency, CancellationToken ct)
WalletOperation { TransactionId, Player:PlayerRef{OperatorId, PlayerId}, Currency, Amount:Money (>0), RoundId, BetId }
WalletRollback  { TransactionId, OriginalTransactionId, Player, Currency, RoundId, BetId }
WalletResult = Ok(BalanceAfter) | InsufficientFunds(Balance) | Duplicate(OriginalResult) | Unavailable(retryable)
TransactionId determinístico: "bet:{betId}:debit" | "bet:{betId}:credit" | "bet:{betId}:rollback"
IDemoWallet
  Task<Balance> RechargeAsync(PlayerRef player, Currency currency, CancellationToken ct)   // RECHARGE_NOT_ALLOWED se saldo ≥ aposta mínima
REST (sessão do jogador, SPEC-0009)
  GET  /api/v1/wallet/balance        → 200 { balance:Money, currency, mode:"demo" }
  POST /api/v1/wallet/demo/recharge  → 200 { balance:Money, currency } | 409 RECHARGE_NOT_ALLOWED | 403 NOT_DEMO
Contrato público (docs/integration/operator-wallet-api.md): POST {walletBaseUrl}/apogeu/v1/{balance|debit|credit|rollback},
  corpo JSON com transactionId, playerId, currency, amount (string decimal), roundId, betId; assinatura HMAC (esquema da SPEC-0009);
  respostas { status:"OK"|"INSUFFICIENT_FUNDS"|"DUPLICATE"|"PLAYER_NOT_FOUND", balance, currency, transactionId }; idempotente por transactionId.
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
| Crédito e estorno | crédito de prêmio; estorno de débito | saldo correto; estorno repetido não duplica; estorno de débito inexistente é recusado | UT-04, IT-03 |
| Recarga permitida | saldo < aposta mínima | saldo volta a 3.000,00 | UT-05, IT-04 |
| Recarga negada | saldo ≥ aposta mínima | 409 RECHARGE_NOT_ALLOWED | UT-05, IT-04 |
| Isolamento | sessão do jogador A | só vê e recarrega o próprio saldo; sem sessão → 401 | IT-04 |
| Livro-razão imutável | tentativa de alterar/apagar lançamento | recusada pelo banco (só inserção) | IT-06 |
| Desempenho do débito | 500 débitos | p99 ≤ 20 ms | IT-05 |
| Contrato publicado | respostas REST | conferem com o OpenAPI gerado | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — módulo novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um jogador sem lançamentos, quando o saldo demo é lido, então é 3.000,00 BRL.
- **UT-02** — Dado saldo 3.000,00, quando débito de 10,00 e depois débito de 5.000,00, então Ok com 2.990,00 e InsufficientFunds com saldo inalterado; cada lançamento soma zero.
- **UT-03** — Dado um débito aplicado, quando o mesmo TransactionId chega de novo, então Duplicate com o resultado original.
- **UT-04** — Dado um débito de 10,00, quando crédito de 25,00 e estorno do débito (duas vezes), então saldo 3.025,00 após o crédito, +10,00 uma única vez no estorno, e estorno de débito inexistente é recusado.
- **UT-05** — Dada aposta mínima 1,00, quando a recarga é pedida com saldo 0,50 e com saldo 1,00, então recarrega para 3.000,00 e recusa com RECHARGE_NOT_ALLOWED.

### 7.3 Testes de Integração
- **IT-01** — Dado PostgreSQL real, quando um jogador novo é debitado em 10,00, então o saldo é 2.990,00 e o schema `wallet` tem um lançamento com duas partidas que somam zero.
- **IT-02** — Dados 20 débitos com o mesmo TransactionId em paralelo, quando executados, então existe um único lançamento e todos recebem o mesmo resultado.
- **IT-03** — Dado débito, crédito e estorno reais, quando repetidos, então o saldo final é o esperado e não há lançamento duplicado.
- **IT-04** — Dado o Host real com sessões de dois jogadores, quando cada um lê o saldo e pede recarga, então cada um só vê o próprio saldo, recarga funciona abaixo do mínimo e retorna 409 acima; sem sessão → 401.
- **IT-05** — Dados 500 débitos sequenciais com Postgres real, quando medidos, então p99 ≤ 20 ms (`Category=Performance`).
- **IT-06** — Dado um lançamento gravado, quando se tenta UPDATE/DELETE direto no banco, então o banco recusa.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais de IT-04, quando validadas contra o OpenAPI gerado, então conferem (contrato v1 consumido pelas SPEC-0022, 0023, 0024 e 0026).

### 7.5 Testes E2E
- N/A — o saldo e a recarga aparecem para o jogador pela SPEC-0026, cujos E2E cobrem a jornada (user_facing: false — a jornada do jogador é provada pelos E2E da SPEC-0026, que consome este contrato).

### 7.6 Outros
- Revisão do documento `operator-wallet-api.md` no G4 contra o ADR-0007.

**Dublês e dados de teste:** limites pela porta `IOperatorLimits` com dublê do contrato SPEC-0020@1.

**Ambiente de execução:** Testcontainers (Postgres, Redis); Host real via WebApplicationFactory.

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** schema `wallet` novo (migração inicial); privilégios de só inserção na tabela de lançamentos.
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
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05 com a tag `SPEC-0021:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01 com a tag `SPEC-0021:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0021`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
