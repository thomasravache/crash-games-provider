# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Added
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

### Fixed
- Teste de mutação do `Fairness.Domain` volta a medir de verdade: Stryker.NET com xUnit v3 e score ≥ 85% exigido no nightly (SPEC-0016)
