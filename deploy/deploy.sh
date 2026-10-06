#!/usr/bin/env bash
# Pulls the latest code of both repos and rebuilds/restarts what changed. Migrations run when each API starts.
set -euo pipefail
cd "$(dirname "$0")"

for f in .env prod.env demo.env certs/origin.pem certs/origin.key; do
  [ -f "$f" ] || { echo "Missing deploy/$f (see README.md)"; exit 1; }
done

git -C .. pull --ff-only
git -C ../../qr-menu-web pull --ff-only

docker compose build --pull
docker compose up -d --remove-orphans
# The Caddyfile is mounted, so a change to it alone does not recreate the container: reload it.
docker compose exec -T web caddy reload --config /etc/caddy/Caddyfile
docker image prune -f >/dev/null
docker compose ps
