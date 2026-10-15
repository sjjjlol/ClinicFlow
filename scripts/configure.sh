#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
umask 077
if [ -f .env ]; then
  echo '.env preserved.'
  exit 0
fi
printf 'DB_ROOT_PASSWORD=%s\nDB_PASSWORD=%s\nDEMO_PASSWORD=%s\n' \
  "$(openssl rand -hex 20)" "$(openssl rand -hex 20)" "$(openssl rand -hex 12)" > .env
echo 'Created local .env. DEMO_PASSWORD is used by all three OIS accounts.'
