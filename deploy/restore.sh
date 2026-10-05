#!/usr/bin/env bash
# Restores the Prod database from a backup file:  ./restore.sh backups/QrMenuProd-20261005-0230.bak
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a
file=$(basename "${1:?Usage: ./restore.sh backups/QrMenuProd-<stamp>.bak}")

read -r -p "This REPLACES the live QrMenuProd database with $file. Type RESTORE to continue: " answer
[ "$answer" = "RESTORE" ] || { echo "Cancelled."; exit 1; }

docker compose stop api-prod
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -b -Q \
  "RESTORE DATABASE [QrMenuProd] FROM DISK = N'/var/opt/mssql/backup/$file' WITH REPLACE, CHECKSUM"
docker compose start api-prod
echo "Restored $file."
