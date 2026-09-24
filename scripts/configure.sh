#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
umask 077
if [ -f .env ]; then
  if ! grep -q '^INTEGRATION_TOKEN=' .env; then
    printf 'INTEGRATION_TOKEN=%s\n' "$(openssl rand -hex 24)" >> .env
  fi
  echo '.env preserved; required integration token is present.'
  exit 0
fi
printf 'DB_ROOT_PASSWORD=%s\nDB_PASSWORD=%s\nDEMO_PASSWORD=%s\nINTEGRATION_TOKEN=%s\n' \
  "$(openssl rand -hex 20)" "$(openssl rand -hex 20)" \
  "$(openssl rand -hex 12)" "$(openssl rand -hex 24)" > .env
echo 'Created local .env. DEMO_PASSWORD is used by all three demo accounts.'
