# Progress Log

Newest first. Each entry records what was done, **how it was verified** (actual commands and
results), known issues and next steps.

## 2026-10-21 — App API check, Postman collection complete

**Done**
- Postman collection covers all 62 OpenAPI operations (70 requests): added account picture (set/get/remove),
  artist image get + banner remove, search artists/albums; `userId`/`artistId` captured from Me / My artist.
- Fixed the generator: Health and the Mailpit helper are found by folder name — the index-based lookup had
  pointed the Playlists folder at `localhost:5080` after new folders were inserted.

**Verification**: manual smoke script (not committed) against the local Compose stack through the proxy
(`localhost:3000`) with Bearer tokens, as the app calls it: 84 checks — register → Mailpit link → verify → token →
refresh, account picture, become artist, one-request upload (worker → Ready), cover, playback token URL with
Range (206), waveform, raw two-step upload + cancel, albums, search (all kinds), playlists (CRUD, reorder,
containsTrack, private → 404 for anonymous), cleanup, refresh-token reuse → 401, revoke and bearer logout → 401.
All passed (the sign-out checks re-run after the 1-minute auth rate-limit window; the first run hit 429).
Test data left: the account `smoke-mvhgqf77@example.test` (no delete-account endpoint).

## 2026-10-20 — M2b/M2c: albums, search, playlists, library, discover, dev seed (no tests)

Owner request: finish M2b, M2c and M3 without writing tests.

**Done**
- Catalog: albums (album/EP/single, release date, visibility, tracklist order; cover = first track cover);
  `TrackDto.album/trackNumber`; artist profile lists albums; track metadata editing in the Studio (inline).
- Search (ADR-0010): `unaccent` + `pg_trgm`, `public.vz_fold`, generated `search_vector` columns, GIN + trigram
  indexes; `GET /search`, `/search/{tracks|artists|albums}`; public playlist search. Web: /search page fed by the
  top-bar box (debounced, ?q= in the URL); the old home-page local filter is gone.
- New Playlists module (schema `playlists`): CRUD, add/remove, reorder (positions swap; hidden tracks keep theirs),
  `GET /me/playlists?containsTrack=`; Catalog composes `GET /playlists/{id}/tracks` via `IPlaylistReader`.
  Contracts added: `IUserDirectory` (Identity), `ITrackCatalog.GetArtworkUrlsAsync`.
- Web: album page, playlist page (owner: edit, delete, drag & drop / arrow buttons / Alt+↑↓ reorder, remove),
  Library (/library: playlists + create), Discover (/discover: genres, community playlists, new releases), "Add to
  playlist" menu on track rows, cards and the track page (with quick create), real playlists in the sidebar,
  Studio album manager. Nav: Discover, Library.
- Dev seed: `dotnet run --project src/VibeZone.Api -- seed-dev` (Development only).
- Migrations: `AddAlbumsAndSearch`, `AddPlaylists`.

**Verification**: `dotnet build VibeZone.slnx -warnaserror` ✓; web `typecheck`, `lint`, `format:check` ✓.
**No tests were written or run** (owner's request): search SQL, migrations on a real database, the playlist
reorder, the seed command and every new page are unverified. M3's open items are verification-only (e2e
player spec, browser playback, streaming-memory investigation) and remain open.

**Next**: integration tests (authz matrix for albums/playlists, search accents/typos, reorder), e2e for search →
track and playlist create → add → reorder → reload (M2 acceptance), run `player.spec.ts`.

## 2026-10-20 — Account pictures

- `PUT/DELETE /me/avatar`, `GET /users/{id}/avatar/{size}`, migration `AddUserAvatars`, `MeDto.avatar`; editor in
  Settings → Profile; the account button (top bar/sidebar) shows the picture (initials fallback).
