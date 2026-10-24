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
   `/health/ready`, the SPA, and a note round trip through Caddy. Images are pushed only if that passes.
3. **deploy** (main only): copies `docker-compose.prod.yml` to the server, pulls the new images, and runs
   `docker compose up -d --wait`. The job fails, and the logs are printed, if the API does not become ready.
   That includes the database migrations, which the API applies on startup.

On the server, the stack is:

- `web`: Caddy. Serves the frontend, proxies `/api/*` and `/health*` to the API, and handles HTTPS.
- `api`: ASP.NET Core on port 8080, internal only.
- `postgres`: no published port.

Data is kept in named Docker volumes.

## One-time server setup (Ubuntu 22.04/24.04)

Run as root or with `sudo`:

```bash
# 1. Docker Engine + Compose plugin
curl -fsSL https://get.docker.com | sh

# 2. Deploy user (membership in the docker group is root-equivalent on this host; use it only for deploys)
adduser --disabled-password --gecos "" deploy
usermod -aG docker deploy

# 3. Firewall
ufw allow OpenSSH
ufw allow 80,443/tcp
ufw enable
```

On your own machine, create the key pair GitHub Actions will use:

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/ri-hack-deploy -N ""
ssh-copy-id -i ~/.ssh/ri-hack-deploy.pub deploy@YOUR_SERVER_IP   # or append the .pub to /home/deploy/.ssh/authorized_keys
ssh-keyscan -H YOUR_SERVER_IP > known_hosts.txt                    # for the DEPLOY_KNOWN_HOSTS secret
```

As the `deploy` user on the server:

```bash
mkdir ~/ri-hack-2026 && cd ~/ri-hack-2026
nano .env      # paste deploy/.env.example, then fill it in:
               #   POSTGRES_PASSWORD=$(openssl rand -hex 24)
               #   SITE_ADDRESS=your.domain   (or :80 for plain HTTP by IP)
chmod 600 .env
```

If you use a domain, point its DNS A record at the server before the first deploy, so Caddy can obtain the
certificate.

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
- **Deploy times out waiting for `api`.** Run `docker compose logs api`. This is usually a failed migration
  or wrong database credentials. Credentials are fixed when the `postgres-data` volume is first created.
- **Already running nginx or Apache on 80/443?** Set `SITE_ADDRESS=:80`, `HTTP_PORT=127.0.0.1:8081` and
  `HTTPS_PORT=127.0.0.1:8443` in `.env`, then proxy your domain to `127.0.0.1:8081` from the existing server.
