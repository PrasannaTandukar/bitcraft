FROM python:3.12-slim

WORKDIR /app

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    DEEPEVAL_DISABLE_DOTENV=1

COPY sidecar/requirements.txt /tmp/requirements.txt
RUN pip install --no-cache-dir --upgrade pip \
    && pip install --no-cache-dir -r /tmp/requirements.txt

COPY sidecar/app /app/app

CMD ["sh", "-c", "mkdir -p /var/run/bitcraft && rm -f /var/run/bitcraft/sidecar.sock && exec uvicorn app.main:app --uds /var/run/bitcraft/sidecar.sock"]
