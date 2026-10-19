# VibeZone — Implementation Plan

Legend: `[x]` done **and verified** · `[ ]` not done · `[~]` in progress.
Verification evidence lives in [docs/progress.md](docs/progress.md). A milestone closes only
when every acceptance criterion is checked and reported.

---

## M0 — Repository audit & foundations of the plan ✅

- [x] Audit repository (empty, not a git repo) and toolchain
- [x] Install Node 24 LTS via nvm (Node 12 left as system default), confirm .NET 10 SDK, start Docker
- [x] `git init` (branch `main`), `.gitignore`, `.gitattributes`, `.editorconfig`, `.nvmrc`, `global.json`
- [x] `docker-compose.yml` with profiles: postgres (default), media (MinIO + init), cache (Redis), mail (Mailpit)
- [x] `.env.example` without secrets; compose fails fast when secrets are missing
- [x] Private MinIO bucket + least-privilege app user (`infra/minio/init-minio.sh`)
- [x] `scripts/verify-local-infra.sh`
- [x] Architecture overview, data model, threat model, operations docs, ADR-0001…0005
- [x] AGENTS.md, CLAUDE.md, README.md (local run instructions)

**Acceptance criteria**
- [x] `docker compose --profile "*" up -d --wait` → all services healthy
- [x] `scripts/verify-local-infra.sh` passes (26 checks), and fails when a service is down
- [x] Missing secret in env ⇒ `docker compose config` errors
- [x] `.env` git-ignored and untracked; no secrets in tracked files
- [x] Architecture plan, threat model and per-milestone acceptance criteria written

---

## M1 — Foundation ✅

Backend
- [x] `VibeZone.slnx`, `Directory.Build.props` (nullable, warnings-as-errors, analyzers), `Directory.Packages.props`
- [x] `BuildingBlocks/VibeZone.SharedKernel` (Result/DomainError, UUIDv7). Time via BCL `TimeProvider`; entity base deferred until the first aggregate (M2)
- [x] `VibeZone.Api` host: Minimal APIs, Problem Details (with traceId), build-time OpenAPI document, versioned route group `/api/v1`
- [x] Module pattern end-to-end: `Catalog` genres (`IModule`, `AppModules`, Domain/Application/Infrastructure/Endpoints)
- [x] `AppDbContext`, Npgsql, snake_case naming, schema-per-module, baseline migration `InitialCatalog` (genre reference data)
- [x] Serilog JSON logging (no headers/bodies logged, EF parameter logging off); OpenTelemetry traces + metrics, OTLP when configured
- [x] `/health/live`, `/health/ready` (Postgres + pending-migrations check, 5 s timeout, no details exposed)
- [x] Config from environment/user-secrets; `appsettings.json` without secrets; fail-fast options validation
- [x] Security headers; CORS not registered; global per-IP rate limiter (429 Problem Details); trusted-proxy forwarded headers
- [x] Tests: unit, integration (Testcontainers Postgres 18.6), architecture (ArchUnitNET)

Frontend
- [x] `apps/web`: Next.js 16.4 (App Router, TS strict + noUncheckedIndexedAccess), Tailwind 4, shadcn/ui (Radix), ESLint, Prettier
- [x] Design tokens (surfaces, text, coral accent, feedback, radius, shadows, type scale, layout sizes), app shell (sidebar, mobile top bar + bottom nav, skip link, player slot)
- [x] `/api/*` rewrite to backend; openapi-fetch client typed from the OpenAPI contract; TanStack Query provider with retry policy
- [x] Route-level loading / error / not-found / global-error
- [x] Vitest + RTL (GenreBrowser loading/empty/error/success/retry); Playwright smoke suite (desktop + mobile)

DevOps
- [x] Dockerfiles (API, web) — multi-stage, pinned bases, non-root
- [~] GitHub Actions CI (backend, frontend, e2e with Postgres service, image builds) — validated with actionlint and every step run locally; **not yet executed on GitHub** (no remote configured)
- [x] README updated with run instructions for API + web
- [x] Full stack via Docker Compose (`--profile app`: migration bundle → API → web) with healthchecks, verified by `scripts/verify-local-infra.sh`

