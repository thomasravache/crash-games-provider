# Apogeu

Provedor B2B de crash games (primeiro jogo: Pipa). Monólito modular .NET 10 com PostgreSQL e Redis, front-end em workspace pnpm (Vite + React + TypeScript) e orquestração local com Aspire. Contexto, decisões e specs estão em [`docs/`](docs/).

## Pré-requisitos

- .NET SDK 10.0.x (fixado em `global.json`)
- Node.js na linha LTS indicada em `src/frontend/.nvmrc` (24) e pnpm 10 (`packageManager` em `src/frontend/package.json`)
- Docker (testes de integração e ambiente local das próximas specs)

## Estrutura

```text
src/backend/{Host,BuildingBlocks,Modules/<Modulo>/<Modulo>.<Camada>}
src/frontend/{apps/<app>,packages/<pkg>}
src/demo/DemoCasino          (cassino demo: operador externo, só HTTP assinado com a Apogeu)
tests/backend/<Projeto>.Tests, tests/e2e
docs/{adr,specs}
```

Módulos: Rounds, Fairness, Betting, Wallet, Operators e Backoffice. Cada um expõe só o projeto `<Modulo>.Contracts`; as camadas internas são `Features` (vertical slices), `Infrastructure` e, em Rounds, Fairness, Betting e Wallet, `Domain` (ADR-0001). O `Host` registra cada módulo com `Add<Modulo>Module()` e `Map<Modulo>Endpoints()`.

## Comandos

Todos na raiz do repositório.

```bash
# Back-end
dotnet build Apogeu.slnx                        # 0 avisos, 0 erros (avisos são erros)
dotnet test Apogeu.slnx                         # testes (os de integração são lentos: usam git, dotnet e pnpm reais)
dotnet format Apogeu.slnx --verify-no-changes   # exit 0 se formatado
dotnet format Apogeu.slnx                       # aplica a formatação

# Front-end
pnpm -C src/frontend install --frozen-lockfile
pnpm -C src/frontend build
pnpm -C src/frontend lint
pnpm -C src/frontend typecheck
pnpm -C src/frontend format:check
pnpm -C src/frontend format                     # aplica a formatação
pnpm -C src/frontend contracts:generate         # regenera packages/contracts a partir do backend (rode dotnet build antes)
pnpm -C src/frontend contracts:check            # falha se os contratos versionados divergem do backend

# Controle SDD
python3 tools/sdd/spec_graph.py status
git config core.hooksPath .githooks             # ativa commit-msg e pre-commit (uma vez por clone)
```

## Rodar a Pipa demo

Com o Docker aberto:

```bash
dotnet run --project src/backend/AppHost
```

O terminal mostra o link de login do painel do Aspire (porta 15270). No painel, copie o endereço do recurso `api` e abra `<endereço>/dev/launch-demo` (só em Development): ele cria uma sessão demo e redireciona para o jogo. O token de lançamento vale uma vez — para recarregar, volte a esse link. Rodadas, histórico, "Comprovadamente Justo" e som funcionam. Dá para apostar com a carteira demo (R$ 3.000,00): painel de aposta com cash-out manual e automático, perda no crash e estorno de rodada anulada. Para fixar a porta da API, use `AppHost__ApiHttpPort=5080`. Os E2E sobem o mesmo ambiente com `tools/ci/run-e2e.sh`.

### Testar no celular (túnel)

Dois túneis do cloudflared (sem conta) expõem a API e o jogo locais; o AppHost recebe os endereços públicos e os repassa à API (link de lançamento e CORS) e ao jogo (endereço da API).

```bash
brew install cloudflared

# 1. Suba o AppHost com as portas fixas
AppHost__ApiHttpPort=5080 AppHost__GameHttpPort=5173 dotnet run --project src/backend/AppHost

# 2. Em outros dois terminais, abra um túnel para cada porta e anote os endereços https://….trycloudflare.com
cloudflared tunnel --url http://localhost:5080
cloudflared tunnel --url http://localhost:5173

# 3. Pare o AppHost (Ctrl+C) e suba de novo com os endereços públicos (os túneis continuam abertos)
AppHost__ApiHttpPort=5080 AppHost__GameHttpPort=5173 \
AppHost__PublicApiUrl=https://<api>.trycloudflare.com \
AppHost__PublicGameUrl=https://<jogo>.trycloudflare.com \
dotnet run --project src/backend/AppHost
```

No celular, abra `https://<api>.trycloudflare.com/dev/launch-demo`. Sem os endereços públicos, tudo funciona como antes (só no Mac).

> **Atenção:** enquanto um túnel estiver aberto, qualquer pessoa com o link abre o jogo demo. Ao terminar, feche os túneis com Ctrl+C.

## Qualidade de código

- C#: `Nullable`, `TreatWarningsAsErrors`, `AnalysisMode=Recommended` e Meziantou.Analyzer em `Directory.Build.props`; versões de pacotes em `Directory.Packages.props` (Central Package Management) com `packages.lock.json` versionados; fonte de pacotes única em `nuget.config`.
- TypeScript: `tsconfig.base.json` estrito; ESLint (typescript-eslint `strictTypeChecked`, react-hooks, jsx-a11y) e Prettier.
- Commits em Conventional Commits com o rodapé `Refs: SPEC-NNNN` (hook `commit-msg`).
- Segredos locais (`.env*`, `secrets.json`, `appsettings.*.local.json`) são ignorados pelo git.

## Contratos com o front-end

O backend é a fonte única (ADR-0008): o build do Host gera `artifacts/openapi/v1.json` (versionado) e o manifesto dos hubs, e `contracts:generate` produz os tipos e clientes TypeScript em `src/frontend/packages/contracts`, que nunca é editado à mão. Dinheiro (`Money`) e multiplicador (`Multiplier`) trafegam como string decimal, instantes em ISO-8601 UTC com `Z`, enums em camelCase — no REST e nos hubs SignalR.

## CI

GitHub Actions (`.github/workflows`): `ci` em todo PR e push na `main` (jobs `backend`, `frontend`, `e2e`, `security`, `contracts` e `sdd`, com filtro de caminhos), `images` no merge (imagens como artefato `container-images`) e `nightly` (simulação de RTP e mutação). Os checks são informativos (sem proteção de branch, SPEC-0015) e o merge acontece só com a CI verde. O repositório está público temporariamente (desde 2026-10-15, até concluir a jornada do jogador) porque a cota gratuita de Actions do repositório privado acabou; ao voltar a privado, a CI passa para o runner próprio da SPEC-0059 (variável `CI_RUNNER`). Os testes live do pipeline (`Category=CiLive`) abrem PRs de prova; veja `tests/ci/README.md`.
