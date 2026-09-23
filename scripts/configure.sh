#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ -f .env ]; then echo '.env already exists; preserved.'; exit 0; fi
umask 077
printf 'DB_ROOT_PASSWORD=%s
DB_PASSWORD=%s
DEMO_PASSWORD=%s
' "$(openssl rand -hex 20)" "$(openssl rand -hex 20)" "$(openssl rand -hex 12)" > .env
echo 'Created local .env. DEMO_PASSWORD is used by all three demo accounts.'
