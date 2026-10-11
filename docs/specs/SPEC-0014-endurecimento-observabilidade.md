---
id: SPEC-0014
title: Endurecimento do mascaramento de logs e health checks
tier: lite
type: fix
user_facing: false
status: approved
created: 2026-10-08
parent:
depends_on: []
consumes_contract: []
touches: ["src/backend/ServiceDefaults/**", "tests/backend/ServiceDefaults.Tests/**", "tests/backend/AppHost.Tests/**"]
adrs: [ADR-0014, ADR-0017, ADR-0010]
external: []
size: S
approved_by: thomas
approved_at: 2026-10-10
---

# SPEC-0014 — Endurecimento do mascaramento de logs e health checks

<!-- Tier lite: bug fix ou mudança pequena, um módulo, sem mudança de contrato público, tamanho S. type: fix | feature | refactor. user_facing: true se o defeito aparece numa jornada do usuário (o teste de regressão deveria ser E2E). Se qualquer condição falhar, use o tier full. Substitua todos os marcadores com chaves duplas. -->

## 1. Problema
Achados menores da revisão (G4) da SPEC-0005: (1) escopos de log (`BeginScope`) são exportados por OTLP sem mascaramento, embora o console os mascare — contraria a regra do ADR-0014; (2) texto de exceções (`exception.ToString()`, `exception.message`/`stacktrace`) sai sem mascaramento; (3) o casamento por substring mascara campos públicos como `serverSeedHash`, útil para auditoria provably fair; (4) o ServiceDefaults depende de Npgsql/StackExchange.Redis só para health checks, duplicando o que as integrações Aspire dos módulos registrarão; (5) `/health` expõe nomes de dependências em qualquer ambiente; (6) `RedisHealthCheck` lê `_connection` fora do lock sem leitura volátil.

## 2. Causa Raiz
`SensitiveDataMaskingProcessor` só trata Attributes/FormattedMessage/Body; `MaskingJsonConsoleFormatter` grava a exceção sem tratar; `SensitiveData` usa substring sem lista de permitidos; `ServiceDefaultsExtensions` mapeia endpoints de saúde sem restrição; double-checked locking sem `Volatile.Read`.

## 3. Mudança Proposta
Desliga `IncludeScopes` no pipeline OTLP (ou mascara escopos), mascara texto de exceções, adiciona lista de campos públicos permitidos (`serverSeedHash`, `terminalHash`, `commitment`), usa `Volatile.Read`; health checks migram para as integrações Aspire quando os módulos adotarem persistência; exposição de `/health` revista com a SPEC-0012. Não muda: nomes de entradas `postgres`/`redis` e o contrato de `/health` da SPEC-0005.

**Padrão seguido:** `src/backend/ServiceDefaults/**` (SPEC-0005)

**Rollback:** revert do commit

**Outras ocorrências:** Nenhuma

## 4. Plano de Testes (TDD)
<!-- Mínimo: um teste de regressão que reproduz o problema e FALHA antes da correção, no nível em que o defeito aparece (IT para fronteiras de I/O, E2E para jornadas de usuário). Código sem cobertura: primeiro CH-xx fixando o comportamento atual. Tag no código: `SPEC-0014:<ID>`. Categoria sem teste: "N/A — motivo" SEM ID. -->
- Caracterização: N/A — o comportamento atual é coberto por UT-01 e IT-04 da SPEC-0005
- **UT-01** — Dado um log com escopo contendo `token=abc` e uma exceção cuja mensagem contém `serverSeed=xyz`, quando exportado por OTLP e pelo console, então nem `abc` nem `xyz` aparecem, e `serverSeedHash` continua visível.
- **IT-01** — Dado o Host real com coletor OTLP de teste, quando um endpoint de teste registra escopo e exceção sensíveis, então o payload OTLP e a saída do console não contêm os valores.

## 5. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento (seção Registro de Impedimentos). -->
Nenhuma

## 6. Aprovação (H1)
Registrada no frontmatter (`approved_by`, `approved_at`) somente depois que o humano responder "Aprovado".

## 7. Checklist de Implementação
<!-- Preenchido na fase PLAN, após a aprovação. -->

<!-- Preenchido na fase PLAN (2026-10-10). Cada fase começa pelos testes. -->

**Fase 1: Regressão e correção**
- [ ] Red: escrever UT-01, IT-01 com a tag `SPEC-0014:<ID>` e confirmar que falham pelo motivo certo
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
| G0 Spec | PASS | validate: 0 erro(s) — eeff2ee | 2026-10-09 |
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
