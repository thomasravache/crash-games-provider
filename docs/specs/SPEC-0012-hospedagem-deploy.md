---
id: SPEC-0012
title: "Hospedagem na VPS (Aspire, Docker Compose, Caddy e sslip.io)"
tier: epic
type: foundation
status: in-progress
created: 2026-10-07
depends_on: [SPEC-0004, SPEC-0047]
adrs: [ADR-0029, ADR-0013, ADR-0014, ADR-0017, ADR-0018]
external: []
approved_by: thomas
approved_at: 2026-10-15
---

# SPEC-0012 — Hospedagem na VPS (Aspire, Docker Compose, Caddy e sslip.io) (Épico)

<!-- type: feature | migration | foundation (produto novo: ver references/greenfield.md). O épico não é executado: define visão, decisões, decomposição e entrega. Quem executa são as specs filhas (tier full ou lite, tamanho S/M, `parent: SPEC-0012`). A ordem de execução é calculada a partir do frontmatter das filhas (`spec_graph.py waves`). -->

## 1. Visão
A jornada do jogador acessível de qualquer lugar, celular inclusive, em endereços fixos com HTTPS e sem custo: `https://cassino.143-95-218-240.sslip.io` (cassino demo), `https://jogo.143-95-218-240.sslip.io` (Pipa) e `https://api.143-95-218-240.sslip.io` (API pública da Apogeu), publicada pelo Aspire na VPS própria (ADR-0029). O G6 deixa de ser N/A: cada versão tem deploy, smoke e rollback em um passo. Pedido de thomas em 2026-10-15 ("quero deployar com o aspire aqui na vps com um domínio gratuito"); escolhas dele: sslip.io, só a jornada exposta, deploy por script na VPS com a confirmação dele e, depois, deploy pelo GitHub Actions por SSH com aprovação.

## 2. Escopo
**Objetivos (dentro do escopo):**
- SPEC-0065: o AppHost publica a plataforma como Docker Compose de produção (front-ends estáticos, ambiente `Production`, carteira seamless do cassino, chaves do Data Protection persistidas, segredos só como variáveis).
- SPEC-0066: a VPS roda essa publicação atrás do Caddy com HTTPS, com deploy, rollback, backup e smoke por script.
- SPEC-0067 (proposta, depois): deploy pelo GitHub Actions por SSH, com aprovação de thomas no Environment `production` e uma chave restrita ao comando de deploy.

**Não-objetivos (fora do escopo):**
- Backoffice, painel do Aspire e Keycloak acessíveis pela internet.
- Staging separado (a VPS tem um ambiente só, de demonstração).
- Dinheiro real e conformidade (ADR-0020).
- Domínio próprio e alta disponibilidade.

## 3. Arquitetura Alvo
**Contexto:** a plataforma roda hoje só pelo AppHost em modo de desenvolvimento (ADR-0003, ADR-0013). O ADR-0029 decide a hospedagem: publish do Aspire para Docker Compose, Caddy na frente e sslip.io.

```text
internet ──443──► Caddy ─┬─ cassino.<host>  → demo-site (estático) e /casino/* → demo-casino
                         ├─ jogo.<host>     → game-pipa (estático)
                         └─ api.<host>      → api (REST + hubs SignalR)
rede interna: api ↔ postgres, redis, keycloak; demo-casino ↔ api (HMAC) e api → demo-casino (carteira seamless)
```
A SPEC-0065 entrega o compose e as imagens (contrato com a 0066: serviços, variáveis esperadas no `.env` e volumes); a SPEC-0066 entrega o Caddy, os scripts e a operação.

**Decisões (ADRs):** ADR-0029 — hospedagem na VPS com Aspire, Docker Compose, Caddy e sslip.io (supera a parte "hospedagem adiada" do ADR-0013); ADR-0014 (segredos fora do repositório; chaves do Data Protection protegidas).

**Regras de arquitetura a garantir (G3):** só o Caddy publica portas; nenhum segredo literal no compose nem no repositório; produção sem rotas `/dev/*` — garantidas pelo UT-01 da SPEC-0065 e pelo smoke da SPEC-0066.

## 4. Decomposição
<!-- Uma linha por spec filha. As colunas de dependência são informativas: a fonte da verdade é o frontmatter de cada filha. -->
| Spec | Título | Tier | Tipo | Tamanho | Depende de | Consome contrato de |
|---|---|---|---|---|---|---|
| SPEC-0065 | Publicação do AppHost para Docker Compose em produção | full | foundation | M | SPEC-0047 | — |
| SPEC-0066 | Deploy na VPS com HTTPS, backup e rollback | full | foundation | M | SPEC-0065 | — |
| SPEC-0067 | Deploy pelo GitHub Actions na VPS com aprovação e chave restrita | lite | foundation | S | SPEC-0066 | — |

