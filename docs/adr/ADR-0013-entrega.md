---
id: ADR-0013
title: "Entrega: CI no GitHub Actions, validação local e hospedagem adiada"
status: accepted
origin: decision
date: 2026-10-07
pillars: [entrega]
baseline: entrega@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Workflows do GitHub Actions obrigatórios na proteção de branch; deploy_staging/deploy_production do sdd-config vazios e G6 = N/A apontando a spec de hospedagem até ela existir"
---

# ADR-0013 — Entrega: CI no GitHub Actions, validação local e hospedagem adiada

## Contexto e Problema
Fase pessoal: o usuário decidiu validar tudo localmente por enquanto e escolher hospedagem depois. Mesmo assim, a main precisa estar sempre publicável, para que a hospedagem seja só um passo a mais, e não uma reestruturação.

## Direcionadores da Decisão
- Sem custo de hospedagem agora (decisão do usuário, 2026-10-07).
- Repositório no GitHub; CI gratuito.
- Artefato de deploy pronto desde o início (imagem de container).

## Opções Consideradas
- GitHub Actions com CI por caminho + imagens de container geradas pelo Aspire; staging/produção adiados para uma spec de hospedagem
- Hospedar staging desde já (Azure Container Apps, AWS São Paulo ou Fly.io/Railway) — adiado por decisão do usuário

## Resultado da Decisão
**Opção escolhida:** **GitHub Actions** + **validação local via Aspire**. O CI constrói e testa tudo e gera as imagens de container a cada merge (sem publicar ainda). Staging e produção ficam para a **spec "Hospedagem e deploy"**, que será criada quando o usuário decidir a nuvem. Ela trará a estimativa de custo (pilar custo) e reativará as regras de staging e produção abaixo.

**Regras (verificáveis):**
- Um único pipeline leva do commit à produção; nada é publicado fora dele.
- A branch principal está sempre em estado publicável (build, testes e imagem de container verdes).
- Staging é atualizado automaticamente a cada merge, com smoke e E2E contra ele — **suspensa até a spec de hospedagem**; até lá, smoke e E2E rodam contra o ambiente Aspire no CI.
- Produção exige confirmação humana por release; estratégia (direta, flag, canário, blue-green) definida por spec.
- Rollback em um passo, ensaiado; mudanças de dados compatíveis com a versão anterior.
- Métricas DORA acompanhadas a partir do primeiro deploy real.
- CI filtrado por caminho (`src/backend/**`, `src/frontend/**`, `docs/**`), com o job SDD sempre obrigatório.

### Consequências
- **Boa**, porque não há custo agora e a decisão de nuvem fica para quando houver mais informação (operadores, jurisdição).
- **Ruim**, porque o *walking skeleton* não chega a produção na fundação; problemas de ambiente real (rede, TLS, latência) aparecem mais tarde. Risco registrado no épico de fundação.

### Confirmação (G3)
Pipeline GitHub Actions; `deploy_staging`, `deploy_production` e `smoke_test` no sdd-config preenchidos pela spec de hospedagem; até lá G6 = N/A com referência a ela.

## Prós e Contras das Opções
| Critério (peso) | Local + CI agora, hospedagem depois | Hospedar staging já |
|---|---|---|
| Custo na fase pessoal (5) | ✅ zero | ❌ mensal |
| Detecção de problemas de ambiente real (3) | ❌ tardia | ✅ cedo |
| Decisão de nuvem bem informada (3) | ✅ | ⚠️ prematura |

## Mais Informações
Referências neutras: DORA (Accelerate), Twelve-Factor App. Cota gratuita do GitHub Actions para repositório privado: **não verificada nesta sessão**. Verificado em 2026-10-07.
