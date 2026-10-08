---
id: SPEC-0005
title: "Ambiente local com Aspire e observabilidade base"
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0002]
consumes_contract: []
contract_version: 1
touches: ["src/backend/AppHost/**", "src/backend/ServiceDefaults/**", "src/backend/BuildingBlocks/Observability/**", "tests/backend/AppHost.Tests/**", "tests/backend/ServiceDefaults.Tests/**", "src/backend/Host/Program.cs", "src/backend/Host/Host.csproj", "src/backend/Host/appsettings*.json"]
adrs: [ADR-0003, ADR-0010, ADR-0014, ADR-0017]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0005 — Ambiente local com Aspire e observabilidade base

## 1. Visão Geral
Cria o AppHost do Aspire que sobe com um comando todo o ambiente local (PostgreSQL, Redis, Keycloak, API, apps Vite) e o `ServiceDefaults` com OpenTelemetry, health checks, resiliência e mascaramento de dados sensíveis em logs.

## 2. Motivação & Escopo
**Motivação:** a validação é local por decisão do usuário (ADR-0013); o ambiente precisa refletir a topologia real e ter telemetria desde o primeiro dia.

**Objetivos (dentro do escopo):**
- `src/backend/AppHost`: recursos `postgres` (volume persistente, um banco `apogeu`), `redis`, `keycloak` (com import de realm vazio `apogeu`, preenchido pela SPEC-0009), `api` (Host), `game-pipa`, `backoffice` e `demo-site` via `AddViteApp` com pnpm.
- Parâmetros secretos do Aspire para senhas e chaves (sem valores no repositório).
- `src/backend/ServiceDefaults`: OpenTelemetry (logs, métricas, traces, OTLP), `/health` e `/alive`, resiliência padrão de HttpClient, logs estruturados com ID de correlação.
- Mascaramento de campos sensíveis em logs (`serverSeed`, `token`, `secret`, `signature`, `password`, `authorization`).

**Não-objetivos (fora do escopo):**
- Backend de observabilidade de produção (com a hospedagem).
- Realm Keycloak configurado (SPEC-0009).

## 3. Dependências
- **Implementações necessárias:** SPEC-0002 — Host e apps a orquestrar.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** Docker em execução.

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0003 (Aspire), ADR-0010 (dados), ADR-0014 (segredos), ADR-0017 (observabilidade).

**Decisão:** AppHost em C# com integrações oficiais (PostgreSQL, Redis, Keycloak, JavaScript/Vite); `ServiceDefaults` referenciado pelo Host; processador de logs que mascara chaves sensíveis antes de qualquer exportador.

**Justificativa:** um comando, mesma topologia de produção, telemetria visível no painel do Aspire.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** Docker Compose (ADR-0003).

**ADRs:** ADR-0003, ADR-0010, ADR-0014, ADR-0017.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** ambiente saudável em ≤ 90 s em máquina de desenvolvimento com imagens em cache — medido por IT-01.
- **Segurança:** nenhum segredo no repositório; senhas via parâmetros secretos — verificado por IT-05.
- **Privacidade e dados pessoais:** mascaramento de campos sensíveis — verificado por UT-01 e IT-04.
- **Disponibilidade e resiliência:** health checks refletem dependências — IT-02.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A — local.

## 6. Artefato A — Contrato
**Interface:** `dotnet run --project src/backend/AppHost` (ou `aspire run`) + endpoints de saúde.

```text
Recursos: postgres, redis, keycloak, api, game-pipa, backoffice, demo-site
GET /alive  → 200 sempre que o processo responde
GET /health → 200 {status:"Healthy"} | 503 {status:"Unhealthy", entries:{postgres|redis:...}}
Logs: JSON estruturado com TraceId, SpanId e propriedades; chaves sensíveis → "***"
OTLP: endpoint do painel do Aspire (local)
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ambiente sobe completo | AppHost iniciado | todos os recursos Running/Healthy | IT-01 |
| Saúde reflete dependências | Postgres parado | `/health` → 503 citando postgres; `/alive` → 200 | IT-02 |
| Rastreamento ponta a ponta | requisição à API | trace exportado; logs com o mesmo TraceId | IT-03 |
| Dado sensível em log | log com `serverSeed`/`token` | valor mascarado na saída e no exportador | UT-01, IT-04 |
| Sem segredos versionados | busca no repositório | nenhuma senha/chave literal no AppHost | IT-05 |
| Configuração obrigatória ausente | connection string faltando | inicialização falha com mensagem clara | UT-02 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dado um registro de log com propriedades `serverSeed`, `token` e `amount`, quando passa pelo processador de mascaramento, então `serverSeed` e `token` viram `***` e `amount` permanece.
- **UT-02** — Dada a configuração sem `ConnectionStrings:apogeu`, quando o Host valida as opções na inicialização, então lança erro citando a chave faltante.

### 7.3 Testes de Integração
- **IT-01** — Dado o AppHost via `Aspire.Hosting.Testing`, quando iniciado, então todos os recursos chegam a Healthy em ≤ 90 s.
- **IT-02** — Dado o ambiente em execução, quando o recurso postgres é parado, então `/health` retorna 503 citando postgres e `/alive` retorna 200.
- **IT-03** — Dada uma requisição a `/health`, quando concluída, então existe um trace exportado e os logs da requisição carregam o mesmo TraceId.
- **IT-04** — Dado um endpoint de teste que registra `token=abc`, quando chamado, então nem a saída de log nem o exportador OTLP contêm `abc`.
- **IT-05** — Dado o código do AppHost, quando o gitleaks e uma busca por literais de senha rodam, então nada é encontrado.

### 7.4 Testes de Contrato
- N/A — não há contrato entre specs (ambiente).

### 7.5 Testes E2E
- N/A — sem jornada de usuário; os apps só são servidos (user_facing: false).

### 7.6 Outros
- N/A

**Dublês e dados de teste:** nenhum; dependências reais via Aspire.

**Ambiente de execução:** local e CI (Docker).

## 8. Plano de Rollout
- **Estratégia:** merge direto.
- **Dados/schema:** banco `apogeu` vazio (schemas criados pelos módulos).
- **Compatibilidade:** N/A
- **Observabilidade:** é o que esta spec entrega; painel do Aspire local.
- **Rollback:** reverter o merge commit.
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0003, ADR-0010, ADR-0014, ADR-0017 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02 com a tag `SPEC-0005:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05 com a tag `SPEC-0005:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx` exit 1 (red: [xUnit.net 00:02:04.02]     Dada uma requisição a /health, quando concluída, então existe ) — ae79f2a | 2026-10-08 |
| G2 Green | PASS | build exit 0 (✓ built in 61ms); test exit 0 (Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: 1 m 6 s -); lint exit 0 (All matched files use Prettier code style!) — ccf128b | 2026-10-08 |
| G3 Arquitetura | N/A | Suíte de arquitetura chega com a SPEC-0003 na mesma onda; as regras rodam sobre o resultado integrado no G5 | 2026-10-08 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0005`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
