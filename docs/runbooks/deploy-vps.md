# Runbook — deploy na VPS (SPEC-0066, ADR-0029)

A plataforma publicada roda na VPS (143.95.218.240) como um projeto Docker Compose `apogeu`: o compose gerado pelo
AppHost (`aspire publish`, SPEC-0065) com `api`, `demo-casino`, `postgres` e `redis` numa rede interna, mais o serviço
`proxy` (Caddy 2.11.7, `infra/caddy/`), o único que publica portas (80 e 443).

| Endereço | O que é |
|---|---|
| `https://cassino.143-95-218-240.sslip.io` | cassino demo (estáticos do `demo-site`; `/casino/*` → `demo-casino:8080`) |
| `https://jogo.143-95-218-240.sslip.io` | jogo Pipa (estáticos do `game-pipa`, build de produção, sem gancho de teste) |
| `https://api.143-95-218-240.sslip.io` | API (`api:8080`, REST e `/hubs` por WebSocket) |

HTTP redireciona para HTTPS; certificados do Let's Encrypt no volume `apogeu_caddy-data`. Painel do Aspire, Keycloak,
backoffice, banco e Redis não são expostos.

Toda mudança em produção (deploy, rollback, restore, firewall) é feita com a confirmação de thomas (ADR-0013).

## Onde fica cada coisa

| Caminho (na VPS) | Conteúdo |
|---|---|
| `~/apogeu-deploy/.env` | **segredos** (gerados na primeira vez, permissão 600, nunca versionados nem impressos) |
| `~/apogeu-deploy/src/` | checkout da última versão buscada (fora do repositório de trabalho) |
| `~/apogeu-deploy/releases/<commit>/` | compose, `compose.proxy.yaml` e `release.env` (nomes das imagens) de cada versão |
| `~/apogeu-deploy/state/current`, `previous` | versão no ar e a anterior (alvo do rollback) |
| `~/apogeu-deploy/backups/apogeu-<carimbo>.dump` + `apogeu-keys-<carimbo>.tar.gz` | backup diário: dump do Postgres e chaves do Data Protection (7 dias) |
| imagens `apogeu-api`, `apogeu-demo-casino`, `apogeu-proxy` | tag = 12 primeiros caracteres do commit |

**Segredos.** O `.env` tem `OPERATORS_DEMO_KEY_ID`, `OPERATORS_DEMO_KEY_SECRET`, `PLAYER_SESSION_SIGNING_KEY`,
`POSTGRES_PASSWORD`, `REDIS_PASSWORD` (aleatórios) e `BACKOFFICE_AUTHORITY`, `PUBLIC_API_URL`, `PUBLIC_GAME_URL`. Ele só
existe na VPS: não vai para o Git, para o GitHub, para as imagens nem para os logs dos scripts. Para ver um valor, abra o
arquivo na VPS; não o cole em issue, PR ou chat. Perder o `.env` com o banco já criado obriga a restaurar a senha antiga
do Postgres (ou recriar o volume): faça cópia dele junto com os backups, num lugar seu, fora do repositório.
Opcional: `APOGEU_ACME_EMAIL=voce@exemplo.com` no `.env` registra a conta ACME com e-mail (avisos de expiração).

**Configuração dos scripts** (variáveis de ambiente, com os padrões da VPS): `APOGEU_DEPLOY_DIR` (`~/apogeu-deploy`),
`APOGEU_DEPLOY_REPO` (`git@github.com:thomasravache/crash-games-provider.git`), `APOGEU_COMPOSE_PROJECT` (`apogeu`),
`APOGEU_DOMAIN` (`143-95-218-240.sslip.io`), `APOGEU_HTTP_PORT`/`APOGEU_HTTPS_PORT` (80/443), `APOGEU_IMAGE_PREFIX`
(`apogeu`), `APOGEU_DEPLOY_HEALTH_TIMEOUT` (300 s), `APOGEU_SMOKE_TIMEOUT` (180 s). Referência: `tests/deploy/README.md`.

## Primeiro deploy (uma vez, com thomas)

Pré-requisitos na VPS: Docker com o plugin compose, .NET SDK do `global.json`, `git`, `python3`, `curl`, e o usuário no
grupo `docker`. Node/pnpm não são necessários (os front-ends são compilados dentro da imagem do proxy).