## 5. Estratégia de Entrega
- **Ambientes:** um ambiente de demonstração na VPS (produção demo); cada deploy com a confirmação de thomas até a SPEC-0067.
- **Entrega por onda:** onda 1: SPEC-0065; onda 2: SPEC-0066 com o primeiro deploy (G6); a SPEC-0067 depois, quando thomas aprovar.
- **Feature flags:** N/A
- **Rollback:** `tools/deploy/vps-rollback` volta à versão anterior em um passo; o código, por revert do merge.
- **Métricas de sucesso pós-release:** smoke verde após cada deploy; memória da VPS abaixo de 85% na primeira semana; jornada jogada por thomas pelo celular.

## 6. Riscos & Mitigações
- VPS compartilhada com desenvolvimento e outros projetos — limites de memória/CPU por container e o AppHost de desenvolvimento desligado quando a produção subir.
- Backup na mesma máquina — retenção de 7 dias; destino externo numa spec futura.
- Limite de certificados do Let's Encrypt no domínio sslip.io compartilhado — Caddy reaproveita os certificados no volume; se o limite bater, cair para o certificado ZeroSSL que o Caddy também usa.
- Endereço expõe o IP da VPS — só 80/443 e SSH abertos; fail2ban já ativo.

## 7. Critérios de Aceite do Épico
<!-- Verificáveis ponta a ponta. Cada critério cita o teste que o prova (`SPEC-NNNN:E2E-01`; IT para critérios que não são jornada). O épico só fecha com todos marcados e todas as filhas implemented/deprecated. -->
- [ ] Jornada completa pelos endereços públicos com HTTPS: entrar, abrir a Pipa, apostar, retirar e ver o saldo do cassino — SPEC-0066:E2E-01
- [ ] Deploy e rollback em um passo, e backup restaurável — SPEC-0066:IT-01, SPEC-0066:IT-02
- [ ] Compose de produção sem segredos e só com o proxy exposto — SPEC-0065:UT-01

## 8. Questões em Aberto
<!-- Dúvidas que impedem fechar a spec, respondidas ANTES do H1. Aberta: `- [ ] pergunta (quem responde)`. Respondida: `- [x] pergunta — resposta (quem, data)`. Com alguma aberta o G0 reprova. Sem dúvidas: escreva "Nenhuma". Dúvida que surge depois da aprovação vira impedimento. -->
- [x] Qual domínio gratuito? — sslip.io (thomas, 2026-10-15)
- [x] O que fica exposto? — só a jornada do jogador (thomas, 2026-10-15)
- [x] Como fazer o deploy? — script na VPS com a confirmação de thomas; depois uma Action segura (thomas, 2026-10-15); abordagem da Action: deploy por SSH com aprovação no Environment e chave restrita, o padrão de mercado para VPS (thomas, 2026-10-15)
- [x] Nuvem e orçamento (pergunta do rascunho anterior)? — VPS própria, custo zero (ADR-0029)
- [x] Residência dos dados (pergunta do rascunho anterior)? — N/A enquanto for só demonstração, sem jogador real (ADR-0020)

## 9. Aprovação (H1)
Uma aprovação humana cobre o épico e as specs filhas apresentadas junto com ele. Registrada no frontmatter (`approved_by`, `approved_at`) do épico e de cada filha.

## 10. Registro de Impedimentos
<!-- Problemas que envolvem várias filhas (conflito de integração da onda, CI quebrado, decisão transversal). Impedimento que trava uma filha específica vai na própria filha. -->
| ID | Aberto em | Fase/Gate | Tipo | Descrição | Tentativas | Responsável | Resolução | Fechado em |
|---|---|---|---|---|---|---|---|---|

## 11. Relatório de Entrega
<!-- Preenchido ao fechar o épico: o que foi entregue, como (ondas e deploys com versão/data), resultado dos critérios de aceite com os testes que os provam, métricas pós-release, pendências como novas specs. `spec_graph.py report SPEC-0012` ajuda a montar. -->

## 12. Emendas
<!-- Mudança em spec aprovada: uma linha por emenda, aprovada pelo humano. -->
| Versão | Data | Mudança | Motivo | Specs impactadas | Aprovado por |
|---|---|---|---|---|---|
