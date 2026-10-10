---
id: SPEC-0035
title: Site demo de cassino
tier: epic
type: feature
status: approved
created: 2026-10-10
depends_on: [SPEC-0019]
adrs: [ADR-0001, ADR-0007, ADR-0009, ADR-0025, ADR-0027]
external: []
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0035 — Site demo de cassino (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0035`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
Um cassino de demonstração que se integra à Apogeu exatamente como um operador real: lobby com os jogos do catálogo, login fictício, lançamento assinado pelo servidor do cassino e o jogo aberto num iframe. Serve para thomas testar o fluxo completo, prova o contrato de integração ponta a ponta e é o material da demo comercial.

## 2. Escopo
**Objetivos (dentro do escopo):**
- App `demo-site` (já esqueleto) com entrada (login fictício), lobby (catálogo via `GET /api/v1/games` do lado do servidor) e página do jogo com iframe — referência visual aprovada em 2026-10-10 (Projeto Stitch "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu — Lobby demo").
- Backend do cassino demo (BFF) que guarda a chave HMAC e chama `POST /api/v1/operator/launch` — o segredo nunca vai ao navegador (Q2).
- Carteira do cassino conforme Q3.

**Não-objetivos (fora do escopo):**
- Cadastro, KYC, pagamentos reais, jogos de outros provedores. Hospedagem pública (SPEC-0012).

## 3. Arquitetura Alvo
**Contexto:** O cassino demo é um **cliente externo** da Apogeu: fala só pela API pública de operador (SPEC-0009/0020) e, conforme Q3, implementa o contrato público da carteira (SPEC-0021, docs/integration/operator-wallet-api.md).

Navegador → demo-site (React) → BFF do cassino (assina HMAC) → API Apogeu (`/api/v1/games`, `/api/v1/operator/launch`) → URL de lançamento → iframe do game-pipa. Nenhum código do cassino referencia módulos internos da Apogeu.

**Decisões (ADRs):** Forma do BFF (Q2) vira ADR após a resposta.

**Regras de arquitetura a garantir (G3):** O cassino demo não referencia nenhum projeto da Apogeu além dos contratos públicos gerados (teste de arquitetura); nenhum segredo HMAC no bundle do navegador (teste de build).

## 4. Decomposição
<!-- Uma linha por spec filha. As colunas de dependência são informativas: a fonte da verdade é o frontmatter de cada filha. -->
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0043 | Backend do cassino e entrada | full | feature | M | SPEC-0009 | SPEC-0009@1 |
| SPEC-0044 | Lobby com o catálogo da Apogeu | full | feature | M | SPEC-0043, SPEC-0020 | SPEC-0020@2 |
| SPEC-0045 | Página do jogo com lançamento assinado | full | feature | M | SPEC-0044 | SPEC-0009@1 |
| SPEC-0046 | Carteira seamless: adaptador HTTP da Apogeu | full | feature | M | SPEC-0021, SPEC-0024 | SPEC-0021@4 |
| SPEC-0047 | Carteira própria do cassino (seamless) | full | feature | M | SPEC-0045, SPEC-0046 | SPEC-0021@4 |

## 5. Estratégia de Entrega
- **Ambientes:** local (Aspire) até a SPEC-0012.
- **Entrega por onda:** entrada + lobby, depois página do jogo; carteira do cassino por último.
- **Feature flags:** N/A.
- **Rollback:** reverter o merge da onda.
- **Métricas de sucesso pós-release:** N/A até haver ambiente.

## 6. Riscos & Mitigações
- Segredo HMAC exposto no navegador — BFF obrigatório e teste de build.
- O demo "trapacear" usando atalhos internos — regra de arquitetura: só API pública.

## 7. Critérios de Aceite do Épico
<!-- Verificáveis ponta a ponta. Cada critério cita o teste que o prova (`SPEC-NNNN:E2E-01`; IT para critérios que não são jornada). O épico só fecha com todos marcados e todas as filhas implemented/deprecated. -->
- [ ] O jogador entra no cassino demo, escolhe a Pipa no lobby e joga no iframe com lançamento assinado — SPEC-0043:E2E-01, SPEC-0044:E2E-01, SPEC-0045:E2E-01
- [ ] Nenhum segredo HMAC chega ao navegador e o cassino não referencia código da Apogeu — SPEC-0043:UT-02, SPEC-0043:UT-03
- [ ] Com a carteira própria, o saldo do cassino reflete apostas e cash-outs — SPEC-0047:E2E-01
- [ ] O adaptador seamless resiste a operador fora do ar sem duplicar créditos — SPEC-0046:IT-03, SPEC-0046:IT-04

## 8. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento. -->
- [x] Q1. O cassino demo substitui o atalho `/dev/launch-demo` como forma principal de testar? Recomendação: sim; o atalho fica só para os testes automatizados. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")
- [x] Q2. Onde fica o backend do cassino (BFF): recomendação — um projeto .NET mínimo separado no AppHost (`DemoCasino`), que age como operador externo; alternativa: rotas no próprio Host (mais simples, mas mistura cassino e provedor). — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")
- [x] Q3. Carteira: recomendação em duas fases — primeiro o cassino usa a carteira demo da Apogeu (transparente); depois implementa o contrato público de carteira do operador (seamless), provando a integração que operadores reais farão. — Resposta: recomendação aceita (thomas, 2026-10-10: "recomendado")

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0035` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
