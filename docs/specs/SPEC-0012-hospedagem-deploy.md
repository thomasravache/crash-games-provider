---
id: SPEC-0012
title: "Hospedagem e deploy (staging e produção)"
tier: full
type: foundation
user_facing: false
status: proposed
created: 2026-10-07
parent: 
depends_on: [SPEC-0004, SPEC-0010]
consumes_contract: []
contract_version: 1
touches: ["infra/**", ".github/workflows/deploy-*.yml", "docs/runbooks/**"]
adrs: [ADR-0013, ADR-0017, ADR-0018]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0012 — Hospedagem e deploy (staging e produção)

## 1. Visão Geral
Leva a plataforma a ambientes reais: staging e produção como código, deploy pelo pipeline, banco gerenciado com backup, gerenciador de segredos, backend de observabilidade e smoke/E2E contra o ambiente. **Rascunho**: a nuvem e o orçamento ainda não foram decididos (ADR-0013); ao responder as questões abaixo a spec é completada e submetida ao H1.

## 2. Motivação & Escopo
**Motivação:** a fundação valida tudo localmente por decisão do usuário; este é o passo que fecha o G6 (deploy) de todas as specs e reativa as regras de staging/produção do ADR-0013.

**Objetivos (dentro do escopo):**
- Escolha da nuvem com estimativa de custo (atual, 2x e 10x) e comparação com o orçamento.
- Staging e produção como código (IaC), deploy automático em staging e com confirmação humana em produção.
- Banco gerenciado com backup e recuperação testada; segredos no gerenciador da nuvem; backend OTLP e alertas por SLO.

**Não-objetivos (fora do escopo):**
- Conformidade para dinheiro real (spec própria, ADR-0020).

## 3. Dependências
- **Implementações necessárias:** SPEC-0004 (pipeline) e SPEC-0010 (esqueleto andante para publicar).
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** conta de nuvem e orçamento definidos pelo usuário.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0013 (entrega), ADR-0017 (observabilidade), ADR-0018 (resiliência).

**Decisão:** a definir após as questões em aberto (candidatas: Azure Container Apps, AWS São Paulo, Fly.io/Railway).

**Justificativa:** a definir com a estimativa de custo.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** a definir.

**ADRs:** ADR-0013, ADR-0017, ADR-0018.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** metas do ADR-0019 verificadas em staging com k6.
- **Segurança:** segredos no gerenciador da nuvem; TLS; sem acesso público ao banco.
- **Privacidade e dados pessoais:** residência dos dados a confirmar (ADR-0014).
- **Disponibilidade e resiliência:** RPO ≤ 5 min e RTO ≤ 1 h (ADR-0018).
- **Acessibilidade (UI):** N/A — sem interface nova.
- **Custo:** estimativa obrigatória antes de escolher; alerta de gasto configurado.

## 6. Artefato A — Contrato
**Interface:** pipeline de deploy e URLs dos ambientes.

```text
A definir após as questões em aberto.
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Deploy em staging | merge na main | ambiente atualizado e smoke verde | IT-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado o módulo de infraestrutura, quando validado (`plan`/`validate` da ferramenta de IaC escolhida), então não há erros.

### 7.3 Testes de Integração
- **IT-01** — Dado um merge na main, quando o pipeline de deploy roda, então staging é atualizado e o smoke test passa.

### 7.4 Testes de Contrato
- N/A — a definir.

### 7.5 Testes E2E
- N/A — rascunho; a spec final inclui E2E contra staging (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** a definir.

**Ambiente de execução:** staging.

## 8. Plano de Rollout
- **Estratégia:** a definir.
- **Dados/schema:** a definir.
- **Compatibilidade:** N/A
- **Observabilidade:** ADR-0017.
- **Rollback:** a definir.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [ ] Qual nuvem e qual orçamento mensal para staging e produção? (thomas)
- [ ] Os dados dos jogadores precisam ficar em território brasileiro? (thomas, com assessoria jurídica)

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0012`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
