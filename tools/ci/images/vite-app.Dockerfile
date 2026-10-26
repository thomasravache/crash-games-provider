# Container image of a Vite app of the front-end workspace (SPEC-0004, images.yml). The app is built by the
# workflow (`pnpm -C src/frontend build`); this image only serves its static `dist/` (build context) with an
# unprivileged nginx on port 8080. Base image pinned by digest; Dependabot (docker, /tools/ci/images) updates it.
FROM nginxinc/nginx-unprivileged:1.31-alpine@sha256:b5047b9b76845eac124358373c6852377c52e4b05834a47647e955dd91cb8f1e

COPY --from=config nginx.conf /etc/nginx/conf.d/default.conf
COPY . /usr/share/nginx/html

EXPOSE 8080
