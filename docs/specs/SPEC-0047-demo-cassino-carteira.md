---
id: SPEC-0047
title: "Cassino demo: carteira própria (seamless)"
tier: full
type: feature
user_facing: true
status: implemented
created: 2026-10-10
parent: SPEC-0035
depends_on: [SPEC-0045, SPEC-0046]
consumes_contract: [SPEC-0021@4]
contract_version: 1
touches: ["src/demo/DemoCasino/**", "tests/demo/DemoCasino.Tests/**", "src/frontend/apps/demo-site/**", "tests/e2e/demo-site/**", "src/backend/AppHost/Program.cs"]
adrs: [ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027]
external: []
size: M
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0047 — Cassino demo: carteira própria (seamless)

## 1. Visão Geral
O cassino demo passa a ter carteira própria e implementa o contrato público da carteira do operador; o operador demo é configurado em modo seamless, e o saldo aparece no topo do cassino e muda conforme as apostas no jogo.

## 2. Motivação & Escopo
**Motivação:** prova ponta a ponta a integração que os cassinos reais farão (fase 2 da decisão de thomas, 2026-10-10).

**Objetivos (dentro do escopo):**
- Endpoints `/apogeu/v1/{balance|debit|credit|rollback}` no DemoCasino, verificando a assinatura, idempotentes por transactionId, com saldo inicial R$ 3.000,00 por jogador.
- Saldo no topo do cassino atualizado ao voltar do jogo e a cada 5 s.
- Configuração do operador demo em modo seamless no ambiente de desenvolvimento.

**Não-objetivos (fora do escopo):**
- Persistência durável (memória do processo basta para o demo).

## 3. Dependências
- **Implementações necessárias:** SPEC-0045, SPEC-0046.
- **Contratos consumidos:** SPEC-0021@4 (contrato público da carteira).
- **Pré-requisitos externos:** N/A

## 4. Decisão Arquitetural
**Contexto:** o `demo-site` é esqueleto (SPEC-0002/0005) e o AppHost já o sobe. O cassino é um cliente externo (ADR-0027): fala só pela API pública de operador (SPEC-0009/0020). Referência de cliente HMAC: a assinatura de referência dos testes da SPEC-0009 (`ReferenceSignature`).

**Decisão:** o DemoCasino implementa o contrato público como qualquer operador; nada da Apogeu é referenciado.

**Justificativa:** é a referência viva para integradores.

**Desvio do padrão existente:** Nenhum.

**Alternativas descartadas:** usar a carteira demo da Apogeu para sempre (não prova o seamless).

**ADRs:** ADR-0002, ADR-0003, ADR-0009, ADR-0025, ADR-0027.

## 5. Requisitos Não-Funcionais
- **Desempenho e escala:** resposta da carteira do cassino ≤ 50 ms local.
- **Segurança:** assinatura da Apogeu verificada em toda chamada; requisição sem assinatura → 401 — IT-02.
- **Privacidade e dados pessoais:** só ids fictícios.
- **Disponibilidade e resiliência:** N/A — demo.
- **Acessibilidade (UI):** WCAG 2.2 AA; axe sem violações nas telas — E2E-01.
- **Custo:** N/A

## 6. Artefato A — Contrato
**Interface:** contrato público da carteira (docs/integration/operator-wallet-api.md, SPEC-0021@4) implementado pelo DemoCasino.

