# Changelog

Todas as mudanças relevantes deste projeto são documentadas aqui.

O formato segue [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/)
e o projeto adota [Versionamento Semântico](https://semver.org/lang/pt-BR/).
Cada entrada cita a spec de origem (`SPEC-NNNN`); gere-as com `spec_graph.py report SPEC-NNNN --changelog`.

## [Unreleased]

### Added
- Contratos gerados do backend para o front-end: OpenAPI versionado, tipos e clientes TypeScript de REST e hubs em `packages/contracts`, verificação de divergência (`contracts:check`) e convenções de JSON (dinheiro e multiplicador como string decimal, datas UTC) também nos hubs (SPEC-0006)
- Ambiente local com Aspire (PostgreSQL, Redis, Keycloak, API e apps Vite) e observabilidade base com OpenTelemetry, health checks e mascaramento de dados sensíveis (SPEC-0005)
- Harness de testes (Testcontainers, Vitest, Playwright com axe, cobertura, Stryker) e testes de arquitetura em .NET e no front que garantem os ADRs (SPEC-0003)
- Estrutura do repositório e qualidade de código: solução .NET 10 em monólito modular (6 módulos), workspace pnpm com os apps e pacotes do front, análise estrita, lint/format e hooks de commit (SPEC-0002)
