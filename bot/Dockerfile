FROM python:3.12-slim

ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    VIRTUAL_ENV=/app/.venv \
    PATH="/app/.venv/bin:$PATH"

WORKDIR /app

COPY pyproject.toml /app/pyproject.toml
COPY uv.lock /app/uv.lock
RUN pip install --no-cache-dir uv \
    && uv venv /app/.venv \
    && uv sync --frozen --no-dev

COPY . /app/

CMD ["/app/.venv/bin/python", "src/main.py"]