- Verification: `dotnet build -warnaserror` ✓, web typecheck/lint ✓. **No tests run** (owner's request).

## 2026-10-20 — Artist avatar & banner; fix 500 on one-request upload

**Done**
- Fix: `POST /track-uploads` → 500 (FK 23503): EF inserted `media_assets`/`upload_jobs` before the new
  `catalog.tracks` row (cross-module FKs aren't in the EF model). Track saved first in an explicit transaction.
- Artist avatar (square 160/640) and banner (3:1, 800/1600): `PUT/DELETE /me/artist/{avatar|banner}`, public
  `GET /artists/{id}/{avatar|banner}/{small|large}`, migration `AddArtistImages`, `IArtistCatalog` contract,
  image processor generalised (`ImageSpec`: aspect ratio, orientation applied before cropping).
- Web: Studio profile card (banner + avatar, click/drop to change, remove); artist page banner; real avatars
  in artist lists, search, track page.

**Verification**: `dotnet build -warnaserror` ✓; web `typecheck`, `lint`, `format:check` ✓. **No tests run**
(owner asked to skip testing) — the processor refactor and new endpoints are unverified by tests.

## 2026-10-20 — One-request track upload, cover artwork, UI motion (ADR-0009)

Owner request: uploads should send the audio, cover and details together (no "create, then upload"),
tracks need cover images, and the UI should feel smoother.

**Done**
- API: `POST /api/v1/track-uploads` (multipart, streamed; all or nothing), `GET /tracks/{id}/artwork/{small|large}`,
  `PUT/DELETE /tracks/{id}/artwork`. Covers re-encoded with SkiaSharp 4.153.1 (square JPEG 320/1200 px, metadata
  stripped, EXIF orientation applied). `TrackDto.artwork`. Migration `AddTrackArtwork`. The worker keeps covers.
- Web: Studio form redesigned (audio drop zone with title suggested from the file name, cover picker with
  preview, progress/cancel, success state); cover change by clicking the art on a Studio row; real covers in
  cards, lists, track page, player bar, queue and now-playing (gradient fallback, fade-in).
- Motion: page enter (`[locale]/template.tsx`), staggered list/grid entrances, button press scale, player art
  transition on track change, striped upload progress; all off under prefers-reduced-motion.
- Docs: ADR-0009, security.md §10, data-model, API guide, Postman collection, plan. e2e `player.spec.ts`
  updated to the new flow.

**Verification (actually run)**
- `dotnet build -warnaserror` ✓; unit tests 87/87 (incl. 9 new artwork processor tests).
- Web: `typecheck` ✓, `lint` ✓, `format:check` ✓, `npm test` 37/37 (5 new upload form tests).
- Integration `TrackUploadTests`: the first runs failed 10/12 — every multipart file part was rejected as
  `multipart_invalid` (`IsFormDisposition()` excludes parts that have a file name). Fixed; the re-run was
  **stopped at the owner's request**, so the fix is **not verified by integration tests**. Cover
  replace/remove (incl. cross-user 404) passed in the first run.
- Not run: full integration suite, e2e, `npm run build`, Docker images (SkiaSharp native lib in the API image).

**Next**: re-run `TrackUploadTests` and the e2e upload/playback spec; build the API image.

## 2026-10-20 — Web UI redesign (Resso-style layout, lime theme)

Owner request: restyle the existing web app after a Resso reference (dark, left sidebar, top search,
banner + card sections, artists and now-playing panel on the right), primary colour `#A8D64A`, artwork
and mock data allowed. Frontend only — no API, schema, auth or player-logic changes.

**Done**
- Theme: tokens in `globals.css` switched to the owner's palette (`#050505` page, `#101010` sidebar,
  `#151515` cards, `#A8D64A` primary + subtle/border variants); styled `.vz-range` sliders.
- Shell: new `Sidebar` (icon rail 72 px on tablets, 220 px from lg; lime pill for the current page; planned
  items Your FM / For you / Charts / library entries shown disabled with "Soon", not links), new `Topbar`
  (back/forward, search, language, account; guests see Sign in + Create account). Mobile keeps the
  bottom tab bar; search wraps under the header.
- Home (`features/home`): banner with the real "play all" over new releases (count · total time); new
  releases as cards with hover play/queue; "View all / Show less" reveals loaded items (no listing pages
  exist); artists derived from new releases (link to artist pages) as a side list (xl) or a horizontal
  rail; `NowPlayingCard` in the side panel reads/commands the existing player store (still one `<audio>`).
- Search: no search API exists, so the top-bar query (`src/stores/search-store.ts`) filters what the home
  page has loaded (tracks, artists, genres, showcase items); typing on another page navigates home.
- Artwork: 16 generated SVG illustrations in `public/artwork` (local, so no CSP change), used for genre tiles,
  the banner collage and showcase cards, with a gradient fallback on load error. Real tracks keep the
  `TrackArt` gradient (no artwork upload exists yet).
- UI showcase (`features/showcase`): sample Most played / Recommended / playlists, and Hot songs / artists
  only when the catalog is empty. Dev-only (or `NEXT_PUBLIC_SHOWCASE=true`), "Demo"-badged, not playable,
  fictional names, no play/follower counts. AGENTS.md updated (UI palette, mock-data exception).

**Verification (actually run)**
- `npm run typecheck` ✓, `npm run lint` ✓, `npm test` **32/32**, `npm run build` ✓.
- Dev server screenshots at 1440/1280/1024/820/390 px: no horizontal overflow, no page errors (only the
  existing anonymous `/me` 401). Scripted check: play from a card → one `<audio>`, plays; pause from the
  now-playing card pauses the shared engine and the player bar follows; search filters and navigates home.
- Production build: no "Demo" content rendered.
- Playwright against the Compose API: desktop smoke + auth ✓; mobile smoke + auth ✓ (when re-run serially —
  the parallel run hit 429s because the Compose API uses the default auth rate limit).

**Known issues / next steps**
- `e2e/player.spec.ts` (new, never run before) fails on desktop at the ArrowLeft-shortcut step: after
  `fill()` the Seek slider keeps focus, so ArrowLeft moves the slider by 1 s instead of the 5 s shortcut.
  The same `<input type=range>` existed before the redesign; the test needs the owner's call (e.g. blur the
  slider first). Everything before that step (play, pause/resume, seek) passed.
