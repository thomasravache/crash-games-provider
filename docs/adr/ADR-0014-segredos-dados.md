---
id: ADR-0014
title: "Segredos, sementes e dados pessoais"
status: accepted
origin: decision
date: 2026-10-07
pillars: [segredos-dados]
baseline: segredos-dados@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "gitleaks no CI; teste de integração de mascaramento de logs; teste de arquitetura: sementes só acessadas pelo módulo Fairness"
---

# ADR-0014 — Segredos, sementes e dados pessoais

## Contexto e Problema
Além de credenciais comuns (banco, Redis, chaves de operadores), o produto guarda as **sementes do servidor** do provably fair: se vazarem antes do uso, alguém prevê os pontos de crash. Recebemos também dados de jogadores dos operadores, sujeitos à LGPD.

## Direcionadores da Decisão
- Sementes não reveladas = segredo crítico do negócio.
- Validação local agora; gerenciador de segredos de nuvem quando houver hospedagem.
- Minimizar dados pessoais: o jogador é do operador.

## Opções Consideradas
- Local: `dotnet user-secrets` + parâmetros secretos do Aspire; produção: gerenciador de segredos da nuvem escolhida
- Arquivo `.env` local não versionado (descartado: fácil de vazar, sem padrão no .NET)

## Resultado da Decisão
**Opção escolhida:** **user-secrets + parâmetros secretos do Aspire** no ambiente local; **gerenciador de segredos da nuvem** (Key Vault, Secrets Manager ou equivalente) definido na spec de hospedagem. **Sementes cifradas em repouso** com chave fora do banco (ASP.NET Core Data Protection ou KMS da nuvem). Dados de jogador **mínimos e pseudônimos**.

**Regras (verificáveis):**
- Nenhum segredo no repositório; varredura de segredos (gitleaks) no CI bloqueia o merge.
- Segredos ficam em user-secrets/parâmetros do Aspire (local) e no gerenciador de segredos da nuvem (staging/produção), separados por ambiente, com acesso mínimo.
- Rotação de credenciais a cada 90 dias e imediata em caso de vazamento; chaves de API de operador rotacionáveis sem indisponibilidade (duas chaves ativas durante a troca).
- Dados pessoais classificados, com base legal, finalidade e prazo de retenção registrados. Recebemos do operador apenas ID pseudônimo do jogador, apelido opcional, moeda e país.
- Dados pessoais, tokens e sementes não reveladas nunca aparecem em logs, traces ou métricas (mascaramento verificado por teste).
- Criptografia em trânsito (TLS) e em repouso; sementes não reveladas cifradas em nível de aplicação.
- Apenas o módulo Fairness lê sementes não reveladas.

### Consequências
- **Boa**, porque o segredo mais sensível (sementes) tem proteção própria, além da do banco.
- **Ruim**, porque a cifragem em nível de aplicação exige gestão de chaves e um teste de recuperação.

### Confirmação (G3)
gitleaks em `security_scan`; teste de mascaramento de logs; teste de arquitetura sobre o acesso às sementes.

## Prós e Contras das Opções
| Critério (peso) | user-secrets + Aspire + gerenciador da nuvem | .env local |
|---|---|---|
| Risco de vazamento (5) | ✅ baixo | ❌ alto |
| Padrão do ecossistema .NET (3) | ✅ | ⚠️ |

## Mais Informações
Referências neutras: OWASP ASVS; LGPD (Lei 13.709/2018). Papel do provedor na LGPD (provavelmente operador de dados, com o cassino como controlador): **não verificado juridicamente; confirmar com assessoria**. Verificado em 2026-10-07.
