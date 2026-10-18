#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
set -a
source .env
set +a
export API_URL="${API_URL:-http://127.0.0.1:${APP_PORT:-5080}}"
node tests/auth.mjs
node tests/http-ois.mjs
node tests/http-controllers.mjs
