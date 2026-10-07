---
id: ADR-0011
title: Fluxo de mudança por pull request no GitHub
status: accepted
origin: decision
date: 2026-10-07
pillars: [fluxo-mudanca]
baseline: fluxo-mudanca@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Proteção de branch no GitHub; change_flow: pr no sdd-config; job sdd (pr-check) obrigatório; hooks commit-msg/pre-commit"
---

# ADR-0011 — Fluxo de mudança por pull request no GitHub

## Contexto e Problema
Projeto pessoal hoje, produto com equipe amanhã. Sem fluxo definido desde já, código sem teste e mudanças fora de spec entram direto na branch principal, e o histórico deixa de servir de evidência para auditoria.

## Direcionadores da Decisão
- Repositório no GitHub, conta `thomasravache`; acompanhamento só no repositório.
- Hoje uma pessoa só: o GitHub não permite aprovar o próprio PR.
- Trilha auditável (o laboratório e os operadores vão pedir).

## Opções Consideradas
- PR obrigatório com checks obrigatórios (revisão humana obrigatória a partir do segundo colaborador)
- Commits diretos na main (`change_flow: direct`)

## Resultado da Decisão
**Opção escolhida:** **PR obrigatório**, porque mantém a CI como guardiã mesmo sozinho e já deixa o processo pronto para equipe.

**Regras (verificáveis):**
- Toda mudança entra na branch principal por solicitação de mudança (PR).
- Checks obrigatórios (build, testes, `sdd`) e pelo menos 1 revisão humana antes do merge **a partir da entrada do segundo colaborador**; até lá, o próprio dono revisa o PR e a revisão independente (G4) é feita por agente revisor, registrada no gate.
- Sem push direto nem force-push na branch principal, inclusive para administradores.
- Merge com merge commit (sem squash), para as evidências dos gates continuarem válidas.
- Arquivos de controle (tools/sdd, sdd-config, ADRs, hooks, workflows) têm dono obrigatório na revisão (CODEOWNERS: @thomasravache).
- Correção urgente segue o mesmo caminho, com spec lite criada na hora e revisão acelerada.
- Commits em Conventional Commits com `Refs: SPEC-NNNN`; CHANGELOG em Keep a Changelog; versões em SemVer.

### Consequências
- **Boa**, porque a main fica sempre verde e o histórico serve de evidência para auditoria.
- **Ruim**, porque cada mudança leva alguns minutos a mais (PR + CI), mesmo sozinho.
- **Ruim**, porque, enquanto o projeto tem uma pessoa só, a revisão humana obrigatória fica desligada; a mudança dessa regra está prevista acima.

### Confirmação (G3)
Proteção de branch no GitHub; `change_flow: pr` no sdd-config; `vendor --codeowners`; `validate` aponta desvios.

## Prós e Contras das Opções
| Critério (peso) | PR obrigatório | Commit direto |
|---|---|---|
| Main sempre verde (5) | ✅ | ❌ |
| Velocidade solo (2) | ⚠️ | ✅ |
| Pronto para equipe/auditoria (4) | ✅ | ❌ |

## Mais Informações
Referências neutras: trunk-based development. GitHub rulesets — https://docs.github.com/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets — limitação de autoaprovação de PR **não verificada nesta sessão**. Verificado em 2026-10-07.
