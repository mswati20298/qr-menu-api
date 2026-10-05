#!/usr/bin/env bash
# Nightly backup of the Prod database and uploaded photos. Keeps 14 days.
# Cron (as root):  30 2 * * *  /opt/qrenvo/qr-menu-api/deploy/backup.sh >> /var/log/qrenvo-backup.log 2>&1
# Copy ./backups somewhere off this server too (rclone to Backblaze/Drive, etc.).
set -euo pipefail
cd "$(dirname "$0")"
set -a; . ./.env; set +a

stamp=$(date +%Y%m%d-%H%M)
mkdir -p backups

docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SQL_SA_PASSWORD" -C -b -Q \
  "BACKUP DATABASE [QrMenuProd] TO DISK = N'/var/opt/mssql/backup/QrMenuProd-$stamp.bak' WITH COMPRESSION, INIT, CHECKSUM"

docker run --rm -v qrenvo_uploads-prod:/data:ro -v "$PWD/backups:/backup" alpine \
  tar czf "/backup/uploads-prod-$stamp.tar.gz" -C /data .

find backups -name 'QrMenuProd-*.bak' -mtime +14 -delete
find backups -name 'uploads-prod-*.tar.gz' -mtime +14 -delete
echo "Backup $stamp done."
