# Frontend: brief for the agent

Read the root `CLAUDE.md` first; this file adds what the frontend needs.

## Where the contract is

- `docs/tehnicki-dokument.md`, section 6, is the API contract. Section 6.10 covers how the client works (token
  store, single-flight refresh, `usePoll`). Section 6.11 lists what the backend already serves, phase by
  phase, and the behaviour the types do not show.
- The "Stanje backenda" column in section 1 says which phases are done. Build against the real API for done phases.
  For later phases, use fixtures copied from section 6 (`src/lib/fixtures/`).
- `src/types/api.ts` mirrors the backend DTOs and is the source of truth. The backend updates it in the same
  PR that changes a DTO. Do not reshape DTOs on the client. If the contract is missing something, raise it
  with the backend instead of working around it.
- The game rules behind the screens (scoring, labels, mastery, deck) are in section 4. The game idea is
  `docs/Ri-Hack 4.0 — IDEJA IGRE v2.md`.

## Running against the real API

```bash
docker compose up -d postgres                                        # repo root, needs .env
cd backend && dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api
cd backend && dotnet run --project src/App.Api                       # http://localhost:5080, seeds questions
```

- Scalar is at http://localhost:5080/scalar, and the raw schema at `/openapi/v1.json`. Use `Authorize` with
  the `accessToken` from register or login.
- Reset links are printed in the API log locally; there is no mail server.
- Rate limits are on locally too: 10 logins per minute.

## Rules that are easy to miss

- Match timers come from `phaseDeadline - serverTime`, never from the client clock. Poll
  `GET /api/matches/{id}` once a second. Every command returns the same `MatchStateDto`, so replace the
  state with it.
- Errors are Problem Details. Branch on `problem.type` (`AppErrorCode`) and show `detail`. Never parse
  `title`.
- Web first, mobile-first layout. The Android build comes later through Capacitor, so do not use cookies,
  and keep the refresh token behind `tokenStore`.
