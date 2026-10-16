# syntax=docker/dockerfile:1
# Proxy image of the published platform (SPEC-0066, ADR-0029): Caddy serving the production builds of the demo-site
# (cassino.<domain>) and of the game-pipa (jogo.<domain>), and proxying /casino/* and the api (infra/caddy/Caddyfile).
#
# Build context: the root of the repository (only src/frontend and infra/caddy get in, see Dockerfile.dockerignore):
#   docker build --file infra/caddy/Dockerfile --build-arg APOGEU_DOMAIN=143-95-218-240.sslip.io --tag <image> .
#
# No secret goes into the image: the front-ends only learn the public address of the API, and the game is built without
# its test hook (VITE_APOGEU_TEST_HOOK is never set; the build fails if the bundle installs window.__APOGEU_TEST__ —
# SPEC-0066 E2E-01; the minifier may keep a harmless read of it, never the assignment).

FROM node:24.21.0-bookworm-slim AS frontend
ARG APOGEU_DOMAIN=143-95-218-240.sslip.io
ENV CI=true
WORKDIR /workspace/src/frontend
RUN corepack enable pnpm
COPY src/frontend/ ./
RUN --mount=type=cache,id=apogeu-pnpm-store,target=/root/.local/share/pnpm/store \
    pnpm install --frozen-lockfile
RUN unset VITE_APOGEU_TEST_HOOK \
    && export VITE_APOGEU_API_URL="https://api.${APOGEU_DOMAIN}" \
    && pnpm --filter @apogeu/demo-site --filter @apogeu/game-pipa build \
    && test -f apps/demo-site/dist/index.html \
    && test -f apps/game-pipa/dist/index.html \
    && if grep -rqE "__APOGEU_TEST__ ?=[^=]" apps/game-pipa/dist; then echo "test hook installed by the production bundle" >&2; exit 1; fi \
    && grep -rqF "https://api.${APOGEU_DOMAIN}" apps/game-pipa/dist

FROM caddy:2.11.7
ARG APOGEU_DOMAIN=143-95-218-240.sslip.io
ENV APOGEU_DOMAIN=${APOGEU_DOMAIN}
COPY infra/caddy/Caddyfile /etc/caddy/Caddyfile
COPY --from=frontend /workspace/src/frontend/apps/demo-site/dist/ /srv/cassino/
COPY --from=frontend /workspace/src/frontend/apps/game-pipa/dist/ /srv/jogo/
