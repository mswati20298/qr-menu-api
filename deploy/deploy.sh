#!/usr/bin/env bash
# Pulls the latest code of both repos and rebuilds/restarts what changed. Migrations run when each API starts.
set -euo pipefail
cd "$(dirname "$0")"

for f in .env prod.env demo.env certs/origin.pem certs/origin.key; do
  [ -f "$f" ] || { echo "Missing deploy/$f (see README.md)"; exit 1; }
done

caddy_before=$(sha256sum Caddyfile | cut -d' ' -f1)
git -C .. pull --ff-only
git -C ../../qr-menu-web pull --ff-only
caddy_after=$(sha256sum Caddyfile | cut -d' ' -f1)

docker compose build --pull
docker compose up -d --remove-orphans
# The Caddyfile is mounted as a single file. git replaces the file on pull, and a running container keeps
# seeing the old one (a reload would read the old file too), so a changed Caddyfile needs a new container.
if [ "$caddy_before" != "$caddy_after" ]; then
  # Check the new file in a throwaway container first: if it is broken, stop here and the old one keeps running.
  docker compose run --rm --no-deps -T web caddy validate --config /etc/caddy/Caddyfile
  docker compose up -d --force-recreate --no-deps web
else
  docker compose exec -T web caddy reload --config /etc/caddy/Caddyfile
fi
docker image prune -f >/dev/null
docker compose ps
