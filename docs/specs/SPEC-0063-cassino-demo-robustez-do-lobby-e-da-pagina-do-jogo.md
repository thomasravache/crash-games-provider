---
id: SPEC-0063
title: "Cassino demo: robustez do lobby e da página do jogo"
tier: full
type: fix
user_facing: true
status: proposed
created: 2026-10-15
parent: SPEC-0061
depends_on: [SPEC-0045, SPEC-0047]
consumes_contract: [SPEC-0009@1, SPEC-0020@3]
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: ["src/demo/DemoCasino/**", "tests/demo/DemoCasino.Tests/**", "src/frontend/apps/demo-site/**", "src/frontend/apps/game-pipa/public/kit/**", "tests/e2e/demo-site/**"]
adrs: [ADR-0009, ADR-0025, ADR-0027]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0063 — Cassino demo: robustez do lobby e da página do jogo

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Corrige os pontos não-bloqueantes dos reviews (G4) das SPEC-0044 e SPEC-0045 no cassino demo: respostas rápidas com a Apogeu lenta, só abrir jogo da origem configurada, navegação e foco corretos, artes leves e falhas visíveis na observabilidade.

## 2. Motivação & Escopo
**Motivação:** O cassino demo é a vitrine para vender os jogos; travar 100 s, perder o foco ou voltar ao jogo sem querer passa má impressão, e a `launchUrl` sem checagem de origem é uma brecha de defesa em profundidade.

**Objetivos (dentro do escopo):**
- Timeout de 10 s no cliente "Apogeu" (sem retry), com tempo esgotado → 502 PROVIDER_UNAVAILABLE no lobby e no lançamento.
- A `launchUrl` só é aceita na origem de `Apogeu:GameBaseUrl` (https fora de Development); outra origem → 502.
- Catálogo com arte ausente: o jogo aparece sem imagem em vez de esconder a lista.
- Navegação: resultado de lançamento descartado se o jogador saiu de `/jogo/{code}`; falha volta ao lobby com `replaceState`.
- Foco: `h1` do jogo ao abrir; aviso ou lobby ao voltar.
- Artes do kit em WebP/JPEG otimizadas, banner ≥ 1280 px, `width`/`height` e `loading`/`fetchpriority`; gradiente do banner pelo token de cor.
- Contadores do DemoCasino exportados por OTLP quando `OTEL_EXPORTER_OTLP_ENDPOINT` estiver definido.

**Não-objetivos (fora do escopo):**
- Mudanças de visual: thomas aprovou o visual implementado (2026-10-15).
- Mudanças no contrato público da Apogeu.

## 3. Dependências
<!-- As dependências entre specs ficam no frontmatter. Use `depends_on` só quando a IMPLEMENTAÇÃO da outra spec for necessária; se bastar o CONTRATO, use `consumes_contract` (permite rodar em paralelo). -->
- **Implementações necessárias:** SPEC-0045 e SPEC-0047 — o lobby, a página do jogo e o saldo do topo precisam estar na main.
- **Contratos consumidos:** SPEC-0009@1 (lançamento) e SPEC-0020@3 (catálogo com artes).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
<!-- Projeto existente: o padrão atual é lei. Qualquer desvio (lib, framework, camada, padrão ou convenção nova) só com type: migration ou ADR aprovado. -->
**Contexto:** segue `src/demo/DemoCasino/Lobby/ApogeuCatalog.cs` e `Play/ApogeuLauncher.cs` (SPEC-0044/0045) e o front `src/frontend/apps/demo-site/src/` (`App.tsx`, `routes.ts`, `game-screen.tsx`, `lobby-screen.tsx`).

**Decisão:** ajustes nos mesmos arquivos, sem slice novo.

**Justificativa:** corrige os achados dos reviews sem mudar fronteiras (ADR-0027).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** retry no lançamento (não é idempotente: cada chamada emite um token de uso único).

**ADRs:** ADR-0009, ADR-0025, ADR-0027.

## 5. Requisitos Não-Funcionais
<!-- Preocupações transversais de design doc. Não omita linhas: cada uma tem requisito + como será medido, ou "N/A — motivo". -->
- **Desempenho e escala:** com a Apogeu sem responder, o lobby e o lançamento respondem 502 em ≤ 10 s — IT-01.
- **Segurança:** `launchUrl` só na origem configurada (iframe com `allow-same-origin` não pode apontar para o cassino) — UT-01, IT-02.
- **Privacidade e dados pessoais:** N/A — só ids fictícios; a `launchUrl` continua fora dos logs.
- **Disponibilidade e resiliência:** timeout de 10 s sem retry; arte ausente não derruba o lobby — IT-01, UT-01.
- **Acessibilidade (UI):** WCAG 2.2 AA; foco gerenciado (2.4.3) e axe sem violações — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** `GET /casino/games` | `POST /casino/play/{gameCode}` | telas lobby e /jogo/{gameCode}

