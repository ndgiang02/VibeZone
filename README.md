# VibeZone

A music streaming platform for artists and listeners: upload, real audio processing, streaming
with seeking, playlists, and a community around tracks.

> **Status: M2a (accounts & languages) complete.** Real accounts work end to end: register,
> email confirmation, sign-in, password reset, profile/language settings and device sessions, in
> Vietnamese (default) and English. Catalog/search/playlists follow in M2b–M2c; uploads and
> playback in M3. See
> [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md) and [docs/progress.md](docs/progress.md).
> This is not production-ready.

## Stack

Next.js 16 (App Router, TypeScript) · ASP.NET Core on .NET 10 · PostgreSQL 18 · S3-compatible
storage (MinIO locally) · FFmpeg worker (M3) · Docker Compose. Details in
[docs/architecture/overview.md](docs/architecture/overview.md).

## Prerequisites

| Tool | Version |
|---|---|
| Docker Desktop (Compose v2) | recent |
| Node.js | 24 LTS — `nvm install 24 && nvm use 24` (see `.nvmrc`) |
| .NET SDK | 10.0.x (see `global.json`) |
| Bash | Git Bash on Windows, for `scripts/*.sh` |

## Run everything with Docker Compose

Only Docker is needed (no .NET/Node on the host):

```bash
cp .env.example .env                           # fill every empty value, e.g. `openssl rand -hex 24`
docker compose --profile app up -d --build --wait
bash scripts/verify-local-infra.sh             # optional: end-to-end checks of the running stack
```

This starts PostgreSQL and Mailpit, runs migrations once (`migrate` exits 0), then the API and the web app:

- Web: http://localhost:3000 (Caddy reverse proxy: `/api` → API, everything else → web; redirects to
  `/vi`, English at `/en`)
- API site for native apps / Postman: http://localhost:3001 (`/api/*` only; production `api.<domain>`, ADR-0011)
  · API container directly (debugging, health): http://localhost:5080
- **Emails** (account confirmation, password reset) land in Mailpit: http://localhost:8025 — nothing is
  delivered to real inboxes locally.
- Logs: `docker compose --profile app logs -f api web` · Stop: `docker compose --profile app down`
- After code changes: re-run the `up … --build` command. Ports are configurable in `.env`
  (`API_PORT`, `WEB_PORT`, `API_PUBLIC_PORT`); `API_ENVIRONMENT=Development` exposes `/openapi/v1.json`.
- The containerised API and `dotnet run` both use port 5080 — run one at a time.

## Develop on the host (hot reload)

```bash
# 1. Local dependencies
cp .env.example .env              # fill every empty value, e.g. `openssl rand -hex 24`
docker compose --profile mail up -d --wait   # PostgreSQL + Mailpit (the API sends email to localhost:1025)
bash scripts/verify-local-infra.sh

# 2. API (http://localhost:5080)
dotnet tool restore
bash scripts/setup-dev-secrets.sh # copies the DB connection string into .NET user-secrets
bash scripts/db-migrate.sh        # applies EF Core migrations (never automatic)
dotnet run --project src/VibeZone.Api

# 3. Web (http://localhost:3000) — in another terminal
cd apps/web
nvm use 24
npm ci
npm run dev                       # /api/* is proxied to the API
```

Useful endpoints: `GET /health/live`, `GET /health/ready`, `GET /api/v1/genres`,
`GET /openapi/v1.json` (Development only).

## Checks

| What | Command |
|---|---|
| Backend build (warnings = errors) | `dotnet build VibeZone.slnx -warnaserror` |
| Backend tests (unit, architecture, integration — needs Docker; the media tests build the worker image and take ~10 min) | `dotnet test --solution VibeZone.slnx` |
| Backend formatting | `dotnet format VibeZone.slnx --verify-no-changes` |
| Frontend lint / types / format | `npm run lint`, `npm run typecheck`, `npm run format:check` (in `apps/web`) |
| Frontend unit tests | `npm test` |
| End-to-end (real API + DB + mail + media) | `npm run build && npx playwright test` (Postgres migrated, Mailpit and MinIO running, plus the media worker: `docker compose --profile app up -d --wait worker`; stop the `api`/`web`/`proxy` containers first — the API uses port 5080) |
| Local infrastructure | `bash scripts/verify-local-infra.sh` |

CI runs all of these on every push/PR: [.github/workflows/ci.yml](.github/workflows/ci.yml).

## Changing the API contract

The API build writes `openapi/vibezone-api.json`; the web client types are generated from it.
After changing endpoints: `dotnet build`, then `npm run api:generate` in `apps/web`, and commit
both files. CI fails if either is stale.

## Containers

```bash
docker build -f src/VibeZone.Api/Dockerfile -t vibezone-api .   # port 8080, non-root
docker build -t vibezone-web apps/web                            # port 3000, non-root
```

The API needs `ConnectionStrings__Database`; telemetry is exported when
`OTEL_EXPORTER_OTLP_ENDPOINT` is set. Deployment and rollback procedures arrive in M5.

## Local services

| Service | Profile | URL / port (localhost only) |
|---|---|---|
| PostgreSQL | default | `127.0.0.1:5432` — db/user from `.env` |
| MinIO S3 API / console | `media` | http://127.0.0.1:9000 / http://127.0.0.1:9001 |
| Redis | `cache` | `127.0.0.1:6379` (password required) |
| Mailpit | `mail`, `app` | SMTP `127.0.0.1:1025`, UI http://127.0.0.1:8025 |

Stop: `docker compose --profile "*" down` · Reset all local data: `docker compose --profile "*" down -v`

## Documentation

- [AGENTS.md](AGENTS.md) — engineering rules (architecture, security, conventions, testing)
- [docs/architecture/](docs/architecture/) — overview, data model, security/threat model, operations
- [docs/adr/](docs/adr/) — architecture decision records
- [docs/progress.md](docs/progress.md) — verified progress log
