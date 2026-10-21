# Architecture Overview

Status: **M1.** Implemented: API host, `Catalog` module (genres), persistence, health, logging,
telemetry, web shell. Everything else below is the target for M2+. Update this document when
reality diverges.

## 1. Goals and constraints

- A real music streaming product: upload → process → stream with seeking, social features, moderation.
- Production-grade from the start in the things that are expensive to retrofit: authz model,
  data model, private storage, observability hooks, migrations.
- Minimal moving parts. One deployable backend (modular monolith) + one media worker + one web app.
  New infrastructure (Redis, Kafka, OpenSearch, HLS/CDN) is added only when a milestone needs it.

## 2. System context

```
                         ┌──────────────────────────────────────────┐
 Browser ── HTTPS ──►    │  Reverse proxy (prod: Caddy/nginx/ingress)│   web site: single origin for browsers
 Apps ──── HTTPS ──►     │  vibezone.vn  |  api.vibezone.vn (ADR-0011)│   API site: /api only, apps & gateway
                         └───────┬──────────────────────┬───────────┘
                                 │ /                    │ /api/*, /hubs/*
                                 ▼                      ▼
                        ┌────────────────┐     ┌──────────────────────┐
                        │  apps/web       │────►│  VibeZone.Api        │  ASP.NET Core (modular monolith)
                        │  Next.js (SSR)  │ SSR │  REST + SignalR      │
                        └────────────────┘     └──┬───────┬───────┬───┘
                                                  │       │       │
                                     ┌────────────▼┐  ┌───▼────┐ ┌▼──────────────┐
                                     │ PostgreSQL  │  │ Redis  │ │ S3 / MinIO    │ private bucket
                                     │ data, FTS,  │  │ (M4+,  │ │ originals,    │
                                     │ job queue,  │  │ when   │ │ renditions,   │
                                     │ outbox      │  │ needed)│ │ waveforms     │
                                     └──────▲──────┘  └────────┘ └──────▲────────┘
                                            │                           │
                                     ┌──────┴───────────────────────────┴──┐
                                     │  VibeZone.Worker (FFmpeg, bounded)   │
                                     └─────────────────────────────────────┘
```

In local development the reverse proxy role is played by Next.js `rewrites` (`/api/*` → API),
so the browser still sees a single origin and auth cookies stay first-party.

## 3. Backend: modular monolith

The 15 functional areas from the product brief are grouped into 8 code modules (see
[ADR-0001](../adr/0001-modular-monolith.md)). Each module owns its tables (own Postgres schema),
its endpoints and its application logic.

| Module          | Functional areas covered                               | Schema          |
|-----------------|--------------------------------------------------------|-----------------|
| `Identity`      | Identity, User Profiles, sessions, roles               | `identity`      |
| `Catalog`       | Artists, Tracks, Albums, Genres, Search (Postgres FTS) | `catalog`       |
| `Playlists`     | Playlists, ordering, visibility                        | `playlists`     |
| `Media`         | Uploads, Media Processing, Playback (streaming)        | `media`         |
| `Social`        | Likes, Follows, Comments                               | `social`        |
| `Analytics`     | Play events, listening history, trending, valid listens| `analytics`     |
| `Notifications` | In-app notifications, email dispatch, realtime push    | `notifications` |
| `Moderation`    | Content reports, Administration, audit log             | `moderation`    |

Layering **inside** each module (enforced by architecture tests from M1):

```
Endpoints (Minimal API)  ─►  Application (use cases, DTOs, validation, ports)  ─►  Domain (entities, rules)
        Infrastructure (EF Core configs, S3, FFmpeg, email) ─► implements Application ports
```

- `Domain` references nothing but `SharedKernel` (no EF Core, no ASP.NET Core, no AWS SDK).
- `Application` defines ports (`IObjectStorage`, `IMediaProbe`, `IClock`, `IEmailSender`, …).
- Cross-module calls go through a module's public `Contracts` (interfaces + DTOs + integration
  events). A module never touches another module's entities or tables directly.
