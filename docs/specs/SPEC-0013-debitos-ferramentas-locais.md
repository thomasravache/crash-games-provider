---
id: SPEC-0013
title: Robustez do hook format-staged e override de peer do jsx-a11y
tier: lite
type: fix
user_facing: false
status: proposed
created: 2026-10-08
parent:
depends_on: []
consumes_contract: []
touches: [".githooks/format-staged", "src/frontend/pnpm-workspace.yaml", "tests/backend/Repository.Tests/**"]
adrs: [ADR-0011, ADR-0012]
external: []
size: S
approved_by:
approved_at:
---

# SPEC-0013 — Robustez do hook format-staged e override de peer do jsx-a11y

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Débitos aceitos na revisão (G4) da SPEC-0002: (1) `.githooks/format-staged` passa a lista de arquivos staged sem aspas, então um caminho com espaço é dividido em vários argumentos e a verificação falha ou ignora o arquivo; (2) o hook verifica a cópia do arquivo na árvore de trabalho, não a cópia staged — com `git add -p` pode aprovar ou reprovar conteúdo que não vai no commit; (3) `pnpm-workspace.yaml` força `eslint-plugin-jsx-a11y@6.10.2` a aceitar ESLint 10 (fora do intervalo declarado pelo plugin). Impacto baixo: não há caminhos com espaço hoje e a CI (SPEC-0004) é a verificação definitiva.

## 2. Causa Raiz
`.githooks/format-staged` linhas 14 e 22 expandem a lista sem aspas e leem os arquivos da árvore de trabalho; o plugin jsx-a11y não publicou versão com suporte declarado ao ESLint 10 (última 6.10.2, de 2024-10-26).

## 3. Mudança Proposta
Itera os caminhos linha a linha (com aspas) e verifica o conteúdo staged (`git show :<arquivo>` / `git checkout-index` para um diretório temporário); remove o override de peer assim que o jsx-a11y declarar suporte ao ESLint 10. Não muda: o hook continua só verificando (nunca reescreve) e não falha em commits vazios.

**Padrão seguido:** `.githooks/format-staged` e `.githooks/pre-commit` (SPEC-0002)

**Rollback:** revert do commit

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0013:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — o comportamento atual do hook já é coberto pelo IT-05 da SPEC-0002
- **UT-01** — Dado um arquivo staged mal formatado cujo caminho contém espaço, quando o hook roda, então o commit é rejeitado citando esse arquivo.
- **IT-01** — Dado um arquivo .cs bem formatado na árvore de trabalho mas com a versão staged mal formatada (`git add -p`), quando o hook roda, então o commit é rejeitado.

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
| G0 Spec | PENDING | | |
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
