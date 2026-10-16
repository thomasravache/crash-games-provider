---
id: SPEC-0065
title: Publicação do AppHost para Docker Compose em produção
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-15
parent: SPEC-0012
depends_on: [SPEC-0047]
consumes_contract: []
contract_version: 3
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: ["src/backend/AppHost/**", "src/backend/Host/**", "src/backend/Modules/Fairness/Fairness.Infrastructure/**", "src/backend/Modules/Operators/Operators.Features/Development/**", "src/backend/Modules/Operators/Operators.Infrastructure/**", "src/backend/Modules/Wallet/Wallet.Infrastructure/**", "tests/backend/AppHost.Tests/**", "tests/backend/Modules/Fairness.Tests/**", "tests/backend/Modules/Operators.Tests/**", "tests/backend/Modules/Wallet.Tests/**", "Directory.Packages.props", "infra/compose/**", "src/backend/Modules/Fairness/Fairness.Features/**", "src/backend/Modules/Rounds/Rounds.Features/**", "src/backend/Modules/Betting/Betting.Features/**", "src/backend/Modules/Wallet/Wallet.Features/**", "tests/ci/Ci.Tests/Unit/AspNetCoreVersionTests.cs", "tools/contracts/HubContracts/packages.lock.json"]
adrs: [ADR-0029, ADR-0003, ADR-0014, ADR-0016]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-15
---

# SPEC-0065 — Publicação do AppHost para Docker Compose em produção

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
O AppHost passa a publicar a plataforma como um Docker Compose de produção (`aspire publish`, `Aspire.Hosting.Docker`): API, cassino demo, Postgres, Redis e Keycloak, em ambiente `Production`, com o banco migrado e o operador demo pronto na subida, a carteira seamless do cassino ligada, as chaves do Data Protection persistidas e todos os segredos como variáveis. É a base que a SPEC-0066 roda na VPS.

## 2. Motivação & Escopo
**Motivação:** Hoje a plataforma só sobe em modo de desenvolvimento: as migrações e o operador demo dependem do setup de Development, as chaves do Data Protection ficam no contêiner (um reinício torna as sementes do provably fair ilegíveis) e não existe artefato de produção (ADR-0029).

**Objetivos (dentro do escopo):**
- Ambiente Docker Compose no AppHost só no modo publish (`AddDockerComposeEnvironment`), com `aspire publish -o infra/compose/out` (saída não versionada) gerando `docker-compose.yaml` + `.env` sem valores.
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

**Desvio do padrão existente:** a migração na subida (ADR-0010 prevê migração pelo pipeline) vale só no compose da demonstração, ligada por `Database:MigrateOnStartup`; registrada no ADR-0029 como regra do ambiente demo (Emenda v3). O pacote novo está no ADR-0029.

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
**Interface:** `aspire publish` → `infra/compose/out/docker-compose.yaml` + `.env` (sem valores)