- `npm run format:check` flags `e2e/player.spec.ts` (pre-existing, untouched).
- Notifications/Premium were left out of the top bar (no such features). "Soon" items have no pages.

### Follow-up: track detail, artist page, waveform track list

**Done**
- `TrackList` is now a waveform list: number → play on hover, artwork, title, `MiniWaveform`, duration,
  add-to-queue; the waveform wraps under the title on phones.
- `MiniWaveform`: SVG bars from the worker's peaks (never synthesised), fetched when the row nears the
  viewport. It shows live progress for the current track and a hover time preview. Click seeks, or starts
  that track (with the list as queue) at the clicked point. It is a keyboard slider (Enter/Space
  play-pause, arrows ±5 s) and sends commands through the player store only.
- Track page: hero tinted from the artwork hue (artist avatar + link, date, duration, genre chip, big play,
  add to queue, owner status), the waveform card (existing wavesurfer), About, "More from {artist}" (≤ 8,
  this track highlighted), sticky artist card (bio, "View artist").
- Artist page: hero (large avatar, track count · total time, Play all, Shuffle), track list with
  waveforms, About card (bio or placeholder, "On VibeZone since" from `createdAt`).

**Verification (actually run)**
- `npm run typecheck`, `lint`, `format` ✓; `npm test` 32/32; `npm run build` ✓.
- Dev screenshots of both pages at 1440/1024/390: no horizontal overflow, no page errors. A click in the
  middle of a 40 s track's mini waveform started it at ~20 s (one `<audio>`, slider `aria-valuenow` 22/40).
- Playwright, `--workers=1` against the Compose API: 19 passed, 1 skipped, 2 failed. Both failures are
  `player.spec.ts:102` (ArrowLeft shortcut), after the artist-page steps (play from the new list,
  pause/resume, seek) had passed. Desktop: the Seek slider keeps focus after `fill()`. Mobile: the assertion
  expects the time to drop below `beforeShortcut − 2` while playback is only ~1.5 s in. Both are test issues;
  the test was not changed (owner's call).

## 2026-10-20 — Response envelope, mobile playback URLs, M3 integration tests

Owner request: one common API response format, then finish M3 so playback works in the mobile app too.
The owner chose to keep Problem Details for errors and to wrap successes in `{data, meta}`.
Decisions are recorded in ADR-0008. Testing was stopped by the owner before the full run.

**Done**
- Envelope: `ApiResponse<T>` / `ApiListResponse<T>` + `ApiResults` (`VibeZone.Infrastructure.Web`), applied to
  every JSON success response (incl. `/auth/csrf`, token endpoints, upload status, waveform). `PageRequest` /
  `Paged<T>` (SharedKernel); `GET /tracks` (new releases) and `GET /me/tracks` take `page`/`pageSize` (max 50);
  `limit` on `/tracks` was replaced. Web `unwrap()` returns `data`; API types regenerated; tests use `DataAsync()`.
- Mobile playback: `GET /tracks/{id}/playback` → `{streamUrl, waveformUrl, contentType, sizeBytes, durationMs,
  expiresAt}`. The URLs carry `?token=vzp_…` (Data Protection; `Media:PlaybackTokenLifetime`, default 6 h).
  `/stream` and `/waveform` accept the token and re-check visibility as the viewer it was issued to.
  New error code `playback_token_invalid` (vi/en).
- Bug fixes found by the new tests:
  - Worker: the lease heartbeat updates the job row (new xmin) while the processor holds a stale tracked
    entity, so every job running longer than lease/3 (~100 s by default) failed with
    `DbUpdateConcurrencyException` and was retried until `processing_attempts_exhausted`.
    Fix: `JobRowGate` serialises the heartbeat with the commits, and `FindJobAsync` reloads tracked rows.
  - Waveform: AAC padding produced >1000 peaks (1004/1007); extra samples now fold into the last bucket.
- Tests:
  - Integration, `tests/…/Media/`, serial collection:
    - The real worker image is built from `src/VibeZone.Worker/Dockerfile` and reaches Postgres/MinIO
      via `host.docker.internal`.
    - Audio is generated in code (`TestAudio`): WAV tones, corrupt WAV, renamed PNG.
    - Classes: `UploadPipelineTests`, `StreamingTests`, `PlaybackTokenTests`, `WorkerRecoveryTests`
      (docker kill + re-claim), `LargeMediaTests`.
  - Unit: paging, playback tokens, range parsing.
  - E2E: `apps/web/e2e/player.spec.ts`.
- CI:
  - e2e job: MinIO (`docker run`), worker image build + run, Storage env, worker logs on failure.
  - Timeouts raised: backend 40 min, e2e 30 min.
- Docs: ADR-0008, `docs/api/README.md` (response format, just_audio playback), Postman collection
  regenerated (38 requests), security.md §10, AGENTS.md (API responses), README (test prerequisites).

**Verification (actually run)**
- `dotnet build VibeZone.slnx -warnaserror` ✓ (after the last code change; integration test project rebuilt too).
- Unit tests: **76/76 passed**.
- Web: `npm run typecheck`, `lint`, `format:check` ✓; `npm test` **32/32**.
- Media integration tests: 4 failures in the first full run (the worker concurrency bug and the peak
  count). In the second full run (after the fixes), **14/15 passed**:
  - upload → Ready (AAC/MP4, ~1000 peaks);
  - rejections: renamed PNG (`unsupported_file_type`), oversized (declared and while streaming), truncated;
  - corrupt WAV → Failed;
  - owner-only upload/status/cancel;
  - Range 200/206/suffix/clamped/416;
  - private track 404 for others and for anonymous users, including `/playback`;
  - credential-less playback via token with seek; token stops working once the track turns private;
  - tampered, foreign and expired tokens → 401;
  - killed worker → job re-claimed, Completed with 2 attempts.
- The failing test was the 30-minute file. Its first failure was the test's own wrong assumption (a sine
  tone encodes far below 256 kbps, so the rendition is < 50 MB); the test was rewritten. The rewritten
  test then failed on the memory check: streaming 48,105,768 bytes allocated 67,009,704 bytes in the test
  process. The source of these allocations (API, AWS SDK or TestServer) was being investigated when the
  owner stopped testing — **unresolved**.

