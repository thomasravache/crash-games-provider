---
id: SPEC-0007
title: "Justiça do resultado: cadeia de sementes e ponto de crash"
tier: full
type: foundation
user_facing: false
status: in-progress
created: 2026-10-07
parent: SPEC-0001
depends_on: [SPEC-0003]
consumes_contract: []
contract_version: 1
touches: ["src/backend/Modules/Fairness/**", "tests/backend/Modules/Fairness.Tests/**", "tests/vectors/**"]
adrs: [ADR-0001, ADR-0006, ADR-0010, ADR-0014]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-07
---

# SPEC-0007 — Justiça do resultado: cadeia de sementes e ponto de crash

## 1. Visão Geral
Implementa o módulo Fairness: geração da cadeia de sementes por sala, cálculo determinístico do ponto de crash a partir de semente + sal + RTP + limite, verificação pública, armazenamento cifrado das sementes e vetores de teste compartilhados com o front.

## 2. Motivação & Escopo
**Motivação:** o resultado é o coração regulado do produto; precisa ser imprevisível, reprodutível, verificável pelo jogador e certificável (ADR-0006).

**Objetivos (dentro do escopo):**
- `Fairness.Domain`: algoritmo v1 (abaixo), puro, sem I/O.
- `Fairness.Infrastructure`: persistência da cadeia no schema `fairness`, com a origem da cadeia cifrada (Data Protection) e checkpoints.
- `Fairness.Contracts`: `IFairnessService` v1 (criar cadeia, próxima rodada, revelar, verificar).
- `tests/vectors/fairness-v1.json`: vetores fixos usados pelos testes C# e TS.
- Simulação de RTP noturna (≥ 10 milhões de rodadas).

**Não-objetivos (fora do escopo):**
- Entropia externa (ex.: hash de bloco público) — possível emenda futura.
- Tela de verificação (SPEC-0010).

## 3. Dependências
- **Implementações necessárias:** SPEC-0003 — harness, fixtures e regras de arquitetura.
- **Contratos consumidos:** N/A
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** projeto novo; ADR-0001 (Fairness.Domain sem I/O), ADR-0006 (algoritmo), ADR-0014 (sementes cifradas).

**Decisão:** algoritmo v1:
1. Cadeia por sala: `h_0` = 32 bytes de `RandomNumberGenerator`; `h_{k+1} = SHA256(h_k)` até `k = N` (N padrão 1.000.000). Publica-se o **hash terminal** `h_N` e um **sal** aleatório da cadeia no momento da criação.
2. A rodada `r` (1..N) usa a semente `s_r = h_{N-r}`. Verificação de encadeamento: `SHA256(s_r) = s_{r-1}` (com `s_0 = h_N`). O compromisso publicado antes das apostas da rodada `r` é `SHA256(s_r)`, que coincide com a semente já revelada da rodada anterior.
3. `H = HMAC-SHA256(chave = s_r, mensagem = sal)`; `X` = primeiros 52 bits de `H` como inteiro; `E = 2^52`.
4. `crash = min(limite, max(1.00, floor(100 · RTP · E / (E − X)) / 100))` calculado com inteiros grandes (sem ponto flutuante). Com isso, `P(crash ≥ m) = RTP / m` para 1 ≤ m ≤ limite.
5. RTP permitido: 0,90 a 0,99 (padrão demo 0,97); limite padrão 10.000,00x.

Armazenamento: só `h_0` cifrado + checkpoints a cada 10.000 elos; obter `s_r` custa no máximo 10.000 hashes.

**Justificativa:** padrão de mercado (cadeia de hashes), matematicamente simples de auditar; aritmética inteira garante o mesmo resultado em C# e TS.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** sementes de cliente dos primeiros apostadores (faria o resultado depender de dados de jogadores, contra a regra do ADR-0006); gerar cada semente independente (perde a verificação encadeada).

**ADRs:** ADR-0001, ADR-0006, ADR-0010, ADR-0014.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** cálculo do ponto de crash ≤ 1 ms; obter a semente da rodada ≤ 20 ms (≤ 10.000 hashes) — medido em UT-07 e IT-02.
- **Segurança:** sementes não reveladas cifradas e nunca em logs; só `RandomNumberGenerator` — IT-03 e regras de arquitetura (SPEC-0003).
- **Privacidade e dados pessoais:** N/A — nenhum dado de jogador entra no módulo.
- **Disponibilidade e resiliência:** índice da próxima rodada persistido de forma atômica; reinício não reutiliza nem pula semente — IT-02.
- **Acessibilidade (UI):** N/A — sem interface.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** `Fairness.Contracts.IFairnessService` (v1) + vetores `tests/vectors/fairness-v1.json`.