**Acceptance criteria**
- [x] `dotnet build -warnaserror` and `dotnet test` green — 31/31 incl. Testcontainers integration tests
- [x] `npm run lint`, `npm run typecheck`, `npm test` (5/5), `npm run build` green
- [x] Migration applies to an empty DB; `/health/ready` 503 with Postgres stopped, 200 after restore (manual + integration tests)
- [x] Web shell renders genres from the real API through the `/api` rewrite (Playwright asserts same-origin request + 23 items)
- [x] Logs are JSON with trace/span ids; DB password absent from logs (checked, incl. container run)
- [x] Architecture tests fail on deliberate violations (Domain → EF Core; public Infrastructure type), then reverted
- [x] Docker images build and run as non-root (UID 1654 / `node`), API + web containers verified end to end

---

## M2 — Core product

Decisions (2026-10-19, project owner): global audio player moves to **M3** (needs real media);
artists are **self-serve** (any verified user can create an artist profile → `Artist` role);
OAuth **deferred** until real provider credentials exist; UI is **bilingual (vi + en)** from M2.
Delivered in slices, each verified before the next.

### M2a — Identity & i18n foundation ✅
- [x] Identity module: ASP.NET Core Identity stores on `AppDbContext` (schema `identity`), profiles, server-side sessions
- [x] Register, email verification, resend, login, logout, forgot/reset password, change password, `me`
- [x] Cookie auth (`__Host-` cookie, server-side session validation, revocation), antiforgery on unsafe methods
- [x] Auth rate limits, lockout, anti-enumeration (uniform responses, dummy hashing), per-email send throttle
- [x] SMTP email (MailKit → Mailpit locally), vi/en templates, background send queue (in-memory; durable outbox in M4)
- [x] Roles seeded (Artist, Moderator, Admin); `ICurrentUser`; roles resolved per request from the session (resource-authorization helpers arrive with the first owned resources in M2b)
- [x] Sessions list / revoke one / revoke others
- [x] Web: i18n routing (vi default, en), auth pages (login, register, verify, forgot, reset), settings (profile, password, sessions), user menu, language switcher
- [x] Web: nonce-based Content-Security-Policy (ADR-0006)
- [x] OAuth: deferred by decision until real provider credentials exist
- [x] Native-app (Flutter) token sign-in: bearer access + rotating refresh tokens on the same sessions (ADR-0007); Postman collection in `docs/api/`

