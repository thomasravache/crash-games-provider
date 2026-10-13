---
id: SPEC-0046
title: "Carteira seamless: adaptador HTTP da carteira do operador"
tier: full
type: feature
user_facing: false
status: in-progress
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0021, SPEC-0024]
consumes_contract: [SPEC-0021@4]
contract_version: 1
touches: [src/backend/Modules/Wallet/**, tests/backend/Modules/Wallet.Tests/**, src/backend/Modules/Operators/Operators.Contracts/**, src/backend/Host/Internal/Generated/**, src/backend/Host/Program.cs]
adrs: [ADR-0002, ADR-0007, ADR-0009, ADR-0014, ADR-0018, ADR-0027]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0046 — Carteira seamless: adaptador HTTP da carteira do operador

## 1. Visão Geral
Implementa o adaptador `IOperatorWallet` que fala com a carteira do próprio operador por HTTP (modelo seamless, ADR-0007), seguindo o contrato público `docs/integration/operator-wallet-api.md`: assinatura HMAC, idempotência por transactionId, prazo, novas tentativas com backoff para crédito e estorno e registro no livro-razão espelho. O modo de carteira passa a ser configuração do operador (demo ou seamless).

## 2. Motivação & Escopo
**Motivação:** é o que os cassinos reais vão usar; a fase 2 do cassino demo prova isso ponta a ponta (decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- `SeamlessOperatorWallet` em Wallet.Infrastructure: chamadas `POST {walletUrl}/apogeu/v1/{balance|debit|credit|rollback}` assinadas.
- Débito com prazo de 2 s; resultado incerto → estorno automático pelo fluxo da SPEC-0022; crédito e estorno reenviados até confirmar (outbox, ADR-0002/0018).
- Seleção do adaptador por operador (`walletMode: demo | seamless`, URL da SPEC-0038).
- Livro-razão espelho com as respostas do operador.

**Não-objetivos (fora do escopo):**
- Carteira de transferência. Conciliação automática.

## 3. Dependências
- **Implementações necessárias:** SPEC-0021, SPEC-0024.
- **Contratos consumidos:** SPEC-0021@4.
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** contrato público da carteira (SPEC-0021) e adaptador demo existente; outbox do Wolverine ligado pela SPEC-0022.

**Decisão:** novo adaptador ao lado do demo, escolhido por operador; o restante (aposta, cash-out, liquidação) não muda.

**Justificativa:** trocar demo por real é trocar o adaptador (ADR-0007).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** cliente HTTP por operador sem livro-razão espelho (perde auditoria).

**ADRs:** ADR-0002, ADR-0007, ADR-0009, ADR-0014, ADR-0018, ADR-0027.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** débito p99 ≤ 300 ms com o operador respondendo em 100 ms — IT-05.
- **Segurança:** HMAC em toda chamada; o segredo nunca é logado (ADR-0014) — IT-01.
- **Privacidade e dados pessoais:** só playerId do operador.
- **Disponibilidade e resiliência:** operador fora → débito falha com WALLET_UNAVAILABLE, crédito/estorno reenviados até confirmar inclusive após reinício — IT-03, IT-04.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** adaptador de `IOperatorWallet` (SPEC-0021@4) + contrato público.

```text
POST {walletUrl}/apogeu/v1/debit|credit|rollback|balance  (headers HMAC do contrato público)
  corpo { transactionId, playerId, currency, amount, roundId?, betId }
  respostas { status: OK | INSUFFICIENT_FUNDS | DUPLICATE | PLAYER_NOT_FOUND | TRANSACTION_NOT_FOUND, balance, currency, transactionId }
Mapeamento → WalletResult: OK→Ok; INSUFFICIENT_FUNDS→InsufficientFunds; DUPLICATE→Duplicate; TRANSACTION_NOT_FOUND→TransactionNotFound;
  timeout/5xx/rede → Unavailable(retryable:true); PLAYER_NOT_FOUND → Unavailable(retryable:false) + alerta
Reenvio: crédito/estorno com backoff exponencial (1 s … 5 min) até confirmar; débito nunca é reenviado (a aposta não é aceita)
Operador: walletMode demo|seamless (padrão demo)
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Débito OK | operador responde OK | Ok com saldo | IT-01 |
| Saldo insuficiente | INSUFFICIENT_FUNDS | InsufficientFunds | UT-01 |
| Duplicado | DUPLICATE | Duplicate | UT-01 |
| Prazo | operador demora 3 s | Unavailable(retryable) e aposta não aceita | IT-02 |
| Crédito com operador fora | 5xx por 1 min | reenviado até OK, uma vez só | IT-03 |
| Reinício | processo reinicia com crédito pendente | reenviado e confirmado | IT-04 |
| Assinatura | qualquer chamada | HMAC válido pelo contrato público | CT-01 |
| Desempenho | operador a 100 ms | débito p99 ≤ 300 ms | IT-05 |
| Pendência sem configuração | configuração some ou modo vira demo com crédito pendente | pendência mantida (log Error) e entregue uma vez quando volta | IT-06 |
| Operador lento | um operador sempre estoura o prazo | créditos dos outros operadores confirmados sem esperar | IT-07 |
| Respostas e destinos inseguros | 3xx, moeda/transactionId divergentes, http fora do loopback | incerto, incerto, recusado sem chamada | UT-02 |
| Sem serviço próprio de varredura | Wallet.Infrastructure | nenhum IHostedService (reenvio pelo Wolverine) | UT-03 |
| Uma cadeia por transação | N créditos com o mesmo transactionId e operador fora | uma única cadeia de reenvio e chamadas limitadas | IT-08 |
| Pendência agendada na 1ª tentativa | not_configured com crédito/estorno guardado | Unavailable(retryable) para o chamador | UT-04 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-02** — Dado um operador que responde 307/308, uma resposta com `currency` ou `transactionId` divergentes e uma URL http fora do loopback, quando a carteira chama, então incerto, incerto e recusado sem chamada (Unavailable(false) e log Error); http em 127.0.0.1 é aceito.
- **UT-03** — Dado o assembly Wallet.Infrastructure, quando inspecionado, então nenhum tipo concreto implementa `IHostedService` (reenvio só pelo Wolverine).
- **UT-04** — Dado um crédito ou estorno cuja 1ª tentativa dá `not_configured` e cuja pendência foi agendada, quando a carteira responde ao chamador, então `Unavailable(retryable: true)` (o débito continua `retryable: false`).
- **UT-01** — Dadas as respostas OK, INSUFFICIENT_FUNDS, DUPLICATE, PLAYER_NOT_FOUND, TRANSACTION_NOT_FOUND, timeout e 500, quando mapeadas, então viram os `WalletResult` do contrato.

### 7.3 Testes de Integração
- **IT-01** — Dado um operador de teste HTTP real (WireMock-like em processo), quando a Apogeu debita em modo seamless, então a chamada vai assinada e o resultado Ok volta com o saldo do operador.
- **IT-02** — Dado o operador demorando 3 s, quando o débito é feito, então Unavailable e a aposta não é aceita (SPEC-0022).
- **IT-03** — Dado o operador respondendo 500 por 1 min, quando um crédito é feito, então é reenviado com backoff até OK e aplicado uma única vez.
- **IT-04** — Dado um crédito pendente e o processo reiniciado, quando a varredura roda, então o crédito é confirmado.
- **IT-06** — Dado um crédito pendente cujo operador perde a configuração ou passa a demo, quando o tempo passa, então a pendência não é descartada (log Error) e, quando a configuração volta, o crédito é entregue uma única vez com o mesmo transactionId.
- **IT-07** — Dados um operador que sempre estoura o prazo, outro cuja leitura de configuração lança e um saudável, quando há créditos pendentes nos três, então o do saudável é confirmado em até 12 s e o do que lançava é entregue quando a leitura volta.
- **IT-08** — Dado o operador fora do ar, quando o mesmo crédito (mesmo transactionId) é pedido N vezes, então existe uma única cadeia de reenvio agendada e o número de chamadas ao operador fica limitado ao backoff de uma cadeia; ao voltar, é aplicado uma vez.
- **IT-05** — Dado o operador respondendo em 100 ms, quando 300 débitos são medidos, então p99 ≤ 300 ms (`Category=Performance`).

### 7.4 Testes de Contrato
- **CT-01** — Dadas as requisições reais enviadas ao operador de teste, quando verificadas com a assinatura de referência do contrato público, então conferem.

### 7.5 Testes E2E
- N/A — sem interface; a jornada completa é provada pelo E2E da SPEC-0047 (cassino demo com carteira própria).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operador de teste HTTP em processo com respostas programáveis.

**Ambiente de execução:** Testcontainers (Postgres) e Host real.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** coluna `wallet_mode` do operador (padrão demo) e tabela de pendências de reenvio (expansão).
- **Compatibilidade:** operadores existentes continuam demo.
- **Observabilidade:** métricas `wallet.remote_calls{op,status}`, `wallet.retries`, alerta para PLAYER_NOT_FOUND.
- **Rollback:** reverter o merge commit e a migração.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0046:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
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
| G0 Spec | PASS | validate: 0 erro(s) — 420d3b6 | 2026-10-10 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:02:23.01]     Dado o backend compilado, quando contracts:generate roda, entã) — 4dc45ab | 2026-10-13 |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | FAIL | Reviewer independente (2ª revisão, e7d91c1): CHANGES REQUESTED — MAJOR A sem deduplicação por transactionId cada chamada incerta do chamador cria outra cadeia de reenvio (amplificação de carga e M3 reaberto); minor B not_configured com pendência agendada devolve Unavailable(false) e a Betting manda para dead-letter | 2026-10-13 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0046`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-13 | Plano de testes e Mapa com IT-06, IT-07, UT-02, UT-03 (correções do 1º G4) e IT-08, UT-04 (2º G4): uma única cadeia de reenvio por transactionId; pendência agendada responde `Unavailable(retryable: true)`; lock files dos projetos que referenciam a Wallet mudam junto; crédito e estorno exigem chamador com retry durável (todos os da Betting têm) | 2º review G4: sem deduplicação cada nova tentativa do chamador criava outra cadeia (amplificação de carga) e o `not_configured` deixava a Betting divergente | Nenhuma | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
| 1 (inalterado) | 2026-10-13 | `touches` inclui `src/backend/Host/Program.cs`: descoberta dos handlers da Wallet no Wolverine e fila local do reenvio com paralelismo explícito (só o Host configura o Wolverine, ADR-0028 / `MessagingOwnedByHost`); o host de teste da Wallet passa a manter o `HostedMessaging` do ServiceDefaults | Implementer: os handlers de reenvio não são descobertos sem o Host, e o filtro de hosted services do `WalletApiHost` desligava o Wolverine nos testes | Nenhuma | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
| 1 (inalterado) | 2026-10-13 | `touches` inclui `src/backend/Host/Internal/Generated/**` (codegen do Wolverine, ADR-0028); o reenvio durável de crédito e estorno usa mensagem agendada do Wolverine pelo outbox (ADR-0002/0018), como `CreditCashOut`/`RefundBet`, sem fila própria; pendência sem configuração ou com o modo trocado não é descartada; um operador lento não atrasa os demais | Review G4: a 1ª implementação criou uma fila própria (`wallet.pending_resends` + varredura), desvio do ADR-0002 sem ADR, e podia descartar prêmio pendente | Nenhuma | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
| 1 (inalterado) | 2026-10-13 | Origem provisória de `IOperatorWalletSettings`: implementação no Wallet.Infrastructure lida da configuração (`Wallet:Operators:{op}:Mode`, `WalletUrl`, `KeyId`, `Secret` por variável de ambiente/segredo), operador sem configuração = demo; a SPEC-0038 troca pela coluna `wallet_mode` do cadastro de operadores | Test-writer: o Host precisa de uma implementação e o cadastro do operador (SPEC-0038) ainda não existe nem está no `touches` | SPEC-0038, SPEC-0047 | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
