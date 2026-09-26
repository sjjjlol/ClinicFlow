#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
set -a
source .env
set +a
export TEST_DB="Server=127.0.0.1;Port=3308;Database=clinicflow_tests;User=root;Password=$DB_ROOT_PASSWORD"
if [[ ! -d agent-runtime/node_modules ]]; then npm --prefix agent-runtime ci; fi
exec ./scripts/dotnet.sh test tests "$@"
