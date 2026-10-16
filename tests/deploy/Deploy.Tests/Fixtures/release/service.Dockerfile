# SPEC-0066 test fixture: a static HTTP server on 8080 in the role of the api and of the demo-casino.
FROM busybox:1.37.0-musl
COPY www /www
EXPOSE 8080
CMD ["httpd", "-f", "-p", "8080", "-h", "/www"]
