#!/usr/bin/env bash
# Deploys automatically when either repo's main branch has new commits. Run by cron every 2 minutes:
#   */2 * * * * /opt/qrenvo/qr-menu-api/deploy/auto-deploy.sh >> /var/log/qrenvo-autodeploy.log 2>&1
# Does nothing (and prints nothing) when both repos are up to date.
set -euo pipefail
cd "$(dirname "$0")"

# Only one run at a time: a build can take longer than 2 minutes.
exec 9>/tmp/qrenvo-auto-deploy.lock
flock -n 9 || exit 0

changed=""
for repo in .. ../../qr-menu-web; do
  git -C "$repo" fetch --quiet origin main
  if [ "$(git -C "$repo" rev-parse HEAD)" != "$(git -C "$repo" rev-parse origin/main)" ]; then
    changed="$changed $(basename "$(git -C "$repo" rev-parse --show-toplevel)")@$(git -C "$repo" rev-parse --short origin/main)"
  fi
done

[ -z "$changed" ] && exit 0

echo "=== $(date '+%F %T') new commits:$changed — deploying"
if ./deploy.sh; then
  echo "=== $(date '+%F %T') deploy finished"
else
  echo "=== $(date '+%F %T') DEPLOY FAILED — the previous version keeps running. Check the output above."
  exit 1
fi
