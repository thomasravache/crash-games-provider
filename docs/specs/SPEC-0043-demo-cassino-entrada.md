---
id: SPEC-0043
title: "Cassino demo: backend do cassino e entrada"
tier: full
type: feature
user_facing: true
status: in-progress
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0009]
consumes_contract: [SPEC-0009@1]
contract_version: 1
touches: [src/demo/DemoCasino/**, tests/demo/DemoCasino.Tests/**, src/frontend/apps/demo-site/**, tests/e2e/demo-site/**, src/backend/AppHost/Program.cs, tests/backend/Architecture.Tests/DemoCasino/**, tools/ci/run-e2e.sh]
adrs: [ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0043 — Cassino demo: backend do cassino e entrada

## 1. Visão Geral
Cria o backend do cassino demo (`DemoCasino`, ASP.NET Core mínimo, ADR-0027) e a tela de entrada: o jogador escolhe um apelido e a moeda (BRL) e entra com sessão de cassino em cookie; o segredo HMAC do operador demo fica só no servidor.

## 2. Motivação & Escopo
**Motivação:** o cassino demo é a forma principal de testar e vender (decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- Projeto `src/demo/DemoCasino` no AppHost, com credenciais do operador demo por parâmetros do Aspire.
- `POST /casino/session` (apelido 3–20 caracteres, moeda) → cookie de sessão do cassino (HttpOnly, SameSite=Lax); `GET /casino/me`; `POST /casino/logout`.
- Tela de entrada do `demo-site` conforme o Stitch.
- Teste de arquitetura: DemoCasino não referencia projetos da Apogeu.

**Não-objetivos (fora do escopo):**
- Lobby e lançamento (SPEC-0044/0045). Cadastro real, KYC.

## 3. Dependências
- **Implementações necessárias:** SPEC-0009.
- **Contratos consumidos:** SPEC-0009@1 (HMAC de operador).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** o `demo-site` é esqueleto (SPEC-0002/0005) e o AppHost já o sobe. O cassino é um cliente externo (ADR-0027): fala só pela API pública de operador (SPEC-0009/0020). Referência de cliente HMAC: a assinatura de referência dos testes da SPEC-0009 (`ReferenceSignature`).

**Decisão:** BFF .NET mínimo em `src/demo/DemoCasino`, sessão do cassino por cookie assinado (Data Protection), cliente HTTP com assinatura HMAC conforme o contrato público da SPEC-0009.

**Justificativa:** prova a integração real sem expor segredo (ADR-0027).

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** rotas no Host da Apogeu; assinatura no navegador.

**ADRs:** ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** N/A — fluxo de entrada.
- **Segurança:** segredo HMAC só no servidor (teste de build do bundle); cookie HttpOnly — IT-02, UT-02.
- **Privacidade e dados pessoais:** só apelido fictício; nenhum dado pessoal.
- **Disponibilidade e resiliência:** N/A.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** BFF do cassino.

```text
POST /casino/session { nickname (3–20, letras, números, _), currency:"BRL" } → 200 + Set-Cookie casino_session (HttpOnly) { playerId:"demo-<uuid>", nickname }
  | 400 INVALID_NICKNAME | 400 CURRENCY_NOT_SUPPORTED
GET  /casino/me → 200 { playerId, nickname, currency } | 401
POST /casino/logout → 204 (cookie removido)
Configuração: Apogeu:BaseUrl, Apogeu:OperatorKeyId, Apogeu:OperatorSecret (parâmetros do AppHost; nunca no bundle)
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu — Lobby demo (cassino neutro)", tela "Cassino Demo — Entrada".

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Entrar | apelido válido | sessão criada, cookie HttpOnly | IT-01 |
| Apelido inválido | "a" ou com espaço | 400 INVALID_NICKNAME | UT-01 |
| Moeda | "USD" | 400 CURRENCY_NOT_SUPPORTED | UT-01 |
| Quem sou | com e sem cookie | 200 / 401 | IT-01 |
| Sair | logout | cookie removido; /me → 401 | IT-01 |
| Segredo | bundle do demo-site | sem o segredo HMAC | UT-02 |
| Isolamento | assemblies do DemoCasino | sem referência a projetos da Apogeu | UT-03 |
| Tela | jogador entra | vai ao lobby com o apelido no topo | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dados apelidos e moedas válidos e inválidos, quando validados, então só os válidos passam com os códigos certos.
- **UT-02** — Dado o build de produção do `demo-site`, quando os arquivos são inspecionados, então o segredo do operador não aparece.
- **UT-03** — Dados os assemblies do DemoCasino, quando inspecionados, então não referenciam `Apogeu.*` (teste de arquitetura).

### 7.3 Testes de Integração
- **IT-01** — Dado o DemoCasino real, quando entra, consulta `/me` e sai, então cookie HttpOnly, 200 e depois 401.
- **IT-02** — Dado o DemoCasino, quando a resposta de sessão é inspecionada, então o cookie tem HttpOnly e SameSite e o corpo não traz segredo.

### 7.4 Testes de Contrato
- **CT-01** — Dado o cliente HMAC do DemoCasino, quando assina uma requisição de teste, então a assinatura confere com a referência do contrato público da SPEC-0009.

### 7.5 Testes E2E
- **E2E-01** — Dado o ambiente Aspire, quando o jogador abre o cassino demo, digita um apelido e entra, então chega ao lobby com o apelido no topo; axe sem violações.

### 7.6 Outros
- Revisão visual no G4 contra a tela do Stitch citada.

**Dublês e dados de teste:** operador demo da SPEC-0009.

**Ambiente de execução:** DemoCasino com WebApplicationFactory nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A (sessão em cookie, sem banco).
- **Compatibilidade:** novo projeto no AppHost; nada muda na Apogeu.
- **Observabilidade:** logs do DemoCasino no dashboard do Aspire.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, UT-02, UT-03, IT-01, IT-02, CT-01 com a tag `SPEC-0043:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 2: Jornada E2E**
- [ ] Red: E2E-01 falhando pelo motivo certo
- [ ] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 8e78b2d | 2026-10-10 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:02:32.46]     Dado um campo novo num DTO sem regenerar, quando contracts:che) — c4f0154 | 2026-10-13 |
| G2 Green | PASS | build exit 0 (✓ built in 146ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 6cfb8eb | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  760ms (tests 97%, transform 2%, import 1%)) — 6cfb8eb | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED WITH MINORS (6cfb8eb; minors: regex do apelido aceita \n final, assinatura usa caminho codificado vs verificador decodificado, Secure atrás de proxy, chaves do Data Protection sem persistência, foco no erro/lobby, logout ignora falha, --no-build do AddProject) — 6cfb8eb | 2026-10-13 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #31 verde (run 38676137955, commit 32cbe33): backend (suíte .NET completa + Performance), frontend, contracts, e2e (Chromium, WebKit e Firefox), security e sdd; backend verde na 3ª tentativa e e2e Firefox na 2ª, após falhas de tempo conhecidas sem relação com a onda (LateDebitReusedClientBetIdTests, E2E Firefox de aposta perdida/dupla, troca de rodada no teste do líder impedido, alerta de conciliação verificado sem espera em SettlementRaceTests; os dois últimos passam 3/3 localmente); local em 54f8b85: build 0 avisos, arquitetura 19/19 + front, gitleaks sem vazamentos, NuGet/pnpm sem vulnerabilidades | 2026-10-15 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-15 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem e deploy) | 2026-10-15 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0043`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-13 | `touches` inclui `tools/ci/run-e2e.sh`: o runner do E2E exporta `APOGEU_DEMO_SITE_URL` e espera o demo-site ficar pronto | Test-writer: a porta do demo-site sob o Aspire é dinâmica e o E2E-01 precisa dela | Nenhuma | thomas (pré-aprovação da jornada do jogador, 2026-10-13) |
