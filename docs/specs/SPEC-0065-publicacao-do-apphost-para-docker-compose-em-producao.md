---
id: SPEC-0065
title: Publicação do AppHost para Docker Compose em produção
tier: full
type: foundation
user_facing: false
status: proposed
created: 2026-10-15
parent: SPEC-0012
depends_on: [SPEC-0047]
consumes_contract: []
contract_version: 1
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: ["src/backend/AppHost/**", "src/backend/Host/**", "src/backend/Modules/Fairness/Fairness.Infrastructure/**", "src/backend/Modules/Operators/Operators.Features/Development/**", "src/backend/Modules/Operators/Operators.Infrastructure/**", "src/backend/Modules/Wallet/Wallet.Infrastructure/**", "tests/backend/AppHost.Tests/**", "tests/backend/Modules/Fairness.Tests/**", "tests/backend/Modules/Operators.Tests/**", "Directory.Packages.props", "infra/compose/**"]
adrs: [ADR-0029, ADR-0003, ADR-0014, ADR-0016]
external: []
size: M
approved_by:
approved_at:
---

# SPEC-0065 — Publicação do AppHost para Docker Compose em produção

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O AppHost passa a publicar a plataforma como um Docker Compose de produção (`aspire publish`, `Aspire.Hosting.Docker`): API, cassino demo, Postgres, Redis e Keycloak, em ambiente `Production`, com o banco migrado e o operador demo pronto na subida, a carteira seamless do cassino ligada, as chaves do Data Protection persistidas e todos os segredos como variáveis. É a base que a SPEC-0066 roda na VPS.

## 2. Motivação & Escopo
**Motivação:** Hoje a plataforma só sobe em modo de desenvolvimento: as migrações e o operador demo dependem do setup de Development, as chaves do Data Protection ficam no contêiner (um reinício torna as sementes do provably fair ilegíveis) e não existe artefato de produção (ADR-0029).

**Objetivos (dentro do escopo):**
- Ambiente Docker Compose no AppHost só no modo publish (`AddDockerComposeEnvironment`), com `aspire publish -o infra/compose/out` (saída não versionada) gerando `docker-compose.yaml` + `.env` de exemplo sem valores.
- Em publish: API e demo-casino em `Production`; front-ends (Vite) fora do compose (servidos pelo Caddy, SPEC-0066); nenhuma porta publicada no host (só o proxy da 0066 publica); volumes para Postgres e para as chaves do Data Protection; limites de memória por serviço.
- Configuração de produção: `Operators:GameBaseUrl` e CORS a partir de parâmetros do Aspire (`public-game-url`, `public-api-url`), carteira seamless do operador demo apontando para o demo-casino na rede interna.
- Subida em produção aplica as migrações de todos os módulos e garante o operador demo, catálogo e limites quando `Operators:Demo:Enabled=true` (hoje só no setup de Development).
- Data Protection com diretório de chaves configurável (`DataProtection:KeysPath`, volume) e `SetApplicationName` fixo (pendência da SPEC-0007, ADR-0014).
- `Microsoft.EntityFrameworkCore.Relational` alinhado à versão do EF Core (pendência do G4 da SPEC-0007).

**Não-objetivos (fora do escopo):**
- Caddy, HTTPS, front-ends estáticos, scripts de deploy e backup (SPEC-0066).
- Backoffice no compose de produção (não exposto, ADR-0029).
- Cofre de segredos de nuvem: os segredos ficam no `.env` da VPS (ADR-0029).

## 3. Dependências
<!-- As dependências entre specs ficam no frontmatter. Use `depends_on` só quando a IMPLEMENTAÇÃO da outra spec for necessária; se bastar o CONTRATO, use `consumes_contract` (permite rodar em paralelo). -->
- **Implementações necessárias:** SPEC-0047 — a jornada completa (carteira seamless do cassino) é o que vai para a VPS.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** pacote `Aspire.Hosting.Docker` 13.6.1 (ADR-0029, ADR-0016); Docker na máquina que publica.

