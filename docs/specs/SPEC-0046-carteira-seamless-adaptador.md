---
id: SPEC-0046
title: "Carteira seamless: adaptador HTTP da carteira do operador"
tier: full
type: feature
user_facing: false
status: proposed
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0021, SPEC-0024]
consumes_contract: [SPEC-0021@4]
contract_version: 1
touches: ["src/backend/Modules/Wallet/**", "tests/backend/Modules/Wallet.Tests/**", "src/backend/Modules/Operators/Operators.Contracts/**"]
adrs: [ADR-0002, ADR-0007, ADR-0009, ADR-0014, ADR-0018, ADR-0027]
external: []
size: M
approved_by:
approved_at:
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

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dadas as respostas OK, INSUFFICIENT_FUNDS, DUPLICATE, PLAYER_NOT_FOUND, TRANSACTION_NOT_FOUND, timeout e 500, quando mapeadas, então viram os `WalletResult` do contrato.

### 7.3 Testes de Integração
- **IT-01** — Dado um operador de teste HTTP real (WireMock-like em processo), quando a Apogeu debita em modo seamless, então a chamada vai assinada e o resultado Ok volta com o saldo do operador.
- **IT-02** — Dado o operador demorando 3 s, quando o débito é feito, então Unavailable e a aposta não é aceita (SPEC-0022).
- **IT-03** — Dado o operador respondendo 500 por 1 min, quando um crédito é feito, então é reenviado com backoff até OK e aplicado uma única vez.
- **IT-04** — Dado um crédito pendente e o processo reiniciado, quando a varredura roda, então o crédito é confirmado.
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

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PENDING | | |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0046`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
