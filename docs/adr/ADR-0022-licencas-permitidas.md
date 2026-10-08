---
id: ADR-0022
title: Ampliação das licenças permitidas
status: accepted
origin: user
date: 2026-10-08
pillars: [dependencias]
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Verificação de licenças no CI (ferramenta escolhida na SPEC-0004) usando a lista permitida do ADR-0016 ampliada por este ADR"
---

# ADR-0022 — Ampliação das licenças permitidas

## Contexto e Problema
O ADR-0016 permite só MIT, Apache-2.0, BSD, ISC e MPL-2.0 e exige ADR para qualquer outra. A revisão independente da SPEC-0002 (G4) encontrou dependências permissivas fora da lista, todas trazidas por ferramentas já decididas: Npgsql (PostgreSQL License, via EF Core + PostgreSQL do ADR-0010), minimatch (BlueOak-1.0.0, ferramentas de build), language-subtag-registry (CC0-1.0, via eslint-plugin-jsx-a11y) e caniuse-lite (CC-BY-4.0, dados usados pelo Vite/browserslist no build).

## Direcionadores da Decisão
- Decisão do usuário (opção A, 2026-10-08): ampliar a lista em vez de trocar de ferramentas.
- As licenças são permissivas e compatíveis com produto comercial fechado.

## Opções Consideradas
- N/A — escolha do usuário (origin: user). A alternativa descartada era restringir a lista às dependências de runtime.

## Resultado da Decisão
**Opção escolhida:** ampliar a lista permitida do ADR-0016.

**Regras (verificáveis):**
- Lista permitida passa a ser: MIT, Apache-2.0, BSD-2-Clause, BSD-3-Clause, ISC, MPL-2.0, **PostgreSQL**, **BlueOak-1.0.0**, **CC0-1.0**, **0BSD** e **Unlicense**.
- **CC-BY-4.0** é permitida **somente** para dados usados no build e não distribuídos como código (ex.: caniuse-lite); a atribuição é mantida pelo próprio pacote.
- Qualquer outra licença (inclusive GPL/AGPL e comerciais) continua exigindo ADR.

### Consequências
- **Boa**, porque mantém EF Core + PostgreSQL, Vite e jsx-a11y sem atrito.
- **Ruim**, porque a verificação automática precisa distinguir CC-BY-4.0 de build de dependência de runtime.

### Confirmação (G3)
Verificação de licenças no CI (SPEC-0004) com esta lista.

## Prós e Contras das Opções
N/A — escolha do usuário.

## Mais Informações
Licenças identificadas por `pnpm licenses list` e pelos metadados do nuget.org na revisão da SPEC-0002 (2026-10-08). Textos das licenças: spdx.org/licenses — **não verificados nesta sessão**.
