---
id: SPEC-0066
title: Deploy na VPS com HTTPS, backup e rollback
tier: full
type: foundation
user_facing: true
status: in-progress
created: 2026-10-15
parent: SPEC-0012
depends_on: [SPEC-0047]
consumes_contract: [SPEC-0065@3]
contract_version: 5
# breaking: "o que quebra para consumidores e como migrar"   # só se houver quebra; vai para o PR e pede `!` no título
touches: ["infra/caddy/**", "infra/vps/**", "tools/deploy/**", "docs/runbooks/**", "tests/deploy/**"]
adrs: [ADR-0029, ADR-0013, ADR-0014, ADR-0018]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-15
---

# SPEC-0066 — Deploy na VPS com HTTPS, backup e rollback

<!-- type: feature | fix | refactor | migration | foundation. user_facing: true quando a mudança altera uma jornada do usuário (UI ou API pública) — exige teste E2E. Substitua todos os marcadores com chaves duplas: o G0 (`spec_graph.py validate`) reprova a spec enquanto restar algum. As seções de Checklist em diante são preenchidas depois da aprovação, sem marcadores. -->

## 1. Visão Geral
Põe a plataforma no ar na VPS: o Caddy recebe `cassino.143-95-218-240.sslip.io`, `jogo.143-95-218-240.sslip.io` e `api.143-95-218-240.sslip.io` com HTTPS automático, serve os front-ends já compilados e encaminha para a API e o cassino demo do compose da SPEC-0065. Scripts fazem deploy, rollback, backup e smoke em um passo.

## 2. Motivação & Escopo
**Motivação:** thomas quer jogar a jornada de qualquer lugar, sem custo (ADR-0029); hoje só dá por túnel SSH ou túneis efêmeros com autorização a cada uso.

**Objetivos (dentro do escopo):**
- `infra/caddy/`: imagem do proxy (Caddy 2.11.7) que compila `demo-site` e `game-pipa` em modo produção (com `VITE_APOGEU_API_URL=https://api.143-95-218-240.sslip.io`) e serve os estáticos; `Caddyfile` com os três sites, `/casino/*` → `demo-casino:8080`, `api` → `api:8080` (WebSocket incluso), cabeçalhos de segurança e certificados num volume.
- `tools/deploy/vps-deploy <ref>`: busca a `main` (ou a ref) num diretório de deploy fora do repositório de trabalho, gera o compose (SPEC-0065), cria o `.env` com segredos aleatórios na primeira vez (permissão 600, nunca versionado), constrói as imagens com tag do commit, sobe, espera a saúde e roda o smoke; guarda a versão anterior.
- `tools/deploy/vps-rollback`: volta à versão anterior em um passo.
- `tools/deploy/backup`: `pg_dump` diário (timer do systemd) com retenção de 7 dias e `restore` testado.
- `tools/deploy/smoke`: HTTPS dos três endereços, `/health` da API, lançamento do cassino.
- Abertura das portas 80/443 no UFW (com a confirmação de thomas no G6) e o runbook `docs/runbooks/deploy-vps.md`.
- `deploy_production` e `smoke_test` do `sdd-config` preenchidos (G6 deixa de ser N/A).

**Não-objetivos (fora do escopo):**
- Deploy disparado pelo GitHub (SPEC-0067).
- Backup fora da VPS e staging separado.
- Expor painel do Aspire, Keycloak, backoffice, banco ou Redis.

## 3. Dependências
<!-- As dependências entre specs ficam no frontmatter. Use `depends_on` só quando a IMPLEMENTAÇÃO da outra spec for necessária; se bastar o CONTRATO, use `consumes_contract` (permite rodar em paralelo). -->
- **Implementações necessárias:** SPEC-0065 — o compose de produção que esta spec roda.
- **Contratos consumidos:** SPEC-0065@3 — serviços (inclui o `keys-init` de vida curta), rede, volumes e variáveis do compose.
- **Pré-requisitos externos:** imagem `caddy:2.11.7`; DNS do sslip.io; portas 80/443 alcançáveis da internet (UFW da VPS; firewall do provedor, se houver).

