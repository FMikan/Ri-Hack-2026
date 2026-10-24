# Deployment

Live at **https://hack.fmikan.me**, served from the VPS by the `hack` user.

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
   `/health/ready`, the SPA, and a register round trip. Images are pushed only if that passes.
3. **deploy** (main only):
   - copies `docker-compose.prod.yml` to the server
   - records the commit SHA as `IMAGE_TAG` in the server `.env`
   - pulls the new images and runs `docker compose up -d --wait`

   The job fails, and the logs are printed, if the API does not become ready. That includes the database
   migrations, which the API applies on startup.

Runs on the same branch queue instead of overlapping, so `main` always deploys in push order.

## How it fits on a shared server

The VPS already runs other sites behind a host-level Caddy on ports 80/443. This stack never touches those
ports. It listens only on `127.0.0.1:8081`, and the host Caddy proxies `hack.fmikan.me` to it and handles HTTPS.

```text
browser --HTTPS--> host Caddy (80/443) --> 127.0.0.1:8081 (web: Caddy in Docker, frontend files)
                                                 |-- /api/*, /health* --> api:8080 --> postgres
```

- `web` and `api` are on a private Docker network; `postgres` has no published port.
- Containers, volumes and the network are all prefixed `ri-hack-2026-prod`.
- The deploy only ever prunes images labelled with this repository.

The frontend expects to be served from `/` of its own (sub)domain, not from a sub-path.

## One-time server setup

All commands run as root on the server unless stated otherwise.

**1. Check what is already there.** These commands are read-only.

```bash
docker version && docker compose version     # Compose must be v2.17 or newer
ss -ltnp | grep -E ':(80|443|8081) '         # who owns 80/443; is 8081 free?
```

- **Docker missing entirely:** install it with `curl -fsSL https://get.docker.com | sh`.
- **Docker present but Compose missing:** install only the plugin, with `apt install docker-compose-v2`.
  Never run get.docker.com on a host that already has Docker; it replaces the engine and restarts the other
  apps' containers.
- **8081 taken:** use another free port as `WEB_PORT` in step 3.

**2. SSH key for GitHub Actions.** On your own machine:

```bash
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/ri-hack-deploy -N ""
ssh-keyscan -H hack.fmikan.me > ~/.ssh/ri-hack-deploy.known_hosts    # add -p PORT if SSH isn't on 22
```

Compare the ED25519 fingerprint (`ssh-keygen -lf ~/.ssh/ri-hack-deploy.known_hosts`) with the one on the
server (`ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub`). `DEPLOY_HOST` must be exactly the name you
scanned (`hack.fmikan.me`).

**3. Dedicated user, key and app directory.** Use a user that doesn't exist yet. Don't reuse an account
another app deploys with. Paste the public key from `~/.ssh/ri-hack-deploy.pub`.

```bash
if id hack >/dev/null 2>&1; then echo "user hack already exists, stop"; else
  useradd -m -s /bin/bash -G docker hack       # docker group = root-equivalent; key-only login
  install -d -m 700 -o hack -g hack /home/hack/.ssh /home/hack/ri-hack-2026
  echo 'PASTE_PUBLIC_KEY' >> /home/hack/.ssh/authorized_keys
  chown hack:hack /home/hack/.ssh/authorized_keys && chmod 600 /home/hack/.ssh/authorized_keys
  cat > /home/hack/ri-hack-2026/.env <<EOF
POSTGRES_DB=app
POSTGRES_USER=app
POSTGRES_PASSWORD=$(openssl rand -hex 24)
JWT_SIGNING_KEY=$(openssl rand -hex 32)
WEB_PORT=8081
EOF
  chown hack:hack /home/hack/ri-hack-2026/.env && chmod 600 /home/hack/ri-hack-2026/.env
fi
```

- **Locked password:** `useradd` creates the account with a locked password. Key login still works with
  Ubuntu's default `UsePAM yes`. If `sshd -T | grep usepam` says `no`, also run `usermod -p '*' hack`.
- **Verify** from your machine, with no password prompt:
  `ssh -i ~/.ssh/ri-hack-deploy -o IdentitiesOnly=yes hack@hack.fmikan.me docker ps`

**4. DNS.** Create an A record `hack.fmikan.me` pointing at the server IP. If you use Cloudflare, set it to
"DNS only".

**5. Reverse proxy in the host web server.**

