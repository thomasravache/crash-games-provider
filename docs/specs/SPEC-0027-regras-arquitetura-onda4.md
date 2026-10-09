---
id: SPEC-0027
title: Regras de arquitetura robustas a tipos gerados e regra do hub sem regra de negócio
tier: lite
type: fix
user_facing: false
status: proposed
created: 2026-10-09
parent:
depends_on: []
consumes_contract: []
touches: ["tests/backend/Architecture.Tests/**"]
adrs: [ADR-0001, ADR-0005]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0027 — Regras de arquitetura robustas a tipos gerados e regra do hub sem regra de negócio

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão (G4) da SPEC-0008: (1) as regras de arquitetura lançam NullReferenceException em tipos gerados pelo compilador no namespace global (`<>z__ReadOnlyList`, `<>y__InlineArray2`, `<PrivateImplementationDetails>`), forçando contornos no código de produção (`.ToArray()`, `Task[]`); (2) o ADR-0005 promete um teste de arquitetura de que o hub não contém regra de negócio, e ele não existe.

## 2. Causa Raiz
`TypeConditions.ModuleLayer.Of` assume namespace não nulo; não há regra sobre dependências dos hubs.

## 3. Mudança Proposta
Regras ignoram tipos gerados pelo compilador (namespace nulo ou nomes com `<`); nova regra: tipos `*Hub` só dependem de `Contracts`, mensageria e tipos do próprio slice de tradução — nunca de `Domain` nem de `Infrastructure`. Não muda: as regras existentes e o que elas proíbem.

**Padrão seguido:** `tests/backend/Architecture.Tests/Rules/**` (SPEC-0003)

**Rollback:** revert do commit

**Outras ocorrências:** A leitura da cadeia pela Fairness (`GetChainInfoAsync`, substituindo a tabela `rounds.chains` e o "auto-reparo") exige emenda de contrato da SPEC-0007 — fica registrada aqui como pendência para spec própria quando o épico de apostas terminar.

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0027:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — as regras atuais são cobertas pelos testes da SPEC-0003
- **UT-01** — Dado um assembly-fixture com coleção `[.. x]` para `IReadOnlyList<T>` e `Task.WhenAll(a, b)` (params span), quando a suíte de regras roda, então não lança exceção e não reporta violação desses tipos gerados.
- **IT-01** — Dado um hub-fixture que referencia um tipo de `Domain`, quando a regra do hub roda, então reprova; com os hubs reais (`RoundsHub`, `/hubs/bets`), passa.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 1c84d6b | 2026-10-09 |
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
