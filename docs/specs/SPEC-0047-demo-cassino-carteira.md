---
id: SPEC-0047
title: "Cassino demo: carteira própria (seamless)"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0045, SPEC-0046]
consumes_contract: [SPEC-0021@4]
contract_version: 1
touches: ["src/demo/DemoCasino/**", "tests/demo/DemoCasino.Tests/**", "src/frontend/apps/demo-site/**", "tests/e2e/demo-site/**"]
adrs: [ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0047 — Cassino demo: carteira própria (seamless)

## 1. Visão Geral
O cassino demo passa a ter carteira própria e implementa o contrato público da carteira do operador; o operador demo é configurado em modo seamless, e o saldo aparece no topo do cassino e muda conforme as apostas no jogo.

## 2. Motivação & Escopo
**Motivação:** prova ponta a ponta a integração que os cassinos reais farão (fase 2 da decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- Endpoints `/apogeu/v1/{balance|debit|credit|rollback}` no DemoCasino, verificando a assinatura, idempotentes por transactionId, com saldo inicial R$ 3.000,00 por jogador.
- Saldo no topo do cassino atualizado ao voltar do jogo e a cada 5 s.
- Configuração do operador demo em modo seamless no ambiente de desenvolvimento.

**Não-objetivos (fora do escopo):**
- Persistência durável (memória do processo basta para o demo).

## 3. Dependências
- **Implementações necessárias:** SPEC-0045, SPEC-0046.
- **Contratos consumidos:** SPEC-0021@4 (contrato público da carteira).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** o `demo-site` é esqueleto (SPEC-0002/0005) e o AppHost já o sobe. O cassino é um cliente externo (ADR-0027): fala só pela API pública de operador (SPEC-0009/0020). Referência de cliente HMAC: a assinatura de referência dos testes da SPEC-0009 (`ReferenceSignature`).

**Decisão:** o DemoCasino implementa o contrato público como qualquer operador; nada da Apogeu é referenciado.

**Justificativa:** é a referência viva para integradores.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** usar a carteira demo da Apogeu para sempre (não prova o seamless).

**ADRs:** ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** resposta da carteira do cassino ≤ 50 ms local.
- **Segurança:** assinatura da Apogeu verificada em toda chamada; requisição sem assinatura → 401 — IT-02.
- **Privacidade e dados pessoais:** só ids fictícios.
- **Disponibilidade e resiliência:** N/A — demo.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** contrato público da carteira (docs/integration/operator-wallet-api.md, SPEC-0021@4) implementado pelo DemoCasino.

```text
POST /apogeu/v1/balance|debit|credit|rollback (assinado pela Apogeu) → { status, balance, currency, transactionId }
Regras: saldo inicial 3.000,00; débito sem saldo → INSUFFICIENT_FUNDS; mesmo transactionId → DUPLICATE com o resultado original; estorno sem débito → TRANSACTION_NOT_FOUND
Cassino: GET /casino/balance → { balance, currency }
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu — Lobby demo (cassino neutro)", telas "Cassino Demo — Lobby" e "Cassino Demo — Pipa (Refinado)" (saldo no topo).

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Débito | aposta R$ 10 no jogo | saldo do cassino 2.990,00 | IT-01, E2E-01 |
| Sem saldo | aposta acima do saldo | INSUFFICIENT_FUNDS no jogo | IT-01 |
| Duplicado | mesmo transactionId | DUPLICATE com o resultado original | UT-01 |
| Estorno sem débito | rollback desconhecido | TRANSACTION_NOT_FOUND | UT-01 |
| Assinatura | chamada sem assinatura | 401 | IT-02 |
| Contrato | respostas | conferem com o contrato público | CT-01 |
| Jornada | apostar e retirar no cassino demo | saldo do topo reflete débito e crédito | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dada a carteira do cassino, quando débito, débito repetido, crédito e estorno sem débito, então OK, DUPLICATE com o mesmo resultado, OK e TRANSACTION_NOT_FOUND.

### 7.3 Testes de Integração
- **IT-01** — Dados DemoCasino e Apogeu reais em modo seamless, quando o jogador aposta e retira, então as chamadas chegam assinadas e o saldo do cassino reflete débito e crédito; aposta acima do saldo é recusada.
- **IT-02** — Dada uma chamada sem assinatura ou com assinatura inválida, quando chega ao DemoCasino, então 401.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais do DemoCasino, quando validadas contra o contrato público da carteira, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o cassino demo em modo seamless, quando o jogador aposta R$ 10,00, retira no voo e volta ao lobby, então o saldo do topo é 3.000 − 10 + pagamento; axe sem violações.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operador demo configurado em seamless só no ambiente de desenvolvimento.

**Ambiente de execução:** DemoCasino + Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A (memória).
- **Compatibilidade:** operador demo muda de demo para seamless só em Development.
- **Observabilidade:** contagem de chamadas por status no DemoCasino.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, IT-01, IT-02, CT-01 com a tag `SPEC-0047:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 2: Jornada E2E**
- [ ] Red: E2E-01 falhando pelo motivo certo
- [ ] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 66ae264 | 2026-10-10 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0047`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
