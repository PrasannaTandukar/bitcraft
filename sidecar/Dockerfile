FROM python:3.12-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    DEEPEVAL_DISABLE_DOTENV=1 \
    # C3: deepeval 4.2.x posts telemetry to PostHog on every evaluate unless
    # opted out. Bitcraft ships no first-party analytics by default.
    DEEPEVAL_TELEMETRY_OPT_OUT=1

COPY sidecar/requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r /tmp/requirements.txt

COPY sidecar/app /app/app

# Transport selection (mirrors the backend Benchmarking:DeepEval:Transport option):
#   uds  (default) - Unix domain socket, no TCP port exposed at all.
#   http           - TCP listener on BITCRAFT_SIDECAR_HTTP_PORT (default 48317,
#                    deliberately uncommon to avoid host/container port clashes).
# The HTTP listener binds the container network only; compose maps no host port.
CMD ["sh", "-c", "mkdir -p /var/run/bitcraft && rm -f /var/run/bitcraft/sidecar.sock && if [ \"$BITCRAFT_SIDECAR_TRANSPORT\" = \"http\" ]; then exec uvicorn app.main:app --host 0.0.0.0 --port \"${BITCRAFT_SIDECAR_HTTP_PORT:-48317}\"; else exec uvicorn app.main:app --uds /var/run/bitcraft/sidecar.sock; fi"]
