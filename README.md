# Ri-Hack-2026

Starter for a hackathon app: React + TypeScript (Vite) frontend, ASP.NET Core (.NET 10) API, PostgreSQL.
PostgreSQL runs in Docker; the frontend and backend run on your machine so they are easy to debug.

The starter page shows API and database health and a tiny "notes" example that writes to and reads from
PostgreSQL. The notes example exists only to prove the wiring; delete it once real features exist.

## Stack

| Layer    | Tech |
|----------|------|
| Frontend | React 19, TypeScript, Vite, Tailwind CSS v4, shadcn/ui (Radix), lucide-react |
| Backend  | ASP.NET Core minimal APIs on .NET 10, EF Core 10, Npgsql, built-in OpenAPI + Scalar |
| Database | PostgreSQL 18 (Docker Compose) |

## Layout

```text
frontend/                     Vite app
  src/lib/api-client.ts       every API call goes through here
  src/types/api.ts            TypeScript types for API contracts
  src/hooks/use-async.ts      tiny loading/error/data hook
  src/features/<feature>/     feature components (status, notes)
  src/components/ui/          shadcn/ui components (generated, restyle freely)
  src/components/shared/      small shared components
backend/
  App.slnx                    solution (.NET 10 default .slnx format)
  src/App.Api/                HTTP endpoints (Endpoints/*.cs), DI, CORS, OpenAPI, Program.cs
  src/App.Application/        use cases/services, DTOs + validation, IAppDbContext
  src/App.Domain/             entities and domain rules, no dependencies
  src/App.Infrastructure/     AppDbContext, Npgsql config, migrations, health checks
  tests/App.Api.Tests/        xUnit + WebApplicationFactory
docker-compose.yml            PostgreSQL only
.env.example                  copy to .env (DB credentials + backend connection string)
```

Dependencies point inward: `Api -> Application, Infrastructure`, `Infrastructure -> Application, Domain`,
`Application -> Domain`. Domain references nothing.

## Prerequisites

- .NET SDK 10 (`dotnet --list-sdks` shows `10.0.x`)
- Node.js 22.12+ (24 LTS recommended) and npm
- Docker with the Compose plugin

## First-time setup

```bash
# 1. Environment. Edit .env if you like; the defaults work locally.
cp .env.example .env

# 2. Database
docker compose up -d postgres
docker compose ps            # wait for "healthy"

# 3. Backend: restore packages and the local dotnet-ef tool, then create the schema
cd backend
dotnet restore
dotnet tool restore
dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api

# 4. Frontend
cd ../frontend
npm install
```

## Run

```bash
docker compose up -d postgres                      # from the repository root

cd backend && dotnet run --project src/App.Api     # terminal 1, http://localhost:5080
cd frontend && npm run dev                         # terminal 2, http://localhost:5173
```

| What | URL |
|------|-----|
| Frontend | http://localhost:5173 |
| API | http://localhost:5080 |
| Liveness | http://localhost:5080/health |
| Readiness (checks PostgreSQL) | http://localhost:5080/health/ready |
| API reference (Scalar, Development only) | http://localhost:5080/scalar |
| OpenAPI document (Development only) | http://localhost:5080/openapi/v1.json |

`backend/src/App.Api/App.Api.http` has ready-made requests for VS Code / Rider / Visual Studio.

## Environment variables

**Root `.env`** (git-ignored, copied from `.env.example`). Docker Compose reads it, and the backend loads it
on startup by walking up from its working directory (stopping at the repository root). Variables already set in your real environment win.

| Variable | Used by | Purpose |
|----------|---------|---------|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Compose | Database created on the container's first start |
| `POSTGRES_PORT` | Compose | Host port for PostgreSQL (default 5432, bound to 127.0.0.1 only) |
| `ConnectionStrings__Default` | Backend | Npgsql connection string. Keep it in sync with the values above |

The backend runs on the host, so its connection string uses **`Host=localhost`** and the published port,
**not** the Compose service name `postgres`. The service name only resolves for containers on the
Compose network.

