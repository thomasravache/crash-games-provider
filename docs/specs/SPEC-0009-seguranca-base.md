---
id: SPEC-0009
title: "Segurança base: lançamento demo, sessão do jogador e acesso"
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0003, SPEC-0005]
consumes_contract: []
contract_version: 1
touches: ["src/backend/Modules/Operators/**", "src/backend/BuildingBlocks/Security/**", "src/backend/AppHost/Realms/**", "src/backend/Host/**", "artifacts/openapi/**", "src/frontend/packages/contracts/src/**", "tests/backend/Modules/Operators.Tests/**", "tests/backend/Security.Tests/**"]
adrs: [ADR-0001, ADR-0014, ADR-0015, ADR-0019, ADR-0020]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0009 — Segurança base: lançamento demo, sessão do jogador e acesso

## 1. Visão Geral
Implementa as três identidades do ADR-0015: requisições de operadores assinadas com HMAC, lançamento de jogo com token de uso único e sessão curta do jogador (inclusive no hub SignalR), e o backoffice protegido por Keycloak com isolamento por operador. Também negação por padrão, cabeçalhos de segurança, limite de taxa e a trava que impede dinheiro real.

## 2. Motivação & Escopo
**Motivação:** sem identidade e autorização desde a fundação, todo endpoint posterior nasce inseguro; a trava de dinheiro real garante a fase demo (ADR-0020).

**Objetivos (dentro do escopo):**
- Módulo Operators: operador `demo` semeado em Development, chaves de API (duas ativas para rotação), associação operador → sala (`demo`).
- Autenticação HMAC para `/api/v1/operator/*`; lançamento e troca por sessão; política do hub `/hubs/rounds` (só a sala do token).
- Realm `apogeu` no Keycloak (import no AppHost): papéis `provider-admin` (MFA), `operator-admin`, `operator-viewer`; organizações = operadores; `GET /api/v1/backoffice/me` como prova.
- `GET /dev/launch-demo` só em Development (redireciona para uma launchUrl demo).
- Política global "negar por padrão", cabeçalhos de segurança e limite de taxa nos endpoints públicos.

**Não-objetivos (fora do escopo):**
- Telas do backoffice; carteira e apostas; adaptadores de agregador.

## 3. Dependências
- **Implementações necessárias:** SPEC-0003 (harness, fixture Keycloak) e SPEC-0005 (Keycloak/Postgres no AppHost).
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0015 (identidades), ADR-0014 (segredos), ADR-0020 (trava de dinheiro real).

**Decisão:**
- Assinatura: `HMAC-SHA256(secret, timestamp + "\n" + METHOD + "\n" + path + "\n" + hex(SHA256(body)))`, cabeçalhos `X-Apogeu-Key`, `X-Apogeu-Timestamp` (Unix s), `X-Apogeu-Signature` (hex); janela ±300 s; assinatura já vista na janela = replay (cache Redis).
- Token de lançamento: 32 bytes aleatórios, hash guardado, TTL 60 s, uso único. Sessão: JWT assinado pelo provedor (chave em parâmetro secreto), 8 h, claims `sub` (jogador pseudônimo), `op`, `sala`, `cur`, `mode`.
- Backoffice: JwtBearer OIDC padrão contra o Keycloak, sem SDK de fornecedor.

**Justificativa:** padrões de mercado de integração com cassinos + OIDC trocável.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** OAuth client credentials para operadores (menos comum entre cassinos; HMAC é o esperado pelos integradores); sessão de jogador em cookie (o jogo roda em iframe de outro domínio).

**ADRs:** ADR-0001, ADR-0014, ADR-0015, ADR-0019, ADR-0020.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** lançamento p99 ≤ 100 ms local — medido em IT-01.
- **Segurança:** negação por padrão; OWASP (autenticação quebrada, controle de acesso, replay) coberto por IT-02..IT-06; cabeçalhos HSTS, CSP de frame-ancestors configurável por operador, `X-Content-Type-Options` — IT-08.
- **Privacidade e dados pessoais:** só ID pseudônimo, apelido opcional, moeda e idioma; tokens, segredos e assinaturas mascarados em logs — IT-07.
- **Disponibilidade e resiliência:** limite de taxa protege lançamento e sessão — IT-08.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A — Keycloak local.