## 4. Decisão Arquitetural
<!-- Projeto existente: o padrão atual é lei. Qualquer desvio (lib, framework, camada, padrão ou convenção nova) só com type: migration ou ADR aprovado. -->
**Contexto:** scripts de `tools/ci/` (padrão de shell e Python), `tools/ci/run-e2e.sh` (subir, esperar saúde, derrubar) e os E2E de `tests/e2e/demo-site/`.

**Decisão:** Caddy como único ponto de entrada, servindo os estáticos e fazendo o proxy; scripts idempotentes na VPS; versões identificadas pelo commit.

**Justificativa:** decisões do ADR-0029; rollback em um passo e confirmação humana em produção (ADR-0013); RPO/RTO do ADR-0018 aproximados com backup diário (registrado como dívida).

**Desvio do padrão existente:** RPO diário em vez de ≤ 5 min do ADR-0018: aceito para o ambiente de demonstração no ADR-0029.

**Alternativas descartadas:** Nginx + certbot (mais peças para o mesmo resultado); servir os front-ends pelo Vite (modo de desenvolvimento).

**ADRs:** ADR-0029, ADR-0013, ADR-0014, ADR-0018.

## 5. Requisitos Não-Funcionais
<!-- Preocupações transversais de design doc. Não omita linhas: cada uma tem requisito + como será medido, ou "N/A — motivo". -->
- **Desempenho e escala:** cassino interativo no celular pela internet — medido pela SPEC-0030 contra os endereços públicos; aqui o smoke.
- **Segurança:** HTTPS em tudo (HTTP redireciona), HSTS e cabeçalhos de segurança; só 80/443 e SSH abertos; `.env` com 600 e fora do repositório; nenhum segredo em log do deploy — UT-01, IT-01.
- **Privacidade e dados pessoais:** N/A — jogadores fictícios; logs do Caddy sem cookies.
- **Disponibilidade e resiliência:** deploy idempotente; falha de saúde ou de smoke volta sozinho à versão anterior; backup restaurável — IT-01, IT-02.
- **Acessibilidade (UI):** N/A — mesmas telas já verificadas; o E2E-01 roda o axe contra o ambiente publicado.
- **Custo:** zero.

## 6. Artefato A — Contrato
**Interface:** `https://cassino.143-95-218-240.sslip.io` · `https://jogo.143-95-218-240.sslip.io` · `https://api.143-95-218-240.sslip.io` · `tools/deploy/*`