```csharp
Task<ChainInfo> CreateChainAsync(SalaId sala, int length, CancellationToken ct);
  // ChainInfo(ChainId, TerminalHash, Salt, Length) — publicado
Task<RoundSeedCommitment> NextRoundAsync(SalaId sala, CancellationToken ct);
  // (ChainId, RoundIndex, Commitment = SHA256(s_r)); erro ChainExhausted → cria nova cadeia
Task<RevealedRound> RevealAsync(ChainId chain, int roundIndex, Rtp rtp, Multiplier cap, CancellationToken ct);
  // (Seed s_r, PreviousSeed s_{r-1}, Salt, CrashPoint); erro RoundNotFound
CrashPoint ComputeCrashPoint(Seed seed, Salt salt, Rtp rtp, Multiplier cap);   // puro
VerificationResult Verify(Seed seed, Seed previousSeed, Salt salt, Rtp rtp, Multiplier cap, CrashPoint claimed); // puro
  // Valid | ChainMismatch | CrashPointMismatch
Erros de entrada: InvalidRtp (fora de 0,90–0,99), InvalidCap (< 1,00), InvalidSeed (≠ 32 bytes)
Vetores (JSON): [{ seed, previousSeed, salt, rtp, cap, crashPoint }]  — hex minúsculo, valores decimais como string
```

**Design:** N/A — sem interface.

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Ponto de crash reprodutível | semente, sal, RTP, limite dos vetores | crash exatamente igual ao vetor | UT-01, CT-01 |
| Crash instantâneo | X tal que o valor < 1,00 | crash = 1.00 | UT-01 |
| Limite máximo | X próximo de E | crash = limite | UT-02 |
| Encadeamento válido | `s_r` e `s_{r-1}` reais | `Verify` → Valid | UT-03 |
| Semente adulterada | `s_r` alterada | `Verify` → ChainMismatch | UT-04 |
| Crash adulterado | crash informado diferente | `Verify` → CrashPointMismatch | UT-04 |
| Entrada inválida | RTP 0,85 ou 1,00; limite 0,5; semente 31 bytes | erro de validação correspondente | UT-05 |
| Distribuição correta | 1.000.000 de rodadas determinísticas | proporção com crash ≥ 2 ≈ RTP/2 (tolerância estatística) | UT-06 |
| Desempenho do cálculo | 100.000 cálculos | média ≤ 1 ms | UT-07 |
| Persistência cifrada | cadeia criada | banco só tem origem cifrada + checkpoints; revelar devolve a semente correta | IT-01 |
| Continuidade após reinício | reinício do processo entre rodadas | próxima rodada usa o índice seguinte, sem repetir | IT-02 |
| Sementes fora dos logs | criar, avançar, revelar | nenhuma semente não revelada aparece nos logs | IT-03 |
| Cadeia esgotada | índice > N | erro ChainExhausted | IT-04 |
| RTP de longo prazo | 10 milhões de rodadas (noturno) | RTP medido = configurado ± 0,1 p.p. | IT-05 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — projeto novo.

### 7.2 Testes Unitários
- **UT-01** — Dados os vetores de `fairness-v1.json` (incluindo um caso de 1.00x), quando `ComputeCrashPoint` é chamado, então cada resultado é idêntico ao vetor.
- **UT-02** — Dado X = E − 1 e limite 10.000,00, quando calculado, então o resultado é 10000.00.
- **UT-03** — Dada uma cadeia curta gerada no teste, quando `Verify` recebe `s_r` e `s_{r-1}` corretos e o crash correto, então retorna Valid.
- **UT-04** — Dada uma semente com um byte alterado, então `Verify` retorna ChainMismatch; dado um crash informado diferente, então CrashPointMismatch.
- **UT-05** — Dados RTP 0,85, RTP 1,00, limite 0,5 e semente de 31 bytes, quando validados, então lançam InvalidRtp, InvalidRtp, InvalidCap e InvalidSeed.
- **UT-06** — Dado 1.000.000 de rodadas a partir de uma cadeia com origem fixa, quando medida a fração com crash ≥ 2,00, então fica em RTP/2 ± 0,3 p.p.
- **UT-07** — Dados 100.000 cálculos, quando medidos, então a média é ≤ 1 ms.

