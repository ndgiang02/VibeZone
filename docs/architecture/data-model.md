# Data Model

Status: **M2a.** Implemented: `catalog.genres` (`InitialCatalog`), the `identity` schema and
`infra.data_protection_keys` (`AddIdentity`), `infra.__ef_migrations_history`. Everything else is planned and lands with the milestone that
ships it. When a migration changes the model, update this file in the same change.

## Conventions

- PostgreSQL 18, one database, **one schema per module** (`identity`, `catalog`, …).
- `snake_case` identifiers (EFCore.NamingConventions).
- Primary keys: `uuid`, generated in the application as **UUIDv7** (`Guid.CreateVersion7()`),
  time-ordered for index locality. Small lookup tables (genres) may use `int`.
- All timestamps `timestamptz`, written in UTC (`created_at`, `updated_at`, …).
- Enums stored as `text` with `CHECK` constraints (readable, migration-friendly).
- Optimistic concurrency via Postgres `xmin` on aggregates edited concurrently (tracks,
  playlists, profiles).
- Soft delete (`deleted_at`) only where history matters (tracks, comments); filtered by default.
- Keyset (cursor) pagination for feeds and lists; offset pagination only for admin tables.
- Binary media is **never** stored in Postgres — only object keys and metadata.
- Polymorphic references (`subject_type`, `subject_id`) have no FK; validated in application.
- **Reference data** every environment needs (genre taxonomy) is seeded with EF `HasData` in
  migrations; ids are stable forever. **Demo/sample data** is development/test only, via an
  explicit command, never in migrations.

## Entities

