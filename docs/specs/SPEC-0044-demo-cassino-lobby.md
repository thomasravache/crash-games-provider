---
id: SPEC-0044
title: "Cassino demo: lobby com o catálogo da Apogeu"
tier: full
type: feature
user_facing: true
status: proposed
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0043, SPEC-0020]
consumes_contract: [SPEC-0020@1]
contract_version: 1
touches: ["src/demo/DemoCasino/**", "tests/demo/DemoCasino.Tests/**", "src/frontend/apps/demo-site/**", "tests/e2e/demo-site/**", "src/backend/Modules/Operators/Operators.Features/Catalog/**", "tests/backend/Modules/Operators.Tests/**", "src/frontend/apps/game-pipa/public/kit/**"]
adrs: [ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0044 — Cassino demo: lobby com o catálogo da Apogeu

## 1. Visão Geral
Lobby do cassino demo: o BFF busca os jogos habilitados na Apogeu (`GET /api/v1/games`, assinado) e o lobby mostra cada jogo com a miniatura do kit visual da Apogeu, banner de destaque e abas de categoria.

## 2. Motivação & Escopo
**Motivação:** é onde o jogador escolhe o jogo, como num cassino real (decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- `GET /casino/games` no BFF (cache de 60 s) a partir do catálogo da Apogeu.
- Kit visual do jogo: miniatura e banner servidos pela Apogeu; o catálogo passa a informar `thumbnailUrl` e `bannerUrl` (extensão compatível do contrato da SPEC-0020 — Emenda v2 nela).
- Tela do lobby conforme o Stitch: banner da Pipa, abas Todos/Crash/Novidades, cartões com selo "Novo" e "Em breve".

**Não-objetivos (fora do escopo):**
- Busca de jogos com backend (o filtro é local na lista).

## 3. Dependências
- **Implementações necessárias:** SPEC-0043, SPEC-0020.
- **Contratos consumidos:** SPEC-0020@1 (`GET /api/v1/games`).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** o `demo-site` é esqueleto (SPEC-0002/0005) e o AppHost já o sobe. O cassino é um cliente externo (ADR-0027): fala só pela API pública de operador (SPEC-0009/0020). Referência de cliente HMAC: a assinatura de referência dos testes da SPEC-0009 (`ReferenceSignature`).

**Decisão:** o BFF consulta o catálogo assinado e repassa só o necessário; as artes vêm do kit do jogo (`game-pipa/public/kit`).

**Justificativa:** o lobby mostra exatamente o que o operador tem habilitado.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** lista fixa no front (não prova o catálogo).

**ADRs:** ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** lobby interativo ≤ 2 s no perfil mobile (medição da SPEC-0030).
- **Segurança:** chamada assinada no servidor; o navegador nunca chama a Apogeu direto — IT-01.
- **Privacidade e dados pessoais:** N/A.
- **Disponibilidade e resiliência:** Apogeu indisponível → lobby mostra aviso "Jogos indisponíveis no momento" — IT-02.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** BFF + extensão do catálogo.

```text
Apogeu GET /api/v1/games → [{ code, name, currencies, thumbnailUrl, bannerUrl }]   // thumbnailUrl/bannerUrl: novos campos (compatível)
BFF GET /casino/games → 200 [{ code, name, thumbnailUrl, bannerUrl, category:"crash", isNew }] | 502 PROVIDER_UNAVAILABLE | 401 sem sessão
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu — Lobby demo (cassino neutro)", tela "Cassino Demo — Lobby".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Lista | operador com a Pipa habilitada | Pipa com miniatura | IT-01 |
| Jogo desabilitado | admin desabilita a Pipa | some do lobby em ≤ 60 s | IT-03 |
| Apogeu fora | API indisponível | 502 e aviso na tela | IT-02 |
| Sem sessão | /casino/games | 401 | IT-01 |
| Kit visual | thumbnailUrl | imagem servida (200, tipo image/*) | CT-01 |
| Tela | jogador no lobby | banner, abas e cartões | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dada a resposta do catálogo, quando mapeada para o lobby, então categoria e selo "Novo" são derivados corretamente.

### 7.3 Testes de Integração
- **IT-01** — Dados DemoCasino e Apogeu reais, quando o jogador logado pede `/casino/games`, então recebe a Pipa com miniatura; sem sessão, 401.
- **IT-02** — Dada a Apogeu indisponível, quando o lobby é pedido, então 502 PROVIDER_UNAVAILABLE.
- **IT-03** — Dada a Pipa desabilitada para o operador demo, quando o cache expira, então ela some da lista.

### 7.4 Testes de Contrato
- **CT-01** — Dado o `GET /api/v1/games` real com os campos novos, quando validado contra o OpenAPI gerado e as URLs são buscadas, então confere e as imagens respondem 200 com tipo de imagem.

### 7.5 Testes E2E
- **E2E-01** — Dado o jogador logado no cassino demo, quando abre o lobby, então vê o banner da Pipa, a aba Crash e o cartão da Pipa com miniatura; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** catálogo semeado da SPEC-0020; dublê HTTP para a Apogeu fora do ar.

**Ambiente de execução:** DemoCasino + Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A.
- **Compatibilidade:** campos novos no catálogo são opcionais para clientes existentes.
- **Observabilidade:** contagem de falhas ao chamar a Apogeu no DemoCasino.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0044`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
