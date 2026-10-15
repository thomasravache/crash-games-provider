---
id: SPEC-0061
title: Melhorias pós-jornada do jogador
tier: epic
type: feature
status: proposed
created: 2026-10-15
depends_on: [SPEC-0047]
adrs: [ADR-0009, ADR-0025, ADR-0027]
external: []
approved_by:
approved_at:
---

# SPEC-0061 — Melhorias pós-jornada do jogador (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0061`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
Fechar os pontos não-bloqueantes apontados nas revisões (G4) das ondas da jornada do jogador (SPEC-0031, SPEC-0044, SPEC-0045, SPEC-0060), depois que thomas testar a jornada completa (decisão de thomas, 2026-10-15: "vai rodar depois da jornada do usuário porque quero testar isso antes"). Visual: thomas aprovou o que foi implementado nas SPEC-0031/0044/0045 ("gostei da parte que implementou", 2026-10-15); por isso este épico não muda o visual.

## 2. Escopo
**Objetivos (dentro do escopo):**
- SPEC-0062: robustez e testes das abas de apostas da Pipa.
- SPEC-0063: robustez do lobby e da página do jogo do cassino demo (timeout, origem da `launchUrl`, navegação, foco, artes, observabilidade).

**Não-objetivos (fora do escopo):**
- Canonicalização do caminho na assinatura HMAC (spec `fix` própria, se thomas aprovar).
- Robustez dos testes intermitentes do backend (spec própria).
- Mudanças de visual (o implementado foi aprovado por thomas em 2026-10-15).

## 3. Arquitetura Alvo
**Contexto:** projeto existente; nada muda na arquitetura. Cada filha segue o padrão da spec que originou o ponto.

As filhas não têm contrato entre si e não se tocam: a SPEC-0062 fica no `game-sdk`/`game-ui`/`game-pipa`, a SPEC-0063 no `DemoCasino` e no `demo-site`. Podem rodar na mesma onda.

**Decisões (ADRs):** N/A — usa ADR-0009 (testes), ADR-0025 (design) e ADR-0027 (cassino externo).

**Regras de arquitetura a garantir (G3):** as já existentes: o DemoCasino não referencia a Apogeu (ADR-0027, testes de arquitetura da SPEC-0043).

## 4. Decomposição
<!-- Uma linha por spec filha. As colunas de dependência são informativas: a fonte da verdade é o frontmatter de cada filha. -->
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0062 | Abas de apostas: robustez e testes | lite | fix | S | SPEC-0031 | — |
| SPEC-0063 | Cassino demo: robustez do lobby e da página do jogo | full | fix | M | SPEC-0045, SPEC-0047 | — |

## 5. Estratégia de Entrega
- **Ambientes:** local até a SPEC-0012 (hospedagem).
- **Entrega por onda:** uma onda só, depois da jornada; sem feature flag.
- **Feature flags:** N/A
- **Rollback:** reverter o merge commit da onda.
- **Métricas de sucesso pós-release:** E2E da jornada do cassino verdes na CI; nenhum aviso novo de axe.

## 6. Riscos & Mitigações
- Mudanças de navegação no demo-site quebrarem a jornada — o E2E da jornada roda na CI (SPEC-0060).

## 7. Critérios de Aceite do Épico
<!-- Verificáveis ponta a ponta. Cada critério cita o teste que o prova (`SPEC-NNNN:E2E-01`; IT para critérios que não são jornada). O épico só fecha com todos marcados e todas as filhas implemented/deprecated. -->
- [ ] Abas de apostas sem regressões nos casos de reconexão, falha de entrada na sala e histórico — SPEC-0062:UT-01
- [ ] Lobby e página do jogo respondem rápido com a Apogeu lenta e só abrem o jogo da origem configurada — SPEC-0063:IT-01, SPEC-0063:E2E-01

## 8. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento. -->
Nenhuma.

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0061` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
