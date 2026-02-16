#!/bin/sh
set -eu

if [ -z "${SERVER_HOST:-}" ]; then
  echo "SERVER_HOST is not set; skipping self-signed cert bootstrap." >&2
  exit 0
fi

LIVE_DIR="/etc/letsencrypt/live/${SERVER_HOST}"
CERT_FILE="${LIVE_DIR}/fullchain.pem"
KEY_FILE="${LIVE_DIR}/privkey.pem"

if [ -s "$CERT_FILE" ] && [ -s "$KEY_FILE" ]; then
  exit 0
fi

if ! command -v openssl >/dev/null 2>&1; then
  apk add --no-cache openssl >/dev/null
fi

mkdir -p "$LIVE_DIR"

openssl req -x509 -nodes -newkey rsa:2048 \
  -days 1 \
  -keyout "$KEY_FILE" \
  -out "$CERT_FILE" \
  -subj "/CN=${SERVER_HOST}" \
  -addext "subjectAltName=DNS:${SERVER_HOST},DNS:adminer.${SERVER_HOST},DNS:grafana.${SERVER_HOST}" \
  >/dev/null 2>&1

chmod 600 "$KEY_FILE"
