---
id: SPEC-0053
title: Teto de ganho por rodada nos limites do operador
tier: full
type: feature
user_facing: "false"
status: proposed
created: 2026-10-13
parent: SPEC-0019
depends_on: []
consumes_contract: []
contract_version: 1
touches: [src/backend/Modules/Operators/**, tests/backend/Modules/Operators.Tests/**, tests/backend/Modules/Betting.Tests/Contract/**]
adrs: [ADR-0001, ADR-0002, ADR-0020]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0053 — Teto de ganho por rodada nos limites do operador

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Acrescenta aos limites de cada operador e moeda o **teto de ganho por rodada** (`maxRoundWin`): a soma máxima que o operador paga numa rodada. É o dado que a SPEC-0054 usa para cortar a exposição no voo. Padrão BRL: R$ 500.000,00 (5× o ganho máximo por aposta).

## 2. Motivação & Escopo
**Motivação:** a chance de um crash passar de *x* é 0,97/*x*. Multiplicadores de mais de 1.000x aparecem algumas vezes por dia, e como todos os jogadores voam no mesmo multiplicador, o ganho máximo **por aposta** sozinho não limita a perda de uma rodada (5.000 jogadores × R$ 100.000). O thomas decidiu em 2026-10-13, depois de uma rodada de 2.627,77x na demo:
- teto por operador e por moeda;
- padrão de R$ 500.000;
- ao atingir o teto, retirada automática;
- aviso ao jogador.

**Objetivos (dentro do escopo):**
- `BetLimits` ganha `MaxRoundWin:Money` (mesma moeda), lido pelo `IOperatorLimits` com o cache atual.
- Coluna `max_round_win` em `operators.operator_limits`, com migração que preenche as linhas existentes com 5 × `max_win`.
- Seed e configuração com padrão BRL de R$ 500.000,00. A validação exige `MaxRoundWin ≥ MaxWin`; violar isso falha na carga, não em silêncio.

**Não-objetivos (fora do escopo):**
- Usar o teto (SPEC-0054); avisar o jogador (SPEC-0055); editar o teto no backoffice (a SPEC-0039 ganha o campo por emenda própria).

## 3. Dependências
- **Implementações necessárias:** N/A (SPEC-0020 já implementada).
- **Contratos consumidos:** N/A — estende o contrato da SPEC-0020 (Emenda v3 dela, registrada na aprovação desta).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** ADR-0001 dá limites e moedas ao módulo Operators; SPEC-0020 criou `IOperatorLimits`/`BetLimits` com cache invalidado por versão. Referência: `Operators.Contracts/BetLimits.cs`, `Operators.Infrastructure/Catalog/CatalogStore.cs`, `OperatorLimitsRecord`, migração `20261009194827_OperatorsCatalogAndLimits`.

**Decisão:** o teto é mais um campo de `BetLimits`, persistido e cacheado como os outros. Valor monetário em `Money` com a moeda dos limites.

**Justificativa:** quem paga é a carteira do operador (B2B seamless), então o teto é dele, por moeda. Manter o campo junto dos outros limites reaproveita o cache, a validação e a tela futura do backoffice.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** um teto global da Apogeu (um operador grande consumiria o teto dos outros; o thomas escolheu por operador); um teto em multiplicador (não limita valor quando as apostas são grandes).

**ADRs:** ADR-0001, ADR-0002, ADR-0020 (conformidade).

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** leitura continua ≤ 1 ms pelo cache existente — coberta pelo UT-03 da SPEC-0020, sem mudança.
- **Segurança:** N/A — dado de configuração, sem entrada externa nesta spec.
- **Privacidade e dados pessoais:** N/A
- **Disponibilidade e resiliência:** migração aditiva com padrão; nenhum operador fica sem teto — IT-01.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** `Operators.Contracts.IOperatorLimits` / `BetLimits` (extensão compatível do contrato da SPEC-0020).

```text
BetLimits { Currency, MinBet:Money, MaxBet:Money, MaxWin:Money, MaxRoundWin:Money }   // novo: MaxRoundWin
  MaxRoundWin: soma máxima paga pelo operador numa rodada, na moeda dos limites
  invariante: MaxRoundWin ≥ MaxWin (configuração que viole → erro na carga/validação, mensagem cita operador e moeda)
Padrão (seed/configuração): BRL MinBet 1,00 / MaxBet 1.000,00 / MaxWin 100.000,00 / MaxRoundWin 500.000,00
Migração: operators.operator_limits.max_round_win numeric(18,2) not null; linhas existentes = 5 × max_win
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Leitura do teto | operador demo em BRL | `MaxRoundWin` = 500.000,00 | IT-01 |
| Migração | linha existente com `max_win` 100.000,00 sem a coluna | `max_round_win` = 500.000,00 | IT-01 |
| Configuração inválida | `MaxRoundWin` < `MaxWin` | erro na validação citando operador e moeda | UT-01 |
| Contrato | `BetLimits` lido por outro módulo | campo presente, mesma moeda | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — os limites atuais já são cobertos pelos testes da SPEC-0020.

### 7.2 Testes Unitários
- **UT-01** — Dados limites BRL com `MaxWin` 100.000,00 e `MaxRoundWin` 50.000,00, quando validados, então erro que cita o operador e a moeda; com `MaxRoundWin` 500.000,00, então válidos.

### 7.3 Testes de Integração
- **IT-01** — Dado o Postgres real com uma linha de limites criada pela migração anterior (`max_win` 100.000,00), quando a migração nova roda e o `IOperatorLimits` é consultado para o operador demo em BRL, então `MaxRoundWin` = 500.000,00 e a leitura vem do cache na segunda chamada.

### 7.4 Testes de Contrato
- **CT-01** — Dado `BetLimits` em `Operators.Contracts`, quando lido pelo Betting via `IOperatorLimits`, então `MaxRoundWin` existe, é `Money` e tem a moeda dos limites (teste de arquitetura/contrato no Betting.Tests).

### 7.5 Testes E2E
- N/A — sem jornada nova; o efeito para o jogador é testado na SPEC-0054 e na SPEC-0055.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operador demo do seed.

**Ambiente de execução:** Testcontainers (Postgres).

## 8. Plano de Rollout
- **Estratégia:** merge direto (demo local).
- **Dados/schema:** coluna nova com preenchimento 5 × `max_win`; aditiva.
- **Compatibilidade:** campo novo em `BetLimits`; quem não o usa não muda.
- **Observabilidade:** N/A — configuração.
- **Rollback:** reverter o merge commit (a migração tem `Down`).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] O teto vale por operador, global ou os dois? — Por operador e moeda (thomas, 2026-10-13)
- [x] Valor padrão em BRL? — R$ 500.000,00, 5× o ganho máximo por aposta (thomas, 2026-10-13)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 91dd6a5 | 2026-10-13 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0053`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