## 4. Decisão Arquitetural
<!-- Projeto existente: o padrão atual é lei. Qualquer desvio (lib, framework, camada, padrão ou convenção nova) só com type: migration ou ADR aprovado. -->
**Contexto:** `src/backend/AppHost/Program.cs` (recursos e parâmetros gerados `operators-demo-key-*`, `player-session-signing-key`), `Operators.Features/Development/OperatorsDevelopmentSetup.cs` (migrações e operador demo), `Fairness.Infrastructure/FairnessInfrastructure.cs` (`AddDataProtection`); testes do AppHost em `tests/backend/AppHost.Tests`.

**Decisão:** o mesmo AppHost descreve run e publish; o que é só de desenvolvimento fica atrás de `IsRunMode`/`IsDevelopment()`, e o publish ganha o ambiente Compose. A subida de produção reaproveita as migrações e o seed existentes, ligados por configuração em vez do ambiente.

**Justificativa:** uma única descrição do sistema (ADR-0003) e hospedagem na VPS decidida no ADR-0029.

**Desvio do padrão existente:** Nenhum (o pacote novo está no ADR-0029).

**Alternativas descartadas:** docker-compose escrito à mão (duas descrições do sistema que divergem); rodar o AppHost em modo de desenvolvimento na VPS (rotas `/dev/*`, painel e Vite dev expostos).

**ADRs:** ADR-0029, ADR-0003, ADR-0014, ADR-0016.

## 5. Requisitos Não-Funcionais
<!-- Preocupações transversais de design doc. Não omita linhas: cada uma tem requisito + como será medido, ou "N/A — motivo". -->
- **Desempenho e escala:** N/A — mesmas metas do ambiente local (ADR-0019); medição real na VPS fica na SPEC-0048.
- **Segurança:** nenhum segredo literal no compose gerado; nenhuma porta publicada fora do proxy; rotas `/dev/*` inexistentes em `Production` — UT-01, IT-02.
- **Privacidade e dados pessoais:** N/A — só jogadores fictícios do cassino demo.
- **Disponibilidade e resiliência:** chaves do Data Protection sobrevivem a reinício e redeploy — IT-01; migrações idempotentes na subida — IT-02.
- **Acessibilidade (UI):** N/A — sem interface nova.
- **Custo:** zero (VPS própria).

## 6. Artefato A — Contrato
**Interface:** `aspire publish` → `infra/compose/out/docker-compose.yaml` + `.env.example`

