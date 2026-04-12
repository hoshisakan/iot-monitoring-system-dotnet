ARG POSTGRES_ADMINER_VERSION

FROM dpage/pgadmin4:${POSTGRES_ADMINER_VERSION}

# pgAdmin 是基於 Alpine，若需要除錯工具需使用 apk
USER root
RUN apk add --no-cache curl iputils net-tools
USER pgadmin
