---
id: ADR-0012
title: "Qualidade de código: formatadores, linters, analisadores e SAST"
status: accepted
origin: decision
date: 2026-10-07
pillars: [qualidade-codigo]
baseline: qualidade-codigo@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Job lint do CI: dotnet format --verify-no-changes, build com TreatWarningsAsErrors, pnpm lint, pnpm typecheck, prettier --check, Semgrep e gitleaks"
---

# ADR-0012 — Qualidade de código: formatadores, linters, analisadores e SAST

## Contexto e Problema
Duas stacks (C# e TypeScript), código de dinheiro e de resultado certificado, e agentes de IA escrevendo boa parte do código. Sem formatação, lint e análise estática automáticos e bloqueantes, o estilo diverge, bugs triviais chegam à revisão e problemas de segurança passam despercebidos.

## Direcionadores da Decisão
- Regras automáticas e bloqueantes no CI, iguais para humanos e agentes.
- Ferramentas gratuitas e de licença aberta (repositório privado, sem GitHub Advanced Security por ora).
- Feedback rápido local (hooks) e verificação definitiva no CI.

## Opções Consideradas
- **TS lint/format:** ESLint (typescript-eslint, type-aware) + Prettier | Biome
- **C#:** analisadores do SDK + .editorconfig + `dotnet format` + Meziantou.Analyzer | + SonarAnalyzer
- **SAST:** Semgrep CE | CodeQL (exige GitHub Code Security em repositório privado)

## Resultado da Decisão
**Opção escolhida:**
- **.NET:** `.editorconfig` versionado; `dotnet format --verify-no-changes`; `Nullable` habilitado, `TreatWarningsAsErrors`, `AnalysisLevel=latest` com `AnalysisMode=Recommended`; **Meziantou.Analyzer** (MIT) para regras extras (async, culture, `decimal`, disposables). Configuração central em `Directory.Build.props`.
- **TypeScript:** `strict` + `noUncheckedIndexedAccess` + `exactOptionalPropertyTypes`; **ESLint** flat config com typescript-eslint `strictTypeChecked`, react-hooks e jsx-a11y; **Prettier** para formatação; `tsc --noEmit` por pacote.
- **Segurança:** **Semgrep CE** (SAST, regras para C# e TS) e **gitleaks** (segredos) no CI; CodeQL quando o repositório for público ou houver licença.
- **Local:** hooks do SDD (`commit-msg` e `pre-commit`) + formatação dos arquivos alterados no pre-commit.

Porque ESLint type-aware tem as regras de React/acessibilidade de que o HUD precisa (o Biome ainda cobre menos esses casos), e o Semgrep CE é gratuito para repositório privado.

**Regras (verificáveis):**
- Formatador com verificação automática no CI (falha se o código não estiver formatado).
- Linter com regras versionadas no repositório; zero avisos novos nas linhas alteradas (no .NET, todo aviso é erro).
- Checagem de tipos habilitada no modo mais estrito viável para a linguagem (C# com nullable; TS strict + flags acima).
- Suprimir uma regra exige comentário com o motivo na própria linha; supressão sem motivo é achado do revisor.
- Análise estática de segurança (SAST) no CI.
- Proibido `any` explícito e `@ts-ignore` sem motivo; preferir `@ts-expect-error` com comentário.
- Proibido `console.log` em código de produção do front (regra ESLint); logs passam pelo logger do SDK.

### Consequências
- **Boa**, porque estilo e erros triviais ficam fora da revisão humana e do G4.
- **Boa**, porque agentes de IA recebem feedback objetivo e imediato.
- **Ruim**, porque ESLint type-aware é mais lento que o Biome (mitigado por cache no CI).
- **Ruim**, porque `TreatWarningsAsErrors` torna atualizações do SDK e dos analisadores mais trabalhosas.

### Confirmação (G3)
Job `lint` obrigatório no CI rodando todos os comandos acima; comando `lint` e `security_scan` do sdd-config.

## Prós e Contras das Opções
| Critério (peso) | ESLint + Prettier | Biome |
|---|---|---|
| Regras type-aware/React/a11y (5) | ✅ completo | ⚠️ parcial |
| Velocidade (3) | ⚠️ | ✅ |
| Uma ferramenta só (2) | ❌ duas | ✅ |

| Critério (peso) | Semgrep CE | CodeQL |
|---|---|---|
| Custo em repo privado (5) | ✅ grátis | ❌ licença paga |
| Profundidade de análise (3) | ⚠️ | ✅ |

## Mais Informações
Referências neutras: regras publicadas das próprias ferramentas. Versões e licenças (ESLint, typescript-eslint, Prettier, Meziantou.Analyzer, Semgrep CE, gitleaks) e o custo atual do GitHub Code Security: **não verificados nesta sessão; confirmar nas fontes oficiais antes do H1**. Verificado em 2026-10-07.
