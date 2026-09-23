#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [ "${1:-}" = L2 ]; then shift; exec node labs/duplicate.mjs "$@"; fi
set -a
source .env
set +a
export LAB_DB="Server=127.0.0.1;Port=3308;Database=clinicflow_labs;User=root;Password=$DB_ROOT_PASSWORD"
exec ./scripts/dotnet.sh run --project labs/MySqlLabs -- "$@"
