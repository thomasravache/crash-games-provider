---
id: SPEC-0032
title: Pool de conexões limitado no Fairness e no Operators
tier: lite
type: fix
user_facing: false
status: proposed
created: 2026-10-09
parent:
depends_on: []
consumes_contract: []
touches: ["src/backend/Modules/Fairness/Fairness.Infrastructure/FairnessInfrastructure.cs", "src/backend/Modules/Operators/Operators.Infrastructure/OperatorsInfrastructure.cs", "tests/backend/Modules/Fairness.Tests/Infrastructure/**", "tests/backend/Modules/Operators.Tests/Infrastructure/**"]
adrs: [ADR-0010, ADR-0018]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0032 — Pool de conexões limitado no Fairness e no Operators

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
No teste de carga da SPEC-0008 (IMP-02), uma rajada de entradas esgotou o `max_connections` do PostgreSQL ("53300 too many clients"). O Rounds foi corrigido com um pool próprio e limitado, mas o `FairnessDbContext` e o `OperatorsDbContext` ainda usam o pool padrão do Npgsql (até 100 conexões cada), compartilhando o mesmo servidor: uma rajada de lançamentos ou de verificações pode repetir a falha e derrubar os outros módulos junto.

## 2. Causa Raiz
`FairnessInfrastructure.cs:44` e `OperatorsInfrastructure.cs:29` registram o DbContext com a string de conexão sem `Maximum Pool Size`; o padrão do Npgsql (100) por módulo excede o `max_connections` somado do servidor.

## 3. Mudança Proposta
Cada módulo passa a ter pool próprio limitado, configurável (`Fairness:MaxPoolSize`, `Operators:MaxPoolSize`, padrão 10), pelo mesmo mecanismo do Rounds (`WithMaxPoolSize`). Não muda: esquemas, consultas, contratos e o Rounds.

**Padrão seguido:** `src/backend/Modules/Rounds/Rounds.Infrastructure/RoundsInfrastructure.cs` (`DefaultMaxPoolSize`, `WithMaxPoolSize`) e o IT-07 da SPEC-0008.

**Rollback:** revert do commit.

**Outras ocorrências:** Wallet e Betting (SPEC-0021/0022) criam DbContexts novos: devem nascer com o pool limitado seguindo a mesma referência — verificado no review (G4) dessas specs.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0032:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — comportamento funcional coberto pelas suítes da SPEC-0007 e da SPEC-0009.
- **UT-01** — Dado o registro de infraestrutura do Fairness e do Operators sem configuração, quando a string de conexão do DbContext é lida, então `Maximum Pool Size` = 10 e `Minimum Pool Size` ≤ 10; com `Fairness:MaxPoolSize`/`Operators:MaxPoolSize` = 4, vale 4 (falha hoje: pool padrão 100).
- **IT-01** — Dado o PostgreSQL real com `max_connections` baixo (ex.: 30), quando 300 verificações de rodada e 300 lançamentos chegam ao mesmo tempo, então nenhum falha com 53300 "too many clients" (falha hoje).

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma.

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — bd21bbc | 2026-10-09 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