```text
```text
Serviços: api, demo-casino, postgres, redis (rede interna; sem ports no host) e `keys-init` (busybox:1.37.0, vida curta: ajusta o dono do volume `dataprotection-keys` para o usuário da API, UID 1654; a api depende dele com `service_completed_successfully` — Emenda v3). Keycloak fica fora da produção até o backoffice ser publicado (SPEC-0036); a API recebe Security__Backoffice__Authority do parâmetro backoffice-authority (Emenda v1)
Volumes: postgres-data, dataprotection-keys
Variáveis do `.env` gerado pelo Aspire, sem valores (Emenda v1): OPERATORS_DEMO_KEY_ID, OPERATORS_DEMO_KEY_SECRET, PLAYER_SESSION_SIGNING_KEY, POSTGRES_PASSWORD, REDIS_PASSWORD, BACKOFFICE_AUTHORITY, PUBLIC_API_URL (https://api.143-95-218-240.sslip.io), PUBLIC_GAME_URL (https://jogo.143-95-218-240.sslip.io), API_IMAGE e DEMO_CASINO_IMAGE (imagens da versão)
API em Production: com `Database__MigrateOnStartup=true` vindo só do compose (não do `appsettings.Production.json`, Emenda v3) migra os módulos na subida (inclusive o armazenamento do Wolverine); com `DataProtection:KeysPath` sem permissão de escrita a API falha na subida com mensagem clara (Emenda v3); Operators:Demo:Enabled=true garante operador demo, catálogo e limites; Wallet:Operators:demo:Mode=Seamless → http://demo-casino, liberado por `Wallet:Operators:demo:AllowHttpHosts=[demo-casino]` (http só para hosts listados; o padrão continua: http só no loopback, https no resto — Emenda v1); DataProtection:KeysPath=/keys
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
| Volume de chaves | volume novo criado pelo Docker (root) | `keys-init` entrega o volume ao usuário da API antes de ela subir; sem escrita, a API falha na subida | UT-03, IT-03 |
| Subida em produção | banco vazio, `Production`, demo ligado | migrações aplicadas, operador demo e catálogo presentes, `/dev/launch-demo` 404 | IT-02 |
| Configuração | `public-game-url` e `public-api-url` | `Operators:GameBaseUrl` e CORS da API usam os endereços públicos; carteira seamless aponta para o demo-casino | UT-02 |
| Contrato com a SPEC-0066 | compose + `.env` sem valores | `docker compose config` válido com os serviços, rede, volumes e variáveis do §6 | CT-01 |

## 7. Artefato B — Plano de Testes (TDD)
<!-- Todo teste tem um ID e é escrito ANTES da implementação (Red). No código, cada teste carrega a tag `SPEC-0065:<ID>` — o G1 (`spec_graph.py verify`) verifica. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem testes: "N/A — motivo" SEM ID. Teste que protege comportamento existente e passa antes da mudança: marque `(guarda: passa antes da mudança)`. Estratégia completa: references/testing.md da skill. -->

### 7.1 Testes de Caracterização
<!-- Projeto existente sem cobertura na área alterada: CH-xx fixam o comportamento atual e PASSAM antes da mudança. -->
- N/A — o modo run do AppHost segue coberto pelos testes do `AppHost.Tests`, que continuam passando.

### 7.2 Testes Unitários
- **UT-01** — Dado o AppHost em modo publish, quando o compose é gerado, então tem api, demo-casino, postgres e redis (sem keycloak), os volumes de dados e de chaves, `ASPNETCORE_ENVIRONMENT=Production`, limites de memória, nenhuma porta publicada no host e nenhum valor de segredo literal (só referências a variáveis).
- **UT-02** — Dados os parâmetros `public-game-url` e `public-api-url`, quando o compose é gerado, então a API recebe `Operators__GameBaseUrl` e `Cors__GameOrigins__0` com o endereço público do jogo, a carteira seamless do operador demo aponta para `http://demo-casino` com `Wallet__Operators__demo__AllowHttpHosts__0=demo-casino`, e a carteira aceita http para um host listado e recusa http para um host não listado (Emenda v1).

### 7.3 Testes de Integração
<!-- Com as dependências reais (containers/emuladores), não mocks. -->
- **IT-01** — Dado o Host com `DataProtection:KeysPath` num diretório, quando uma cadeia de sementes é cifrada, o Host para e sobe de novo com o mesmo diretório, então a semente é decifrada (Postgres real).
- **UT-03** — Dado o compose publicado, quando lido, então o serviço `keys-init` monta `dataprotection-keys`, troca o dono para 1654:1654, não publica portas, e a api depende dele com `service_completed_successfully` (Emenda v3).
- **IT-03** — Dado um volume Docker novo montado num contêiner que roda como o usuário 1654, quando o `keys-init` roda antes, então o usuário grava em `/keys`; e dado o Host com `DataProtection:KeysPath` num diretório sem escrita, quando sobe, então falha na subida citando o caminho (Emenda v3).
- **IT-02** — Dado o Host em `Production` com banco vazio, `Database:MigrateOnStartup=true` e `Operators:Demo:Enabled=true`, quando sobe, então as migrações de todos os módulos foram aplicadas, o operador demo, o catálogo e os limites existem, e `/dev/launch-demo` responde 404.

### 7.4 Testes de Contrato
<!-- Obrigatório (CT-xx) se esta spec consome contrato de outra ou se outra spec consome o contrato desta. -->
- **CT-01** — Dado o compose gerado e o `.env` sem valores, quando validados pelo `docker compose config`, então o arquivo é válido e declara exatamente os serviços, a rede, os volumes e as variáveis do contrato que a SPEC-0066 consome (§6).

### 7.5 Testes E2E
<!-- Obrigatório (E2E-xx) quando user_facing: true: jornada nova ou atualização do E2E que já cobre a jornada. Poucos e significativos. -->
- N/A — sem jornada nova; a jornada contra o ambiente publicado é o E2E da SPEC-0066.

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

<!-- Preenchido na fase PLAN (2026-10-15). Cada fase começa pelos testes. -->

**Fase 1: Publicação e subida em produção**
- [ ] Red: escrever UT-01, UT-02, IT-01, IT-02 e CT-01 com a tag `SPEC-0065:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: ambiente Compose no publish, configuração de produção, migrações e operador demo na subida, Data Protection persistido
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): primeiro uso no deploy da SPEC-0066
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — af14212 (árvore suja) | 2026-10-15 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:05.73]     Dado o Host com DataProtection:KeysPath num diretório, quando ) — f8683df | 2026-10-15 |
| G2 Green | PASS | build exit 0 (✓ built in 332ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 7933873 | 2026-10-15 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  977ms (tests 96%, transform 3%, import 1%)) — 7933873 | 2026-10-15 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente (2ª rodada): APPROVED. 1ª rodada CHANGES_REQUESTED (B1: volume de chaves sem escrita para a API, provado com Docker) resolvida pela Emenda v3: keys-init com a mesma montagem e ordem garantida (service_completed_successfully), falha cedo na subida sem escrita no KeysPath, migração só pelo compose (sem appsettings.Production.json, IT-02 prova que Production sem a chave não migra). Minors: N1 chaves sem cifra em repouso (antes do lançamento); N8 cookie do DemoCasino sem chaves persistidas; N9 UID 1654 fixo depende do usuário app das imagens .NET (a API falha cedo se mudar); N11 busybox por tag; lock files do f095e1e justificados — 7933873 | 2026-10-15 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #40 (run 38828060281, commit 51d172e): sdd, security (gitleaks na árvore e no histórico, semgrep, vulnerabilidades), contracts e e2e (Chromium, WebKit e Firefox) verdes; backend com Deploy.Tests 22/22 e o resto da suíte verde, exceto testes de tempo conhecidos que alternaram em 4 tentativas (LateDebitReusedClientBetIdTests, SeamlessCreditRetryTests, MultiInstanceCashOutTests). SeamlessCreditRetryTests já falhava na CI da main antes desta onda (run 38765199325, a44a7f7) e passa 2/2 isolado no código integrado; merge com essa evidência por decisão de thomas (2026-10-15), correção na SPEC-0064; merge f48b104 | 2026-10-15 |
| H2 Integração aprovada | PASS | aprovado por thomas — "Mergear com a evidência" (2026-10-15), PR #40 mergeado em f48b104 | 2026-10-15 |
| G6 Deploy | PASS | MANUAL: Primeiro deploy na VPS em 2026-10-16 01:25 UTC (22:25 BRT), confirmado por thomas: versão f48b10495817 (main, merge do PR #40) com tools/deploy/vps-deploy main (EXIT=0); smoke verde (api /health, cassino e jogo por HTTPS, redirecionamentos http→https, lançamento da Pipa pelo cassino); certificado Let's Encrypt (YE1) para cassino.143-95-218-240.sslip.io válido até 2027-01-14; 5 containers no ar sem reinício (≈330 MB de RAM); UFW com 80/443 abertos; backup manual (dump + chaves, 600) e timer diário ativo; SPEC-0066:E2E-01 nos endereços públicos: 2/2 (chromium e webkit-mobile) | 2026-10-15 |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-15 | G1 | spec | §7.5 cita E2E-01 com ID (lido como teste da 0065); REDIS_PASSWORD e bind mount do realm do Keycloak fora do contrato do §6; nomes das variáveis de imagem e do arquivo .env indefinidos; carteira seamless recusa http fora do loopback (http://demo-casino); migrações de Development nos *.Features fora do touches | test-writer escreveu UT-01, UT-02, IT-01, IT-02 e CT-01 (red c89e314, 32096f8) e validou as asserções contra um esboço descartado | architect | Emenda v2 (contrato): sem Keycloak, REDIS_PASSWORD, AllowHttpHosts, nomes de imagem e .env | 2026-10-15 |
| IMP-02 | 2026-10-15 | G4 | spec | Review G4 (CHANGES_REQUESTED, B1): volume dataprotection-keys criado como root:root 755 e a API roda como app (UID 1654): sem escrita, sem chave, sem rodada em produção; MigrateOnStartup em appsettings.Production.json vale para qualquer Production (ADR-0010); G2: teste de guarda da SPEC-0058 sem EntityFrameworkCore.Relational na família e lock do HubContracts desatualizado (NU1004) | Reviewer provou com docker run -u 1654 sobre volume novo: Permission denied | architect | Emenda v3 (contrato): keys-init, falha cedo sem escrita, MigrateOnStartup só no compose, família EF no teste de guarda, lock do HubContracts | 2026-10-15 |

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
| 3 | 2026-10-15 | Serviço `keys-init` (busybox:1.37.0) entrega o volume `dataprotection-keys` ao usuário da API (UID 1654) antes da subida; API falha cedo se o diretório de chaves não aceitar escrita; `Database:MigrateOnStartup` só pelo compose (fora do `appsettings.Production.json`, em linha com o ADR-0010 para produção real); `EntityFrameworkCore.Relational` na lista da família do teste de guarda da SPEC-0058 e lock do HubContracts atualizado; UT-03 e IT-03 | Review G4 (B1: API sem escrita no volume de chaves, provado com Docker; N3) e G2 (teste de guarda da SPEC-0058; NU1004 no lock do HubContracts) | SPEC-0066 (consome @3) | decisão recomendada em piloto automático; thomas autorizou implementar (2026-10-15) |
| 2 | 2026-10-15 | Keycloak fora do compose de produção (backoffice não publicado; `BACKOFFICE_AUTHORITY` como parâmetro); `REDIS_PASSWORD` no contrato; variáveis de imagem `API_IMAGE`/`DEMO_CASINO_IMAGE` e arquivo `.env` sem valores; carteira seamless aceita http só para hosts listados em `AllowHttpHosts` (demo-casino na rede interna); armazenamento do Wolverine criado na subida de produção; `*.Features` dos módulos no `touches` (migrações); §7.5 sem ID de teste de outra spec | Test-writer (SPEC_DEFECT, IMP-01) | SPEC-0066 (contrato do compose: 4 serviços e variáveis novas) | decisão recomendada em piloto automático; thomas autorizou implementar (2026-10-15) |
