#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
./scripts/configure.sh
docker compose --profile full up -d --build --wait
for attempt in $(seq 1 60); do
  if curl -fsS "http://127.0.0.1:${APP_PORT:-5080}/api/health" >/dev/null; then
    echo "ClinicFlow ready: http://localhost:${APP_PORT:-5080} — demo password is in local .env"; exit 0
  fi
  sleep 1
done
echo 'API did not become ready. Inspect docker compose logs app.' >&2
exit 1
