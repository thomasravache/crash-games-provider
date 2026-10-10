---
id: SPEC-0035
title: Site demo de cassino
tier: epic
type: feature
status: proposed
created: 2026-10-10
depends_on: [SPEC-0019]
adrs: [ADR-0001, ADR-0007, ADR-0009, ADR-0025]
external: []
approved_by:
approved_at:
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
| (a criar após Q1–Q3) | Entrada e sessão do cassino | full | feature | M | SPEC-0009 | — |
| (a criar) | Lobby com catálogo | full | feature | M | SPEC-0020 | — |
| (a criar) | Página do jogo e lançamento assinado | full | feature | M | SPEC-0026 | — |
| (a criar, se Q3 = carteira do cassino) | Carteira do cassino (contrato do operador) | full | feature | M | SPEC-0021 | — |

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
- [ ] Os critérios de aceite são escritos com as filhas, após as respostas de Q1–Q3.

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