```text
```text
Serviços: api, demo-casino, postgres, redis, keycloak (rede interna; sem ports no host)
Volumes: postgres-data, dataprotection-keys
Variáveis do .env (sem valor no repositório): OPERATORS_DEMO_KEY_ID, OPERATORS_DEMO_KEY_SECRET, PLAYER_SESSION_SIGNING_KEY, POSTGRES_PASSWORD, KEYCLOAK_ADMIN_PASSWORD, PUBLIC_API_URL (https://api.143-95-218-240.sslip.io), PUBLIC_GAME_URL (https://jogo.143-95-218-240.sslip.io), imagens/tag da versão
API em Production: migra os módulos na subida; Operators:Demo:Enabled=true garante operador demo, catálogo e limites; Wallet:Operators:demo:Mode=Seamless → http://demo-casino; DataProtection:KeysPath=/keys
Rede compartilhada com o proxy da SPEC-0066: api:8080 e demo-casino:8080 alcançáveis pelo nome
```
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches` no frontmatter. N/A

### 6.1 Mapa de Comportamentos
<!-- Uma linha por comportamento observável (sucesso e cada erro). Toda linha referencia ao menos um teste da seção 7 — o G0 verifica. -->
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Compose de produção | `aspire publish` | serviços e volumes esperados, ambiente Production, sem portas no host, sem segredo literal | UT-01 |
| Reinício | Host reinicia com o mesmo volume de chaves | sementes cifradas antes continuam legíveis | IT-01 |
| Subida em produção | banco vazio, `Production`, demo ligado | migrações aplicadas, operador demo e catálogo presentes, `/dev/launch-demo` 404 | IT-02 |
| Configuração | `public-game-url` e `public-api-url` | `Operators:GameBaseUrl` e CORS da API usam os endereços públicos; carteira seamless aponta para o demo-casino | UT-02 |
| Contrato com a SPEC-0066 | compose + `.env.example` | `docker compose config` válido com os serviços, rede, volumes e variáveis do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
<!-- Todo teste tem um ID e é escrito ANTES da implementação (Red). No código, cada teste carrega a tag `SPEC-0065:<ID>` — o G1 (`spec_graph.py verify`) verifica. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem testes: "N/A — motivo" SEM ID. Teste que protege comportamento existente e passa antes da mudança: marque `(guarda: passa antes da mudança)`. Estratégia completa: references/testing.md da skill. -->

### 7.1 Testes de Caracterização
<!-- Projeto existente sem cobertura na área alterada: CH-xx fixam o comportamento atual e PASSAM antes da mudança. -->
- N/A — o modo run do AppHost segue coberto pelos testes do `AppHost.Tests`, que continuam passando.

### 7.2 Testes Unitários
- **UT-01** — Dado o AppHost em modo publish, quando o compose é gerado, então tem api, demo-casino, postgres, redis e keycloak, os volumes de dados e de chaves, `ASPNETCORE_ENVIRONMENT=Production`, limites de memória, nenhuma porta publicada no host e nenhum valor de segredo literal (só referências a variáveis).
- **UT-02** — Dados os parâmetros `public-game-url` e `public-api-url`, quando o compose é gerado, então a API recebe `Operators__GameBaseUrl` e `Cors__GameOrigins__0` com o endereço público do jogo e a carteira seamless do operador demo aponta para `http://demo-casino`.

### 7.3 Testes de Integração
<!-- Com as dependências reais (containers/emuladores), não mocks. -->
- **IT-01** — Dado o Host com `DataProtection:KeysPath` num diretório, quando uma cadeia de sementes é cifrada, o Host para e sobe de novo com o mesmo diretório, então a semente é decifrada (Postgres real).
- **IT-02** — Dado o Host em `Production` com banco vazio e `Operators:Demo:Enabled=true`, quando sobe, então as migrações de todos os módulos foram aplicadas, o operador demo, o catálogo e os limites existem, e `/dev/launch-demo` responde 404.

### 7.4 Testes de Contrato
<!-- Obrigatório (CT-xx) se esta spec consome contrato de outra ou se outra spec consome o contrato desta. -->
- **CT-01** — Dado o compose gerado e o `.env.example`, quando validados pelo `docker compose config`, então o arquivo é válido e declara exatamente os serviços, a rede, os volumes e as variáveis do contrato que a SPEC-0066 consome (§6).

### 7.5 Testes E2E
<!-- Obrigatório (E2E-xx) quando user_facing: true: jornada nova ou atualização do E2E que já cobre a jornada. Poucos e significativos. -->
- N/A — sem jornada nova; a jornada contra o ambiente publicado é o E2E-01 da SPEC-0066.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** Postgres por Testcontainers; diretório temporário de chaves.

**Ambiente de execução:** testes do AppHost e do Host locais e na CI (job backend).

## 8. Plano de Rollout
- **Estratégia:** merge direto; o primeiro uso do compose é o deploy da SPEC-0066.
- **Dados/schema:** migrações existentes aplicadas na subida; nenhuma migração nova.
- **Compatibilidade:** modo run do AppHost inalterado (desenvolvimento e E2E).
- **Observabilidade:** logs e métricas pelo OpenTelemetry existente (exportador configurável por variável).
- **Rollback:** revert do merge.
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
| G0 Spec | PASS | validate: 0 erro(s) — af14212 (árvore suja) | 2026-10-15 |
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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0065`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