```text
```text
GET /casino/games → 200 [{ code, name, thumbnailUrl?, bannerUrl?, category, isNew }] | 401 | 502 PROVIDER_UNAVAILABLE (inclui tempo esgotado em 10 s)
POST /casino/play/{gameCode} → 200 { launchUrl } (origem = Apogeu:GameBaseUrl) | 401 | 404 GAME_NOT_AVAILABLE | 502 PROVIDER_UNAVAILABLE (inclui tempo esgotado e launchUrl de outra origem)
Tela /jogo/{gameCode}: foco no h1 ao abrir; lançamento tardio descartado se a rota mudou; falha → lobby com aviso, via replaceState
```
```

**Design:** telas do Stitch "Cassino Demo — Lobby" e "Cassino Demo — Pipa (Refinado)" (projeto 1861192770945985645), no visual implementado, aprovado por thomas em 2026-10-15.

**Arquivos/módulos afetados:** ver `touches` no frontmatter. N/A

### 6.1 Mapa de Comportamentos
<!-- Uma linha por comportamento observável (sucesso e cada erro). Toda linha referencia ao menos um teste da seção 7 — o G0 verifica. -->
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Apogeu lenta | sem resposta por mais de 10 s | 502 PROVIDER_UNAVAILABLE em ≤ 10 s, no lobby e no lançamento | IT-01 |
| Origem da launchUrl | `launchUrl` fora da origem configurada | 502 PROVIDER_UNAVAILABLE; iframe não abre | UT-01, IT-02 |
| Arte ausente | jogo sem `thumbnailUrl` | lobby mostra o jogo sem imagem | UT-01 |
| Navegação e foco | voltar ao lobby durante o lançamento; falha; abrir o jogo | fica no lobby; Voltar não reabre a falha; foco no h1 | E2E-01 |
| Observabilidade | falha ao chamar a Apogeu com OTLP configurado | contadores exportados | IT-03 |
| Contrato consumido | requisições ao catálogo e ao lançamento | conferem com o OpenAPI da Apogeu | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
<!-- Todo teste tem um ID e é escrito ANTES da implementação (Red). No código, cada teste carrega a tag `SPEC-0063:<ID>` — o G1 (`spec_graph.py verify`) verifica. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem testes: "N/A — motivo" SEM ID. Teste que protege comportamento existente e passa antes da mudança: marque `(guarda: passa antes da mudança)`. Estratégia completa: references/testing.md da skill. -->

### 7.1 Testes de Caracterização
<!-- Projeto existente sem cobertura na área alterada: CH-xx fixam o comportamento atual e PASSAM antes da mudança. -->
- N/A — os testes das SPEC-0043/0044/0045 cobrem o comportamento atual.

### 7.2 Testes Unitários
- **UT-01** — Dada a resposta da Apogeu, quando a `launchUrl` é de outra origem ou o catálogo vem sem arte, então o lançamento é recusado e o jogo do lobby fica sem imagem.
- **UT-02** — Dada a configuração do cliente "Apogeu", quando lida, então o timeout é 10 s e não há política de retry.

### 7.3 Testes de Integração
<!-- Com as dependências reais (containers/emuladores), não mocks. -->
- **IT-01** — Dada a Apogeu sem responder, quando o lobby e o lançamento são pedidos, então 502 PROVIDER_UNAVAILABLE em ≤ 10 s.
- **IT-02** — Dada a Apogeu real devolvendo a `launchUrl` na origem configurada, quando o jogador joga, então 200; com a origem trocada, 502.
- **IT-03** — Dado o OTLP configurado, quando uma chamada à Apogeu falha, então os contadores `democasino.apogeu.failures` e `democasino.launch.failures` são exportados.

### 7.4 Testes de Contrato
<!-- Obrigatório (CT-xx) se esta spec consome contrato de outra ou se outra spec consome o contrato desta. -->
- **CT-01** — Dadas as requisições reais do DemoCasino à Apogeu (catálogo e lançamento) com o timeout e a checagem de origem novos, quando validadas contra o OpenAPI gerado (SPEC-0009@1, SPEC-0020@3), então conferem.

### 7.5 Testes E2E
<!-- Obrigatório (E2E-xx) quando user_facing: true: jornada nova ou atualização do E2E que já cobre a jornada. Poucos e significativos. -->
- **E2E-01** — Dado o cassino demo, quando o jogador abre a Pipa, volta ao lobby durante um relançamento e provoca uma falha de lançamento, então fica no lobby, o Voltar do navegador não reabre o jogo que falhou e o foco vai para o h1 ao abrir; axe sem violações.

### 7.6 Outros
- Peso das artes do kit: cada arte ≤ 120 KB (verificado no CT de artes da SPEC-0044 estendido no IT-02).

**Dublês e dados de teste:** handler trocável do cliente "Apogeu" (atraso e origem), Apogeu real com Testcontainers, coletor OTLP em memória.

**Ambiente de execução:** DemoCasino + Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A
- **Compatibilidade:** `thumbnailUrl`/`bannerUrl` passam a opcionais na resposta do BFF (consumida só pelo demo-site).
- **Observabilidade:** contadores do DemoCasino exportados por OTLP quando configurado.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0063`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
