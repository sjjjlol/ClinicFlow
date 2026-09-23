#!/usr/bin/env bash
set -euo pipefail
if command -v dotnet >/dev/null; then exec dotnet "$@"; fi
exec "$HOME/.local/share/clinicflow-dotnet/dotnet" "$@"
