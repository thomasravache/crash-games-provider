# SPEC-0066 test fixture: the proxy with the real Caddyfile of the release (infra/caddy/Caddyfile), without the
# front-end builds of the real image. Build context: the root of the checkout.
FROM caddy:2.11.7
COPY infra/caddy/Caddyfile /etc/caddy/Caddyfile
