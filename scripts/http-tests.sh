#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
set -a
source .env
set +a
node tests/auth.mjs

node tests/http-scheduling.mjs

node tests/http-tasks.mjs

node tests/http-fhir.mjs

node tests/http-response-loss.mjs
