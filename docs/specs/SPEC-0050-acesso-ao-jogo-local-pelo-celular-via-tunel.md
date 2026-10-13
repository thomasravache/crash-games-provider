---
id: SPEC-0050
title: Acesso ao jogo local pelo celular via túnel
tier: lite
type: feature
user_facing: false
status: in-progress
created: 2026-10-12
parent: SPEC-0001
depends_on: []
consumes_contract: []
touches: ["src/backend/AppHost/Program.cs", "src/frontend/apps/game-pipa/vite.config.ts", "README.md", "tests/backend/AppHost.Tests/**", "tests/frontend/pipa/dev-tunnel/**"]
adrs: [ADR-0004]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-12
---

# SPEC-0050 — Acesso ao jogo local pelo celular via túnel

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Atual: com o AppHost rodando no Mac e um túnel (cloudflared) para a API, abrir `https://<túnel>/dev/launch-demo` num celular redireciona para `http://localhost:<porta-do-jogo>/?lt=…`, que no celular não existe; o jogo também chama a API em `localhost` e o CORS só libera a origem local (relato do thomas, 2026-10-12). Esperado: com dois túneis (API e jogo) e os endereços públicos informados ao AppHost, o jogo abre e funciona num celular; sem os endereços, tudo funciona como hoje.

## 2. Causa Raiz
`src/backend/AppHost/Program.cs` injeta sempre os endpoints locais: `Operators__GameBaseUrl` e `Cors__GameOrigins__0` da API recebem o endpoint http do `game-pipa`, e `VITE_APOGEU_API_URL` do jogo recebe o endpoint http da API; a porta do jogo é dinâmica e o Vite recusa hosts desconhecidos.

## 3. Mudança Proposta
Muda (só no modo de execução local): parâmetros opcionais de configuração do AppHost `AppHost:GameHttpPort` (porta fixa do jogo), `AppHost:PublicApiUrl` e `AppHost:PublicGameUrl`; quando os públicos estão definidos, `Operators__GameBaseUrl` e `Cors__GameOrigins__0` usam o endereço público do jogo (a origem local continua liberada em `Cors__GameOrigins__1`) e `VITE_APOGEU_API_URL` usa o endereço público da API; o Vite do game-pipa aceita hosts `.trycloudflare.com` só no servidor de desenvolvimento; README com o passo a passo dos dois túneis e o aviso de que o túnel expõe o jogo demo enquanto estiver aberto. Não muda: o comportamento sem os parâmetros, publish/deploy, o Host, o jogo, o backoffice e o demo-site.

**Padrão seguido:** `AppHost:ApiHttpPort` em `src/backend/AppHost/Program.cs` (parâmetro opcional lido da configuração, usado pelo `tools/ci/run-e2e.sh`).

**Rollback:** revert do commit.

**Outras ocorrências:** O backoffice e o demo-site têm a mesma limitação; ficam como estão (não pedidos).

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0050:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- **CH-01** — Dado o modelo do AppHost sem os parâmetros públicos, quando as variáveis de ambiente da API e do game-pipa são resolvidas (Aspire.Hosting.Testing, sem subir containers), então `Operators__GameBaseUrl`, `Cors__GameOrigins__0` e `VITE_APOGEU_API_URL` apontam para os endpoints locais, como hoje.
- **UT-01** — Dada a configuração do Vite do game-pipa, quando lida no modo de desenvolvimento, então `server.allowedHosts` aceita `.trycloudflare.com`.
- **IT-01** — Dado o modelo do AppHost com `AppHost:GameHttpPort`, `AppHost:PublicApiUrl=https://api.example.trycloudflare.com` e `AppHost:PublicGameUrl=https://jogo.example.trycloudflare.com`, quando as variáveis são resolvidas, então `Operators__GameBaseUrl` e `Cors__GameOrigins__0` são o endereço público do jogo, `Cors__GameOrigins__1` é o endpoint local do jogo, `VITE_APOGEU_API_URL` é o endereço público da API e o endpoint do game-pipa usa a porta fixa.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
- [x] Fazer agora o acesso pelo celular via túnel? — Sim, com dois túneis cloudflared e parâmetros opcionais do AppHost (thomas, 2026-10-12)

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
**Fase 1: Endereços públicos**
- [ ] Caracterização: CH-01 passando no código atual
- [ ] Red: UT-01 e IT-01 com a tag `SPEC-0050:<ID>`, falhando pelo motivo certo
- [ ] Green: parâmetros no AppHost, allowedHosts no Vite, README
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy via pipeline com smoke/E2E no ambiente (G6)
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 0ca62a9 | 2026-10-12 |
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive&Category!=Performance" && dotnet test Apogeu.slnx --filter Category=Performance -m:1 && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:00:03.29]     Dado o modelo do AppHost com a porta fixa do jogo e os endereç) — e0d39b1 | 2026-10-12 |
| G2 Green | PASS | build exit 0 (✓ built in 167ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 6049d8d | 2026-10-13 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  772ms (tests 97%, transform 2%, import 1%)) — 6049d8d | 2026-10-13 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (minors: m1 texto sobre exposição no README, m2 avisar quando só uma URL pública está definida, m3 passos do README) — 6049d8d | 2026-10-13 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #30 verde (run 38351277297, commit b3a04b0): backend (suíte .NET completa + Performance), frontend, contracts, e2e (Chromium, WebKit e Firefox; Firefox E2E-04 da SPEC-0026 instável no 1º attempt, verde no re-run), security e sdd; local em 85dd922: build, arquitetura e segurança PASS; testes do AppHost e E2E locais falham só por conflito de portas 5080/5173 com o AppHost do thomas aberto desde 2026-10-12 08:03 (não encerrado por ser sessão dele) | 2026-10-13 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-13 |
| G6 Deploy | PENDING | | |
| G7 Pronto & Docs | PENDING | | |

## 9. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 10. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Para status implemented o validate exige tudo preenchido, todo teste do plano com PASS + evidência e a Definição de Pronto marcada. -->

### O que foi entregue

### Como foi feito

### Prova de Correção
<!-- type fix: teste de regressão falhou antes (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->

### Verificação
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|

### Definição de Pronto
- [ ] Teste de regressão falhou antes e passa depois da correção
- [ ] Suíte completa, arquitetura e CI verdes (G2, G3, G5)
- [ ] Review independente sem achados blocker/major (G4)
- [ ] Padrão existente mantido
- [ ] Disponível no ambiente-alvo via pipeline (G6)
- [ ] Documentação/CHANGELOG atualizados quando aplicável (G7)
- [ ] Outras ocorrências registradas como novas specs (ou nenhuma)

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
