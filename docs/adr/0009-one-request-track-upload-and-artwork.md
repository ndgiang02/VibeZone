# ADR-0009: One-request track upload and cover artwork

- Status: Accepted
- Date: 2026-10-20
- Related: ADR-0001 (modules), ADR-0003 (object storage), ADR-0004 (background processing), ADR-0008 (envelope)

## Context

The Studio created a Draft track first and then asked for its audio on the track's row. The project
owner wants one step: the user picks the audio and a cover, fills in the details, presses Upload, and
everything is sent together. Tracks also had no cover image (placeholders only).

## Decision

### `POST /api/v1/track-uploads` (multipart/form-data)

- Fields `title`, `visibility`, optional `genreSlug`, `description`; files `audio` (required) and `artwork`
  (optional). Parsed as a stream with `MultipartReader` (never `Request.Form`): text fields capped at 16 KB,
  the cover at `Media:MaxArtworkBytes`, the audio spooled to a temp file with the existing size limit and
  SHA-256. Body limit = audio + cover limits + 1 MB.
- **All or nothing.** Catalog stages the Draft (`ITrackCatalog.StageNewTrackAsync`, same validation as
  `POST /tracks`), Media stores the cover renditions and the original, stages the job and marks the track
  Processing — one `SaveChanges`. On any error nothing is committed and written objects are deleted.
- Response: `202 { data: UploadStatusDto }`, `Location: /api/v1/tracks/{id}`.
- The old endpoints stay: `POST /tracks` + `PUT /tracks/{id}/upload` (re-upload after Failed/Cancelled,
  and existing Drafts).

### Cover artwork

- Processed **synchronously in the API** with SkiaSharp (MIT; native Skia, the decoder Chrome ships):
  signature allow-list (JPEG/PNG/WebP — no SVG/GIF/HEIC) checked before decoding; dimensions read from
  the header first (min side 200 px, max 40 MP); JPEG DCT downscaling while decoding; center square crop;
  EXIF orientation applied; sRGB; alpha flattened; encoded from scratch as two JPEGs — `artwork_small`
  (≤ 320 px) and `artwork_large` (≤ 1200 px). No source metadata (EXIF/GPS, XMP, ICC, comments) survives.
  The original image is not stored.
- Stored as `media_assets` (kinds `artworksmall`, `artworklarge`); `catalog.tracks.artwork_id` holds the
  current version so `TrackDto.artwork = { small, large }` URLs change when the cover changes.
- `GET /tracks/{id}/artwork/{small|large}` serves it to whoever can see the track (same rule as
  `GET /tracks/{id}`; invisible → 404), `Cache-Control: private`. `PUT /tracks/{id}/artwork` (raw image
  body) replaces it; `DELETE` removes it. Owner only.
- The worker's completion step now replaces audio assets only, so a cover survives re-uploads.

## Alternatives considered

- **Client orchestration** (create Draft → upload audio → upload cover from the browser): no API change,
  but a closed tab or failed upload leaves orphan Drafts, and the app client would have to repeat it.
- **Cover processing in the worker with FFmpeg**: no new dependency, but the cover would be unavailable
  until processing ends, cover changes on existing tracks would need a new job type, and invalid images
  would be reported asynchronously instead of as a field error in the same request.
- **ImageSharp**: managed code, but its Split License is not a plain OSS licence for commercial use.

## Consequences

- A native image decoder runs in the API process on untrusted input. Mitigated by the signature
  allow-list, header-first dimension/pixel limits, size limit, rate limit (uploads policy) and
  re-encoding; Skia is kept current with the dependency updates. Recorded in security.md §10.
- The API image needs Skia's native library: `SkiaSharp.NativeAssets.Linux.NoDependencies` (glibc,
  no fontconfig) for containers; Windows/macOS natives come with `SkiaSharp`.
- Through the Next.js dev proxy the whole multipart body is buffered (as with audio uploads, ADR-0002);
  production routes `/api` at the reverse proxy.

## Addendum (2026-10-20): artist avatar and banner

> The artist avatar was removed by ADR-0013 (2026-10-23): the artist picture is the account picture. Only the
> banner remains, at `/me/artist/banner` and `/artists/{artistId}/banner/{size}`.

Same pipeline for artist profile images, with an `ImageSpec` per use (aspect ratio, minimum size, two widths):
avatar square 160/640 px (min 200 × 200), banner 3:1 800/1600 px wide (min 600 × 200). Stored in
`media.artist_images` (kinds `avatarsmall|avatarlarge|bannersmall|bannerlarge`, FK → `catalog.artists` added by
hand); `catalog.artists.avatar_id` / `banner_id` hold the current versions (`IArtistCatalog` contract).
`PUT/DELETE /api/v1/me/artist/{avatar|banner}` (raw image body, owner = the signed-in user's profile);
`GET /api/v1/artists/{artistId}/{avatar|banner}/{small|large}` is public (artist profiles are public).
`ArtistDto.avatar` / `banner` and `ArtistRefDto.avatarUrl` carry the URLs. Error codes `image_*`.

Also fixed: `POST /track-uploads` returned 500 — EF inserted the media rows before the new track because the
cross-module FKs aren't in the EF model. The track is now saved first inside an explicit transaction.

## Addendum (2026-10-20): account pictures

Every user (listener or artist) can set an account picture: `PUT/DELETE /api/v1/me/avatar` (raw image, square
160/640 px, same pipeline), served at `GET /api/v1/users/{userId}/avatar/{small|large}` (public, like artist
images; user ids are UUIDv7 and appear only where the API exposes them). Stored in `media.user_images`
(FK → `identity.users`, added by hand); `identity.user_profiles.avatar_id` holds the version (`IUserAvatars`
contract); `MeDto.avatar`. Shown in the account menu (top bar / sidebar) and editable in Settings.