```text
```text
cassino.143-95-218-240.sslip.io: /casino/* → demo-casino:8080; resto → estáticos do demo-site (fallback SPA para index.html)
jogo.143-95-218-240.sslip.io:    estáticos do game-pipa (fallback SPA)
api.143-95-218-240.sslip.io:     → api:8080 (REST e /hubs com WebSocket)
http://* → https://*; HSTS; X-Content-Type-Options, Referrer-Policy; certificados no volume caddy-data
tools/deploy/vps-deploy [ref=main] → exit 0 com a versão no ar e smoke verde | exit ≠ 0 e versão anterior restaurada
tools/deploy/vps-rollback → versão anterior no ar
tools/deploy/backup | restore <arquivo>
```

**Interface dos scripts (Emenda v1; Emenda v4: `APOGEU_BIND_ADDRESS` para o endereço em que o proxy publica, padrão `0.0.0.0` na VPS e `127.0.0.1` nos testes; logs do Docker com rotação, `max-size`/`max-file`, em todos os serviços do proxy; o runbook recomenda rodar o deploy dentro de `tmux` (Emenda v5: sem `systemd-run --scope`, que não sobrevive à queda do SSH) e cita o clone por https enquanto o repositório for público):** a referência é `tests/deploy/README.md`, fixada pelos testes:
- `vps-deploy --prepare` cria só o diretório e o `.env` (600; mantém um `.env` existente; segredos aleatórios e distintos, sem caracteres especiais de shell; `PUBLIC_API_URL`/`PUBLIC_GAME_URL` a partir de `APOGEU_DOMAIN`; `BACKOFFICE_AUTHORITY` presente).
- Configuração por variáveis de ambiente com os padrões da VPS: `APOGEU_DEPLOY_DIR`, `APOGEU_DEPLOY_REPO`, `APOGEU_COMPOSE_PROJECT` (`apogeu`), `APOGEU_IMAGE_PREFIX`, `APOGEU_DOMAIN` (`143-95-218-240.sslip.io`), `APOGEU_HTTP_PORT`/`APOGEU_HTTPS_PORT`, ganchos `APOGEU_DEPLOY_BUILD`/`APOGEU_DEPLOY_SMOKE`, `APOGEU_DEPLOY_HEALTH_TIMEOUT`, `APOGEU_POSTGRES_CONTAINER`, `APOGEU_BACKUP_DIR`.
- O proxy é o serviço `proxy`, o único com portas publicadas, na rede do compose; imagens com tag do commit (≥ 7 caracteres); o compose sem algum serviço do contrato é recusado antes de subir; as variáveis de imagem do `--env-file` ganham das do shell (a volta à versão anterior não pode reusar as imagens novas).
- `backup` grava `apogeu-*` em `APOGEU_BACKUP_DIR`, imprime o caminho como última linha e apaga só os `apogeu-*` com mais de 7 dias; `restore` cria o banco se não existir.
```

**Design:** N/A — telas já aprovadas (SPEC-0044/0045/0047).

**Arquivos/módulos afetados:** ver `touches` no frontmatter. N/A

### 6.1 Mapa de Comportamentos
<!-- Uma linha por comportamento observável (sucesso e cada erro). Toda linha referencia ao menos um teste da seção 7 — o G0 verifica. -->
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Rotas do proxy | Caddyfile | três sites, rotas e cabeçalhos corretos; `caddy validate` ok | UT-01 |
| Deploy e rollback | versão A, depois B; B com saúde ruim | B no ar; B ruim volta para A sozinho; rollback manual volta para A | IT-01 |
| Backup | dump diário | restauração num Postgres limpo reproduz os dados | IT-02 |
| Jornada pública | endereços com HTTPS | entrar, abrir a Pipa, apostar, retirar e ver o saldo do cassino; axe sem violações | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
<!-- Todo teste tem um ID e é escrito ANTES da implementação (Red). No código, cada teste carrega a tag `SPEC-0066:<ID>` — o G1 (`spec_graph.py verify`) verifica. Modo privado: sem tag no código; aponte o teste no fim da linha com `→ caminho::nome` (ex.: `→ tests/test_soma.py::test_soma`). Categoria sem testes: "N/A — motivo" SEM ID. Teste que protege comportamento existente e passa antes da mudança: marque `(guarda: passa antes da mudança)`. Estratégia completa: references/testing.md da skill. -->

### 7.1 Testes de Caracterização
<!-- Projeto existente sem cobertura na área alterada: CH-xx fixam o comportamento atual e PASSAM antes da mudança. -->
- N/A — infraestrutura nova.

### 7.2 Testes Unitários
- **UT-01** — Dado o `Caddyfile` com o host de teste, quando validado pelo `caddy validate` (imagem 2.11.7) e inspecionado, então só existem os três sites, `/casino/*` vai para o demo-casino, a API para `api:8080`, HTTP redireciona para HTTPS, os cabeçalhos de segurança estão presentes e os `X-Forwarded-*` que chegam da internet são descartados e reescritos pelo proxy (sem `trusted_proxies`; a API confia em `X-Forwarded-*` — Emenda v3); o log de acesso não grava tokens: `lt` e `access_token` da query são apagados (filtro no formato do log), além de Cookie e Authorization (Emenda v4); nenhuma saída do proxy (`docker logs`) contém tokens — nem o log de erros (502 com o backend fora do ar), nem o `Referer`, nem o `Location` do redirecionamento http→https (308) —, provado também com o backend fora do ar e com uma requisição `http://` com `?lt=` (Emenda v5).
- **UT-02** — Dado um diretório de deploy sem `.env`, quando o `vps-deploy` prepara a versão, então cria o `.env` com todos os segredos aleatórios e permissão 600, não os imprime, e numa segunda execução mantém os mesmos valores.

### 7.3 Testes de Integração
<!-- Com as dependências reais (containers/emuladores), não mocks. -->
- **IT-01** — Dado um projeto compose de teste na máquina (portas e certificados de teste), quando a versão A e depois a B são publicadas, então B fica no ar; com B sem saúde, o script volta sozinho para A; e o `vps-rollback` volta de B para A.
- **IT-02** — Dado um Postgres com dados e o volume `dataprotection-keys` com chaves, quando o `backup` roda e o `restore` carrega o arquivo num Postgres limpo e num volume limpo, então as contagens das tabelas de cada módulo coincidem e as chaves restauradas são idênticas (sem elas as sementes não reveladas ficam ilegíveis — Emenda v3); com um arquivo de chaves corrompido ou sem nenhuma chave, o `restore` recusa antes de tocar no banco e no volume, e as chaves atuais continuam intactas (Emenda v4/v5); as chaves são importadas antes do banco (Emenda v5).

### 7.4 Testes de Contrato
<!-- Obrigatório (CT-xx) se esta spec consome contrato de outra ou se outra spec consome o contrato desta. -->
- **CT-01** — Dado o compose gerado pela SPEC-0065, quando o `vps-deploy` o lê, então todos os serviços (inclusive o `keys-init` do @3), a rede e as variáveis esperadas existem e o proxy alcança `api` e `demo-casino` pelo nome; e um compose em que qualquer serviço além do `proxy` publique portas é recusado antes de subir (porta publicada pelo Docker passa por cima do UFW — Emenda v4).

### 7.5 Testes E2E
<!-- Obrigatório (E2E-xx) quando user_facing: true: jornada nova ou atualização do E2E que já cobre a jornada. Poucos e significativos. -->
- **E2E-01** — Dados os endereços públicos depois do deploy na VPS, quando o jogador entra em `https://cassino.143-95-218-240.sslip.io`, abre a Pipa, aposta R$ 10,00, retira no voo e volta ao lobby, então o saldo do topo é 3.000 − 10 + pagamento; axe sem violações. Acompanha a rodada só pelo que aparece na tela: o gancho de teste `window.__APOGEU_TEST__` não existe no build de produção e não deve ser ligado lá (Emenda v1). Só roda com `APOGEU_E2E_PUBLISHED=1`.

### 7.6 Outros
- Verificação dos cabeçalhos e do TLS dos três endereços com `curl` no smoke.

**Dublês e dados de teste:** projeto compose de teste com nome próprio; Postgres por Testcontainers no IT-02.

**Ambiente de execução:** UT/IT/CT locais e na CI; E2E-01 contra a VPS no G6.

## 8. Plano de Rollout
- **Estratégia:** deploy direto na VPS com a confirmação de thomas a cada versão.
- **Dados/schema:** primeira subida com banco vazio (SPEC-0065 migra); depois, migrações compatíveis com a versão anterior (ADR-0013).
- **Compatibilidade:** nenhuma: primeiro ambiente publicado.
- **Observabilidade:** logs do Caddy e do compose; saúde da API no smoke; uso de memória da VPS conferido no deploy.
- **Rollback:** `tools/deploy/vps-rollback` em menos de 2 minutos; thomas decide (ou automático quando a saúde falha no deploy).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-15). Cada fase começa pelos testes. -->

**Fase 1: Proxy, scripts e backup**
- [ ] Red: escrever UT-01, UT-02, IT-01, IT-02 e CT-01 com a tag `SPEC-0066:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: imagem do Caddy com os estáticos, `vps-deploy`, `vps-rollback`, `backup`/`restore`, `smoke`, runbook
- [ ] Validar: build + testes afetados + arquitetura (G2/G3)

**Fase 2: Primeiro deploy (G6, com a confirmação de thomas)**
- [ ] Portas 80/443 no UFW, AppHost de desenvolvimento desligado, `vps-deploy main`
- [ ] E2E-01 contra os endereços públicos

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy na VPS com smoke e E2E (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — af14212 (árvore suja) | 2026-10-15 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:02:19.42]     Dada uma aposta X cujo débito fica retido até depois da varred) — 861597e | 2026-10-15 |
| G2 Green | FAIL | build exit 0 (✓ built in 397ms); test exit 1 ([xUnit.net 00:04:23.58]     Dado o operador respondendo 500 por 1 min, quando um crédito é); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — b00973d | 2026-10-15 |
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
| IMP-01 | 2026-10-15 | G1 | spec | §6 não fixava a interface dos scripts (modo --prepare, variáveis de ambiente, ganchos de build/smoke, serviço proxy, nomes dos backups); E2E-01 citava o E2E da SPEC-0047, que depende do gancho de teste ausente no build de produção; §3 citava SPEC-0065@1 | test-writer fixou a interface em tests/deploy/README.md e escreveu os 6 testes (red 3d7ebf0, 1bc902b) | architect | Emenda v2: interface dos scripts (tests/deploy/README.md), E2E-01 sem gancho de teste, SPEC-0065@2 | 2026-10-15 |
| IMP-02 | 2026-10-15 | G4 | spec | Review G4 (CHANGES_REQUESTED, B1): o log de acesso do Caddy grava o launch token (?lt= em jogo.) e o token de sessão do WebSocket (access_token= em api./hubs), violando o ADR-0014; também: serviços fora do contrato poderiam publicar portas (Docker passa por cima do UFW), restore apaga as chaves antes de validar o arquivo, proxy de teste publicado em 0.0.0.0 na VPS, logs sem rotação | Reviewer leu o Caddyfile, o LaunchIssuer e o accessTokenFactory do game-sdk | architect | Emenda v4: log sem tokens, recusa de portas fora do proxy, restore seguro, bind address, rotação de logs | 2026-10-15 |
| IMP-03 | 2026-10-15 | G4 | spec | Review G4 2ª rodada (CHANGES_REQUESTED, B1 ainda aberto): o log de erros do Caddy (http.log.error, ex.: 502 com a api fora durante o deploy) grava a URI com access_token e o Referer com lt; o resp_headers>Location do 308 http→https grava ?lt=; o UT-01 só prova o caminho feliz | Reviewer reproduziu com o Caddyfile real (502 sem backend e 308 por http) | architect | Emenda v5: tokens fora de toda saída do proxy (log de erros, Referer, Location), testes 502/308, restore mais seguro, runbook com tmux | 2026-10-15 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0066`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 5 | 2026-10-15 | Nenhuma saída do proxy com tokens: filtro também no log padrão/de erros (URI e `Referer`) e no `Location` do 308; UT-01 com backend fora do ar (502) e com `http://` com `?lt=` (308); restore recusa arquivo de chaves sem chaves e importa as chaves antes do banco; runbook só com `tmux`; fixture do `keys-init` igual ao real (`network_mode: none`, `chown` sem `-R`) | Review G4 2ª rodada da SPEC-0066 (B1 ainda aberto; N1, N3, N4, N5) | Nenhuma | decisão recomendada em piloto automático; thomas autorizou implementar (2026-10-15) |
| 4 | 2026-10-15 | Log de acesso do Caddy sem tokens (`lt`, `access_token`); deploy recusa serviço fora do `proxy` com portas publicadas; `restore` valida o arquivo de chaves antes de apagar as atuais; `APOGEU_BIND_ADDRESS` (testes em `127.0.0.1`); rotação dos logs do Docker; runbook com `tmux`/`systemd-run` e clone por https | Review G4 da SPEC-0066 (B1 e não-bloqueantes 1, 2, 3, 4, 6, 7) | Nenhuma | decisão recomendada em piloto automático; thomas autorizou implementar (2026-10-15) |
| 3 | 2026-10-15 | Backup e restauração incluem o volume `dataprotection-keys` junto com o Postgres; o proxy descarta e reescreve `X-Forwarded-*`; consome SPEC-0065@3 (serviço `keys-init`) | Review G4 da SPEC-0065 (N2, N6) | Nenhuma | decisão recomendada em piloto automático; thomas autorizou implementar (2026-10-15) |
| 2 | 2026-10-15 | Interface dos scripts fixada (`--prepare`, variáveis de ambiente e ganchos, serviço `proxy`, nomes dos backups; referência `tests/deploy/README.md`); E2E-01 acompanha a rodada pela tela, sem o gancho de teste; consome SPEC-0065@2 | Test-writer (SPEC_DEFECT, IMP-01) | SPEC-0067 (chama `vps-deploy`/`vps-rollback`) | decisão recomendada em piloto automático; thomas autorizou implementar (2026-10-15) |
