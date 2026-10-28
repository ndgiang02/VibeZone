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

### M2b — Catalog & search ✅ (built 2026-10-20 without tests — ADR-0010; verified 2026-10-28)
- [x] Artists (self-serve "become an artist"), tracks (metadata, visibility, status machine, edit in Studio), albums
      (album/EP/single, tracklist order, Studio album manager), genres on tracks (one per track)
- [x] Search: Postgres FTS + trigram over tracks/artists/albums/playlists, accent-insensitive, pagination
- [x] Pages: Home, Discover, Search results, Track detail, Artist profile (with albums), Album detail — all states
- [x] Dev seed command: `dotnet run --project src/VibeZone.Api -- seed-dev` (Development only; demo artists,
      draft tracks, an album and a playlist each; no audio, nothing Ready)

### M2c — Playlists & library ✅ (verified 2026-10-28)
- [x] Playlists CRUD, ordering (drag & keyboard), public/private; Playlist detail; Library page; "Add to playlist"
      on track lists, cards and the track page; your playlists in the sidebar

**Acceptance criteria**
- [x] E2E: register → verify email (Mailpit) → login → logout (vi and en) — M2a
- [x] E2E: search → open track detail (`catalog.spec.ts`; tracks uploaded through the API and converted by the real worker)
- [x] E2E: create playlist → add tracks → reorder (mouse + keyboard) → reload persists order (`catalog.spec.ts`)
- [x] Authorization tests: private playlist/track/album of another user → 404; non-owner edit → 404 (`PlaylistTests`, `CatalogAuthorizationTests`)
- [x] Rate limit and lockout verified by tests; register/forgot/resend responses don't reveal account existence — M2a
- [~] Responsive check: every e2e runs at desktop and mobile (Pixel 7) widths ✓; keyboard reorder and player shortcuts ✓; tablet width not checked

> 2026-10-22: no build work is left in M2b/M2c — the open items are tests and manual checks, deferred by the owner
> ("development phase, no tests yet"). M4 was started on the owner's instruction before they were done.

---

## M3 — Real media & playback 🟡 (verified by integration + e2e tests 2026-10-28; streaming-memory item open)

Built first without tests (owner's request), then verified on 2026-10-20 with integration tests against
real Postgres, MinIO and the real worker image (FFmpeg) — see progress.md. Includes the minimal catalog
slice M3 needs (self-serve artist, tracks); the rest of M2b/M2c is still open.

- [x] `IObjectStorage` (S3 SDK) against MinIO; readiness check for storage
- [x] Upload API: auth, owner check, size limit while streaming, declared-length check, signature sniff, server-generated keys, SHA-256
- [x] Upload Studio UI: become an artist, create track, upload with progress/cancel, processing status (polling), delete (upload → Ready covered by `player.spec.ts`)
- [x] `VibeZone.Worker` + container with FFmpeg: job claiming (SKIP LOCKED), lease/heartbeat, retries, cancellation, concurrency limit, timeouts
      (fixed 2026-10-20: heartbeat vs. commit row-version conflict failed every job longer than lease/3)
- [x] `ffprobe` validation (container/codec allow-list, duration limits) — reject invalid files
- [x] Transcode to AAC-LC/MP4 faststart; waveform peaks, at most 1000
- [x] Cover artwork (ADR-0009): JPEG/PNG/WebP → square JPEG 320/1200 px in the API (metadata stripped,
      orientation applied); `GET/PUT/DELETE /tracks/{id}/artwork`; shown everywhere `TrackArt` is used
- [x] One-request upload (ADR-0009): `POST /track-uploads` multipart (details + audio + optional cover),
      all or nothing; Studio form redesigned around it (drag & drop, cover preview, progress/cancel)
- [x] Artist avatar + banner (ADR-0009 addendum): `PUT/DELETE /me/artist/{avatar|banner}`, public GET;
      editable in the Studio profile card, shown on the artist page, track page, artist lists.
      ADR-0013 (2026-10-23): artist = account — name/URL/picture come from the account; only the banner is artist-only
- [x] Track state machine Draft → Processing → Ready/Failed/Cancelled
- [x] Streaming endpoint with Range semantics (200/206/416), authorization before bytes
- [x] Response envelope `{data, meta}` + paging for growing lists (ADR-0008)
- [x] Native-app playback: `GET /tracks/{id}/playback` → tokenised stream/waveform URLs (ADR-0008)
- [~] Global audio player (Zustand store, single `<audio>`): play/pause, next/prev, seek, volume/mute, shuffle,
      repeat off/all/one, queue add/remove/jump, keyboard shortcuts, buffering/error/retry, mobile controls,
      persisted queue/preferences, no autoplay, Media Session
- [~] wavesurfer.js waveform from real peaks, bound to the global player (see progress.md note)
- [~] Track detail, artist profile, Home "new releases" (chronological, not personalised)

**Acceptance criteria**
- [x] Integration tests: valid upload → Ready; renamed non-audio file → rejected; oversized → rejected; corrupt → Failed
- [~] Range tests: `bytes=0-`, `bytes=100-199`, suffix range, unsatisfiable → 416, no Range → 200 ✓;
      "memory stays flat for a large file" ✗ — streaming 48 MB allocated 67 MB in the test process (cause not yet identified)
- [x] Private track stream by non-owner → 404; anonymous public stream works
- [~] ≥ 50 MB / ≥ 30 min file: upload → Ready → range seek verified at API level; browser playback not run
- [x] E2E: upload in Studio → Ready → play/pause/seek/next with the global player; state survives navigation; keyboard-only operation
      (`apps/web/e2e/player.spec.ts`, desktop + mobile, 2026-10-28)
- [x] Worker killed mid-job → job re-claimed after lease expiry and completes
- [~] Docker images: worker image builds (Testcontainers); api/web images and compose stack not re-verified after these changes

> 2026-10-22: the remaining M3 items are verification only (e2e player run, browser playback of long files, image
> builds) plus the streaming-allocation investigation; deferred with the M2 tests by the owner.

---

## M4 — Social ✅ (built 2026-10-22 — ADR-0012; verified by integration + e2e tests 2026-10-28)

- [x] Likes, follows (artist profiles), comments (timed, one reply level), listening history, recently played
- [x] Play events ingestion with idempotency + valid-listen rules; trending from daily stats (Charts page, Home)
- [x] Related tracks (genre/artist based — labelled as such, not "personalised")
- [x] Notifications (transactional outbox → in-app, SignalR push); Notifications page and top-bar bell
- [x] Content reports (incl. copyright), moderation queue, takedown/restore, audit log
- [x] Admin dashboard (overview, users + roles, tracks, jobs, audit log) — `moderator` / `admin` policies

**Acceptance criteria**
- [x] E2E: like/follow → counts and notifications update (`social.spec.ts`)
- [x] E2E: report a track → moderator sees it → takedown makes it unplayable → audited (`social.spec.ts`)
- [x] Duplicate play events don't double-count; valid-listen rule tests (`PlayEventTests`, `ValidListenRuleTests`)
- [x] Admin endpoints reject non-admins (tests for every endpoint: `ModerationTests`)

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
