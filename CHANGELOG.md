# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Security
- ASP.NET Core, Extensions e EF Core 10.0.13 e SDK 10.0.402: corrige o CVE-2026-70159 (GHSA-334f-vvp4-7g6p), negação de serviço no SignalR; testes de guarda impedem voltar à faixa vulnerável (SPEC-0058)

### Added
- Cassino demo: página do jogo — botão Jogar no lobby, lançamento assinado no BFF (`POST /casino/play/{gameCode}`, uso único, 404 GAME_NOT_AVAILABLE e 502 PROVIDER_UNAVAILABLE) e a Pipa num iframe com sandbox mínimo, tela cheia, "Voltar ao lobby" e a coluna "Sobre o jogo" (SPEC-0045)
- Cassino demo: lobby com o catálogo da Apogeu — `GET /casino/games` no BFF (chamada assinada, cache de 60 s, 401 sem sessão e 502 PROVIDER_UNAVAILABLE com a Apogeu fora do ar), banner de destaque, abas Todos/Crash/Novidades e cartões com a arte do kit visual; o catálogo `GET /api/v1/games` passa a trazer `thumbnailUrl` e `bannerUrl` (SPEC-0044)
- Abas de apostas da Pipa: "Todas as apostas" ao vivo com apelido anonimizado e totais da rodada, e "Minhas apostas" com o histórico do jogador e "Carregar mais" por cursor (SPEC-0031)
- Cassino demo: backend próprio (`DemoCasino`, operador externo que fala com a Apogeu só por HTTP assinado) e tela de entrada com apelido e moeda, sessão em cookie e segredo HMAC só no servidor (SPEC-0043)
- Carteira seamless: adaptador HTTP da carteira do próprio operador (assinatura HMAC, idempotência por transação, prazo, reenvio de crédito e estorno até confirmar pelo outbox, livro-razão espelho); modo de carteira por operador (SPEC-0046)
- Apostas ao vivo e histórico pessoal: lotes a cada 200 ms (até 100 itens) com apelido anonimizado e `betRef` opaco, snapshot ao entrar na sala e `GET /api/v1/me/bets` com paginação por cursor; uma instância do Host (várias instâncias na SPEC-0056) (SPEC-0025)
- Fator `APOGEU_PERF_BUDGET_SCALE` para os limites locais dos testes de desempenho em máquinas compartilhadas (padrão 1, ignorado na CI) (SPEC-0052)
- Jogo local no celular por túnel: com `AppHost:PublicApiUrl` e `AppHost:PublicGameUrl` o lançamento demo, a API e o CORS usam os endereços públicos dos túneis; sem eles nada muda (SPEC-0050)
- Painel de aposta da Pipa: dois painéis com valor, fichas e cash-out automático, botões APOSTAR / CANCELAR / RETIRAR / PRÓXIMA, ganho, perda e estorno no painel, saldo demo com recarga, mensagens de erro em português e tremor do "PIPA CORTADA!" (SPEC-0026)
- Liquidação da rodada: apostas não retiradas viram perdidas no crash, rodada anulada estorna as apostas, cash-out em andamento no crash é respeitado e cada rodada fecha um resumo único com o GGR (SPEC-0024)
- Cash-out manual e automático: decisão pelo líder da sala no horário de recebimento (tolerância 0 ms), pagamento truncado em centavos e limitado ao ganho máximo, automático no alvo exato e reconciliado no crash, pedido pendente decidido pelo horário gravado e crédito durável com aviso de saldo (SPEC-0023)
- Apostas pelo hub `/hubs/bets`: até duas apostas por rodada (painéis 1 e 2) com saída automática opcional, aposta durante o voo valendo para a próxima rodada e cancelamento com estorno integral (ou pendente, idempotente); reserva gravada antes do débito, varredura e estorno durável pelo outbox do Wolverine, sem débito sem aposta ou sem estorno (SPEC-0022)
- Carteira demo por jogador e operador: R$ 3.000,00 iniciais, débito, crédito e estorno idempotentes, recarga abaixo da aposta mínima, livro-razão em partidas dobradas só de inserção e contrato público da carteira do operador (`docs/integration/operator-wallet-api.md`) (SPEC-0021)
- Som da Pipa com Web Audio: vento e tensão que crescem com o multiplicador, decolagem, marcos e corte no crash; ligado após o primeiro gesto, botão de mudo acessível por teclado e preferência lembrada (SPEC-0018)
- Animação cinematográfica da Pipa desenhada em código: orla do Rio com morros, pipa de 4 cores com rabiola por física Verlet, céu dia → pôr do sol → noite com estrelas e lua, nuvens em paralaxe, rastro brilhante, marcos 2x–100x e crash com clarão, tremor e queda; movimento reduzido respeitado; 6 quadros-chave aprovados comparados na CI (SPEC-0011)
- Catálogo de jogos e limites por operador e moeda: `GET /api/v1/games` para o operador, lançamento recusa jogo indisponível (GAME_NOT_AVAILABLE) e moeda sem limites (CURRENCY_NOT_ENABLED), limites lidos com cache por versão (SPEC-0020)
- Pipa demo de ponta a ponta, ainda sem apostas: lançamento demo, HUD com fases e multiplicador ao vivo, cena PixiJS com céu por faixa de multiplicador, histórico, verificação "Comprovadamente Justo" no navegador e telas de sessão expirada e reconexão; E2E com o backend real em Chromium, WebKit e Firefox (SPEC-0010)
- Motor de rodada e salas em tempo real: um líder por sala com lease e fencing no Redis, hub SignalR com snapshot, sync e crash, histórico e verificação por REST com sessão do jogador, failover sem repetir rodada e anulação após 31 s sem líder; 1.000 conexões por sala com crash entregue em p99 118 ms (SPEC-0008)
- Segurança base: requisições de operador assinadas com HMAC e proteção contra reenvio, lançamento demo com token de uso único, sessão do jogador, backoffice com Keycloak, negação por padrão e trava de dinheiro real (SPEC-0009)
- Justiça do resultado provably fair: cadeia de sementes SHA-256, ponto de crash em aritmética exata, verificação pública e vetores v1 reproduzíveis (SPEC-0007)
- Pipeline de CI no GitHub Actions: build, testes (unitários, integração, arquitetura, performance e E2E), segurança (gitleaks, Semgrep, vulnerabilidades e licenças), verificação de contratos e job SDD; imagens de container no merge; nightly com RTP e mutação; Dependabot semanal (SPEC-0004)
- Contratos gerados do backend para o front-end: OpenAPI versionado, tipos e clientes TypeScript de REST e hubs em `packages/contracts`, verificação de divergência (`contracts:check`) e convenções de JSON (dinheiro e multiplicador como string decimal, datas UTC) também nos hubs (SPEC-0006)
- Ambiente local com Aspire (PostgreSQL, Redis, Keycloak, API e apps Vite) e observabilidade base com OpenTelemetry, health checks e mascaramento de dados sensíveis (SPEC-0005)
- Harness de testes (Testcontainers, Vitest, Playwright com axe, cobertura, Stryker) e testes de arquitetura em .NET e no front que garantem os ADRs (SPEC-0003)
- Estrutura do repositório e qualidade de código: solução .NET 10 em monólito modular (6 módulos), workspace pnpm com os apps e pacotes do front, análise estrita, lint/format e hooks de commit (SPEC-0002)

### Changed
- A CI roda os testes .NET e os E2E quando o cassino demo muda (`src/demo/**`, `tests/demo/**`); antes esses PRs pulavam o job `backend` (SPEC-0060)
- Gates locais G1/G2 rodam só os testes afetados pela mudança (`tools/ci/affected-tests`); backend, arquivos de apoio compartilhados e arquivos não mapeados continuam rodando a suíte inteira, e o G5 local e a CI do GitHub rodam tudo (SPEC-0051)

### Fixed
- Placar da Pipa no topo da cena, compacto e sem fundo opaco: o multiplicador não cobre mais a pipa (SPEC-0049)
- Teste de mutação do `Fairness.Domain` volta a medir de verdade: Stryker.NET com xUnit v3 e score ≥ 85% exigido no nightly (SPEC-0016)