Caddy (this server): add this to the host Caddyfile (usually `/etc/caddy/Caddyfile`). Then validate and
reload; existing sites keep running. HTTPS is automatic.

```caddy
hack.fmikan.me {
	reverse_proxy 127.0.0.1:8081
}
```

```bash
caddy validate --config /etc/caddy/Caddyfile --adapter caddyfile && systemctl reload caddy
```

nginx (other servers):

- Create `/etc/nginx/sites-available/ri-hack` with the block below.
- Symlink it into `sites-enabled`. nginx.org packages only read `conf.d/`, so put the file there instead.
- Run `nginx -t && systemctl reload nginx && certbot --nginx -d hack.fmikan.me`. This needs certbot with
  `python3-certbot-nginx`.

```nginx
server {
    listen 80;
    server_name hack.fmikan.me;
    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

Until the first deploy, the proxy returns 502 Bad Gateway. That is expected.

## GitHub configuration

Go to **Settings > Secrets and variables > Actions > New repository secret**:

| Secret | Value |
|--------|-------|
| `DEPLOY_HOST` | `hack.fmikan.me` (exactly the name used with `ssh-keyscan`) |
| `DEPLOY_USER` | `hack` |
| `DEPLOY_SSH_KEY` | Contents of `~/.ssh/ri-hack-deploy` (the private key, including the BEGIN/END lines) |
| `DEPLOY_KNOWN_HOSTS` | Contents of `~/.ssh/ri-hack-deploy.known_hosts` |
| `DEPLOY_PORT` | Optional. Only if SSH is not on port 22 |

- **No registry token needed:** the deploy job logs the server into GHCR with the job's own short-lived
  `GITHUB_TOKEN`, and logs out again afterwards.
- **Who can reach the server:** anyone with write access to this repository can push a workflow that uses
  these secrets, and so can reach the server as `hack`. Give write access only to trusted teammates.
- **Optional, Settings > Branches:** protect `main` by requiring a pull request and the `test` check. On a
  private repository this needs a paid GitHub plan.

## Operating the server

Run these as `hack` in `~/ri-hack-2026`:

```bash
docker compose ps                         # status and health
docker compose logs -f api                # API logs (also: web, postgres)
curl -s 127.0.0.1:8081/health/ready       # readiness, bypassing the host web server
docker compose up -d --wait               # restart with the deployed version (IMAGE_TAG in .env)
docker compose exec postgres sh -c 'pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB"' > backup-$(date +%F).sql
```

- **Password reset e-mails:** set `SMTP_HOST`, `SMTP_USERNAME`, `SMTP_PASSWORD` (and `SMTP_FROM` if it differs)
  in `.env` (see `.env.example`), then `docker compose up -d --wait`. Until then the reset link is only in
  `docker compose logs api | grep 'reset link'`.
- **Rate limits** (login, register, forgot password) count per player IP. They rely on the host proxy setting
  `X-Forwarded-For`. Caddy and the nginx block above both do. Behind Cloudflare's proxy, every player would
  share Cloudflare's IPs, so keep the record "DNS only".

- **Redeploy:** open **Actions > CI/CD > Run workflow** and pick `main` as the branch. Other branches only
  run tests.
- **Roll back:** open the older run of that commit on `main` in **Actions**, then click **Re-run jobs**. It
  deploys that commit's images again.

## Troubleshooting

- **`Permission denied (publickey)`:** `DEPLOY_SSH_KEY` doesn't match `/home/hack/.ssh/authorized_keys`, or
  `DEPLOY_USER` is wrong.
- **`Host key verification failed`:** `DEPLOY_KNOWN_HOSTS` is missing or stale, or `DEPLOY_HOST` differs from
  the name that was scanned.
- **`scp: dest open "ri-hack-2026/docker-compose.yml": No such file or directory`:** `/home/hack/ri-hack-2026`
  doesn't exist.
- **`set POSTGRES_PASSWORD in .env`:** the server `.env` is missing or incomplete.
- **`port is already allocated` on 8081:** set another `WEB_PORT` in `.env`, update the host proxy to match,
  then run `docker compose up -d --wait`.
- **Deploy times out waiting for `api`:** run `docker compose logs api`. This is usually a failed migration
  or wrong database credentials. Credentials are fixed when the `postgres-data` volume is first created.
- **502 from the domain, but `curl 127.0.0.1:8081/health` works on the server:** the host proxy config is
  wrong. Check the port.
