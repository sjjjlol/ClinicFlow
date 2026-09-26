#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
set -a
source .env
set +a
export ConnectionStrings__Clinic="Server=127.0.0.1;Port=3308;Database=clinicflow;User=clinicflow;Password=$DB_PASSWORD"
export ASPNETCORE_URLS=http://127.0.0.1:5080
export ASPNETCORE_ENVIRONMENT=Development
if [[ ! -d agent-runtime/node_modules ]]; then npm --prefix agent-runtime ci; fi
exec ./scripts/dotnet.sh run --project backend
