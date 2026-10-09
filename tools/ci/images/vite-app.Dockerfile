# Container image of a Vite app of the front-end workspace (SPEC-0004, images.yml). The app is built by the
# workflow (`pnpm -C src/frontend build`); this image only serves its static `dist/` (build context) with an
# unprivileged nginx on port 8080. Base image pinned by digest; Dependabot (docker, /tools/ci/images) updates it.
FROM nginxinc/nginx-unprivileged:1.29-alpine@sha256:0c79d56aee561a1d81c63f00eee5fb5fe29279560cdc55e91425133104c7fbe6

COPY --from=config nginx.conf /etc/nginx/conf.d/default.conf
COPY . /usr/share/nginx/html

EXPOSE 8080