## 6. Artefato A — Contrato
**Interface:** REST v1 + política do hub (contrato v1 consumido pela SPEC-0010).

```text
POST /api/v1/operator/launch            (HMAC)
  body { playerId, nickname?, currency:"BRL", gameCode:"pipa", mode:"demo", language:"pt-BR" }
  200 { launchUrl, expiresAt }
  400 VALIDATION_ERROR | 401 UNKNOWN_KEY | 401 INVALID_SIGNATURE | 401 TIMESTAMP_OUT_OF_WINDOW
  403 REAL_MONEY_DISABLED (mode != "demo") | 409 REPLAY_DETECTED | 429 RATE_LIMITED
POST /api/v1/sessions
  body { launchToken }
  200 { sessionToken, expiresAt, player:{ nickname }, salaId, currency, mode }
  401 LAUNCH_TOKEN_INVALID | 401 LAUNCH_TOKEN_EXPIRED | 401 LAUNCH_TOKEN_USED | 429 RATE_LIMITED
Hub /hubs/rounds: access_token = sessionToken; JoinSala(salaId) ≠ claim sala → HubException(FORBIDDEN); sem token → 401
GET /api/v1/backoffice/me (Bearer Keycloak) → 200 { userId, roles, operatorId? } | 401 (sem token, emissor, audiência ou assinatura inválidos)
                                           | 403 (token válido do realm sem nenhum papel Apogeu)
GET /dev/launch-demo (só Development) → 302 launchUrl; em outros ambientes → 404
launchUrl = {gameBaseUrl}/?lt={launchToken}
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Assinatura correta | vetores de assinatura | HMAC calculado bate com o esperado | UT-01 |
| Janela de tempo | timestamp ±301 s | rejeitado; ±299 s aceito | UT-02 |
| Token de lançamento | uso único, TTL 60 s | segundo uso e uso após 61 s rejeitados | UT-03 |
| Dinheiro real bloqueado | `mode:"real"` | 403 REAL_MONEY_DISABLED | UT-04, IT-02 |
| Lançamento válido | HMAC correto, operador demo | 200 com launchUrl | IT-01 |
| Lançamento inválido | chave desconhecida, assinatura errada, timestamp fora, corpo inválido | 401/401/401/400 com código | IT-02 |
| Replay | mesma requisição assinada duas vezes | 409 REPLAY_DETECTED | IT-03 |
| Troca por sessão | launchToken válido / reutilizado / expirado / inválido | 200 / 401 USED / 401 EXPIRED / 401 INVALID | IT-04 |
| Hub restrito à sala | JoinSala de outra sala; sem token | FORBIDDEN; 401 | IT-05 |
| Backoffice isolado | sem token ou token inválido; token válido sem papel Apogeu; papéis válidos | 401; 403; 200 | IT-06 |
| Segredos fora dos logs | lançamento e sessão | assinatura, segredo e tokens mascarados | IT-07 |
| Limite de taxa e cabeçalhos | 61 lançamentos/min por chave; qualquer resposta | 429; cabeçalhos de segurança presentes | IT-08 |
| Rota de desenvolvimento | `/dev/launch-demo` em Production | 404 | IT-09 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dados segredo, timestamp, método, caminho e corpo fixos, quando a assinatura é calculada, então é igual ao vetor de referência; com 1 byte do corpo alterado, difere.
- **UT-02** — Dado o relógio fixo (FakeTimeProvider), quando o timestamp está a 299 s, então aceito; a 301 s, então TIMESTAMP_OUT_OF_WINDOW.
- **UT-03** — Dado um token emitido, quando usado duas vezes, então o segundo uso falha com USED; quando usado após 61 s, então EXPIRED.
- **UT-04** — Dado `mode:"real"`, quando o lançamento é validado, então REAL_MONEY_DISABLED.

### 7.3 Testes de Integração
- **IT-01** — Dado o operador demo e Postgres/Redis reais, quando o lançamento é assinado corretamente, então 200 com launchUrl e p99 ≤ 100 ms em 200 chamadas (o relógio injetado avança a janela do limite de taxa a cada 50 chamadas, para medir o lançamento sem esbarrar no 429 do IT-08; `Category=Performance`).
- **IT-02** — Dadas chave desconhecida, assinatura errada, timestamp fora, corpo sem `playerId` e `mode:"real"`, quando chamados, então 401 UNKNOWN_KEY, 401 INVALID_SIGNATURE, 401 TIMESTAMP_OUT_OF_WINDOW, 400 VALIDATION_ERROR e 403 REAL_MONEY_DISABLED.
- **IT-03** — Dada a mesma requisição assinada enviada duas vezes dentro da janela, então a segunda recebe 409 REPLAY_DETECTED.
- **IT-04** — Dado um launchToken, quando trocado, então 200 com JWT contendo `op`, `sala`, `cur`, `mode`; reutilizado → 401 LAUNCH_TOKEN_USED; após 61 s → EXPIRED; aleatório → INVALID.
- **IT-05** — Dado um cliente SignalR com sessão da sala `demo`, quando `JoinSala("outra")`, então FORBIDDEN; sem token, então a conexão é recusada com 401.
- **IT-06** — Dado o Keycloak real (Testcontainers) com o realm importado, quando `GET /backoffice/me` sem token → 401; token válido do realm sem nenhum papel Apogeu → 403; `operator-viewer`, `operator-admin` e `provider-admin` → 200 com os papéis. (O isolamento entre operadores — viewer de A pedindo dados de B — fica para o épico do backoffice, quando houver recurso por operador.)
- **IT-07** — Dada a captura de logs, quando lançamento e sessão ocorrem, então segredo, assinatura, launchToken e sessionToken não aparecem.
- **IT-08** — Dadas 61 chamadas de lançamento em 1 min com a mesma chave, então a 61ª recebe 429; toda resposta tem HSTS, `X-Content-Type-Options: nosniff` e CSP `frame-ancestors`.
- **IT-09** — Dado o Host em Production, quando `GET /dev/launch-demo`, então 404; em Development, 302 para uma launchUrl válida.

### 7.4 Testes de Contrato
- **CT-01** — Dado o OpenAPI gerado (SPEC-0006), quando as respostas reais de IT-01, IT-02 e IT-04 são validadas, então conferem com o contrato v1 (consumido pela SPEC-0010).

### 7.5 Testes E2E
- N/A — a jornada do jogador (lançamento → jogo) é provada pelos testes E2E da SPEC-0010, que consome este contrato; o backoffice não tem telas nesta fase (user_facing: false).

### 7.6 Outros
- Checklist OWASP Top 10 aplicável no G4.

**Dublês e dados de teste:** operador demo e usuários Keycloak de teste criados por fixture; FakeTimeProvider.

**Ambiente de execução:** Testcontainers (Postgres, Redis, Keycloak).

## 8. Plano de Rollout
- **Estratégia:** merge direto.
- **Dados/schema:** cria schema `operators`; seed do operador demo só em Development.
- **Compatibilidade:** API de operador v1 — mudança incompatível exige `/v2`.
- **Observabilidade:** métricas `security.launch_rejected{reason}`, `security.replay_detected`, `security.rate_limited`.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0014, ADR-0015, ADR-0019, ADR-0020 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0009:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, IT-07, IT-08, IT-09, CT-01 com a tag `SPEC-0009:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a spec SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — ? | 2026-10-07 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive" && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:02.77]     Dado um DTO com string?, quando o esquema é gerado, então a pr) — 07e1e4f | 2026-10-09 |
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0009`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v1 (inalterado) | 2026-10-09 | Escopo: realm em `src/backend/AppHost/Realms/**` (onde o AppHost importa), `src/backend/Host/**` (pipeline de segurança), contratos gerados (`artifacts/openapi/**`, `src/frontend/packages/contracts/src/**`); `/backoffice/me` no `Operators.Features`; regra do 403 (token sem papel Apogeu) definida e isolamento entre operadores adiado para o épico do backoffice (sem recurso por operador nesta fase); IT-01 mede com a janela do limite de taxa avançada pelo relógio injetado; texto do §7.5 sem citar ID de outra spec | SPEC_DEFECTs do Test-writer (G1) | Nenhuma (a SPEC-0010 consome só lançamento e sessão) | Architect — pela autorização do usuário para seguir recomendações (2026-10-09) |
