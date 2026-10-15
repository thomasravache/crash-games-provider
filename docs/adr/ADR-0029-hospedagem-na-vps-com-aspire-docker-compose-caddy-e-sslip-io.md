---
id: ADR-0029
title: Hospedagem na VPS com Aspire, Docker Compose, Caddy e sslip.io
status: proposed
origin: decision
date: 2026-10-15
pillars: [entrega, seguranca]
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "`tools/deploy/` (deploy e rollback na VPS), teste do compose publicado (SPEC-0065) e smoke do deploy (SPEC-0066); revisão no G4"
---

# ADR-0029 — Hospedagem na VPS com Aspire, Docker Compose, Caddy e sslip.io

<!-- Formato MADR (Markdown Architectural Decision Records). status: proposed | accepted | rejected | deprecated | superseded — vira accepted quando a spec que o introduz é aprovada (H1); superseded exige superseded_by. origin: decision (recomendada: compare 2–3 opções e cite fontes com data, ou declare "não verificado") | user (escolhida pelo usuário: respeite, sem reabrir a discussão, e registre riscos e custo) | as-is (documenta um padrão que já existe no projeto). enforced_by: teste de arquitetura que garante a decisão (G3), ou "N/A — motivo". -->

## Contexto e Problema
A jornada do jogador está pronta e thomas quer jogá-la de qualquer lugar (celular inclusive), sem custo. O ADR-0013 adiou a hospedagem; os túneis do cloudflared são efêmeros e dependem de autorização a cada uso. Existe uma VPS própria (4 vCPU, 7 GB, IP público 143.95.218.240, Ubuntu 22.04, Docker) já usada para desenvolvimento e outros projetos.

## Direcionadores da Decisão
- Custo zero até o lançamento (regra de thomas).
- Usar o Aspire também na publicação (ADR-0003), sem uma segunda descrição do sistema.
- Nenhuma senha ou segredo no repositório (público) nem no GitHub (decisão de thomas, 2026-10-15).
- Expor só a jornada do jogador; painel, Keycloak, banco e Redis fechados.
- A VPS é compartilhada: limites de memória e CPU por container.

## Opções Consideradas
- VPS própria: AppHost publicado como Docker Compose (`Aspire.Hosting.Docker`), Caddy na frente com HTTPS automático e domínio gratuito sslip.io
- Nuvem gerenciada (Azure Container Apps, AWS São Paulo, Fly.io/Railway), como previa o rascunho da SPEC-0012
- Túneis do cloudflared sobre o AppHost em modo de desenvolvimento (SPEC-0050)

## Resultado da Decisão
**Opção escolhida:** **VPS própria com Aspire → Docker Compose, Caddy e sslip.io**, porque custa zero, reaproveita o AppHost como fonte única dos recursos e dá HTTPS de verdade num endereço fixo (`cassino.143-95-218-240.sslip.io`, `jogo.…`, `api.…`), sem conta nem token de DNS. Os segredos são gerados na própria VPS (`.env` com permissão 600, fora do repositório) e o deploy é um script na VPS rodado com a confirmação de thomas a cada versão; o deploy contínuo pelo GitHub vem depois, sem segredo no GitHub (decisão de thomas, 2026-10-15).

**Regras (verificáveis):**
- Nenhum segredo no repositório, nas imagens ou no GitHub; o compose publicado só referencia variáveis.
- Só o Caddy publica portas (80/443); banco, Redis, Keycloak e o painel do Aspire ficam só na rede interna.
- Produção roda em `Production`: rotas `/dev/*` não existem.
- Deploy e rollback em um passo pelo `tools/deploy/`; a versão anterior fica disponível para voltar.
- Backup diário do Postgres com restauração testada.
- Cada container com limite de memória, para não derrubar a VPS compartilhada.

### Consequências
- **Boa**, porque a jornada fica acessível de qualquer lugar sem custo, os problemas de ambiente real (TLS, proxy, rede) aparecem cedo e o G6 deixa de ser N/A.
- **Ruim**, porque a produção divide a VPS com o desenvolvimento e outros projetos (sem isolamento nem alta disponibilidade), o backup fica na mesma máquina até haver um destino externo, e o endereço sslip.io expõe o IP.

### Confirmação (G3)
Teste do compose gerado pelo `aspire publish` (SPEC-0065): só o proxy publica portas, nenhum valor de segredo literal, ambiente `Production`. Smoke do deploy (SPEC-0066) contra os endereços públicos. Revisão no G4.

## Prós e Contras das Opções
<!-- Decisão de tecnologia: matriz com critérios ponderados pelo contexto (references/greenfield.md). as-is: "N/A". -->
| Critério (peso) | VPS + Compose + Caddy + sslip.io | Nuvem gerenciada | Túneis do cloudflared |
|---|---|---|---|
| Custo até o lançamento (5) | ✅ zero | ❌ mensal | ✅ zero |
| Endereço fixo com HTTPS (4) | ✅ | ✅ | ❌ muda a cada túnel |
| Ambiente de produção real (4) | ⚠️ VPS compartilhada | ✅ | ❌ modo de desenvolvimento |
| Segredos fora do GitHub (4) | ✅ gerados na VPS | ⚠️ cofre da nuvem | ✅ |
| Esforço (3) | ✅ médio | ❌ alto | ✅ baixo |

## Mais Informações
<!-- Fontes oficiais consultadas, versão estável/LTS e janela de suporte, com a data da verificação. Se não foi possível verificar, diga explicitamente. Links para specs relacionadas. -->
- NuGet `Aspire.Hosting.Docker` 13.6.1 (mesma versão do Aspire do projeto) — verificado em 2026-10-15.
- Docker Hub `caddy` 2.11.7 — verificado em 2026-10-15.
- sslip.io: `<ip-com-traços>.sslip.io` e subdomínios resolvem para o IP embutido, sem cadastro.
- Supera a parte "hospedagem adiada" do ADR-0013; o resto dele continua valendo.
