FROM postgres:18-trixie

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        build-essential \
        postgresql-server-dev-18 \
        wget && \
    update-ca-certificates && \
    wget https://github.com/pgvector/pgvector/archive/refs/tags/v0.8.2.tar.gz -O pgvector.tar.gz && \
    tar -xzf pgvector.tar.gz && \
    cd pgvector-0.8.2 && \
    make && make install && \
    rm -rf /pgvector* && \
    apt-get remove -y build-essential wget postgresql-server-dev-18 && \
    apt-get autoremove -y && \
    rm -rf /var/lib/apt/lists/*