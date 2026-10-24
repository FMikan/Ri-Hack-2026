# Project conventions

Hackathon starter: React/Vite frontend (`frontend/`), ASP.NET Core .NET 10 API (`backend/`), PostgreSQL in
Docker. See README.md for setup, URLs and troubleshooting.

Product: a ranked 1v1 IT-knowledge quiz game. Spec is `docs/tehnicki-dokument.md` (schema §3, game rules §4,
API contract §6, phases §1, test list §9); the game idea is `docs/Ri-Hack 4.0 — IDEJA IGRE v2.md`. Build
test-first in the order of §9 and keep DTOs identical to §6. Questions live in
`backend/src/App.Infrastructure/Seed/questions/<topic>.json` (shapes in docs §3.2.1); `dotnet test` runs `SeedTests`,
which reject malformed questions and topics that cannot build a full deck.

## Branches

Commit to `development` (or a feature branch). `main` is production: every push to it deploys to the VPS via
`.github/workflows/ci-cd.yml`. Never commit or push to `main` directly; release through a PR
`development -> main`. Deployment details: `deploy/README.md`.

## Commands

```bash
docker compose up -d postgres                                  # root; needs .env (cp .env.example .env)
cd backend && dotnet run --project src/App.Api                 # http://localhost:5080, Scalar at /scalar
cd backend && dotnet build && dotnet test
cd backend && dotnet ef migrations add <Name> --project src/App.Infrastructure --startup-project src/App.Api
cd backend && dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api
cd frontend && npm run dev                                     # http://localhost:5173
cd frontend && npm run build && npm run lint                   # build = tsc -b + vite build
cd frontend && npx shadcn@latest add <component>
```

`dotnet` may live in `~/.dotnet` and not be on PATH: prefix commands with `PATH=$HOME/.dotnet:$PATH`.

## Backend

- Layers: Api -> Application, Infrastructure; Infrastructure -> Application, Domain; Application -> Domain.
  Never reference Infrastructure from Application or Domain.
- Minimal APIs. One `Endpoints/<Feature>Endpoints.cs` per feature with a `Map<Feature>Endpoints` extension,
  routes under `/api/<feature>`, mapped in `Program.cs`.
- Use cases are plain classes in `App.Application/<Feature>/` that take `IAppDbContext`, registered in
  `Program.cs`. No repository layer or generic abstractions.
- Request validation uses DataAnnotations on DTOs; .NET 10 `AddValidation()` returns 400 Problem Details
  before the handler runs.
- Errors are Problem Details. Do not return raw exception messages.
- Async everywhere; pass the endpoint `CancellationToken` down.
- Connection string comes only from configuration (`ConnectionStrings__Default` in the root `.env`, loaded
  by `LoadDotEnv` in Program.cs). Never hardcode credentials in code or appsettings.
- After changing entities, add a migration; commit the migration files. Production applies them on startup.
- Health: `/health` = liveness (no checks), `/health/ready` = checks tagged `"ready"` (database).

## Frontend

- Frontend agents: read `frontend/CLAUDE.md` (what the backend already serves, where the contract is).
- All HTTP goes through `src/lib/api-client.ts` using the full origin from `VITE_API_BASE_URL` (no Vite
  proxy). Keep `src/types/api.ts` in sync with backend DTOs. No `any`.
- Feature UI in `src/features/<feature>/`, shared bits in `src/components/shared/`, shadcn components in
  `src/components/ui/` (generated; excluded from lint).
- Use the `@/` import alias. Server data via `useAsync` or local state; no global state library.
- Tailwind v4 is configured in CSS (`src/index.css`, `@theme`); there is no tailwind.config.js. Theme tokens
  live in `:root`, including `--success` and `--warning`.
- Accessible by default: real labels, visible focus, text next to color, semantic elements.
- `VITE_*` variables are public. No secrets in the frontend.

## Ports

Frontend 5173 (strict), API 5080 (HTTP only in dev), PostgreSQL `POSTGRES_PORT` (default 5432). If you
change a port, update README, `.env.example`, `launchSettings.json`, CORS origins and `VITE_API_BASE_URL`
together.