1. **Acesso ao repositório.** Com o repositório privado, a VPS precisa de uma chave de leitura: crie uma deploy key
   *read-only* (`ssh-keygen -t ed25519 -f ~/.ssh/apogeu_deploy`), cadastre a pública no GitHub (Settings → Deploy keys)
   e aponte-a em `~/.ssh/config` para `github.com`. Teste: `git ls-remote git@github.com:thomasravache/crash-games-provider.git main`.
2. **Desligar o AppHost de desenvolvimento** (libera memória da VPS; as portas 5080/5173/5180 e os túneis deixam de
   responder):
   ```bash
   kill "$(cat ~/.cache/apogeu-demo/apphost.pid)"
   for t in ~/.cache/apogeu-demo/tun-*.pid; do [ -f "$t" ] && kill "$(cat "$t")"; done
   docker ps        # confira que não sobrou container do AppHost de desenvolvimento ocupando memória
   ```
3. **Abrir 80/443 no UFW** (e no firewall do provedor, se houver). SSH continua aberto; nenhuma outra porta:
   ```bash
   sudo ufw allow 80/tcp comment 'apogeu caddy (http->https, ACME)'
   sudo ufw allow 443/tcp comment 'apogeu caddy (https)'
   sudo ufw status verbose
   ```
   Confira que nada mais escuta em 80/443: `sudo ss -ltnp '( sport = :80 or sport = :443 )'`.
4. **Preparar o diretório e os segredos** (cria `~/apogeu-deploy/.env` com 600; numa segunda vez mantém os valores):
   ```bash
   tools/deploy/vps-deploy --prepare
   ```
5. **Deploy** (passo seguinte).
6. **Backup diário** (timer do systemd, unidade de usuário):
   ```bash
   mkdir -p ~/.config/systemd/user
   cp infra/vps/apogeu-backup.service infra/vps/apogeu-backup.timer ~/.config/systemd/user/
   systemctl --user daemon-reload
   systemctl --user enable --now apogeu-backup.timer
   sudo loginctl enable-linger "$USER"     # o timer roda mesmo sem sessão aberta
   systemctl --user list-timers apogeu-backup.timer
   ```
7. **E2E contra os endereços públicos** (SPEC-0066 E2E-01), de qualquer máquina com o workspace instalado:
   ```bash
   APOGEU_E2E_PUBLISHED=1 pnpm -C src/frontend exec playwright test --config ../../tests/deploy/e2e/playwright.config.ts
   ```

## Deploy de uma versão

```bash
tools/deploy/vps-deploy            # main
tools/deploy/vps-deploy <ref>      # branch, tag ou commit
```

O script (pode ser rodado do repositório de trabalho ou de `~/apogeu-deploy/src/tools/deploy/`):

1. cria o `.env` se faltar (nunca o imprime);
2. busca a ref em `~/apogeu-deploy/src` e faz checkout do commit;
3. roda `tools/deploy/build-release` da versão: `aspire publish` do AppHost (`infra/compose/out/docker-compose.yaml`),
   imagens `apogeu-api` e `apogeu-demo-casino` (publish de container do SDK .NET) e `apogeu-proxy`
   (`infra/caddy/Dockerfile`), todas com a tag do commit;
4. confere o compose contra o contrato SPEC-0065@2 (serviços, volumes, rede única `aspire`, variáveis definidas no
   `.env`) e recusa antes de subir qualquer coisa, dizendo o que falta;
5. sobe a versão com o `proxy`, espera a saúde e roda `tools/deploy/smoke` (HTTPS e cabeçalhos dos três endereços,
   redirecionamento HTTP → HTTPS, `/health` da API, entrada no cassino e lançamento da Pipa);
6. sucesso: a versão anterior fica guardada para o rollback; falha na saúde ou no smoke: **volta sozinho** para a versão
   anterior e sai com código ≠ 0. Falha no build ou no contrato: nada muda no ar.

Ao final o script mostra a memória da VPS (`free -m`); confira que sobra memória para os outros projetos. Logs:
`docker compose -p apogeu logs -f proxy api demo-casino`. Estado: `docker compose -p apogeu ps`.