- One `AppDbContext` composes every module's entity configurations (single migration history,
  real foreign keys to stable keys such as `UserId`/`TrackId`). Trade-off recorded in ADR-0001.

Hosts:
- `VibeZone.Api` — HTTP API, SignalR hubs, health endpoints, OpenAPI.
- `VibeZone.Worker` — background processing (media pipeline, outbox dispatch). Same module
  assemblies, different composition root. Scaled independently; FFmpeg lives only in this image.

## 4. Frontend

Next.js App Router, TypeScript strict, Tailwind + shadcn/ui, TanStack Query for server state,
Zustand for the player store.

- **One playback engine**: a single `HTMLAudioElement` owned by a `PlayerProvider` mounted in the
  root layout. Track cards dispatch intents (`playTrack`, `enqueue`) to the store; they never own
  audio. Waveform (wavesurfer.js) renders **pre-computed peaks** from the backend and is bound to
  the shared media element, so there is no second decoder.
- Server Components fetch public data from the API (internal URL) for SEO/first paint; mutations
  and per-user data go through TanStack Query against `/api/*` with the auth cookie.
- Forms: React Hook Form + Zod (M2).
- **API contract**: the API build emits `openapi/vibezone-api.json` (committed); the web app
  generates `src/lib/api/schema.d.ts` from it and calls the API through a typed `openapi-fetch`
  client. CI fails if either file is stale, so frontend and backend cannot drift silently.

## 5. Key flows

**Auth** — ASP.NET Core Identity, HttpOnly secure cookie on the web origin, server-side session
records (revocable), antiforgery token for unsafe methods. No tokens in JS-readable storage.
See [ADR-0002](../adr/0002-authentication-sessions.md).

**Upload → ready** ([ADR-0004](../adr/0004-background-processing.md))
1. Authenticated artist creates a draft track → API returns upload intent (`UploadJob`, limits).
2. Client uploads the file to the API (streamed to private storage under a server-generated key;
   size limit enforced while streaming). Pre-signed direct-to-S3 multipart upload is an M5/M6
   optimisation.
3. API verifies the object (size, checksum), marks job `Uploaded`, enqueues processing.
4. Worker claims the job (`FOR UPDATE SKIP LOCKED`), runs `ffprobe` (format/duration/codec
   checks — client MIME/filename are never trusted), transcodes to the playback rendition
   (AAC-LC in MP4, faststart), computes waveform peaks, writes `MediaAssets`.
5. Track → `Ready` only when required assets exist. Failures retry with backoff, then `Failed`
   with a sanitised reason. Cancellation is honoured between and during steps.

**Streaming** ([ADR-0005](../adr/0005-audio-delivery.md)) — `GET /api/v1/tracks/{id}/stream`
authorises (visibility, ownership, track state), then proxies the playback rendition from S3
with RFC 9110 range semantics (206/416, `Content-Range`, `Accept-Ranges`), streaming without
buffering whole files. Later: short-lived signed URLs / CDN, then HLS.

## 6. Cross-cutting

- **Observability**: Serilog (JSON) + OpenTelemetry traces/metrics (OTLP). `/health/live` and
  `/health/ready` (Postgres, storage, Redis when used). See [operations.md](operations.md).
- **Security**: see [security.md](security.md) (threat model).
- **Data**: see [data-model.md](data-model.md).
- **Configuration**: environment variables / secret manager; `appsettings.json` holds no secrets.

## 7. Deliberately deferred

| Technology                  | Trigger to adopt                                                  |
|-----------------------------|-------------------------------------------------------------------|
| Redis                       | Multi-instance API (SignalR backplane, distributed rate limits, cache) |
| Kafka                       | Event volume/fan-out beyond Postgres outbox + worker throughput   |
| OpenSearch/Elasticsearch    | Postgres FTS + trigram relevance/latency insufficient (measured)  |
| HLS / multi-bitrate / CDN   | After range streaming is stable; bandwidth/mobile network needs   |
| Kubernetes                  | Only with a concrete scaling/ops need that Compose/PaaS can't meet |
