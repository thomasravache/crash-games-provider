---
id: ADR-0024
title: Áudio dos jogos com Web Audio nativa e efeitos CC0
status: accepted
origin: decision
date: 2026-10-09
pillars: []
decision_makers: [thomas]
consulted: []
informed: []
supersedes:
superseded_by:
enforced_by: "dependency-cruiser: só o scene-kit acessa AudioContext (áudio via GameAudio); revisão no G4 do registro de origem e da licença CC0 de cada arquivo em public/audio; orçamento de pacote no CI"
---

# ADR-0024 — Áudio dos jogos com Web Audio nativa e efeitos CC0

## Contexto e Problema
O jogo precisa de som para a fase de testes e de demonstração a operadores, antes da arte final (ADR-0023), com **custo zero**. Falta decidir como produzir e tocar o som sem pesar no pacote (ADR-0019), respeitando a política de autoplay dos navegadores e a acessibilidade (ADR-0020).

## Direcionadores da Decisão
- Custo zero até o lançamento (decisão do usuário, 2026-10-09).
- Pacote pequeno: orçamento de ≤ 1,5 MB do jogo (ADR-0019).
- Sons que reagem ao multiplicador em tempo real (vento e tensão contínuos), não só arquivos fixos.
- Licença clara para produto comercial.

## Opções Consideradas
- **Web Audio API nativa + síntese própria**, com arquivos **CC0** só quando a síntese não bastar
- **Howler.js** (biblioteca de reprodução de arquivos, MIT)
- **Tone.js** (framework de síntese e sequenciamento, MIT)

## Resultado da Decisão
**Opção escolhida:** **Web Audio API nativa com síntese própria**, porque os sons principais (vento, tensão da linha, marcos) são contínuos e guiados pelo multiplicador. Isso se faz com poucos nós de áudio (ruído filtrado, osciladores, envelopes), sem dependência e com poucos KB. Arquivos entram só para o que a síntese não reproduz bem, e apenas com licença **CC0** (domínio público).

**Regras (verificáveis):**
- Todo áudio passa pela interface `GameAudio` do `scene-kit`; nenhum outro pacote cria `AudioContext` (dependency-cruiser).
- Nenhum som antes do primeiro gesto do jogador; `AudioContext` suspenso com a aba oculta.
- Arquivos de áudio só CC0, com registro de origem (fonte, autor, licença, data) ao lado do arquivo; nada gerado por IA nem de bibliotecas pagas nesta fase.
- Código de áudio ≤ 40 KB gzip; arquivos ≤ 150 KB por jogo, carregados sob demanda.
- Ganho mestre ≤ 0,8 e rampas em toda mudança de volume (sem cliques nem picos).

### Consequências
- **Boa**, porque custa zero, não adiciona dependência e o som acompanha o multiplicador com precisão.
- **Boa**, porque a interface `GameAudio` deixa trocar por arquivos produzidos na arte final (ADR-0023) sem mexer no jogo.
- **Ruim**, porque som sintetizado exige ajuste fino e soa mais "eletrônico" que efeitos gravados por um sound designer.
- **Ruim**, porque mantemos código de áudio próprio (mixagem, rampas, agendamento) em vez de uma biblioteca pronta.

### Confirmação (G3)
dependency-cruiser (só o `scene-kit` usa `AudioContext`); testes da SPEC-0018 com `OfflineAudioContext`; revisão no G4 das licenças dos arquivos; orçamento de pacote no CI.

## Prós e Contras das Opções
| Critério (peso) | Web Audio nativa + síntese | Howler.js | Tone.js |
|---|---|---|---|
| Custo e licença (5) | ✅ zero, sem dependência | ✅ MIT | ✅ MIT |
| Som contínuo guiado pelo multiplicador (5) | ✅ nós de áudio diretos | ⚠️ focado em arquivos | ✅ ótimo |
| Tamanho no pacote (4) | ✅ poucos KB próprios | ✅ pequeno | ❌ grande para o uso |
| Esforço de implementação (3) | ⚠️ código próprio | ✅ simples para arquivos | ✅ alto nível |

## Mais Informações
- Web Audio API e política de autoplay (som só após gesto do usuário) — MDN — **não verificado nesta sessão** (conhecimento consolidado; confirmar versões de navegador no G4).
- Howler.js e Tone.js: licença MIT e tamanhos aproximados — **não verificados nesta sessão**.
- Fontes de efeitos CC0 (ex.: pacotes de áudio da Kenney) — licença a confirmar arquivo por arquivo no registro de origem.
- Spec que introduz este ADR: SPEC-0018. Relacionados: ADR-0004, ADR-0019, ADR-0020, ADR-0021, ADR-0023.