**Not run**
- Full test suite after the last changes, `dotnet format --verify-no-changes`, Playwright e2e (incl. the new
  player spec), CI on GitHub, Docker image builds for api/web, compose stack.

**Known issues / next steps**
- Find the streaming allocation source. Try a direct `IObjectStorage` read vs. through TestServer, or measure
  the API container under Kestrel. Then fix it, or adjust the check with the owner's agreement.
- Run the e2e player spec. Playwright's bundled Chromium may lack AAC; if so use the Chrome channel.
- Playback tokens are not revoked with the session (bounded by the lifetime, see ADR-0008).
- No Flutter code was run; the Dart snippets in `docs/api/README.md` are illustrative.

## 2026-10-19 — Native-app token auth (Flutter) + Postman collection

Owner request: email + password sign-in for their existing Flutter app (social logins deferred), plus a
Postman collection of every endpoint. Decision recorded in ADR-0007.

**Done**
- `POST /auth/token`, `/auth/token/refresh` (single-use rotation, reuse ⇒ session revoked), `/auth/token/revoke`.
  Opaque Data Protection access tokens (15 min) + SHA-256-stored refresh tokens on `identity.user_sessions`
  (migration `AddRefreshTokens`). Default scheme `vz-auth` picks bearer vs cookie; both validate the session
  and load roles per request (shared `SessionAuthentication.AuthenticateSessionAsync`).
- CSRF: skipped for bearer requests; register/verify/resend/forgot/reset and token endpoints are CSRF-exempt
  (`DisableCsrf()` metadata) so apps can call them; login/logout unchanged.
- `docs/api/VibeZone.postman_collection.json` (36 requests, auto-store + auto-refresh of tokens, Mailpit dev
  helper) generated by `scripts/generate-postman-collection.cjs`; `docs/api/README.md` (Flutter guide).
- Integration tests: `TokenAuthTests` (13). Fixed the M3 regression that broke the whole integration suite
  (required `Storage:*` config missing in `ApiFactory`) and added a real MinIO container (`MinioFixture`,
  same image as compose) so `/health/ready` is tested with storage. `dotnet format` issues in
  `MediaAsset.cs`/`UploadJob.cs` (from M3) fixed. Web: `invalid_refresh_token` message (vi/en), API types regenerated.
- Changed test: `CsrfTests.Unsafe_requests_without_a_token_are_rejected_before_doing_anything` now uses
  login instead of register (register is intentionally CSRF-exempt now); new theory covers the exempt endpoints.

**Verification (actually run)**
- `dotnet build VibeZone.slnx -warnaserror` ✓; `dotnet format --verify-no-changes` ✓.
- `dotnet test --solution VibeZone.slnx` → **119/119 passed** (unit, architecture, integration incl. Postgres,
  Mailpit, MinIO containers).
- Web: `npm run typecheck`, `lint`, `format:check` ✓; `npm test` 32/32.
- Docker stack rebuilt (`migrate` applied `AddRefreshTokens`, api/worker healthy). Curl smoke through the
  proxy with bearer only: register 202 → verify 204 → token 200 → me 200 → artist 201 → track 201 →
  upload 202 → Ready → stream 206 (owner) / 404 (anonymous, private) → refresh 200 → sessions list shows
  the device name → replay old refresh 401 `invalid_refresh_token` → session revoked (new refresh 401,
  access 401 with `WWW-Authenticate: Bearer error="invalid_token"`).
- Newman 6.2.1 ran the collection's own requests in app order against the stack: all 24 returned the
  expected status (waveform/stream 404 `media_not_ready` because they ran ~1 s after upload).

