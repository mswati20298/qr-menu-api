#!/usr/bin/env bash
# Wipes the demo (database QrMenuDemo + its photos) and starts it fresh with the sample restaurant.
# Prod is never touched. EMERGENCY ONLY: the normal reset is Super admin -> Settings -> Reset demo, which keeps
# plans, settings and the keys saved in the panel. This script drops all of that too. Do not put it in cron.
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a

docker compose stop api-demo
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -b -Q \
  "IF DB_ID('QrMenuDemo') IS NOT NULL BEGIN ALTER DATABASE [QrMenuDemo] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [QrMenuDemo]; END"
docker run --rm -v qrenvo_uploads-demo:/data alpine sh -c 'rm -rf /data/*'
# On start the API creates the database again and seeds the sample restaurant (Seed__DemoData=true).
docker compose start api-demo
echo "Demo reset."
