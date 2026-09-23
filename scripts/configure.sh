#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ -f .env ]; then if ! grep -q '^INTEGRATION_TOKEN=' .env; then printf 'INTEGRATION_TOKEN=%s\n' "$(openssl rand -hex 24)" >> .env; fi; echo '.env preserved; missing integration token generated.'; exit 0; fi
umask 077
printf 'DB_ROOT_PASSWORD=%s
DB_PASSWORD=%s
DEMO_PASSWORD=%s
' "$(openssl rand -hex 20)" "$(openssl rand -hex 20)" "$(openssl rand -hex 12)" > .env
printf 'INTEGRATION_TOKEN=%s\n' "$(openssl rand -hex 24)" >> .env
echo 'Created local .env. DEMO_PASSWORD is used by all three demo accounts.'
