ARG POSTGRES_VERSION

FROM postgres:${POSTGRES_VERSION}

ARG POSTGRES_LANG_NAME
ARG POSTGRES_LANG_INPUTFILE
ARG POSTGRES_LANG_CHARMAP

RUN apt-get update && apt-get install -y --no-install-recommends \
    iputils-ping \
    telnet \
    net-tools \
    locales \
    && localedef -i ${POSTGRES_LANG_INPUTFILE} -c -f ${POSTGRES_LANG_CHARMAP} -A /usr/share/locale/locale.alias ${POSTGRES_LANG_NAME} \
    && rm -rf /var/lib/apt/lists/*


ENV LANG=${POSTGRES_LANG_NAME}