```text
POST /apogeu/v1/balance|debit|credit|rollback (assinado pela Apogeu) → { status, balance, currency, transactionId }
Regras: saldo inicial 3.000,00; débito sem saldo → INSUFFICIENT_FUNDS; mesmo transactionId → DUPLICATE com o resultado original; estorno sem débito → TRANSACTION_NOT_FOUND
Emenda v1: jogador desconhecido é provisionado com 3.000,00 no primeiro uso (vale para os jogadores do /dev/launch-demo e mantém os E2E da Pipa); `balance` como string decimal com 2 casas em todas as respostas, inclusive TRANSACTION_NOT_FOUND; o débito aceita `roundId: null`
Assinatura (Emenda v1): a Apogeu assina e o DemoCasino verifica com a chave do operador demo que o cassino já usa (`Apogeu:OperatorKeyId`/`OperatorSecret`), sobre o caminho `/apogeu/v1/...` e os bytes exatos do corpo; chave desconhecida, segredo errado, corpo alterado ou timestamp fora de ±300 s (pelo TimeProvider do DI) → 401
Cassino: GET /casino/balance → 200 { balance, currency } (exatamente esses campos) | 401 sem sessão; o saldo aparece no topo do lobby e da página do jogo, lido ao entrar no lobby, ao voltar do jogo e a cada 5 s
Development (Emenda v1): o AppHost configura o Host com `Wallet__Operators__demo__Mode=Seamless`, `WalletUrl` = endpoint do recurso demo-casino, `KeyId`/`Secret` = os parâmetros da chave demo já existentes; a recarga do HUD da Pipa (carteira demo) não se aplica ao operador seamless
```

**Design:** Stitch — projeto "Apogeu Games — Identidade e Backoffice" (1861192770945985645), design system "Apogeu — Lobby demo (cassino neutro)", telas "Cassino Demo — Lobby" e "Cassino Demo — Pipa (Refinado)" (saldo no topo).

**Arquivos/módulos afetados:** ver `touches`.

### 6.1 Mapa de Comportamentos
| Cenário | Condição / Entrada | Resultado esperado | Testes |
|---|---|---|---|
| Débito | aposta R$ 10 no jogo | saldo do cassino 2.990,00 | IT-01, E2E-01 |
| Sem saldo | aposta acima do saldo | INSUFFICIENT_FUNDS no jogo | IT-01 |
| Duplicado | mesmo transactionId | DUPLICATE com o resultado original | UT-01 |
| Estorno sem débito | rollback desconhecido | TRANSACTION_NOT_FOUND | UT-01 |
| Assinatura | chamada sem assinatura | 401 | IT-02 |
| Contrato | respostas | conferem com o contrato público | CT-01 |
| Jornada | apostar e retirar no cassino demo | saldo do topo reflete débito e crédito | E2E-01 |

## 7. Artefato B — Plano de Testes (TDD)
### 7.1 Testes de Caracterização
- N/A — código novo; os módulos consultados já têm cobertura das próprias specs.

### 7.2 Testes Unitários
- **UT-01** — Dada a carteira do cassino, quando débito, débito repetido, crédito e estorno sem débito, então OK, DUPLICATE com o mesmo resultado, OK e TRANSACTION_NOT_FOUND.

### 7.3 Testes de Integração
- **IT-01** — Dados DemoCasino e Apogeu reais em modo seamless, quando o jogador aposta e retira, então as chamadas chegam assinadas e o saldo do cassino reflete débito e crédito; aposta acima do saldo é recusada.
- **IT-02** — Dada uma chamada sem assinatura ou com assinatura inválida, quando chega ao DemoCasino, então 401.

### 7.4 Testes de Contrato
- **CT-01** — Dadas as respostas reais do DemoCasino, quando validadas contra o contrato público da carteira, então conferem.

### 7.5 Testes E2E
- **E2E-01** — Dado o cassino demo em modo seamless, quando o jogador aposta R$ 10,00, retira no voo e volta ao lobby, então o saldo do topo é 3.000 − 10 + pagamento; axe sem violações.

### 7.6 Outros
- N/A

**Dublês e dados de teste:** operador demo configurado em seamless só no ambiente de desenvolvimento.

**Ambiente de execução:** DemoCasino + Host real nos IT; Playwright com o ambiente Aspire nos E2E.