**Not done / limitations**
- No Flutter code was run; the Dart snippet in `docs/api/README.md` is illustrative, not tested.
- Physical devices over Wi-Fi can't reach the loopback-only local stack (emulator / `adb reverse` only).
- Email links open the web app; no mobile deep links yet.
- Leftover local test data from the smoke runs: users `tok*`, `pm*` (and possibly `appuser`) in the local DB.

## 2026-10-19 — M3 code complete (backend + frontend) — NOT TESTED

Owner asked to proceed with M3 without running tests. Only compilation was verified
(`dotnet build VibeZone.slnx -warnaserror` ✓, `has-pending-model-changes` ✓, `docker compose config` ✓).
**No unit/integration/e2e tests were written or run for M3; nothing below has been exercised at runtime.**

**Done (code)**
- Minimal catalog slice needed by media: self-serve `Artist` (+ Artist role via `Identity.Contracts.IUserRoles`),
  `Track` with lifecycle Draft→Processing→Ready/Failed/Cancelled and visibility rules; endpoints
  `/me/artist`, `/tracks` (CRUD, new releases), `/me/tracks`, `/artists/{slug}`.
- `Media` module: `UploadJob` (lease/retry/cancel), `MediaAsset`, audio signature sniff, RFC 9110 range parser;
  `UploadService` (owner check, size limit while spooling, SHA-256, declared-length check, private storage,
  job + track Processing in one commit), `PlaybackService`, `MediaProcessor` (ffprobe gate, AAC 256k
  faststart, waveform peaks, single commit, cancellation-aware); FFmpeg via `ArgumentList`; `MediaWorker`
  (N claim loops, `FOR UPDATE SKIP LOCKED`, heartbeat/lease, cancel detection); endpoints
  `PUT/GET/DELETE /tracks/{id}/upload`, `GET /tracks/{id}/stream` (200/206/416), `GET /tracks/{id}/waveform`.
- `IObjectStorage` (SharedKernel) + S3/MinIO implementation and storage readiness check.
- `VibeZone.Worker` host + Dockerfile (FFmpeg, non-root, heartbeat healthcheck); compose `worker` service,
  MinIO in `app` profile; migration `AddTracksAndMedia` (+ hand-written cross-module FKs).
- Web (frontend pass): global player — Zustand store + one `<audio>` engine (`features/player`), player bar
  (desktop + compact mobile), queue panel, shortcuts (Space/K, ←/→ J/L, M, Shift+N/P), buffering/error/retry,
  Media Session, persisted queue/preferences (rehydrated after mount; never autoplays); waveform bound to the
  engine; track detail, artist profile, Home "new releases"; Upload Studio (become artist with slug suggestion,
  create track, drag-and-drop XHR upload with progress/cancel + CSRF retry, processing polling, cancel, delete);
  Studio in navigation; vi/en messages for all new UI and error codes.
- Frontend checks run: `npm run typecheck` ✓, `npm run lint` ✓ (fixed a React Compiler warning: `useWatch`
  instead of `watch()`), `npm run format:check` ✓, `npm run build` ✓. Unit/e2e tests not run.

**Found by the owner's manual test (compose stack)**
- Large uploads failed: first a 500 after ~31 s (Kestrel "Unexpected end of request content"), then a hang.
  Root cause: Next.js buffers proxied request bodies and forwards only the first 10 MB by default while the
  full `Content-Length` is still declared, so the API waits for bytes that never come (the 30 s was Next's
  default `proxyTimeout` cutting the connection). Excluding `/api` from the `proxy.ts` matcher does **not**
  prevent this buffering (my earlier assumption was wrong). Fix: `experimental.proxyClientMaxBodySize: "210mb"`
  and `proxyTimeout: 15 min` in `apps/web/next.config.ts`; the API now maps a truncated body (IOException)
  to 400 `upload_incomplete` instead of 500. Trade-off: the web server buffers each in-flight upload in
  memory (local only — production routes /api at the reverse proxy). Follow-up (owner approved): Caddy 2.11.7 reverse
  proxy added to Compose (`proxy` service, `infra/caddy/Caddyfile`) on `WEB_PORT`: `/api/*`, `/hubs/*` stream
  straight to the API (210 MB body cap, `flush_interval -1`), the rest to web; web has no host port. Runs as
  nobody, read-only, all capabilities dropped, no admin API, does not trust client X-Forwarded-* headers.
  Verified so far: `caddy validate` ✓, `caddy fmt` ✓, `docker compose config` ✓, `bash -n` + shellcheck on
  the verify script ✓. **Not yet run** in the stack.

**Stack run + manual end-to-end verification (2026-10-19, after adding the proxy)**
- `docker compose --profile app up -d --build --wait`: proxy first crash-looped with
  `exec /usr/bin/caddy: operation not permitted` — the official binary carries a file capability, so
  `cap_drop: ALL` blocks exec. Fixed with `cap_add: [NET_BIND_SERVICE]` (still non-root, port 8080).
- `scripts/verify-local-infra.sh`: all checks pass (incl. proxy healthy/loopback/non-root/no Server header,
  web has no host port, /api routed through the proxy).
