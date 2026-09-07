FROM debian:bookworm-slim

RUN apt-get update \
    && apt-get install --yes --no-install-recommends siege \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["siege"]