### 7.3 Testes de Integração
- **IT-01** — Dado PostgreSQL real, quando uma cadeia de 50.000 elos é criada, então o banco contém só a origem cifrada e 5 checkpoints, e `RevealAsync` da rodada 12.345 devolve a semente que encadeia com a 12.344.
- **IT-02** — Dado um processo que avança 3 rodadas e é reiniciado, quando `NextRoundAsync` é chamado, então o índice é 4 e a semente obtida ≤ 20 ms.
- **IT-03** — Dada a captura de logs ativa, quando cadeia é criada, avançada e revelada, então nenhuma semente não revelada aparece em log.
- **IT-04** — Dada uma cadeia de 3 elos já consumida, quando `NextRoundAsync`, então lança ChainExhausted.
- **IT-05** — (Noturno) Dados 10 milhões de rodadas simuladas com RTP 0,97, quando o retorno de uma estratégia de cash-out fixo em 2,00x é medido, então fica em 0,97 ± 0,001.

### 7.4 Testes de Contrato
- **CT-01** — Dado o arquivo `tests/vectors/fairness-v1.json` (contrato v1 consumido pela SPEC-0008 e pelo verificador TS da SPEC-0010), quando validado contra o esquema dos vetores e recalculado pelo domínio, então todos conferem.

### 7.5 Testes E2E
- N/A — módulo interno sem jornada própria; a jornada verificacao-fairness é provada pela SPEC-0010 (user_facing: false).

### 7.6 Outros
- Mutação: Stryker.NET em `Fairness.Domain` com score ≥ 85%.

**Dublês e dados de teste:** cadeias curtas com origem fixa; FakeTimeProvider não necessário (sem tempo).

**Ambiente de execução:** unitários local/CI; integração com Testcontainers; IT-05 no `nightly`.

## 8. Plano de Rollout
- **Estratégia:** merge direto (não há produção).
- **Dados/schema:** cria schema `fairness` (migração inicial, sem dados prévios).
- **Compatibilidade:** algoritmo v1 congelado; mudar exige emenda, nova versão e nova certificação.
- **Observabilidade:** métricas `fairness.rounds_issued`, `fairness.chain_remaining` (alerta abaixo de 5%).
- **Rollback:** reverter o merge commit e a migração (schema novo, sem dados de produção).
- **Etapas de migração/coexistência:** N/A

## 9. Questões em Aberto
Nenhuma.

## 10. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado". O arquiteto nunca aprova a própria spec.

## 11. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. Cada fase começa pelos testes. -->

<!-- Preenchido na fase PLAN (2026-10-07). Cada fase começa pelos testes. -->

**Fase 1: Andaime do contrato**
- [ ] Scaffold (tipos, interfaces, assinaturas lançando NotImplemented, sem lógica) em commit `chore(...): scaffold` separado; ADRs ADR-0001, ADR-0006, ADR-0010, ADR-0014 relidos

**Fase 2: Unitários**
- [ ] Red: escrever UT-01, UT-02, UT-03, UT-04, UT-05, UT-06, UT-07 com a tag `SPEC-0007:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo os ADRs
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte unitária + arquitetura (G2/G3)

**Fase 3: Integração e contrato (dependências reais)**
- [ ] Red: escrever IT-01, IT-02, IT-03, IT-04, IT-05, CT-01 com a tag `SPEC-0007:<ID>`, dependências reais (containers), e confirmar que falham pelo motivo certo
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0007`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| v1 (esclarecimento, sem mudança de versão) | 2026-10-08 | Codificação explicitada: HMAC com chave = 32 bytes crus da semente e mensagem = bytes crus do sal decodificado do hex; X = primeiros 52 bits de H em big-endian; vetores em hex minúsculo, `rtp`/`cap`/`crashPoint` como string decimal (2 casas para cap e crash) | O §4 não fixava a codificação do sal; o Test-writer escolheu a leitura de bytes crus, documentada em `tests/vectors/README.md` e reproduzida por gerador Python independente | SPEC-0008, SPEC-0010 (consomem os vetores; mesmo formato de array do §6) | Architect — esclarecimento dentro do contrato v1, coberto pela autorização do usuário para seguir sem paradas (2026-10-08) |