- curl smoke through the proxy (real register → Mailpit link → verify → login → artist → track):
  63.5 MB WAV upload → 202 in 4.3 s, all 63,504,078 bytes received; Ready after ~26 s, duration 360000 ms;
  stream 200 `audio/mp4` + `Accept-Ranges`; `Range: bytes=100-199` → 206 `bytes 100-199/11242964`;
  out-of-range → 416 `bytes */11242964`. The owner's own uploads (6:14, 6:35 music files) are Ready too.
- **Bug found and fixed**: the worker stored waveform JSON in PascalCase (default `JsonSerializer`) while the
  contract/web app expect camelCase — the waveform would not render. Worker now writes camelCase; the
  endpoint parses stored documents case-insensitively and re-emits the contract, so existing tracks work
  without reprocessing (verified on all 3 tracks).
- Headless Chromium on the owner's track: playback advances, clicking the waveform seeks (to 3:59), one
  `<audio>` element, playback continues across navigation, no CSP violations (only the expected 401 from
  `/api/v1/me` while anonymous). Fixed: the player bar covered the sidebar's sign-in buttons on desktop.
- Leftover test data: artist `smoke1792398131` with a public "Smoke test" track in the local DB.
- Still **not done**: automated tests (unit/integration/e2e) for M3, a ≥ 30 min file, worker-crash recovery.

**Left for M3**: docker image builds + compose run, and **all tests** (range parser, upload
accept/reject, processing, authz/IDOR, browser playback/seek with a large file).

**Waveform note (verified by reading wavesurfer 8.0.2 source)**: creating WaveSurfer with an external
`media` and only `peaks` calls `setSrc("")`, which removes the shared audio element's `src`. Attach it to
the shared element only for the currently playing track and pass `url` equal to the element's current
`src` (setSrc then returns early); render other tracks without `media`.

## 2026-10-19 — M2a complete (Identity & i18n)

**Decisions (project owner)**: player moves to M3; self-serve artists; OAuth deferred; bilingual UI
(vi default, en); strict nonce CSP with per-request rendering (ADR-0006).

**Done**
- Backend: `Identity` module (Domain: `Handle`, `UserSession`, `UserProfile`; Application:
  `AccountService`, `SessionService`, `ProfileService` behind ports; Infrastructure: Identity stores
  on `AppDbContext`, session store with cache, MailKit email queue, vi/en templates, 1 h reset tokens);
  endpoints `/api/v1/auth/*`, `/api/v1/me/*`; antiforgery filter + `/auth/csrf`; auth rate-limit policy;
  Data Protection keys in Postgres; migration `AddIdentity` (roles as reference data).
- Web: next-intl routing (`/vi`, `/en`), translated catalogues, auth pages, settings (profile,
  language, password, sessions), account menu, language switcher, CSRF-aware API client with
  single retry, nonce CSP in `proxy.ts`, `get-nonce` for runtime styles.
- Compose `app` profile: API sends mail to Mailpit, links to `localhost:3000`, local cookie mode.
- CI: Mailpit service for e2e.