**M2a acceptance (verified)**
- [x] E2E: register → verify email (Mailpit) → login → settings → logout, in en and vi; forgot → reset → old password rejected
- [x] Rate limit and lockout verified by tests; register/forgot/resend responses don't reveal account existence
- [x] CSRF rejection cases, cookie attributes, session revocation (own vs. others') covered by integration tests
- [x] Desktop and mobile viewports in e2e; no CSP violations

### M2b — Catalog & search
- [ ] Artists (self-serve "become an artist"), tracks (metadata, visibility, status machine), albums, genres on tracks
- [ ] Search: Postgres FTS + trigram over tracks/artists/albums/playlists, pagination
- [ ] Pages: Home, Discover, Search results, Track detail, Artist profile, Album detail — all states
- [ ] Dev seed command (clearly labelled, development only): artists + draft tracks (no fake audio, nothing "Ready")

### M2c — Playlists & library
- [ ] Playlists CRUD, ordering (drag & keyboard), public/private; Playlist detail; Library page

**Acceptance criteria**
- [x] E2E: register → verify email (Mailpit) → login → logout (vi and en) — M2a
- [ ] E2E: search → open track detail (Ready fixtures inserted by the test only)
- [ ] E2E: create playlist → add tracks → reorder → reload persists order
- [ ] Authorization tests: private playlist/track of another user → 404; non-owner edit → 404
- [x] Rate limit and lockout verified by tests; register/forgot/resend responses don't reveal account existence — M2a
- [ ] Responsive check at mobile/tablet/desktop widths; keyboard navigation

---

## M3 — Real media & playback 🟡 (implemented, not yet tested)

Owner asked to build M3 without running tests. Everything below compiles (`dotnet build -warnaserror`,
`npm run typecheck/lint/build`) but has **not been exercised at runtime**. Items stay `[~]` until verified.
Includes the minimal catalog slice M3 needs (self-serve artist, tracks); the rest of M2b/M2c is still open.

- [~] `IObjectStorage` (S3 SDK) against MinIO; readiness check for storage
- [~] Upload API: auth, owner check, size limit while streaming, declared-length check, signature sniff, server-generated keys, SHA-256
- [~] Upload Studio UI: become an artist, create track, upload with progress/cancel, processing status (polling), delete
- [~] `VibeZone.Worker` + container with FFmpeg: job claiming (SKIP LOCKED), lease/heartbeat, retries, cancellation, concurrency limit, timeouts
- [~] `ffprobe` validation (container/codec allow-list, duration limits) — reject invalid files
- [~] Transcode to AAC-LC/MP4 faststart; waveform peaks (artwork processing deferred)
- [~] Track state machine Draft → Processing → Ready/Failed/Cancelled
- [~] Streaming endpoint with Range semantics (200/206/416), authorization before bytes
- [~] Global audio player (Zustand store, single `<audio>`): play/pause, next/prev, seek, volume/mute, shuffle,
      repeat off/all/one, queue add/remove/jump, keyboard shortcuts, buffering/error/retry, mobile controls,
      persisted queue/preferences, no autoplay, Media Session
- [~] wavesurfer.js waveform from real peaks, bound to the global player (see progress.md note)
- [~] Track detail, artist profile, Home "new releases" (chronological, not personalised)

**Acceptance criteria** (none verified yet)
- [ ] Integration tests: valid upload → Ready; renamed non-audio file → rejected; oversized → rejected; corrupt → Failed
- [ ] Range tests: `bytes=0-`, `bytes=100-199`, suffix range, unsatisfiable → 416, no Range → 200; memory stays flat for a large file
- [ ] Private track stream by non-owner → 404; anonymous public stream works
- [ ] Browser test with a ≥ 50 MB / ≥ 30 min file: playback starts quickly, seeking works
- [ ] E2E: upload in Studio → Ready → play/pause/seek/next with the global player; state survives navigation; keyboard-only operation
- [ ] Worker killed mid-job → job re-claimed after lease expiry and completes
- [ ] Docker images (api, worker, web) build; compose stack healthy with worker

---

## M4 — Social

- [ ] Likes, follows, comments (timed), listening history, recently played
- [ ] Play events ingestion with idempotency + valid-listen rules; trending from daily stats
- [ ] Related tracks (genre/artist based — labelled as such, not "personalised")
- [ ] Notifications (outbox → in-app, SignalR push); Notifications page
- [ ] Content reports (incl. copyright), moderation queue, takedown, audit log
- [ ] Admin dashboard (users, roles, reports, tracks, jobs) — admin-only policies

**Acceptance criteria**
- [ ] E2E: like/follow → counts and notifications update
- [ ] E2E: report a track → moderator sees it → takedown makes it unplayable → audited
- [ ] Duplicate play events don't double-count; valid-listen rule tests
- [ ] Admin endpoints reject non-admins (tests for every endpoint)

---

## M5 — Production hardening

- [ ] Security tests (authz matrix, CSRF, headers, upload fuzzing), dependency & container scanning, secret scanning
- [ ] CI/CD complete; image signing/SBOM; staged deploy with manual approval
- [ ] Observability dashboards/alerts; runbooks (DB outage, storage outage, failed transcoding, queue backlog, rollback, restore)
- [ ] Backups + **executed** restore drill; load tests (browse, stream, upload)
- [ ] Deployment & rollback documentation; production topology ADR (TLS, secrets, managed S3)

**Acceptance criteria**
- [ ] Release checklist in AGENTS.md §8 / README fully satisfied with evidence in progress log

---

## M6 — Scale (only with measured need)

Kafka (versioned events, idempotent consumers, DLQ, outbox relay) · HLS multi-bitrate · CDN + signed URLs ·
OpenSearch · advanced analytics · recommendations · Kubernetes only if justified by an ADR.
