# Deployment

## Branches

| Branch | What happens on push |
|--------|----------------------|
| `development` | Day-to-day work. CI runs backend tests, frontend lint, type-check and build. Nothing is deployed. |
| `main` | Production. CI runs the same tests, then builds Docker images, smoke-tests them, pushes them to GHCR and deploys to the VPS. |

Work on `development` (or feature branches merged into it). To release, open a pull request
`development -> main` and merge it once CI is green. Every push to `main` is a deploy, so don't commit to `main`
directly.

## Pipeline (`.github/workflows/ci-cd.yml`)

1. **test**: `dotnet test` (build + xUnit) and `npm ci && npm run lint && npm run build`.
2. **publish** (main only): builds `ghcr.io/fmikan/ri-hack-2026/api` and `.../web`, tagged with the commit SHA
   and `latest`. It then starts the real production stack (`docker-compose.prod.yml`) in CI and checks
   `/health/ready`, the SPA, and a note round trip. Images are pushed only if that passes.
3. **deploy** (main only): copies `docker-compose.prod.yml` to the server, pulls the new images, and runs
   `docker compose up -d --wait`. The job fails, and the logs are printed, if the API does not become ready.
   That includes the database migrations, which the API applies on startup.

## How it fits on a shared server

The VPS already runs other sites, so this stack does not touch ports 80/443. It listens only on
`127.0.0.1:8081`. The server's existing web server proxies a (sub)domain to it and handles HTTPS.

```text
browser --HTTPS--> existing nginx/Apache/Caddy --> 127.0.0.1:8081 (web: Caddy, frontend files)
                                                        |-- /api/*, /health* --> api:8080 --> postgres
```

- `web` and `api` are on a private Docker network; `postgres` has no published port.
- Containers, volumes and the network are all prefixed `ri-hack-2026-prod`, so they can't collide with the
  other apps.
- The deploy only ever prunes images labelled with this repository.

Use a subdomain (e.g. `hack.example.com`). The frontend expects to be served from `/`, not from a sub-path.

## One-time server setup

**1. Check what is already there.** Run with sudo:

```bash
docker compose version            # Docker + Compose plugin present?
sudo ss -ltnp | grep -E ':(80|443|8081) '   # which web server owns 80/443, and is 8081 free?
```

- **No Docker?** Install it with `curl -fsSL https://get.docker.com | sh`.
- **8081 taken?** Pick another free port and set it as `WEB_PORT` in step 4.
- **Is the existing web server itself a Docker container** (the process shown is `docker-proxy`)? Then
  `127.0.0.1` inside it is not the host, and the proxy config below needs adapting.

**2. Deploy user.** Run with sudo. Membership in the `docker` group is root-equivalent on this host.

```bash
sudo adduser --disabled-password --gecos "" deploy
sudo usermod -aG docker deploy
```

No firewall changes are needed. Nothing new is exposed publicly.

**3. SSH key for GitHub Actions.** On your own machine:

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/ri-hack-deploy -N ""
ssh-copy-id -i ~/.ssh/ri-hack-deploy.pub deploy@YOUR_SERVER     # or append the .pub to /home/deploy/.ssh/authorized_keys
ssh-keyscan -H YOUR_SERVER > known_hosts.txt                     # for the DEPLOY_KNOWN_HOSTS secret
```

**4. App directory and secrets.** As the `deploy` user on the server:

```bash
mkdir ~/ri-hack-2026 && cd ~/ri-hack-2026
nano .env      # paste deploy/.env.example and set POSTGRES_PASSWORD=$(openssl rand -hex 24); WEB_PORT if not 8081
chmod 600 .env
```

**5. DNS.** Add an A record for the subdomain, pointing at the server's IP.

**6. Reverse proxy in the existing web server.** Use whichever server step 1 showed.

nginx: create `/etc/nginx/sites-available/ri-hack` and run
`sudo ln -s /etc/nginx/sites-available/ri-hack /etc/nginx/sites-enabled/`:

```nginx
server {
    listen 80;
    server_name hack.example.com;

    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

```bash
sudo nginx -t && sudo systemctl reload nginx
sudo certbot --nginx -d hack.example.com      # HTTPS certificate + redirect
```

Apache: `sudo a2enmod proxy proxy_http`, then create `/etc/apache2/sites-available/ri-hack.conf`:

```apache
<VirtualHost *:80>
    ServerName hack.example.com
    ProxyPreserveHost On
    ProxyPass / http://127.0.0.1:8081/
    ProxyPassReverse / http://127.0.0.1:8081/
</VirtualHost>
```

```bash
sudo a2ensite ri-hack && sudo apache2ctl configtest && sudo systemctl reload apache2
sudo certbot --apache -d hack.example.com
```

Caddy on the host: add `hack.example.com { reverse_proxy 127.0.0.1:8081 }` to the Caddyfile and reload. HTTPS
is automatic.

Until the first deploy, the proxy returns 502 Bad Gateway. That is expected.

## GitHub configuration

Go to **Settings > Secrets and variables > Actions > New repository secret**:

| Secret | Value |
|--------|-------|
| `DEPLOY_HOST` | Server IP or hostname |
| `DEPLOY_USER` | `deploy` |
| `DEPLOY_SSH_KEY` | Contents of `~/.ssh/ri-hack-deploy` (the private key) |
| `DEPLOY_KNOWN_HOSTS` | Contents of `known_hosts.txt` |
| `DEPLOY_PORT` | Optional. Only if SSH is not on port 22 |

No registry token is needed. The deploy job logs the server into GHCR with the job's own short-lived
`GITHUB_TOKEN` and logs out again afterwards.

Optional: **Settings > Branches**. Protect `main` (require a pull request and the `test` check). On a private
repository this needs a paid GitHub plan.

## Operating the server

Run these as `deploy` in `~/ri-hack-2026`:

```bash
docker compose ps                         # status and health
docker compose logs -f api                # API logs (also: web, postgres)
curl -s 127.0.0.1:8081/health/ready       # readiness, bypassing the outer web server
IMAGE_TAG=<older-commit-sha> docker compose up -d --wait   # roll back to an earlier build
docker compose exec postgres sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > backup-$(date +%F).sql
```

Re-running the last deploy: open **Actions > CI/CD > Run workflow** on `main`.

## Troubleshooting

- **Deploy job: `Permission denied (publickey)`.** `DEPLOY_SSH_KEY` doesn't match the key in
  `/home/deploy/.ssh/authorized_keys`, or `DEPLOY_USER` is wrong.
- **Deploy job: `Host key verification failed`.** `DEPLOY_KNOWN_HOSTS` is missing or stale. Re-run
  `ssh-keyscan`.
- **Deploy job: `scp: ri-hack-2026/docker-compose.yml: No such file or directory`.** The `~/ri-hack-2026`
  directory doesn't exist for the deploy user.
- **`set POSTGRES_PASSWORD in .env`.** The server `.env` is missing or incomplete.
- **`port is already allocated` on 8081.** Set another `WEB_PORT` in `.env`, and update the proxy config to
  match.
- **Deploy times out waiting for `api`.** Run `docker compose logs api`. This is usually a failed migration
  or wrong database credentials. Credentials are fixed when the `postgres-data` volume is first created.
- **502 from the domain but `curl 127.0.0.1:8081/health` works.** The outer proxy config is wrong; check
  `proxy_pass` and the port. If the outer proxy runs in Docker, see step 1.
