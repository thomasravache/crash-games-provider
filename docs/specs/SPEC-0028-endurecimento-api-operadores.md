---
id: SPEC-0028
title: Endurecimento da API de operadores
tier: lite
type: fix
user_facing: false
status: approved
created: 2026-10-09
parent:
depends_on: [SPEC-0047]
consumes_contract: []
touches: ["src/backend/Modules/Operators/**", "src/backend/BuildingBlocks/Security/**", "src/backend/Host/Security/**", "tests/backend/Security.Tests/**", "tests/backend/Modules/Operators.Tests/**", "src/demo/DemoCasino/**", "tests/demo/DemoCasino.Tests/**", "src/backend/Modules/Wallet/Wallet.Infrastructure/Seamless/**", "tests/backend/Modules/Wallet.Tests/**", "docs/integration/**"]
adrs: [ADR-0015, ADR-0014, ADR-0019]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0028 — Endurecimento da API de operadores

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão (G4) da SPEC-0009: (1) o servidor assina o caminho decodificado e a query não entra na assinatura; (2) sem ForwardedHeaders o limite por IP agrupa todos atrás de um proxy, e chamadas não autenticadas ao lançamento (UNKNOWN_KEY/INVALID_SIGNATURE) não têm limite antes da autenticação; (3) `launch_grants` nunca é limpa; (4) o OpenAPI não declara `securitySchemes` nem os cabeçalhos `X-Apogeu-*`; (5) rotas públicas (`/api/v1/meta`, `/health`, `/alive`, `/openapi`) dependem de uma lista no Host em vez de `.AllowAnonymous()` por endpoint.

## 2. Causa Raiz
`OperatorSignatureAuthenticationHandler` usa `PathBase+Path` decodificado; `CreateSessionEndpoint`/lançamento usam `Connection.RemoteIpAddress`; falta tarefa de limpeza; `DenyByDefaultRequirement` tem lista de rotas.

## 3. Mudança Proposta
Canonicaliza caminho + query na assinatura (contrato documentado); ForwardedHeaders configurável e limite por IP antes da autenticação; limpeza periódica de `launch_grants` expirados; esquemas de segurança no OpenAPI; `.AllowAnonymous()` nas rotas públicas com teste de que todo endpoint declara política; proteção contra reenvio e limite de taxa em **todo** endpoint com assinatura de operador (inclusive o `GET /api/v1/games` da SPEC-0020), no handler de autenticação; teste do contador `operators.launch_rejected`; isolamento entre operadores provado com um segundo operador (achados do review da SPEC-0020). Não muda: o algoritmo HMAC, códigos de erro e janelas.

**Padrão seguido:** `src/backend/BuildingBlocks/Security/**` e `Operators.Features/**` (SPEC-0009)

**Rollback:** revert do commit

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0028:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — coberto pelos testes da SPEC-0009
- **UT-01** — Dados caminhos com caracteres codificados e query, quando a assinatura canônica é calculada no cliente e no servidor, então coincidem; mudar a query invalida a assinatura.
- **UT-02** — Dados caminhos com caracteres codificados (acento, espaço) e query, quando o cassino demo assina para a Apogeu e a Apogeu assina para a carteira do cassino (SPEC-0046/0047), então os dois verificadores aceitam a assinatura canônica e recusam a de outro caminho; o contrato público (`docs/integration/operator-wallet-api.md`) descreve a forma canônica (Emenda v1).
- **IT-01** — Dado o Host real atrás de um proxy simulado, quando 61 lançamentos não autenticados chegam do mesmo IP real, então o 61º recebe 429; `launch_grants` expirados somem após a limpeza; todo endpoint declara política ou `AllowAnonymous`.
- **IT-02** — Dado um segundo operador sem a Pipa e sem limites em BRL, quando ele chama `GET /api/v1/games` e lança a Pipa, então recebe `[]` e 400 GAME_NOT_AVAILABLE, sem ver nada do operador demo; a mesma assinatura repetida no `GET /api/v1/games` é recusada como reenvio; cada rejeição de lançamento incrementa `operators.launch_rejected{reason}` uma vez.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, IT-01, IT-02 com a tag `SPEC-0028:<ID>` e confirmar que falham pelo motivo certo
- [ ] Green: implementar o mínimo para passar, seguindo o padrão citado
- [ ] Refactor mantendo tudo verde
- [ ] Validar: build + suíte completa + arquitetura (G2/G3)

**Fase final: Integração, entrega e documentação**
- [ ] Review independente (G4)
- [ ] Integração + CI verde (G5) e aprovação (H2)
- [ ] Deploy (G6): N/A até a SPEC-0012 (hospedagem), registrado no gate
- [ ] Relatório de Entrega, docs raiz e CHANGELOG (G7)

## 8. Registro de Gates
<!-- Status: PENDING | PASS | FAIL | N/A. PASS e N/A exigem evidência. -->
| Gate | Status | Evidência | Data |
|---|---|---|---|
| G0 Spec | PASS | validate: 0 erro(s) — 47b8bcc | 2026-10-10 |
| G1 Red | PENDING | | |
| G2 Green | PENDING | | |
| G3 Arquitetura | PENDING | | |
| G4 Review | PENDING | | |
| G5 Integração & CI | PENDING | | |
| H2 Integração aprovada | PENDING | | |
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
- [ ] Nenhum segredo, token ou semente não revelada em logs, traces ou no repositório (gitleaks e testes de mascaramento verdes)
- [ ] Sem avisos novos de build/lint e nenhuma supressão de regra sem motivo na própria linha

### Deploy

### Pendências

## 11. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
| 1 | 2026-10-15 | O caminho canônico também vale para o cassino demo (assinatura do `OperatorSigningHandler` e verificação do `ApogeuCallVerifier`) e para a assinatura das chamadas da Apogeu à carteira do operador (seamless); `touches` + DemoCasino, Wallet seamless e `docs/integration/**`; UT-02; depende da SPEC-0047 | Pendência da onda 1 (o DemoCasino assina o caminho codificado e o Host verifica o decodificado) e o review da SPEC-0047; a carteira e o cassino nasceram depois da spec | SPEC-0009, SPEC-0021, SPEC-0043, SPEC-0047 | thomas (2026-10-15: "aprovo os ajustes") |