### identity
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `users` ✅ (ASP.NET Identity) | `id`, `user_name` (handle), `normalized_user_name`, `email`, `normalized_email`, `email_confirmed`, `password_hash`, `security_stamp`, lockout fields, `created_at` | unique `normalized_user_name`, unique `normalized_email` |
| `roles`, `user_roles`, `user_logins`, `user_tokens`, `user_claims`, `role_claims` ✅ | Identity defaults | Roles seeded as reference data with fixed ids: `Artist`, `Moderator`, `Admin` (listener = any authenticated user) |
| `user_profiles` ✅ | `user_id` (PK, FK users, cascade), `display_name` (≤ 60), `locale` (`vi`/`en`, CHECK), `created_at`, `updated_at`, `xmin` | bio/avatar arrive with public profiles |
| `user_sessions` ✅ | `id`, `user_id` (FK, cascade), `created_at`, `last_seen_at`, `expires_at` (absolute, 30 d), `revoked_at`, `user_agent` (≤ 256; app sessions store the client's `deviceName`), `refresh_token_hash`, `previous_refresh_token_hash` (SHA-256, token sessions only — ADR-0007) | index `(user_id) where revoked_at is null`; unique `refresh_token_hash` and index `previous_refresh_token_hash` (both partial, `is not null`). No IP stored (privacy; not needed yet). Refresh tokens themselves are never stored |

### catalog
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `artists` | `id`, `user_id` (FK users), `slug`, `name`, `bio`, `avatar_key`, `banner_key`, `is_verified`, `follower_count`, timestamps, `xmin` | unique `user_id` (one artist profile per account in MVP), unique `slug` |
| `genres` ✅ | `id` int (stable, not generated), `slug` (≤ 64, kebab-case), `name` (≤ 64) | unique `slug`; 23 reference rows |
| `tracks` | `id`, `artist_id`, `slug`, `title`, `description`, `status`, `visibility`, `duration_ms`, `artwork_key`, `release_date`, `is_explicit`, `play_count`, `like_count`, `comment_count`, `published_at`, timestamps, `deleted_at`, `search_vector` (generated tsvector), `xmin` | `status ∈ {draft, processing, ready, failed, cancelled}`; `visibility ∈ {public, unlisted, private}`; unique `(artist_id, slug)`; index `(artist_id, created_at desc)`; partial index `(published_at desc) where status='ready' and visibility='public' and deleted_at is null`; GIN `search_vector`; GIN trigram on `title` |
| `track_genres` | `track_id`, `genre_id` | PK `(track_id, genre_id)`; index `genre_id` |
| `albums` | `id`, `artist_id`, `slug`, `title`, `kind` (album/ep/single), `description`, `artwork_key`, `release_date`, `visibility`, timestamps, `xmin` | unique `(artist_id, slug)` |
| `album_tracks` | `album_id`, `track_id`, `position` | PK `(album_id, track_id)`; unique `(album_id, position)` deferrable; tracks must belong to the album's artist |

Artist/album search uses separate generated `search_vector` columns; search queries union the
three result kinds with per-kind limits.

### playlists
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `playlists` | `id`, `owner_user_id`, `title`, `slug`, `description`, `visibility`, `artwork_key`, `track_count`, timestamps, `xmin` | unique `(owner_user_id, slug)`; index `(owner_user_id, updated_at desc)` |
| `playlist_tracks` | `playlist_id`, `track_id`, `position`, `added_at`, `added_by` | PK `(playlist_id, track_id)` (no duplicates in MVP); unique `(playlist_id, position)` deferrable initially deferred; reorder = one transaction + playlist `xmin` check |

A playlist only exposes tracks the viewer may see (private tracks of other users are hidden).

### media
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `upload_jobs` | `id`, `user_id`, `track_id`, `status`, `original_filename` (display only, sanitised), `declared_content_type`, `size_bytes`, `sha256`, `object_key`, `attempt_count`, `max_attempts`, `next_attempt_at`, `locked_by`, `locked_until`, `last_error` (sanitised), timestamps, `completed_at` | `status ∈ {awaiting_upload, uploaded, processing, completed, failed, cancelled}`; partial index `(next_attempt_at) where status in ('uploaded','processing')`; index `(track_id)` |
| `media_assets` | `id`, `track_id`, `kind`, `object_key`, `content_type`, `size_bytes`, `duration_ms`, `codec`, `bitrate`, `sample_rate`, `channels`, `sha256`, `created_at` | `kind ∈ {original, playback_aac, waveform_peaks, artwork_original, artwork_sm, artwork_lg}`; unique `(track_id, kind)`; unique `object_key` |

Object keys are server-generated (`tracks/{trackId}/{kind}/{uuid}.{ext}`); user filenames never
become keys.

### social
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `likes` | `user_id`, `track_id`, `created_at` | PK `(user_id, track_id)`; index `(track_id, created_at desc)` |
| `follows` | `follower_id`, `followee_id`, `created_at` | PK `(follower_id, followee_id)`; `CHECK follower_id <> followee_id`; index `followee_id` |
| `comments` | `id`, `track_id`, `user_id`, `parent_id` (one level), `body` (plain text, ≤ 1000), `timestamp_ms` (waveform position), `created_at`, `edited_at`, `deleted_at` | index `(track_id, created_at desc)`; index `(track_id, timestamp_ms)` |

### analytics
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `play_events` | `id` (client-generated idempotency key), `playback_session_id`, `user_id` (nullable), `track_id`, `event_type`, `position_ms`, `client_ts`, `received_at`, `ip_hash` | unique `id`; index `(track_id, received_at)`; monthly partitioning when volume requires |
| `play_history` | `id`, `user_id`, `track_id`, `started_at`, `listened_ms`, `is_valid_listen` | index `(user_id, started_at desc)` |
| `track_daily_stats` | `track_id`, `day`, `plays`, `valid_listens`, `likes` | PK `(track_id, day)` — drives trending |

`event_type ∈ {playback_started, playback_progress, playback_completed}`. A **valid listen**
is ≥ 30 s of actual playback (or ≥ 50 % for tracks shorter than 60 s), at most one per
user (or anonymous playback session) per track per 30 minutes. Domain events
(`track_liked`, `user_followed`, `comment_created`, `track_transcoded`) are recorded through the
outbox.

### notifications
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `notifications` | `id`, `recipient_user_id`, `type`, `actor_user_id`, `subject_type`, `subject_id`, `payload` jsonb (non-sensitive), `created_at`, `read_at` | index `(recipient_user_id, created_at desc)`; partial index `(recipient_user_id) where read_at is null` |

### moderation
| Table | Key columns | Constraints / indexes |
|---|---|---|
| `content_reports` | `id`, `reporter_user_id`, `subject_type`, `subject_id`, `reason` (copyright, abuse, spam, other), `details`, `status` (open, reviewing, actioned, rejected), `handled_by`, `handled_at`, `resolution_note`, `created_at` | unique `(reporter_user_id, subject_type, subject_id) where status in ('open','reviewing')`; index `(status, created_at)` |
| `audit_logs` | `id`, `actor_user_id`, `action`, `subject_type`, `subject_id`, `metadata` jsonb (no secrets), `ip_hash`, `occurred_at` | append-only (no UPDATE/DELETE grants in prod); index `(subject_type, subject_id)`, `(actor_user_id, occurred_at desc)` |

### infrastructure (`infra` schema)
| Table | Purpose |
|---|---|
| `data_protection_keys` ✅ | ASP.NET Core Data Protection key ring (cookies, antiforgery, Identity tokens) |
| `outbox_messages` | `id`, `type`, `version`, `payload` jsonb, `occurred_at`, `processed_at`, `attempts`, `last_error` — transactional outbox, dispatched by the worker |
| `__ef_migrations_history` | EF Core migration history |

## Ownership and visibility rules

- A track is visible to a viewer iff `deleted_at is null` and (`visibility in (public, unlisted)`
  and `status = ready`) or the viewer owns it, or the viewer is Moderator/Admin.
  `unlisted` is excluded from search, feeds and artist pages but reachable by link.
- Only the owning artist may edit/delete a track or album; only the owner may edit a playlist.
- Streaming requires the same visibility check **plus** `status = ready`.
- These rules live in the Application layer as reusable query specifications and are covered by
  authorization tests (IDOR cases: other user's private track/playlist/upload job).

## Implementation notes (M3 code, not yet runtime-tested)

Migration `AddTracksAndMedia` implements `catalog.artists`, `catalog.tracks`, `media.upload_jobs` and
`media.media_assets` as planned above, with these differences:

- `tracks`: one optional `genre_id` (FK `catalog.genres`, SET NULL) instead of `track_genres` for now;
  `processing_error` holds the last failure code; no slug or artwork yet; `xmin` row version.
- Enum values are lower-case text with CHECK constraints (`playbackaac`, `waveformpeaks` for asset kinds).
- `upload_jobs`: status values `uploaded | processing | completed | failed | cancelled` (no
  `awaiting_upload`: the job is created only after the original is stored and verified);
  `ux_upload_jobs_active_track` enforces one active job per track; `ix_upload_jobs_queue` serves claiming.
- Cross-module FKs (`artists.user_id → identity.users`, `upload_jobs.track_id/user_id`,
  `media_assets.track_id`) are added by hand in the migration (ADR-0001), all `ON DELETE RESTRICT`.
- Migration `AddTrackArtwork` (ADR-0009): `tracks.artwork_id uuid null` (version of the current cover — the
  id of its `artworklarge` asset, no FK) and asset kinds `artworksmall` / `artworklarge` (square JPEGs made
  by the API). The original cover is not stored (`artwork_original` from the plan is not used). Audio
  processing replaces audio kinds only.
- Migration `AddArtistImages`: `artists.avatar_id`, `artists.banner_id` (uuid null, current versions) and
  `media.artist_images` (`id`, `artist_id` FK → `catalog.artists`, `kind ∈ {avatarsmall, avatarlarge,
  bannersmall, bannerlarge}`, `object_key` unique, `content_type`, `size_bytes`, `created_at`; unique
  `(artist_id, kind)`). Object keys: `artists/{artistId}/{kind}/{id}.jpg`.
- Migration `AddAlbumsAndSearch` (ADR-0010): `catalog.albums` (`id`, `artist_id` FK, `title`, `kind ∈ {album, ep, single}`,
  `description`, `release_date` date, `visibility`, timestamps, `deleted_at`, `xmin`; no slug/artwork — the cover is the
  first track's); `tracks.album_id` (FK SET NULL) + `track_number`; extensions `unaccent`, `pg_trgm`; function
  `public.vz_fold`; generated `search_vector` on tracks/artists/albums + GIN and trigram indexes (not in the EF model).
- Migration `AddPlaylists`: schema `playlists`: `playlists` (`id`, `owner_user_id` FK → `identity.users`, `title`,
  `description`, `visibility ∈ {public, private}`, `track_count`, timestamps, `xmin`, generated `search_vector`) and
  `playlist_tracks` (PK `(playlist_id, track_id)`, `position` ≥ 1, `added_at`; FK → playlists CASCADE, → `catalog.tracks`
  RESTRICT). No slug or artwork (mosaic of track covers).
- Migration `AddUserAvatars`: `identity.user_profiles.avatar_id` and `media.user_images` (same shape, `user_id`
  FK → `identity.users`, kinds `avatarsmall|avatarlarge`). Object keys: `users/{userId}/{kind}/{id}.jpg`.
