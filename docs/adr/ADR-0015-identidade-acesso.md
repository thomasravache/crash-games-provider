---
id: ADR-0015
title: "Identidade e acesso: Keycloak (OIDC), lançamento de jogo e API de operadores"
status: accepted
origin: decision
date: 2026-10-07
pillars: [seguranca-acesso]
baseline: seguranca-acesso@2026.09
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "Testes de integração de acesso negado por endpoint; teste de arquitetura: endpoints sem [Authorize]/política explícita falham; nenhuma referência a SDK de fornecedor de identidade"
---

# ADR-0015 — Identidade e acesso: Keycloak (OIDC), lançamento de jogo e API de operadores

## Contexto e Problema
Três tipos de identidade: (1) **usuários do backoffice** (equipe do provedor e de cada operador, com isolamento entre operadores); (2) **sistemas dos operadores** chamando nossa API; (3) **jogadores**, que não têm conta conosco e chegam pelo cassino. O usuário pediu algo gratuito agora e robusto para o futuro.

## Direcionadores da Decisão
- Custo zero na fase pessoal.
- Robustez futura: SSO corporativo (operadores grandes pedem SAML/OIDC com o próprio provedor de identidade), MFA, multi-empresa.
- Sem ficar preso a fornecedor: trocar o provedor de identidade deve ser configuração.

## Opções Consideradas
- Keycloak (open source, auto-hospedado)
- Serviço gerenciado com plano gratuito (Microsoft Entra External ID, Auth0, Clerk)
- Identidade própria com ASP.NET Core Identity (descartado: reinventar MFA/SSO)

## Resultado da Decisão
**Opção escolhida:** **Keycloak 26.x** para o backoffice, rodando localmente via `Aspire.Hosting.Keycloak`, com a aplicação falando **OIDC padrão** (sem SDK de fornecedor), para que migrar para um serviço gerenciado seja só configuração. **Organizations** do Keycloak separam os usuários por operador. Para os outros dois tipos de identidade:
- **Operadores (máquina → máquina):** requisições assinadas com **HMAC-SHA256** (ID da chave + carimbo de tempo + corpo), com janela anti-replay. É o padrão de integrações de cassino.
- **Jogadores:** o operador chama nosso endpoint de lançamento → emitimos um **token de lançamento de uso único e curta duração** → o jogo troca por um **token de sessão** (JWT assinado por nós, curto) vinculado a jogador, operador, moeda e sala. O SignalR autentica com esse token.

**Regras (verificáveis):**
- Autenticação centralizada no Keycloak (backoffice), em HMAC (operadores) e em tokens de sessão (jogadores); sem senha armazenada por nós.
- Autorização negada por padrão; cada endpoint declara política explícita.
- Usuário de um operador nunca vê dados de outro operador (teste de acesso negado obrigatório nas specs do backoffice).
- Menor privilégio para usuários, serviços e credenciais de CI.
- Sessões e tokens com expiração e revogação: token de lançamento ≤ 60 s e uso único; sessão de jogador ≤ 8 h, renovável; chaves de operador revogáveis.
- MFA obrigatório para administradores do provedor.
- O plano de testes de cada spec com regra de acesso inclui o caso de acesso negado.
- Os riscos do OWASP Top 10 aplicáveis são checados na revisão (G4).

### Consequências
- **Boa**, porque é gratuito, tem SSO, SAML, MFA e multiempresa, e não depende de plano pago.
- **Boa**, porque OIDC padrão permite trocar por Entra/Auth0 sem reescrever.
- **Ruim**, porque em produção alguém precisa operar o Keycloak (atualizações, banco, alta disponibilidade). A alternativa é migrar para gerenciado na spec de hospedagem.

### Confirmação (G3)
Testes de integração de acesso negado; teste de arquitetura (todo endpoint com política; sem SDK de fornecedor); checklist OWASP no G4.

## Prós e Contras das Opções
| Critério (peso) | Keycloak | Gerenciado (plano grátis) | ASP.NET Identity |
|---|---|---|---|
| Custo agora e em escala (4) | ✅ grátis (custo é operar) | ⚠️ grátis até um limite; SSO corporativo costuma ser pago | ✅ |
| SSO SAML/OIDC por operador (4) | ✅ | ⚠️ planos pagos | ❌ construir |
| Esforço de operação (3) | ❌ nosso | ✅ fornecedor | ⚠️ |
| Aprisionamento (3) | ✅ nenhum | ⚠️ | ✅ |
| Roda local no Aspire (2) | ✅ | ❌ só nuvem | ✅ |

## Mais Informações
- Keycloak 26.7.0 (jul/2026): SCIM em preview, permissões administrativas por organização — https://versionlog.com/keycloak/26.7/ — verificado em 2026-10-07; **confirmar a última versão em keycloak.org**.
- Aspire.Hosting.Keycloak (preview, linha 13.x) — https://www.nuget.org/packages/Aspire.Hosting.Keycloak
- Limites e preços dos planos gratuitos de Entra External ID, Auth0 e Clerk: **não verificados nesta sessão**.
