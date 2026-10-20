# Operations

Status: **M2a.** Local environment, health checks, structured logging, OpenTelemetry and explicit
migrations are implemented and verified. Production operations (deployment, backups,
monitoring, runbooks) are planned for M5 and listed here so earlier milestones build the right hooks.

## Environments

| Environment | Purpose | State |
|---|---|---|
| Local | Docker Compose dependencies + apps run on the host | **Available (M0)** |
| CI | Build, lint, tests (Testcontainers), e2e, image build — `.github/workflows/ci.yml` | **Defined (M1)**, not yet run on GitHub; hardening in M5 |
| Staging | Production-like, restore drills, load tests | M5 |
| Production | TLS, secret manager, monitoring, backups | M5 — not available |

## Local dependencies

Defined in [`docker-compose.yml`](../../docker-compose.yml). Profiles keep each milestone minimal:

| Service | Profile | Needed from | Host ports (127.0.0.1) | Persistence |
|---|---|---|---|---|
| PostgreSQL 18.6 | default | M1 | `POSTGRES_PORT` (5432) | volume `postgres-data` |
| MinIO + `minio-init` | `media` | M3 | `MINIO_API_PORT` (9000), console `MINIO_CONSOLE_PORT` (9001) | volume `minio-data` |
| Redis 8.8 | `cache` | when multi-instance features land (≥ M4) | `REDIS_PORT` (6379) | none (ephemeral) |
| Mailpit | `mail`, `app` | M2 (email verification/reset) | SMTP `MAILPIT_SMTP_PORT` (1025), UI `MAILPIT_UI_PORT` (8025) | in-memory |
| `migrate` → `api`, `worker`, `web` → `proxy` | `app` | M1 (proxy M3) | `WEB_PORT` (3000, Caddy), `API_PORT` (5080, direct API for debugging) | — |

The `app` profile runs the full stack from the repository's Dockerfiles. `migrate` is the EF Core
migration bundle (Dockerfile target `migrator`): idempotent, takes the EF migration lock, and the
API only starts after it exits 0. Container health: API probes `/health/ready` (unhealthy ≈ 20 s
after the database disappears, healthy again within seconds of recovery); web probes `/`.

Verification: `bash scripts/verify-local-infra.sh` — checks health, loopback-only binding,
Postgres settings, Redis auth, MinIO privacy and least-privilege app credentials.

Reset local data (destroys local volumes, local only):
`docker compose --profile "*" down -v`

## API configuration

| Key | Purpose | Local default |
|---|---|---|
| `ConnectionStrings:Database` | PostgreSQL | user-secrets (host) / compose env |
| `App:PublicBaseUrl` | Web origin used in email links (never derived from requests) | `http://localhost:3000` |
| `Email:From`, `Email:SmtpHost`, `Email:SmtpPort`, `Email:SmtpSecurity` (None/StartTls/SslOnConnect), `Email:SmtpUsername`, `Email:SmtpPassword` | Transactional email (MailKit), validated at startup | Mailpit `localhost:1025`, `None` |
| `Security:SecureCookies` | `true` (default): `__Host-` + Secure cookies, needs HTTPS or a trusted TLS proxy. `false`: plain-HTTP localhost only (warning logged) | `false` in Development and the Compose stack |
| `RateLimiting:GlobalPermitsPerMinute`, `RateLimiting:AuthPermitsPerMinute` | Per-IP limits | 600 / 10 |
| `ReverseProxy:KnownProxies` | Proxies trusted for X-Forwarded-For/Proto (required behind TLS termination) | empty |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Enables OTLP export | unset |

Emails are sent by an in-process background queue with 3 retries (2 s / 10 s / 30 s); failures are
logged as "Email '<template>' could not be delivered" without recipient or content. Data Protection
keys live in `infra.data_protection_keys`; deleting them signs everyone out and invalidates email links.

## Health checks (implemented)

- `GET /health/live` — process is up (no dependency checks; used for restarts).
- `GET /health/ready` — Postgres reachable **and** no pending migrations (5 s timeout); object
  storage (M3) and Redis (when enabled) to follow. 200 `Healthy` / 503 `Unhealthy`, status names
  only — failure details go to the logs (`Database health check failed`).
- Both are exempt from rate limiting and excluded from tracing; request logs for them are Verbose.

## Observability (M1 baseline, M5 complete)

- Structured JSON logs (Serilog) with trace/span ids and request path; opaque `user_id`
  enrichment arrives with authentication (M2). Redaction policy per [security.md](security.md).
- OpenTelemetry instrumentation registered: ASP.NET Core, HttpClient, Npgsql (traces + metrics),
  .NET runtime metrics. Planned custom metrics — `vibezone.uploads.failed`,
  `vibezone.transcode.duration`, `vibezone.transcode.failed`, `vibezone.jobs.queue_depth`,
  `vibezone.playback.errors` (client-reported), `vibezone.storage.errors`.
- Export: set `OTEL_EXPORTER_OTLP_ENDPOINT` (gRPC, e.g. `http://localhost:4317`); without it no
  exporter is registered. Verified in M1 against an OpenTelemetry Collector (debug exporter):
  after 3 API requests it received 7 spans and periodic metric batches. Span kinds were not
  inspected individually.
- Logs: JSON (`RenderedCompactJsonFormatter`, `@tr`/`@sp` = trace/span id) by default;
  `Serilog:ConsoleFormat=text` in Development.
- Prometheus/Grafana in M5 if the deployment target lacks a managed equivalent.

## Migrations

- Created with `dotnet ef migrations add` and committed; reviewed like code.
- Applied explicitly: `bash scripts/db-migrate.sh` (wraps `dotnet ef database update`; pass a
  migration name to roll back to it). Deployment will use an EF migration bundle job (M5).
  Readiness stays 503 until migrations are applied, so a new version cannot take traffic early.
  The API never auto-migrates in production. Destructive migrations require an expand/contract
  plan and a backup taken immediately before.

## Planned runbooks (M5, `docs/runbooks/`)

Database outage · storage outage · failed transcoding · queue backlog · deployment rollback ·
backup restoration. A backup is considered valid only after a restore has been executed and
verified in a non-production environment.

## Media pipeline (M3 code, not yet runtime-tested)

- `worker` (Compose `app` profile, `src/VibeZone.Worker/Dockerfile`): FFmpeg from Ubuntu 24.04, non-root,
  `cpus: 2`, `mem_limit: 1g`, heartbeat-file healthcheck. Scale by running more worker replicas;
  each runs `Media:MaxConcurrentJobs` (default 2) jobs.
- Config (API and worker): `Storage:ServiceUrl|Region|Bucket|AccessKey|SecretKey|ForcePathStyle`;
  `Media:MaxUploadBytes` (200 MB), `MinDurationSeconds`/`MaxDurationSeconds` (1 s / 2 h),
  `ProcessingTimeout` (15 min), `MaxAttempts` (3), `LeaseDuration` (5 min), `PollInterval`,
  `WaveformBuckets` (1000), `TempDirectory`, `FfmpegPath`, `FfprobePath`, `HeartbeatFile`,
  `PlaybackTokenLifetime` (6 h, API only: lifetime of `/playback` stream URLs, ADR-0008);
  `RateLimiting:UploadsPerHour` (30, per IP).
- Host development: the worker needs FFmpeg on PATH (or `Media:FfmpegPath`); otherwise run it via Compose.
  `scripts/setup-dev-secrets.sh` now also stores the MinIO app credentials in user-secrets.
- Stuck jobs: a crashed worker's job becomes claimable again when `locked_until` passes; jobs failing
  permanently mark the track `failed` with a code shown to the artist.
