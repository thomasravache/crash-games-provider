---
id: SPEC-0008
title: "Motor de rodada e salas em tempo real"
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0003, SPEC-0005]
consumes_contract: [SPEC-0007@2]
contract_version: 3
touches: ["src/backend/Modules/Rounds/**", "tests/backend/Modules/Rounds.Tests/**", "tests/load/rounds/**", "artifacts/openapi/**", "src/frontend/packages/contracts/src/**", "Directory.Packages.props", "tools/contracts/frontend-modules.mjs", "tests/backend/TestKit/Containers/PostgreSqlContainerFixture.cs"]
adrs: [ADR-0001, ADR-0005, ADR-0010, ADR-0017, ADR-0018, ADR-0019]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0008 — Motor de rodada e salas em tempo real

## 1. Visão Geral
Implementa o módulo Rounds: salas configuráveis, máquina de estados da rodada (apostas abertas → voo → crash), um líder por sala com trava no Redis, transmissão por SignalR com backplane Redis, persistência das rodadas, retomada após queda do líder e histórico com dados de verificação.

## 2. Motivação & Escopo
**Motivação:** é o motor compartilhado por todos os jogos; precisa ser justo (horário do servidor), escalável e resistente a falhas (ADR-0005, ADR-0018, ADR-0019).

**Objetivos (dentro do escopo):**
- Agregado `Sala` (RTP, limite, constante de crescimento, tempos das fases) com uma sala `demo` semeada por configuração.
- Agregado `Round` com estados `Betting → Flying → Crashed` e terminal alternativo `Annulled`.
- Condutor por sala com trava de liderança no Redis; retomada determinística de rodada interrompida.
- Hub SignalR `/hubs/rounds` (contrato v1 abaixo), com interpolação no cliente e sincronização a cada 200 ms.
- REST: histórico da sala e detalhes de verificação da rodada.
- Teste de carga k6 local (1.000 conexões por sala).

**Não-objetivos (fora do escopo):**
- Apostas e cash-out (épico seguinte).
- Autorização de entrada na sala (SPEC-0009 aplica a política ao hub; aqui o hub já exige usuário autenticado via esquema registrado pelo Host).

## 3. Dependências
- **Implementações necessárias:** SPEC-0003 (harness e fixtures) e SPEC-0005 (Redis/Postgres no Aspire, ServiceDefaults).
- **Contratos consumidos:** SPEC-0007@2 — `NextRoundAsync`, `GetCrashPointAsync` (início do voo), `RevealAsync` (só depois do crash), `ComputeCrashPoint`.
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0005 (salas, líder, horário do servidor), ADR-0018 (retomada), ADR-0019 (interpolação, metas).

**Decisão:**
- Multiplicador no instante `t` (ms desde o início do voo): `m(t) = e^(k·t)`, com `k` por sala (padrão 0,00006/ms ≈ 2x em 11,6 s), exibido como `floor(m·100)/100`; para `t ≥ t_c`, `m = crash` exatamente. Instante do crash: `t_c = ln(crash)/k` (sem arredondar). O servidor encerra o voo em `t_c` medido pelo `TimeProvider`.
- **Ponto de crash e semente (emenda v2):** ao iniciar o voo o condutor obtém o ponto com `GetCrashPointAsync` da SPEC-0007@2 e o guarda **só em memória**; nunca persiste nem transmite o ponto antes do crash. A retomada chama `GetCrashPointAsync` de novo (determinístico). `RevealAsync` é chamado só depois do crash, e só então ponto e semente são persistidos e publicados (ADR-0006, ADR-0014).
- **Salas (emenda v2):** id em texto `^[a-z0-9-]{1,32}$` (ex.: `demo`), configurado em `Rounds:Salas:{id}` (RTP, limite, `growthK`, tempos, tamanho da cadeia — padrão 1.000.000; testes usam cadeia curta); o `SalaId` da Fairness é o UUID v5 do id com namespace fixo do módulo.
- Tempos padrão: apostas 6 s, pausa pós-crash 3 s.
- Liderança: chave `rounds:leader:{sala}` com TTL 5 s, renovada a cada 1 s; ao assumir, o líder lê a última rodada no Postgres e retoma (voo em andamento → continua com o mesmo `startedAt`; prazo de retomada excedido (30 s) → `Annulled`).
- O hub só traduz mensagens; regras ficam nos slices e no domínio.