## 8. Plano de Rollout
- **Estratégia:** merge direto (ambiente local até a SPEC-0012).
- **Dados/schema:** N/A (memória).
- **Compatibilidade:** operador demo muda de demo para seamless só em Development.
- **Observabilidade:** contagem de chamadas por status no DemoCasino.
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
- [x] Red: escrever UT-01, IT-01, IT-02, CT-01 com a tag `SPEC-0047:<ID>` e confirmar que falham pelo motivo certo
- [x] Green: implementar o mínimo para passar, seguindo o padrão citado
- [x] Refactor mantendo tudo verde
- [x] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase 2: Jornada E2E**
- [x] Red: E2E-01 falhando pelo motivo certo
- [x] Green: jornada completa passando localmente

**Fase final: Integração, entrega e documentação**
- [x] Review independente (G4)
- [x] Integração + CI verde (G5) e aprovação (H2)
- [x] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [x] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 12. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência (comando + resultado, SHA, execução de CI, veredito). -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 66ae264 | 2026-10-10 |
| G1 Red | PASS | verify G1: PASS; `python3 tools/ci/affected-tests` exit 1 (red: [xUnit.net 00:00:13.93]     Dados DemoCasino e Apogeu reais em modo seamless, quando o jog) — 9babb28 | 2026-10-15 |
| G2 Green | PASS | build exit 0 (✓ built in 337ms); test exit 0 ($ dotnet test Apogeu.slnx --filter Category=Performance -m:1); lint exit 0 (All matched files use Prettier code style!); coverage exit 0 ($ dotnet reportgenerator "-reports:artifacts/coverage/raw/**/coverage.cobertura.xml" -targ) — 7a1b8e6 | 2026-10-15 |
| G3 Arquitetura | PASS | arch_test exit 0 (Duration  1.02s (tests 96%, transform 3%, import 1%)) — 7a1b8e6 | 2026-10-15 |
| G4 Review | PASS | verify G1+G4: PASS; revisão: Reviewer independente: APPROVED (sem bloqueantes; dinheiro, assinatura e ADR-0027 conferidos). Minors: jogo local depende do demo-casino (rodado com --no-build: exige dotnet build antes) e o saldo volta a 3.000 a cada reinício (memória); sem testes do débito depois do próprio estorno nem de concorrência; verificação sobre o caminho decodificado (igual para /apogeu/v1/*); memória sem limite no demo; estorno de crédito responde TRANSACTION_NOT_FOUND sem conferir jogador/moeda; polling continua com a aba oculta; saldo sem papel acessível próprio — 7a1b8e6 | 2026-10-15 |
| G5 Integração & CI | PASS | MANUAL: CI do PR #38 verde (run 38778078986, commit b0f5e0d): backend (suíte .NET completa + Performance, com os testes do DemoCasino desde a SPEC-0060), frontend, contracts, e2e (Chromium, WebKit e Firefox, com SPEC-0047:E2E-01 e os E2E da Pipa com o operador demo em seamless), security e sdd; backend verde na 2ª tentativa após falha de tempo conhecida no teste do líder impedido do cash-out (troca de rodada, sem relação com a carteira); merge 4e038be | 2026-10-15 |
| H2 Integração aprovada | PASS | aprovado por thomas — H2 auto-on-green (CI verde do PR #38, run 38778078986); decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) | 2026-10-15 |
| G6 Deploy | N/A | sem ambiente de deploy até a SPEC-0012 (hospedagem e deploy) | 2026-10-15 |
| G7 Pronto & Docs | PASS | Relatório de Entrega e Definição de Pronto: ok — d38ee62 | 2026-10-15 |

## 13. Registro de Impedimentos
<!-- Toda parada é registrada pelo Architect com `spec_graph.py impede` e fechada com `resolve` — não edite à mão. Tipos: spec (spec errada/incompleta → resolve com Emenda) | decisão (só o humano decide → resposta ou ADR) | trabalho (falta algo que exige código → SPEC-NNNN nova) | externo (acesso, ambiente, terceiro → ação tomada) | falha (3 FAILs seguidos no mesmo gate → diagnóstico e decisão). Com impedimento aberto a spec aparece como parada no INDEX e não pode ser fechada. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|
| IMP-01 | 2026-10-15 | G1 | spec | AppHost (configuração Wallet:Operators:demo:* do Development) fora do touches; política para jogadores desconhecidos não definida (afeta os E2E da Pipa do /dev/launch-demo); chave da assinatura, forma de GET /casino/balance e onde o saldo aparece não fixados | test-writer escreveu UT-01, IT-01, IT-02, CT-01 e E2E-01 (red dea8e7b); E2E-01 não passa sem o AppHost | architect | Emenda v1: AppHost no touches, jogador provisionado no primeiro uso, chave do operador demo e saldo no topo | 2026-10-15 |

## 14. Relatório de Entrega
<!-- Preenchido no CLOSE (G7). Diz o que foi feito, como, e prova que foi resolvido. Para status implemented o validate exige todas as subseções preenchidas, todo teste do plano com PASS + evidência e a Definição de Pronto toda marcada. -->

### O que foi entregue
<!-- comportamento entregue do ponto de vista do usuário/sistema -->
O cassino demo tem carteira própria e implementa o contrato público da carteira do operador: a Apogeu debita, credita e estorna nele (`/apogeu/v1/{balance,debit,credit,rollback}`, assinados), com R$ 3.000,00 por jogador e idempotência por transação. Em Development o operador demo está em modo seamless, então as apostas da Pipa — dentro do cassino ou pelo `/dev/launch-demo` — movem o saldo do cassino, que aparece no topo do lobby e da página do jogo. Fecha a jornada do jogador: entrar → lobby → jogar → apostar → saldo da carteira.

### Como foi feito
<!-- decisões de implementação, módulos/arquivos principais, desvios e emendas (com versão), dívidas assumidas -->
Red dea8e7b (UT-01, IT-01 com DemoCasino em Kestrel e Apogeu real em seamless, IT-02, CT-01, E2E-01); green 7ffef56 (DemoCasino `Wallet/`: `CasinoWallet` em memória sob um `Lock`, `ApogeuCallVerifier` com a assinatura do contrato em tempo constante e ±300 s pelo `TimeProvider`, corpo ≤ 16 KB, métrica `democasino.wallet.calls`), 1b8b802 (demo-site: `CasinoBalance` no topo, polling de 5 s, 401 → sair, BRL sobre a string) e c229aa1 (AppHost: `Wallet__Operators__demo__*` no run mode + Development, sem `WaitFor` em ciclo). Emenda v1: AppHost no `touches`, jogador provisionado no primeiro uso, chave do operador demo nos dois sentidos, `GET /casino/balance` exato (IMP-01). Débito que chega depois do próprio estorno não é aplicado (INSUFFICIENT_FUNDS, nada debitado), compatível com a SPEC-0046. G2 local com `APOGEU_PERF_BUDGET_SCALE=2` (latência do cash-out nesta VM).

### Prova de Correção
<!-- type fix: o teste de regressão falhou antes da correção (commit red + saída) e passa depois (commit green + execução). Outros tipos: "N/A". -->
N/A

### Verificação
<!-- Uma linha por teste do plano (todos os IDs da seção 7). Resultado: PASS. Evidência: execução de CI, commit ou relatório. -->
| Teste | Comportamento | Resultado | Evidência |
|---|---|---|---|
| UT-01 | Dada a carteira do cassino, quando débito, débito repetido, crédito e estorno sem débito, então OK, DUPLICATE… | PASS | G2 do registro de gates; CI do PR #38 verde (run 38778078986 em b0f5e0d), merge 4e038be; jornada completa local em 2026-10-15 (entrada → lobby R$ 3.000,00 → Pipa → aposta e retirada → saldo R$ 3.000,07) |
| IT-01 | Dados DemoCasino e Apogeu reais em modo seamless, quando o jogador aposta e retira, então as chamadas chegam a… | PASS | G2 do registro de gates; CI do PR #38 verde (run 38778078986 em b0f5e0d), merge 4e038be; jornada completa local em 2026-10-15 (entrada → lobby R$ 3.000,00 → Pipa → aposta e retirada → saldo R$ 3.000,07) |
| IT-02 | Dada uma chamada sem assinatura ou com assinatura inválida, quando chega ao DemoCasino, então 401. | PASS | G2 do registro de gates; CI do PR #38 verde (run 38778078986 em b0f5e0d), merge 4e038be; jornada completa local em 2026-10-15 (entrada → lobby R$ 3.000,00 → Pipa → aposta e retirada → saldo R$ 3.000,07) |
| CT-01 | Dadas as respostas reais do DemoCasino, quando validadas contra o contrato público da carteira, então conferem… | PASS | G2 do registro de gates; CI do PR #38 verde (run 38778078986 em b0f5e0d), merge 4e038be; jornada completa local em 2026-10-15 (entrada → lobby R$ 3.000,00 → Pipa → aposta e retirada → saldo R$ 3.000,07) |
| E2E-01 | Dado o cassino demo em modo seamless, quando o jogador aposta R$ 10,00, retira no voo e volta ao lobby, então… | PASS | G2 do registro de gates; CI do PR #38 verde (run 38778078986 em b0f5e0d), merge 4e038be; jornada completa local em 2026-10-15 (entrada → lobby R$ 3.000,00 → Pipa → aposta e retirada → saldo R$ 3.000,07) |

### Definição de Pronto
- [x] Todos os testes do plano passando e listados na Verificação
- [x] Todo comportamento do Mapa de Comportamentos coberto e verificado
- [x] Suíte completa, arquitetura e CI verdes no resultado integrado (G5)
- [x] Review independente sem achados blocker/major (G4)
- [x] Padrão arquitetural existente mantido, ou desvio coberto por ADR aprovado
- [x] Requisitos não-funcionais medidos com evidência (ou N/A justificado) — segurança (IT-02), contrato (CT-01) e acessibilidade (E2E-01) verificadas; jornada completa jogada localmente
- [x] Disponível no ambiente-alvo via pipeline, com smoke/E2E passando no ambiente (G6) — N/A até a SPEC-0012; em main pelo PR #38
- [x] Observabilidade e rollback prontos conforme o Plano de Rollout — métrica `democasino.wallet.calls` e logs; rollback = reverter o merge 4e038be (o operador demo volta à carteira demo)
- [x] Documentação raiz e CHANGELOG atualizados (G7)
- [x] Pendências registradas como novas specs (ou nenhuma)
- [x] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [x] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy
<!-- ambiente(s), versão/tag, data, estratégia, estado da feature flag, execução do pipeline -->
Sem ambiente de deploy até a SPEC-0012 (G6 N/A); disponível em main pelo PR #38 (merge 4e038be, 2026-10-15). Jogo local: `dotnet build Apogeu.slnx` antes do AppHost (README).

### Pendências
<!-- specs criadas para o que ficou de fora, ou "Nenhuma" -->
Minors do review (G4): jogo local depende do demo-casino (compilar antes) e o saldo volta a 3.000 a cada reinício; faltam testes do débito depois do estorno e de concorrência (SPEC-0063); verificação sobre o caminho decodificado (igual para `/apogeu/v1/*`); memória sem limite no demo; polling continua com a aba oculta. Do teste da jornada: saldo do topo leva até 5 s e o iframe de altura fixa esconde as abas de apostas (SPEC-0063).

## 15. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda. Mudou o contrato? Incremente `contract_version` e rode `spec_graph.py impacted SPEC-0047`. -->
| Versão do contrato | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 (inalterado) | 2026-10-15 | `src/backend/AppHost/Program.cs` no `touches` (operador demo seamless no Development, URL e chave da carteira do cassino); jogador desconhecido provisionado com 3.000,00; assinatura com a chave do operador demo nos dois sentidos; `GET /casino/balance` exato e 401 sem sessão; saldo no topo do lobby e da página do jogo; recarga do HUD não se aplica ao seamless | Test-writer (SPEC_DEFECT, IMP-01): AppHost fora do `touches`, efeito nos E2E da Pipa do /dev/launch-demo e decisões não fixadas | Nenhuma | decisão recomendada em piloto automático (autorizada por thomas, 2026-10-15) |
