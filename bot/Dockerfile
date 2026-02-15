FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    VIRTUAL_ENV=/app/.venv \
    PATH="/app/.venv/bin:$PATH"

WORKDIR /app

RUN apt-get update && apt-get install -y \
    make \
    gettext \
    && rm -rf /var/lib/apt/lists/*

COPY pyproject.toml /app/pyproject.toml
COPY uv.lock /app/uv.lock
RUN pip install --no-cache-dir uv \
    && uv venv /app/.venv \
    && uv sync --frozen --no-dev

COPY . /app/
RUN make compile-locales

CMD ["/app/.venv/bin/python", "src/main.py"]
