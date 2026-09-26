#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
# Reuse existing configuration; never replace the user's credentials.
if [[ ! -f .env ]]; then ./scripts/configure.sh; fi
set -a
source .env
set +a
docker compose --profile imaging up -d --wait orthanc
python3 -m venv artifacts/dicom-venv
artifacts/dicom-venv/bin/pip -q install -r imaging/requirements.txt
export Imaging__BaseUrl="http://127.0.0.1:${ORTHANC_PORT:-8042}/"
artifacts/dicom-venv/bin/python imaging/seed.py
