# Container image of a Vite app of the front-end workspace (SPEC-0004, images.yml). The app is built by the
# workflow (`pnpm -C src/frontend build`); this image only serves its static `dist/` (build context) with an
# unprivileged nginx on port 8080. Base image pinned by digest; Dependabot (docker, /tools/ci/images) updates it.
FROM nginxinc/nginx-unprivileged:1.31-alpine@sha256:b9241c6e7b8e9a862f129d8d4199ab64b10390949a78bdd5603379b32c844083

COPY --from=config nginx.conf /etc/nginx/conf.d/default.conf
COPY . /usr/share/nginx/html

EXPOSE 8080