**Justificativa:** interpolação no cliente mantém a banda constante; persistir `startedAt` e o crash pré-definido torna a retomada determinística.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** enviar cada tique do multiplicador (banda proporcional à taxa de quadros, ADR-0019); eleição de líder com Postgres advisory lock (Redis já é dependência e tem TTL nativo).

**ADRs:** ADR-0001, ADR-0005, ADR-0010, ADR-0017, ADR-0018, ADR-0019.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** 1.000 conexões por sala com crash entregue a todos em p99 ≤ 200 ms (local) — teste de carga em 7.6.
- **Segurança:** hub exige usuário autenticado; nenhuma semente não revelada trafega — verificado em IT-02.
- **Privacidade e dados pessoais:** N/A — nenhuma informação de jogador nas mensagens desta spec.
- **Disponibilidade e resiliência:** queda do líder → outro assume em ≤ 6 s sem duplicar rodada; rodada sempre termina — IT-01, IT-04, IT-05.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** hub SignalR `/hubs/rounds` + REST v1 (contrato v1 consumido pela SPEC-0010).

```text
Cliente → servidor
  JoinSala(salaId)            → RoundSnapshot | HubException(SALA_NOT_FOUND)
  GetServerTime()             → { serverTime }   (cálculo do desvio de relógio)
Servidor → cliente (grupo da sala)
  RoundBetting  { salaId, roundId, roundNumber, commitment, bettingEndsAt }
  RoundFlying   { roundId, startedAt, growthK }            // cliente interpola m(t)=e^(k·t)
  RoundSync     { roundId, serverTime, multiplier }        // a cada 200 ms durante o voo
  RoundCrashed  { roundId, crashPoint, crashedAt, revealedSeed }
  RoundAnnulled { roundId, reason }
RoundSnapshot { sala:{id,rtp,cap,growthK},
                current:{roundId, roundNumber, state: "betting"|"flying"|"crashed"|"annulled", commitment, bettingEndsAt,
                         startedAt?, crashPoint?, crashedAt?, revealedSeed?},   // campos ausentes = null; crashPoint e
                                                                                // revealedSeed só depois do crash
                history:[{roundId,roundNumber,crashPoint}] (últimas 20) }
REST (v3: exige a sessão do jogador — Authorization: Bearer sessionToken da SPEC-0009; sem sessão → 401)
  GET /api/v1/salas/{salaId}/rounds?limit=1..100   → 200 [{roundId, roundNumber, crashPoint, crashedAt}] | 404 SALA_NOT_FOUND | 400 INVALID_LIMIT
  GET /api/v1/rounds/{roundId}/verification         → 200 {seed, previousSeed, salt, rtp, cap, crashPoint, terminalHash}
                                                      | 404 ROUND_NOT_FOUND | 409 ROUND_NOT_FINISHED
Tipos (SPEC-0006): multiplicadores e limite como `Multiplier`; `rtp` como string decimal de 2 casas ("0.97"); `growthK` como
  string decimal ("0.00006"); instantes ISO-8601 UTC; `commitment` e `revealedSeed` em hex minúsculo.
Erros: REST → ProblemDetails (RFC 9457) com extensão `code` (ex.: "SALA_NOT_FOUND"); hub → HubException cuja mensagem é o código.
Antes do crash nenhuma mensagem, snapshot ou resposta REST contém `crashPoint` nem semente da rodada corrente.
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ciclo da rodada | líder ativo | Betting → Flying → Crashed → nova Betting | UT-01, IT-03 |
| Transição inválida | Crashed → Flying | erro de domínio | UT-01 |
| Função de crescimento | k e crash dados | `t_c = ln(crash)/k`; m(t) correto | UT-02 |
| Crash instantâneo | crash 1.00 | voo termina no início, `RoundCrashed` imediato | UT-03 |
| Sincronização | voo em andamento (FakeTimeProvider) | `RoundSync.multiplier` = m(t) no horário do servidor | UT-04 |
| Um líder por sala | duas instâncias | só uma conduz; números de rodada sem duplicata | IT-01 |
| Dados de verificação | rodada encerrada | endpoint devolve dados que o `Verify` da SPEC-0007 aceita | IT-02, CT-02 |
| Rodada não encerrada | rodada em voo | verificação → 409 ROUND_NOT_FINISHED | IT-02 |
| Sequência no hub | cliente conectado | recebe Betting, Flying, Sync, Crashed na ordem | IT-03, CT-01 |
| Retomada | líder cai no voo | novo líder continua; crash no ponto e no instante previstos | IT-04 |
| Retomada impossível | sem líder por > 30 s | rodada `Annulled` + métrica de alerta | IT-05 |
| Sala inexistente | JoinSala / histórico com id desconhecido | SALA_NOT_FOUND / 404 | IT-06 |
| Limite inválido | `limit=0` ou `101` | 400 INVALID_LIMIT | IT-06 |
| Rajada de entradas | 300 JoinSala simultâneos com Postgres limitado | todos recebem snapshot; pool respeitado | IT-07 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dada uma rodada em Betting, quando as transições válidas ocorrem, então chega a Crashed; dada uma rodada Crashed, quando se tenta iniciar voo, então lança erro de domínio.
- **UT-02** — Dados k = 0,00006 e crash 2,00, quando calculado `t_c`, então ≈ 11.552,45 ms (sem arredondar); m(t_c) = 2.00 pela regra `t ≥ t_c ⇒ m = crash`; e m(11.552 ms) exibido = 1.99.
- **UT-03** — Dado crash 1.00, quando o voo inicia, então o condutor emite `RoundCrashed` sem `RoundSync`.
- **UT-04** — Dado FakeTimeProvider em t = 5.000 ms de voo, quando o sync é emitido, então o multiplicador é floor(e^(0,3)·100)/100 = 1.34.

### 7.3 Testes de Integração
- **IT-01** — Dadas duas instâncias do condutor com Redis real, quando rodam 5 rodadas, então só uma conduz e os números de rodada são 1..5 sem repetição; ao matar a líder, a outra assume em ≤ 6 s.
- **IT-02** — Dada uma rodada encerrada com Postgres real, quando `GET /verification`, então os dados passam no `Verify` da SPEC-0007; para rodada em voo, 409 com ProblemDetails `code` = ROUND_NOT_FINISHED, e nem o snapshot, nem o hub, nem o REST contêm `crashPoint` ou semente da rodada em voo.
- **IT-03** — Dado um cliente SignalR real (`Microsoft.AspNetCore.SignalR.Client`, WebApplicationFactory com esquema de autenticação de teste + backplane Redis), quando acompanha uma rodada, então recebe Betting, Flying, Sync (≥ 1) e Crashed nessa ordem, com o `crashPoint` igual ao persistido.
- **IT-04** — Dado o líder derrubado no meio do voo, quando outra instância assume, então a rodada termina no mesmo `crashPoint` e em `startedAt + t_c` (± 250 ms).
- **IT-05** — Dado nenhum líder disponível por 31 s com rodada em voo, quando um líder assume, então a rodada fica `Annulled` com motivo e a métrica `rounds.annulled` incrementa.
- **IT-06** — Dados id de sala desconhecido e `limit` 0/101, quando chamados hub e REST, então SALA_NOT_FOUND/404 e 400 INVALID_LIMIT.

- **IT-07** — Dado o Host real com PostgreSQL configurado com `max_connections` baixo (ex.: 30), quando 300 clientes chamam `JoinSala` ao mesmo tempo, então todos recebem o snapshot sem erro e nenhuma conexão além do pool configurado é aberta (`Category=Performance`; regressão do teste de carga k6).

### 7.4 Testes de Contrato
- **CT-01** — Dado o contrato do hub v1 gerado (SPEC-0006), quando as mensagens reais de IT-03 são validadas contra o esquema gerado, então todas conferem (contrato consumido pela SPEC-0010).
- **CT-02** — Dado o contrato SPEC-0007@1, quando o motor usa `NextRoundAsync`/`GetCrashPointAsync`/`RevealAsync` e os vetores, então o ponto de crash publicado bate com `ComputeCrashPoint` e `RevealAsync` só é chamado depois do crash da rodada.

### 7.5 Testes E2E
- N/A — a jornada do jogador sobre este contrato é provada pelos E2E da SPEC-0010 (user_facing: false aqui).

### 7.6 Outros
- Carga (k6, local via Aspire): 1.000 conexões numa sala por 10 rodadas; critério: `RoundCrashed` entregue a todos com p99 ≤ 200 ms e zero desconexões não intencionais. Script em `tests/load/rounds/`; roda sob demanda pela imagem oficial `grafana/k6` fixada por digest (sem instalação local), com a sessão do jogador da SPEC-0009 — por isso a execução e o resultado entram no Relatório de Entrega depois que a SPEC-0009 estiver integrada (G7 desta spec espera por ela).
- Convenções da CI: integração com `Category=Integration`; asserções de tempo de relógio (IT-01 ≤ 6 s, IT-04 ± 250 ms) também com `Category=Performance`, em coleção sem paralelismo.

**Dublês e dados de teste:** FakeTimeProvider nos unitários; Fairness real nos de integração (cadeia curta).

**Ambiente de execução:** Testcontainers (Postgres, Redis); carga local via Aspire.

## 8. Plano de Rollout
- **Estratégia:** merge direto.
- **Dados/schema:** cria schema `rounds` (migração inicial).
- **Compatibilidade:** contrato do hub v1; mudanças incompatíveis exigem emenda.
- **Observabilidade:** métricas `rounds.started`, `rounds.crashed`, `rounds.annulled`, `rounds.leader_changes`, `rounds.crash_broadcast_latency`; traces por rodada.
- **Rollback:** reverter o merge commit e a migração (sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
- [x] Rodada global ou por operador? — Salas configuráveis: começa uma sala por operador, salas compartilhadas por configuração (thomas, 2026-10-07)

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0005, ADR-0010, ADR-0017, ADR-0018, ADR-0019 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04 com a tag `SPEC-0008:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, IT-06, CT-01, CT-02 com a tag `SPEC-0008:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
| G1 Red | PASS | verify G1: PASS; `dotnet test Apogeu.slnx --filter "Category!=Nightly&Category!=CiLive" && pnpm -C src/frontend test` exit 1 (red: [xUnit.net 00:01:51.93]     Dado um campo novo num DTO sem regenerar, quando contracts:che) — 7d311ce | 2026-10-09 |
| G2 Green | PASS | build exit 0 (✓ built in 54ms); test exit 0 (Duration  1.57s (tests 99%, transform 1%)); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 (2026-10-09T09:48:10: Report generation took 0,3 seconds) — 40fe12d | 2026-10-09 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  841ms (tests 97%, transform 2%, import 1%)) — 40fe12d | 2026-10-09 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: APPROVED — re-review em 40fe12d, 0 blocker/major; pendências: teste unitário do LeaderLease, RoundCrashed reenviado na retomada logo após o crash, snapshot com growthK atual da sala — 40fe12d | 2026-10-09 |
| G5 Integração & CI | PASS | build exit 0 (✓ built in 58ms); test exit 0 (Duration  1.20s (tests 98%, transform 1%)); test_integration exit 0 (Passed!  - Failed:     0, Passed:    13, Skipped:     0, Total:    13, Duration: 2 m 50 s ); test_e2e exit 0 (3 passed (4.3s)); arch_test exit 0 (Duration  772ms (tests 97%, transform 2%, import 1%)); security_scan exit 0 (Ran 8 rules on 217 files: 0 findings.) — 3285825 | 2026-10-09 |
| H2 Integração aprovada | PASS | política auto-on-green (aprovada por thomas em 2026-10-08); G5 PASS | 2026-10-09 |
| G6 Deploy | N/A | Sem ambientes ainda: validação local por decisão do usuário (ADR-0013); deploy entra com a SPEC-0012 (hospedagem e deploy) | 2026-10-09 |
| G7 Pronto & Docs | PENDING | | |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-09 | G1 | spec | D1: contrato SPEC-0007@1 não dá o ponto de crash antes do crash sem revelar a semente (ADR-0014); D2: UT-02 contraditório (m(11.552)=1,99); D3: arquivos gerados fora de touches; D4: RoundSnapshot.current, envelope de erro, tipo do RTP, growthK e id da sala indefinidos; D5: cliente SignalR fora dos ADRs; D6: teste de carga sem token antes da SPEC-0009 e sem k6 | Test-writer leu SPEC-0008, SPEC-0007, ADRs e o código; parou antes do scaffold | Architect (emenda) + thomas (autorização para seguir recomendações) | Emenda v2 da SPEC-0008 (D1–D6) + emenda v2 da SPEC-0007 (GetCrashPointAsync) + emenda do ADR-0005 (cliente SignalR nos testes) | 2026-10-09 |
| IMP-02 | 2026-10-09 | G7 | spec | Carga k6 (§7.6): 8 de 1.000 JoinSala falharam com Npgsql 53300 'too many clients' — cada entrada lê o Postgres (SalaSnapshotQuery) e a rajada esgota max_connections; entrega do crash p99 134 ms passou | Repetido com 1.000 VUs/1 rodada (10 falhas) e 500 VUs/10 rodadas (6 falhas): independe do volume | Architect: emenda com teste de regressão (IT-07) e correção no Rounds (snapshot em memória, pool limitado) | Emenda (2026-10-09) — IT-07 e correção em 6a75505: snapshot da sala servido da memória do líder com leitura coalescida nos seguidores, pool de 10 conexões no Rounds; k6 1.000 VUs × 10 rodadas: 0 falhas de entrada, crash p99 118 ms | 2026-10-09 |

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
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0008`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v2 | 2026-10-09 | (D1) ponto de crash via `GetCrashPointAsync` da SPEC-0007@2, só em memória até o crash; `RevealAsync` só depois; (D2) UT-02 corrigido com a regra `t ≥ t_c ⇒ m = crash`; (D3) `touches` com os contratos gerados e `Directory.Packages.props`; (D4) `RoundSnapshot.current`, tipos de `rtp`/`growthK`, id da sala (texto + UUID v5) e erros (ProblemDetails `code`) definidos; (D5) cliente `Microsoft.AspNetCore.SignalR.Client` só nos testes (emenda do ADR-0005); (D6) carga por imagem k6 após a SPEC-0009 | IMP-01 (SPEC_DEFECT do Test-writer, 2026-10-09) | SPEC-0010 (pin → SPEC-0008@2, SPEC-0007@2) | Architect — recomendações aplicadas pela autorização do usuário (thomas, 2026-10-09); a confirmar no resumo da manhã |
| v2 (inalterado) | 2026-10-09 | Escopo: `tools/contracts/frontend-modules.mjs` — `formatTypeScript` roda o Prettier até a saída estabilizar (uma passada não é idempotente para assinaturas longas do hub); a regressão é o IT-02 da SPEC-0006 (`contracts:check` no estado versionado), que falha no scaffold desta spec | D7 do Test-writer: o contrato gerado não satisfazia ao mesmo tempo `contracts:check` e `format:check` | SPEC-0006 (só a correção do gerador; contrato inalterado) | Architect — pela autorização do usuário para seguir recomendações (2026-10-09) |
| v3 (inalterado) | 2026-10-09 | Novo IT-07 (rajada de JoinSala sem esgotar o Postgres); correção: snapshot da sala servido da memória do condutor/Redis em vez de consulta por entrada, e pool de conexões limitado por DbContext | Teste de carga k6 do §7.6 (IMP-02): 8/1.000 entradas falharam com "too many clients" | Nenhuma (contrato inalterado) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09) |

| v3 | 2026-10-09 | REST de rodadas (histórico e verificação) passa a exigir a sessão do jogador (Bearer) e responde 401 sem ela; o hub já exigia | Correção de integração da onda 4 (3050067) aplicou a política de sessão ao REST; o contrato não registrava — lacuna encontrada pelo Implementer da SPEC-0010. Mantido por negação por padrão (ADR-0015); verificador público fica para spec futura | SPEC-0010 (cliente já envia a sessão; pin → @3) | Architect — pela autorização do usuário para seguir pelo recomendado (thomas, 2026-10-09); a confirmar no resumo |