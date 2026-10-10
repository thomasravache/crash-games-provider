---
id: SPEC-0034
title: Backoffice B2B da Apogeu
tier: epic
type: feature
status: proposed
created: 2026-10-10
depends_on: [SPEC-0019]
adrs: [ADR-0001, ADR-0009, ADR-0015, ADR-0020, ADR-0025, ADR-0026]
external: []
approved_by:
approved_at:
---

# SPEC-0034 — Backoffice B2B da Apogeu (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0034`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
O provedor (e, conforme Q1, cada operador) opera o negócio pelo backoffice: vê o desempenho (apostado, pago, GGR, RTP observado) por operador e período, gerencia operadores e suas chaves, habilita jogos e limites por moeda, audita qualquer rodada pelo provably fair, investiga apostas de um jogador e gera o fechamento mensal. É a tela que o comprador B2B avalia na demo comercial — com a identidade Apogeu (ADR-0025).

## 2. Escopo
**Objetivos (dentro do escopo):**
- Shell do app `backoffice` (React) com login SSO via Keycloak (SPEC-0009), menu, seletor de operador e período, modos claro e escuro, tokens do ADR-0025 e logo Arco.
- Telas (referência visual aprovada por thomas em 2026-10-10, Projeto Stitch "Apogeu Games — Identidade e Backoffice" (1861192770945985645)): Painel; Operadores; Catálogo e limites; Rodadas e auditoria; Apostas; Relatórios; Login.
- APIs de leitura do backoffice nos módulos donos dos dados (Operators, Rounds, Betting, Wallet, Fairness), consumidas só pelo app; escritas (habilitar jogo, limites, rotacionar chave, desativar operador) com trilha de auditoria.
- Papéis e escopo de dados conforme Q1/Q2.

**Não-objetivos (fora do escopo):**
- Hospedagem e deploy (SPEC-0012). Conciliação financeira com operadores reais e faturamento. Gestão de jogadores (o jogador pertence ao operador). Edição de RTP/cap em produção sem fluxo de aprovação.

## 3. Arquitetura Alvo
**Contexto:** Monólito modular (ADR-0001) com o app `backoffice` já esqueleto (SPEC-0002/0009: login Keycloak). Nada muda na arquitetura: cada módulo expõe endpoints de backoffice sob `/api/backoffice/*` com a política de backoffice (SPEC-0009), e o app consome só esses endpoints e os contratos gerados (ADR-0008).

Leituras agregadas (painel, relatórios) vêm do **resumo imutável por rodada e operador** da SPEC-0024, nunca de varrer apostas. Busca de apostas lê o Betting; auditoria de rodada reusa a verificação da SPEC-0007/0008; operadores, chaves, catálogo e limites são do Operators (SPEC-0009/0020). Toda escrita grava evento de auditoria (quem, quando, antes/depois).

**Decisões (ADRs):** ADR-0025 (identidade visual) aceito. Papéis/escopo (Q1/Q2) viram ADR após a resposta.

**Regras de arquitetura a garantir (G3):** Endpoints de backoffice exigem a política de backoffice e escopo de operador quando aplicável (teste de que todo endpoint `/api/backoffice/*` declara política); o app `backoffice` não importa o `game-sdk` (dependency-cruiser); cores só por tokens (ADR-0025).

## 4. Decomposição
<!-- Uma linha por spec filha. As colunas de dependência são informativas: a fonte da verdade é o frontmatter de cada filha. -->
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0036 | Shell, login, papéis e trilha de auditoria | full | feature | M | SPEC-0009 | — |
| SPEC-0037 | Painel de desempenho | full | feature | M | SPEC-0036, SPEC-0024 | SPEC-0024@1 |
| SPEC-0038 | Operadores, chaves e comissão | full | feature | M | SPEC-0036, SPEC-0020 | — |
| SPEC-0039 | Catálogo e limites por operador | full | feature | M | SPEC-0036, SPEC-0020 | SPEC-0020@1 |
| SPEC-0040 | Rodadas e auditoria provably fair | full | feature | M | SPEC-0036, SPEC-0024 | SPEC-0024@1 |
| SPEC-0041 | Busca de apostas | full | feature | M | SPEC-0036, SPEC-0024 | SPEC-0024@1, SPEC-0021@4 |
| SPEC-0042 | Relatórios e fechamento mensal | full | feature | M | SPEC-0036, SPEC-0038, SPEC-0024 | SPEC-0024@1 |

## 5. Estratégia de Entrega
- **Ambientes:** local (Aspire) até a SPEC-0012.
- **Entrega por onda:** shell + login primeiro; telas em paralelo depois, cada uma integrada com CI verde.
- **Feature flags:** N/A — app interno, sem tráfego externo antes da SPEC-0012.
- **Rollback:** reverter o merge da onda.
- **Métricas de sucesso pós-release:** N/A até haver ambiente.

## 6. Riscos & Mitigações
- Vazamento de dados entre operadores — escopo por operador vindo do token, testado em IT por tela.
- Relatórios divergentes do que o operador vê — tudo sai do resumo imutável por rodada (SPEC-0024).

## 7. Critérios de Aceite do Épico
<!-- Verificáveis ponta a ponta. Cada critério cita o teste que o prova (`SPEC-NNNN:E2E-01`; IT para critérios que não são jornada). O épico só fecha com todos marcados e todas as filhas implemented/deprecated. -->
- [ ] Admin entra por SSO e vê o shell com a identidade Apogeu nos temas claro e escuro — SPEC-0036:E2E-01, SPEC-0036:E2E-02
- [ ] Usuário de operador vê só os próprios dados em todas as telas — SPEC-0036:IT-02, SPEC-0037:IT-03, SPEC-0041:IT-03, SPEC-0042:IT-03
- [ ] Toda alteração fica na trilha de auditoria imutável — SPEC-0036:IT-03, SPEC-0036:IT-04
- [ ] O painel mostra os mesmos totais do resumo das rodadas — SPEC-0037:E2E-01
- [ ] Admin rotaciona a chave de um operador e altera limites que passam a valer no jogo — SPEC-0038:E2E-01, SPEC-0039:E2E-01
- [ ] Suporte audita uma rodada e encontra uma aposta com suas transações — SPEC-0040:E2E-01, SPEC-0041:E2E-01
- [ ] Financeiro gera o fechamento mensal com a comissão correta — SPEC-0042:E2E-01

## 8. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento. -->
- [x] Q1. Quem usa o backoffice: só a equipe Apogeu, ou também cada operador (vendo apenas os próprios dados)? Recomendação: os dois, no mesmo app, com escopo pelo token. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")
- [x] Q2. Papéis: recomendação — Apogeu: admin, financeiro, suporte; operador: gestor e leitura. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")
- [x] Q3. Quem altera catálogo e limites: recomendação — só admin Apogeu; o operador vê. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")
- [x] Q4. Comissão no fechamento mensal: recomendação — percentual do GGR (revenue share) configurável por operador. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")
- [x] Q5. Trilha de auditoria imutável de toda alteração no backoffice: recomendação — sim, visível para admin. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0034` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
