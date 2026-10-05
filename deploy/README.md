# Deploying QRenvo (Prod + Demo on one server)

| Address | Goes to | Database |
|---|---|---|
| `qrenvo.com`, `www.` | sales page (`landing/index.html`) | none |
| `app.qrenvo.com` | owner panel, kitchen, super admin (Prod) | `QrMenuProd` |
| `<name>.qrenvo.com` | that restaurant's menu (Prod) | `QrMenuProd` |
| `demo.qrenvo.com` | everything, sample data, test payments | `QrMenuDemo` |

Same code, two API containers, one SQL Server with two separate databases. Demo has its own JWT secret,
Razorpay **test** keys, its own photos, and can be wiped any time with `reset-demo.sh`.

## 1. Server

Ubuntu 24.04 VPS, **4 GB RAM** minimum (SQL Server needs ~2 GB). Install Docker:

```bash
curl -fsSL https://get.docker.com | sh
```

Firewall: allow SSH, and 80/443 only from Cloudflare (https://www.cloudflare.com/ips/).

## 2. Cloudflare

1. DNS (all **Proxied**, orange cloud): `A @ <server-ip>`, `A * <server-ip>`, `CNAME www @`.
   `app` and `demo` are covered by `*`.
2. SSL/TLS → mode **Full (strict)**.
3. SSL/TLS → Origin Server → **Create certificate** for `qrenvo.com, *.qrenvo.com` (15 years).
   Save as `deploy/certs/origin.pem` and `deploy/certs/origin.key`.
4. Caching → Cache Rules: **bypass cache** for `/api/*`.

## 3. Code and secrets

```bash
mkdir -p /opt/qrenvo && cd /opt/qrenvo
git clone https://github.com/mswati20298/qr-menu-api.git
git clone https://github.com/mswati20298/qr-menu-web.git
cd qr-menu-api/deploy
cp compose.env.example .env        # ROOT_DOMAIN, SQL sa password
cp prod.env.example prod.env       # live JWT secret, super admin, live Razorpay keys
cp demo.env.example demo.env       # different JWT secret, Razorpay TEST keys
chmod 600 .env prod.env demo.env certs/origin.key
```

Put the sales page at `landing/index.html`.

## 4. Start / update

```bash
./deploy.sh
```

Pulls both repos, rebuilds, restarts. With the cron job below this runs by itself within ~2 minutes of a
push to `main` in either repo (`auto-deploy.sh`; log: `/var/log/qrenvo-autodeploy.log`). Database migrations run when each API starts. Logs:
`docker compose logs -f api-prod`.

## 5. Daily jobs (`crontab -e` as root)

```
*/2 * * * * /opt/qrenvo/qr-menu-api/deploy/auto-deploy.sh >> /var/log/qrenvo-autodeploy.log 2>&1
30 2 * * * /opt/qrenvo/qr-menu-api/deploy/backup.sh >> /var/log/qrenvo-backup.log 2>&1
0 4 * * *  /opt/qrenvo/qr-menu-api/deploy/reset-demo.sh >> /var/log/qrenvo-demo.log 2>&1
```

Copy `deploy/backups/` off the server too. Restore: `./restore.sh backups/QrMenuProd-<stamp>.bak`.

## Restaurant addresses

New restaurants get their slug as an address automatically (`saket-rasoi.qrenvo.com`) when it is free and
not reserved (`app`, `demo`, `admin`, …). Owners can change it under **Settings → Menu web address**; after a
change, reprint the QR cards. `app.qrenvo.com/m/<slug>` keeps working too.

## Troubleshooting

- **`<domain>` shows "Parked Domain … Hostinger":** the Cloudflare A record for `<domain>` still points to
  the registrar's parking IP. Edit it to the server IP (`*` alone covers only subdomains).
- **Auto-deploy did nothing:** `tail -50 /var/log/qrenvo-autodeploy.log`. A failed build leaves the old
  version running.
