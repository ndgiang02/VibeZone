# ADR-0010: Search, albums and the Playlists module

- Status: Accepted
- Date: 2026-10-20
- Related: ADR-0001 (modular monolith), ADR-0008 (envelope, paging)

## Context

M2b/M2c need search over tracks, artists, albums and playlists, albums that group an artist's tracks, and
user playlists with ordering. Vietnamese users often type without diacritics ("son tung" for "Sơn Tùng").
The plan rules out OpenSearch until a measured need (M6).

## Decision

### Search (PostgreSQL only)

- Extensions `unaccent` and `pg_trgm` (trusted, created by the migration) and an immutable wrapper
  `public.vz_fold(text)` = `lower(unaccent(...))`.
- Generated `search_vector tsvector` columns (`simple` config over `vz_fold(...)`) on `catalog.tracks`
  (title + description), `catalog.artists` (name + slug), `catalog.albums` (title + description) and
  `playlists.playlists` (title + description), each with a GIN index; GIN trigram indexes on
  `vz_fold(title|name)`. The columns are not in the EF model (EF never writes generated columns).
- A query matches on a prefix tsquery built from letter/digit runs only (`son:* & tung:*`, so no tsquery
  operators can be injected) **or** on trigram word similarity (`<%`, typos). Ranking: `ts_rank` (+ the
  artist's rank for tracks/albums) + `word_similarity`. All values are SQL parameters.
- Only listed content is searchable: Ready public tracks, all artists, public albums, public playlists
  with at least one track. `GET /search` (overview, 6 per kind + totals), `GET /search/{tracks|artists|albums}`
  (paged), `GET /playlists?q=` (Playlists module).

### Albums (Catalog)

- `catalog.albums` (kind album/ep/single, release date, visibility public/unlisted/private, soft delete);
  `tracks.album_id` (FK, SET NULL) + `track_number`. An album holds only its artist's tracks;
  `PUT /albums/{id}/tracks` replaces the whole tracklist. Tracks keep their own visibility.
- No separate album cover upload: the cover is the first track cover (fewer media tables; revisit if
  artists ask for it).

### Playlists module

- New module `Playlists` (schema `playlists`): `playlists` (owner, title, description, visibility
  public/private, denormalised `track_count`) and `playlist_tracks` (PK playlist+track, 1-based `position`).
  Cross-module FKs to `identity.users` and `catalog.tracks` added by hand (ADR-0001).
- Composition without sharing entities: Playlists owns order and membership; Catalog owns track DTOs.
  Catalog serves `GET /playlists/{id}/tracks` by asking `IPlaylistReader` (Playlists.Contracts) for the
  ordered ids after the playlist visibility check, then filters tracks by the viewer's own visibility.
  Playlists uses `ITrackCatalog.CanViewAsync` (adding a track) and `GetArtworkUrlsAsync` (cover mosaic),
  and `IUserDirectory` (Identity.Contracts) for owner names. `IPlaylistReader` is implemented separately
  from `PlaylistService` to avoid a DI cycle (TrackService → reader; PlaylistService → ITrackCatalog → TrackService).
- Reorder sends the tracks the owner sees in their new order; they swap among their current positions,
  so tracks the owner can't see any more keep their places.

## Consequences

- Search quality is "good enough" lexical search; no stemming, synonyms or personalisation.
- Generated columns live outside the EF model: renaming a searched column needs a hand-written migration.
- Two contract projects now reference each other's modules' contracts (Catalog ↔ Playlists); contracts stay
  framework-free, and module projects never reference each other.