Migrações rodam na subida da API e têm de ser compatíveis com a versão anterior (ADR-0013), para que o rollback não
precise mexer no banco.

## Rollback

```bash
tools/deploy/vps-rollback
```

Sobe a versão anterior com as imagens e o compose dela (sem rebuild), espera a saúde e roda o smoke — em menos de 2
minutos. A versão que saiu vira a "anterior", então um segundo `vps-rollback` volta para frente. As imagens de cada
versão vêm do `release.env` dela: variáveis exportadas no shell (`API_IMAGE`, `DEMO_CASINO_IMAGE`, `PROXY_IMAGE`) são
ignoradas, para que o rollback nunca suba as imagens novas de novo.

## Backup e restauração

Backup manual (o timer faz o mesmo todo dia às 06:15 UTC):

```bash
tools/deploy/backup        # última linha: caminho do dump em ~/apogeu-deploy/backups (as chaves ficam ao lado)
journalctl --user -u apogeu-backup.service --since today
```

Cada backup grava **dois arquivos com o mesmo carimbo UTC**, ambos com permissão 600 (Emenda v3):

- `apogeu-<carimbo>.dump`: `pg_dump` em formato custom do banco `apogeu`, feito dentro do container do Postgres (a senha
  é lida do próprio container);
- `apogeu-keys-<carimbo>.tar.gz`: o conteúdo do volume `apogeu_dataprotection-keys` (chaves do Data Protection da API),
  com dono (1654, o usuário da API) e modo preservados.

**As chaves fazem parte do backup.** As sementes não reveladas ficam cifradas no banco com essas chaves: restaurar só o
banco, sem o arquivo de chaves do mesmo carimbo, deixa essas sementes ilegíveis. Por isso o `restore` recusa um dump sem
o `apogeu-keys-<carimbo>.tar.gz` ao lado, e quem copiar backups para fora da VPS precisa copiar os dois arquivos juntos.
Os arquivos `apogeu-*` (dumps e chaves) com mais de 7 dias são apagados; outros arquivos do diretório ficam. O backup fica
na mesma VPS (dívida registrada no ADR-0029: copie os pares para fora quando importar).

Restauração (substitui os objetos do dump no banco `apogeu`, criando o banco se não existir, e **substitui o conteúdo**
do volume de chaves pelo arquivo do mesmo carimbo):

```bash
docker compose -p apogeu stop api demo-casino          # ninguém escrevendo durante a restauração
tools/deploy/restore ~/apogeu-deploy/backups/apogeu-<carimbo>.dump   # o apogeu-keys-<carimbo>.tar.gz tem de estar ao lado
docker compose -p apogeu start api demo-casino
tools/deploy/smoke
```

O volume de chaves tem de existir (o compose o cria na subida; num host novo, `docker compose ... up --no-start`
primeiro). Outros volumes: `APOGEU_KEYS_VOLUME=<volume>`.

Para ensaiar sem tocar em produção, restaure num Postgres e num volume descartáveis:
`APOGEU_POSTGRES_CONTAINER=<id> APOGEU_KEYS_VOLUME=<volume> tools/deploy/restore <dump>` (o IT-02 da SPEC-0066 faz
exatamente isso).

## Problemas comuns

| Sintoma | Verificar |
|---|---|
| certificado não sai | 80/443 abertos no UFW e no provedor; `docker compose -p apogeu logs proxy`; limite de emissão do Let's Encrypt por domínio (sslip.io é compartilhado) |
| `vps-deploy` recusa o compose | a mensagem nomeia o serviço, volume ou variável que falta (contrato SPEC-0065@2) |
| smoke falha em `/casino/play/pipa` | `docker compose -p apogeu logs demo-casino api`; operador demo e carteira seamless (SPEC-0065) |
| `restore` recusa o dump | falta o `apogeu-keys-<carimbo>.tar.gz` do mesmo carimbo ao lado; sem as chaves as sementes não reveladas ficam ilegíveis |
| outro deploy em andamento | só um `vps-deploy`/`vps-rollback` por vez (trava em `~/apogeu-deploy/.lock`) |