**Backend configuration** (`appsettings*.json`) holds no credentials. `Cors:AllowedOrigins` in
`appsettings.Development.json` lists the frontend origins allowed to call the API (default
`http://localhost:5173`). In other environments, set it with `Cors__AllowedOrigins__0=https://...`.

**Frontend** (`frontend/.env.example`). Copy it to `frontend/.env.local` only to override:

| Variable | Default | Purpose |
|----------|---------|---------|
| `VITE_API_BASE_URL` | `http://localhost:5080` | Full API origin. The browser calls it directly (no Vite proxy) |

Everything prefixed with `VITE_` is bundled into the browser build and is public. Never put secrets there.

## Health checks: liveness vs readiness

- **`GET /health` (liveness)** answers "is the process alive?". It runs no checks and never touches the
  database, so a database outage does not make an orchestrator restart a perfectly healthy API.
- **`GET /health/ready` (readiness)** answers "can it serve real traffic?". It verifies that PostgreSQL
  accepts connections and returns `503` when it does not.

Both return `{"status":"Healthy","checks":{...}}` with status names only, never exception details.

## Errors

Errors are RFC 9457 Problem Details (`application/problem+json`): validation failures return `400` with an
`errors` map, unknown routes return `404`, and unhandled exceptions return a generic `500`. The frontend
`ApiError` surfaces the first validation message or the problem title.

## Migrations

`dotnet-ef` is a local tool pinned in `backend/dotnet-tools.json`; `dotnet tool restore` installs it.
Run these from `backend/`:

```bash
# Create a migration after changing entities or AppDbContext
dotnet ef migrations add <Name> --project src/App.Infrastructure --startup-project src/App.Api

# Apply pending migrations
dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api
```

Migrations are not applied automatically on startup. On a brand-new database the first `database update` logs a
"Failed executing DbCommand ... __EFMigrationsHistory" line before creating that table; that is normal EF output.

## Checks

```bash
(cd backend && dotnet build && dotnet test)       # tests need no running database
(cd frontend && npm run build && npm run lint)    # build includes tsc type-checking
docker compose config -q                          # validate the Compose file (needs .env)
```

## Adding a feature (the pattern)

1. Entity in `App.Domain`, `DbSet` in `IAppDbContext` + `AppDbContext`, then add a migration.
2. DTOs (with DataAnnotations for validation) and a service in `App.Application/<Feature>/`; register the
   service in `Program.cs`.
3. Endpoints in `App.Api/Endpoints/<Feature>Endpoints.cs`, mapped in `Program.cs`.
4. Types in `frontend/src/types/api.ts`, calls in `frontend/src/lib/api-client.ts`, UI in
   `frontend/src/features/<feature>/`.

## Troubleshooting

- **`Bind for 127.0.0.1:5432 failed: port is already allocated`** (or "address already in use"):
  another PostgreSQL already uses 5432, often a system install. Set `POSTGRES_PORT=5433` in `.env`,
  update `Port=` in `ConnectionStrings__Default` to match, and run `docker compose up -d postgres` again.
- **`password authentication failed`** after changing `.env`: credentials are set only when the volume is
  first created. Reset with `docker compose down -v`, which **deletes local data**, then start it again.
- **`Connection string 'Default' is missing`**: there is no `.env` in the repository root. Copy
  `.env.example` to `.env`.
- **Frontend shows "Cannot reach the API"**: the backend is not running on `VITE_API_BASE_URL`, or the
  frontend origin is missing from `Cors:AllowedOrigins`. Vite uses `strictPort`, so it fails loudly
  instead of silently moving off 5173.
- **Notes show "An error occurred while processing your request." while Database is Connected**: the schema
  is missing. Run the `dotnet ef database update` command from [Migrations](#migrations) in `backend/`.
- **Database "Unavailable" while the API is online**: the container is stopped or unhealthy. Check
  `docker compose ps` and `docker compose logs postgres`.
- **`dotnet: command not found`** or the wrong SDK: install .NET 10 and make sure it is first on `PATH`.
  For a user-local install in `~/.dotnet`, add `export DOTNET_ROOT="$HOME/.dotnet"` and
  `export PATH="$HOME/.dotnet:$PATH"` to your shell profile.
- **`dotnet ef` not found**: run `dotnet tool restore` in `backend/`.
