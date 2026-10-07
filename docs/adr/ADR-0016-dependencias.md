---
id: ADR-0016
title: "Dependências, licenças e ciclo de vida da stack"
status: accepted
origin: decision
date: 2026-10-07
pillars: [dependencias]
baseline: dependencias@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "security_scan no CI (dotnet list package --vulnerable, pnpm audit, gitleaks); Dependabot; verificação de licenças no CI"
---

# ADR-0016 — Dependências, licenças e ciclo de vida da stack

## Contexto e Problema
Produto comercial com duas stacks e muitas dependências. Vulnerabilidades, licenças incompatíveis (como já ocorreu com MediatR/MassTransit) e runtimes em fim de suporte são riscos para certificação e para a venda.

## Direcionadores da Decisão
- Gratuito no GitHub.
- Licenças compatíveis com produto fechado e comercial.
- Versões centralizadas e reprodutíveis.

## Opções Consideradas
- Dependabot (nativo do GitHub)
- Renovate (mais configurável)

## Resultado da Decisão
**Opção escolhida:** **Dependabot** semanal (NuGet, npm/pnpm, GitHub Actions, Docker), agrupando atualizações menores; **Central Package Management** (`Directory.Packages.props`) e lock files no .NET; `pnpm-lock.yaml` no front; Node na linha LTS fixada por `.nvmrc`/`packageManager`.

**Regras (verificáveis):**
- Varredura de vulnerabilidades no CI (`dotnet list package --vulnerable --include-transitive`, `pnpm audit --audit-level high`); severidade alta ou crítica bloqueia o merge.
- Atualização de dependências automatizada (Dependabot) com revisão, pelo menos semanal.
- Nenhuma dependência ou runtime fora de suporte (EOL); o calendário de fim de suporte é revisado a cada trimestre e cada item vira spec (.NET 10 LTS até 14/11/2028).
- Arquivos de trava de versão (lockfiles) versionados.
- Licenças compatíveis com o uso do produto: permitidas MIT, Apache-2.0, BSD, ISC, MPL-2.0; qualquer outra (inclusive comercial, GPL/AGPL) exige ADR. A ferramenta de verificação de licenças é escolhida na spec de CI.
- Pacote novo exige ADR ou citação em ADR existente (o `verify` avisa manifesto alterado sem ADR).

### Consequências
- **Boa**, porque vulnerabilidades e mudanças de licença aparecem cedo, antes de virarem problema comercial.
- **Ruim**, porque o volume de PRs do Dependabot exige rotina de revisão.

### Confirmação (G3)
`security_scan` no sdd-config e no CI; `verify` avisa manifesto alterado sem ADR.

## Prós e Contras das Opções
| Critério (peso) | Dependabot | Renovate |
|---|---|---|
| Configuração mínima (3) | ✅ nativo | ⚠️ app/config |
| Flexibilidade de agrupamento (2) | ⚠️ | ✅ |

## Mais Informações
Referências neutras: SLSA, OpenSSF Scorecard. Fim de suporte do .NET 10: https://dotnet.microsoft.com/platform/support/policy/dotnet-core (verificado em 2026-10-07). Linha LTS atual do Node.js: **não verificada nesta sessão**.
