#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
set -a
source .env
set +a
export UPGRADE_DB="Server=127.0.0.1;Port=${DB_PORT:-3308};Database=clinicflow_upgrade_tests;User=root;Password=$DB_ROOT_PASSWORD"
mkdir -p artifacts
./scripts/dotnet.sh run --project tools/UpgradeDrill -- prepare
docker compose exec -T db sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysqldump -uroot --single-transaction --no-tablespaces --set-gtid-purged=OFF clinicflow_upgrade_tests' > artifacts/upgrade-before.sql
./scripts/dotnet.sh run --project tools/UpgradeDrill -- upgrade
docker compose exec -T db sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql -uroot -e "DROP DATABASE clinicflow_upgrade_tests; CREATE DATABASE clinicflow_upgrade_tests"'
docker compose exec -T db sh -c 'MYSQL_PWD="$MYSQL_ROOT_PASSWORD" mysql -uroot clinicflow_upgrade_tests' < artifacts/upgrade-before.sql
./scripts/dotnet.sh run --project tools/UpgradeDrill -- verify-old