**Verification (actually run)**
- `dotnet build -warnaserror` ✓; `dotnet format --verify-no-changes` ✓; no pending model changes;
  no vulnerable packages (after pinning `System.Security.Cryptography.Pkcs` 10.0.13 — NuGet audit
  failed the build on MailKit's transitive 10.0.0, GHSA-846f-f764-76mx).
- `dotnet test`: unit **49/49**, architecture **10/10**, integration **40/40** (real Postgres +
  Mailpit: registration in en/vi, duplicate-email indistinguishability, field error codes, tampered
  token, generic login errors, lock-out, cookie attributes, remember-me, auth rate limit, reset →
  global sign-out + single-use token, change password, sessions list/revoke/IDOR, profile update,
  CSRF rejections, plain-HTTP local mode).
- Web: lint ✓, typecheck ✓, prettier ✓, vitest **32/32** (CSRF client, forms, i18n parity), build ✓
  (all routes dynamic), `npm audit --omit=dev` 0.
- Playwright **19 passed, 1 skipped by design**, twice in a row (desktop + mobile): full account
  lifecycle in en and vi via Mailpit links, reset flow, language switcher, CSP header/nonce
  uniqueness, no CSP violations, 404s.
- Compose stack rebuilt: `verify-local-infra.sh` all checks pass (incl. CSRF via web origin, CSP
  nonce); real browser flow on `localhost:3000` (Production mode): Vietnamese email, link to
  `localhost:3000/vi/verify-email`, sign-in, settings — 0 CSP violations.

**Problems found and fixed during verification**
- OpenAPI document went stale when only module code changed (SDK target only watches the API dll) →
  regeneration forced on every API build.
- Antiforgery threw on plain-HTTP localhost (integration tests use HTTPS so didn't catch it) →
  `Security:SecureCookies` switch + integration tests for both modes.
- Root `loading.tsx` turned 404s into 200s (Suspense flushed before `notFound()`) → removed.
- Radix runtime `<style>` blocked by CSP → nonce passed via `get-nonce`.
- After server-side session revocation the identity-bound CSRF token is stale → client refetches
  and retries once.
- Npgsql GSSAPI probing logged `libgssapi_krb5` errors in containers → GSS encryption disabled by default.
- Parallel e2e with 10 browsers starved the 8 GB machine (2-min hangs) → 3 local workers.

**Known issues / limitations**: see security.md §9 (insecure-cookie switch for local HTTP, per-instance
revocation/rate limits, in-memory email queue, unencrypted key ring, no MFA/HIBP). Session list shows
raw user-agent strings. Resource-authorization helpers arrive with the first owned resources (M2b).

**Next (M2b)**: artists (self-serve), tracks (metadata, visibility, status), albums, genres on
tracks, Postgres FTS search, Home/Discover/Search/Track/Artist/Album pages, labelled dev seed.

## 2026-10-19 — M1: full stack in Docker Compose

**Done**: `app` profile in `docker-compose.yml` — `migrate` (EF Core migration bundle, new
Dockerfile target `migrator`), `api`, `web`, with dependency ordering and healthchecks;
`API_PORT`/`WEB_PORT`/`API_ENVIRONMENT` in `.env.example`; `verify-local-infra.sh` now checks the
application containers; README "Run everything with Docker Compose".

**Verification (actually run)**
- Isolated project (`-p vibezone-verify`, separate ports/volume, empty DB):
  `docker compose --profile app up -d --build --wait` → exit 0 in 145 s; `migrate` applied
  `InitialCatalog` and exited 0; api/web healthy; 23 genres through the web container.
  Re-running `migrate` → "No migrations were applied". Stopping Postgres → API container
  unhealthy after 23 s (`/health/ready` 503); restarting → healthy after 3 s. Project removed
  with `down -v` (its own volume only).
- Real project on default ports: same command → all healthy; `verify-local-infra.sh` →
  **37/37 checks passed** (incl. migrate exit 0, non-root api/web, loopback-only ports, web → api
  proxy). Static CSS/JS from the web container return 200.

**Fixed during verification**: API healthcheck initially used `retries: 12` / `timeout: 5s`, so a
lost database took ~60 s to surface (and probes timed out at the same 5 s as the app's DB check).
Now `timeout: 8s`, `retries: 3`, `start_period: 30s`.

## 2026-10-19 — M1 complete (Foundation)

**Done**
- Backend: `VibeZone.slnx` (central package management, warnings-as-errors, analyzers),
  SharedKernel, Infrastructure building block (module contract, `AppDbContext` composed from
  modules, Serilog, OpenTelemetry, DB health check), `Catalog` module (genres: domain,
  query port, EF implementation, endpoint), `VibeZone.Migrations` (migration `InitialCatalog`
  with 23 genre reference rows), API host (Problem Details, security headers, rate limiting,
  trusted-proxy forwarded headers, health endpoints, build-time OpenAPI → `openapi/vibezone-api.json`).
- Scripts: `setup-dev-secrets.sh` (`.env` → user-secrets), `db-migrate.sh`; `dotnet-ef` tool manifest.
- Tests: `VibeZone.UnitTests` (16), `VibeZone.ArchitectureTests` (6, ArchUnitNET),
  `VibeZone.IntegrationTests` (9, WebApplicationFactory + Testcontainers Postgres 18.6).
- Frontend `apps/web`: Next.js 16.4.2 / React 19.3 / Tailwind 4.3 / shadcn (Radix), dark design
  tokens, app shell (sidebar, mobile top bar + bottom nav, skip link, player slot), shared
  loading/empty/error states, route loading/error/not-found/global-error, typed API client
  (openapi-fetch) + TanStack Query, Home with live genre browser. Vitest/RTL + Playwright.
- Dockerfiles (API, web), `.github/workflows/ci.yml`, docs updated.

**Verification (actually run)**
- `dotnet build VibeZone.slnx -warnaserror` (Debug and Release) → succeeded.
- `dotnet test --solution VibeZone.slnx` → **31/31 passed** (unit 16, architecture 6, integration 9).
- `dotnet format --verify-no-changes` → clean. `dotnet ef migrations has-pending-model-changes` → none.
  `dotnet list package --vulnerable --include-transitive` → no vulnerable packages.
- Architecture tests proven to fail: injected a Domain → `DbContext` dependency and a public
  Infrastructure class → 2 failures with precise messages; changes reverted.
- `bash scripts/db-migrate.sh` on the compose Postgres → `InitialCatalog` applied, 23 genres.
- Running API: `/health/live` 200; `/health/ready` 200 → **503 in 0.26 s after `docker compose stop
  postgres`** (live stays 200, genres → 500 Problem Details without details) → 200 after restart.
  Logs JSON with `@tr`/`@sp`; DB password not present in logs.
- OTLP: API with `OTEL_EXPORTER_OTLP_ENDPOINT` → OpenTelemetry Collector received 7 spans and
  metric batches.
- Frontend: `npm run lint` (0 warnings), `npm run typecheck`, `npm run format:check`,
  `npm test` (**5/5**), `npm run build` → all green.
- `npx playwright test` against the real API + Postgres and the standalone production build →
  **7 passed, 1 skipped by design** (keyboard test runs on desktop only); covers genres through
  the same-origin `/api` rewrite, 404 page, skip-link focus, security headers.
- Screenshots reviewed (desktop, mobile, forced API error state).
- `docker build` of both images succeeded; containers run as UID 1654 / `node`; web container →
  rewrite → API container → Postgres returned genres; `/openapi` is 404 in Production.
- `actionlint` on the workflow → clean. **CI has not run on GitHub** (no remote yet).

**Decisions / deviations**
- shadcn init added the `cn` package (v0.5.0, published days earlier); replaced with the
  established `clsx` + `tailwind-merge` to keep dependencies mature.
- xUnit v3 (4.x) requires Microsoft.Testing.Platform; enabled via `global.json` `test.runner`.
  Use `dotnet test --solution VibeZone.slnx`.
- Genre taxonomy is reference data seeded through the migration (documented in data-model.md);
  no demo data exists anywhere.
- `next.config.ts` rewrites are fixed at build time; production should route `/api` at the reverse
  proxy (ADR-0002). The web image defaults the rewrite target to `http://api:8080`.

**Known issues / limitations**
- Web Content-Security-Policy deferred to M2 (nonce-based, with auth).
- `npm audit` (dev deps only): `braces` GHSA-vfj7-8cjw-p6xm via `eslint-config-next`/`shadcn`,
  no patched release upstream; production audit clean. See security.md §8.
- npm reports `unrs-resolver` postinstall not approved by `allowScripts`; lint works without it.
- No Home content beyond genres: tracks/artists need M2 — no placeholders were added.

**Next (M2)**: Identity (register/login/logout, email verification via Mailpit, password reset,
sessions), cookie + antiforgery, roles; Catalog (artists, tracks, albums), search, playlists,
global audio player, remaining pages.

## 2026-10-19 — M0 complete

**Environment found**
- Repository: empty directory, not a git repository.
- Git 2.55, .NET SDK 10.0.302 (+ 9.0.317), Docker 29.6.2 / Compose v5.3.1 (Docker Desktop was
  installed but stopped — started it; VM has 20 CPUs / ~3.9 GB RAM).
- Node: nvm-windows with only **Node 12.16.1** (too old for Next.js). Installed Node 22.23.3 and
  **24.22.0 (Active LTS)** via nvm; did **not** switch the global default (`nvm use`) to avoid
  affecting other projects. Project pins 24 in `.nvmrc`.
- Not installed on host: ffmpeg, psql, redis-cli, gh (not needed: ffmpeg will live in the worker
  image; DB/Redis tools are used via containers).

**Done**
- `git init -b main` (nothing committed yet), repo hygiene files, `global.json`, `.nvmrc`.
- `docker-compose.yml` (Postgres 18.6, Redis 8.8.3, MinIO community build, Mailpit 1.31.6) with
  profiles, healthchecks, loopback-only ports, `no-new-privileges`, required-secret interpolation.
- MinIO bootstrap: private bucket, bucket-scoped policy `vibezone-media-rw`, app user.
- `scripts/verify-local-infra.sh`.
- Docs: architecture overview, data model, threat model, operations, ADR-0001…0005, AGENTS.md,
  CLAUDE.md, README.md, IMPLEMENTATION_PLAN.md.

**Decision taken with project owner**
- Upstream MinIO images are no longer public (`minio/minio` removed from Docker Hub,
  `quay.io/minio/*` → 401). Owner chose `pgsty/minio` (pinned). Recorded in ADR-0003.

**Verification (actually run)**
- `docker compose --profile "*" up -d --wait` → postgres, redis, minio, mailpit healthy;
  `minio-init` exited 0; re-running `minio-init` is idempotent (exit 0).
- `bash scripts/verify-local-infra.sh` → **26/26 checks passed**: `.env` ignored/untracked,
  compose valid, Postgres 18 / SCRAM / data checksums / `gen_random_uuid()`, Redis auth required,
  MinIO anonymous listing → 403, app user can put/get/delete in its bucket but cannot create
  buckets or use admin APIs, Mailpit API reachable, all ports bound to 127.0.0.1.
- Negative checks: with Postgres stopped the script reports FAIL and exits 1;
  `docker compose --env-file .env.example config` fails with "required variable POSTGRES_PASSWORD
  is missing a value".

**Known issues / limitations**
- MinIO image maintained by a third party (ADR-0003).
- Redis password visible via `docker inspect` (local-only acceptable).
- Local Postgres user is a superuser; separate migration/runtime roles planned for production.
- No CI yet (M1). No application code yet.

**Next (M1)**: .NET solution + API host + baseline migration + health/logging/OTel; Next.js app
shell with design tokens; tests (unit, Testcontainers integration, architecture); Dockerfiles; CI.
