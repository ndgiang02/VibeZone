# ADR-0012: Social, analytics, notifications and moderation (M4)

- Status: Accepted
- Date: 2026-10-22
- Related: ADR-0001 (modules), ADR-0004 (job queue and outbox), ADR-0007 (app tokens), ADR-0010 (composition)

## Context

M4 adds likes, follows, timed comments, play events with valid-listen rules, listening history, trending,
related tracks, in-app notifications with realtime push, content reports with a moderation queue and takedowns,
an audit log and an admin dashboard. These features cross module boundaries constantly (a like on a Catalog
track notifies a user and feeds trending), which is exactly what the outbox in ADR-0004 was reserved for.
The owner asked for M4 to be built without tests for now (development phase).

## Decision

### Modules

Four new modules, as planned in ADR-0001: `Social` (likes, follows, comments), `Analytics` (play events,
listening history, daily stats, trending), `Notifications` (in-app notifications, SignalR hub) and `Moderation`
(content reports, audit log). Each has its own schema. Admin endpoints live in the module that owns the data
(`/admin/users` in Identity, `/admin/tracks` in Catalog, `/admin/jobs` in Media, `/admin/audit-log` in
Moderation) behind named policies `moderator` (Moderator or Admin) and `admin`.

### Outbox (implements ADR-0004)

- `infra.outbox_messages` (type, version, jsonb payload, attempts, next_attempt_at, last_error) is written through
  `IOutbox` in the same `SaveChanges` as the state change. Events are records in the producer's `Contracts`
  implementing `IIntegrationEvent` (static `EventType` `module.snake_case` + `EventVersion`).
- Consumers implement `IIntegrationEventConsumer<T>` and only stage changes. The dispatcher runs each consumer
  in its own scope and transaction together with a row in `infra.outbox_consumptions (message_id, handler)`,
  so every consumer's effect happens exactly once even if a message is delivered again. Side effects outside the
  database (SignalR push) run after the commit, best-effort.
- Claiming uses `FOR UPDATE SKIP LOCKED` and `next_attempt_at` as a 2-minute lease (safe with several
  instances). Failures back off 10 s × 2ⁿ (max 1 h); after 10 attempts the message is dead-lettered (left
  unprocessed, counted on the admin dashboard). Only the exception type is stored, never its message.
- **Deviation from ADR-0004**: the dispatcher runs in the **API** host, not the worker, because notification
  consumers push to SignalR connections that live in the API. The worker still writes events (track processed).

Events in M4: `catalog.track_processed`, `social.track_liked`, `social.artist_followed`,
`social.comment_posted`, `moderation.report_resolved`, `moderation.content_removed`.

### Social

- Follows target **artist profiles** (`social.follows (follower_user_id, artist_id)`), not users: artists are the
  public identities; there are no public user pages. You can't follow your own profile.
- Counts are computed (`COUNT` on indexed tables) instead of denormalised onto Catalog rows, which would need a
  cross-module write path. Revisit with measured load.
- Comments are plain text, ≤ 1000 chars, one reply level, optional `timestamp_ms` (top-level only), soft delete.
  The author, the track's owner or a moderator may delete; a removed top-level comment hides its replies.

### Analytics

- `POST /plays` accepts batches of client events (`started` / `progress` / `completed`) with client-generated
  ids (idempotency keys; duplicates are skipped). Anonymous playback is allowed for public tracks.
- A playback session (`analytics.play_history`) belongs to one listener and one track; listened time is the
  client's report **capped by wall-clock time since the session started + 10 s**, so it can't be inflated.
- Valid listen = ≥ 30 s (half of a track under 60 s), at most once per listener and track per 30 minutes.
  The listener key is the user id, else a SHA-256 of an anonymous client id kept in the browser, else the session.
  No IP address is stored (the data model's `ip_hash` was dropped: not needed for these rules).
- `track_daily_stats` counters are upserted (`ON CONFLICT DO UPDATE`) in the same transaction as the batch.
  Likes reach it through the outbox. Trending = Σ (valid listens + 2 × likes) × weight (7 for today … 1 for six
  days ago), top 100.
- History, likes and trending are rendered by Catalog (it owns `TrackDto`), which asks `IListeningHistory`,
  `ITrendingTracks` and `ILikedTracks` for ordered ids — the ADR-0010 composition pattern.

### Notifications

- One row per recipient (`notifications.notifications`), unique `(source_event_id, recipient_user_id)`, payload
  snapshot (titles, excerpt, codes) as jsonb; actor names/avatars are resolved at read time. Nobody is notified
  about their own action.
- Realtime: SignalR hub at `/api/v1/hubs/notifications` (under `/api` so the existing proxies route it). It is
  server → client only and sends `unread { count }`; clients refetch. Users are addressed by the `sub` claim.
  Apps authenticate with `?access_token=` on hub paths only (WebSockets can't send headers). The hub is exempt
  from the API's CSRF filter: browsers can't attach the header to the upgrade, there are no state-changing hub
  methods, and a cross-site page can't read the negotiate response. The web app polls the unread count every
  2 minutes as a fallback.
- **Single instance**: a push reaches only connections on the instance that dispatched the event. Running several
  API instances needs a SignalR backplane (Redis, already in compose) — not added until there is a second instance.

### Moderation

- Reports on tracks, comments and playlists (reasons copyright / abuse / spam / other); one open report per
  reporter and item (partial unique index); you can't report your own content.
- `remove` takes the content down through the owning module's contract — `Track.TakenDownAt` (hidden from
  everyone but its owner and moderators, unplayable, excluded from every list and search), `Playlist.TakenDownAt`,
  comment soft-delete — resolves every open report on it, writes the audit entry and the events, all in one
  transaction. `dismiss` resolves one report. Tracks and playlists can be restored.
- `moderation.audit_logs` is append-only from the application (no update/delete paths); entries are staged via
  `IAuditLog` (Moderation.Contracts) by any module, e.g. Identity for role changes.

## Consequences

- + Cross-module reactions are reliable and idempotent without new infrastructure.
- + Every M4 table follows the existing conventions (schema per module, UUIDv7, cross-module FKs to stable keys).
- − Notification push is single-instance until a backplane is added.
- − Like/follow/comment counts are live `COUNT`s; fine for MVP volumes.
- − Built without automated tests (owner's decision for this phase); the M4 acceptance criteria stay open.
