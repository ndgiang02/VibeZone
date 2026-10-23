// Generates docs/api/VibeZone.postman_collection.json (Postman v2.1). Run: node scripts/generate-postman-collection.cjs
const fs = require("fs");
const path = require("path");
const out = path.join(__dirname, "..", "docs/api/VibeZone.postman_collection.json");

const json = (obj) => ({ mode: "raw", raw: JSON.stringify(obj, null, 2), options: { raw: { language: "json" } } });
const noauth = { type: "noauth" };
const lines = (s) => s.trim().split("\n");
const test = (src) => [{ listen: "test", script: { type: "text/javascript", exec: lines(src) } }];

const saveTokens = `
pm.test("status 200", () => pm.response.to.have.status(200));
if (pm.response.code === 200) {
  const t = pm.response.json().data;
  pm.collectionVariables.set("accessToken", t.accessToken);
  pm.collectionVariables.set("refreshToken", t.refreshToken);
  pm.collectionVariables.set("accessTokenExpiresAt", String(Date.now() + t.expiresIn * 1000));
}`;

function req(name, method, urlPath, { body, auth, headers = [], query = [], description, events } = {}) {
  const segments = urlPath.replace(/^\//, "").split("/");
  return {
    name,
    event: events,
    request: {
      method,
      auth,
      header: [...(body && body.mode === "raw" ? [{ key: "Content-Type", value: "application/json" }] : []), ...headers],
      body,
      url: {
        raw: "{{baseUrl}}" + urlPath + (query.length ? "?" + query.map((q) => `${q.key}=${q.value}`).join("&") : ""),
        host: ["{{baseUrl}}"],
        path: segments,
        query: query.length ? query : undefined,
      },
      description,
    },
  };
}

const collection = {
  info: {
    name: "VibeZone API",
    schema: "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    description: lines(`
VibeZone REST API v1 for native apps (Flutter) — see docs/api/README.md and docs/adr/0007-native-app-token-auth.md.

Quick start (local Docker stack, \`docker compose --profile app up -d\`):
1. Set the collection variables \`email\`, \`password\`, \`handle\` (or keep the defaults).
2. Auth (app) → Register → "DEV: read verification email (Mailpit)" → Verify email → Sign in (token).
3. Every other request then sends \`Authorization: Bearer {{accessToken}}\` (collection auth).
   The collection pre-request script refreshes the access token automatically when it is about to expire.

Rules the app must follow:
- Access token: 15 min. On 401 with \`WWW-Authenticate: Bearer error="invalid_token"\` → refresh once, retry.
- Refresh token: single use, rotates every refresh. Run at most ONE refresh at a time (single-flight);
  reusing an old refresh token signs the device out. On 401 \`invalid_refresh_token\` → go to the login screen.
- Store the refresh token in secure storage (flutter_secure_storage). Never log tokens.
- Errors are RFC 9457 Problem Details with a stable \`code\` (and \`errors[field] = [code]\` for validation).
- Bearer requests need no CSRF token. Cookie/CSRF requests in the "Web (cookie) only" folder are for browsers.
`),
  },
  auth: { type: "bearer", bearer: [{ key: "token", value: "{{accessToken}}", type: "string" }] },
  event: [
    {
      listen: "prerequest",
      script: {
        type: "text/javascript",
        exec: lines(`
// Auto-refresh: if the access token expires within 30 s and we have a refresh token, rotate first.
const url = pm.request.url.toString();
const refresh = pm.collectionVariables.get("refreshToken");
const expiresAt = Number(pm.collectionVariables.get("accessTokenExpiresAt") || 0);
if (refresh && !url.includes("/auth/") && Date.now() > expiresAt - 30000) {
  pm.sendRequest({
    url: pm.collectionVariables.get("baseUrl") + "/api/v1/auth/token/refresh",
    method: "POST",
    header: { "Content-Type": "application/json" },
    body: { mode: "raw", raw: JSON.stringify({ refreshToken: refresh }) },
  }, (err, res) => {
    if (!err && res.code === 200) {
      const t = res.json().data;
      pm.collectionVariables.set("accessToken", t.accessToken);
      pm.collectionVariables.set("refreshToken", t.refreshToken);
      pm.collectionVariables.set("accessTokenExpiresAt", String(Date.now() + t.expiresIn * 1000));
    } else {
      console.warn("Refresh failed — sign in again", err || res.code);
      pm.collectionVariables.set("accessToken", "");
      pm.collectionVariables.set("refreshToken", "");
    }
  });
}`),
      },
    },
  ],
  variable: [
    { key: "baseUrl", value: "http://localhost:3001", description: "API site (ADR-0011): local stack port 3001 (Android emulator: http://10.0.2.2:3001); production https://api.<domain>." },
    { key: "mailpitUrl", value: "http://localhost:8025", description: "Local Mailpit (dev only) to read verification/reset emails." },
    { key: "email", value: "app-user@example.test" },
    { key: "password", value: "correct horse battery staple" },
    { key: "handle", value: "appuser" },
    { key: "deviceName", value: "Postman" },
    { key: "accessToken", value: "" },
    { key: "refreshToken", value: "" },
    { key: "accessTokenExpiresAt", value: "0" },
    { key: "userId", value: "" },
    { key: "verifyToken", value: "" },
    { key: "resetToken", value: "" },
    { key: "newPassword", value: "another long passphrase 2" },
    { key: "artistSlug", value: "appuser" },
    { key: "artistId", value: "", description: "Set by \"My artist profile\" / \"Become an artist\"" },
    { key: "trackId", value: "" },
    { key: "albumId", value: "", description: "Set by \"Create album\"" },
    { key: "playlistId", value: "", description: "Set by \"Create playlist\"" },
    { key: "q", value: "son tung", description: "Search text (accents optional)" },
    { key: "streamUrl", value: "", description: "Set by \"Playback URLs\" (relative, includes the token)" },
    { key: "waveformUrl", value: "" },
    { key: "sessionId", value: "" },
    { key: "uploadLength", value: "0", description: "Exact size in bytes of the file selected in 'Upload audio'." },
    { key: "csrfToken", value: "" },
    { key: "commentId", value: "", description: "Set by \"Post a comment\"" },
    { key: "reportId", value: "", description: "Pick one from \"Report queue\"" },
    { key: "notificationId", value: "" },
    { key: "targetUserId", value: "", description: "Admin: the user whose roles you change" },
    { key: "playSessionId", value: "", description: "Set by \"Report playback\" (one per track start)" },
  ],
  item: [
    {
      name: "Auth (app)",
      description: "Account + token endpoints. No cookies, no CSRF.",
      item: [
        req("Register", "POST", "/api/v1/auth/register", {
          auth: noauth,
          body: json({ email: "{{email}}", handle: "{{handle}}", password: "{{password}}", displayName: "App User", locale: "vi" }),
          description: "202 for any valid input (anti-enumeration): a verification email — or an 'account exists' email — follows. handle: 3–30 chars a-z 0-9 _ -; password ≥ 10 chars; locale vi|en.",
          events: test(`pm.test("202 Accepted", () => pm.response.to.have.status(202));`),
        }),
        req("DEV: read verification email (Mailpit)", "GET", "/api/v1/search", {
          auth: noauth,
          description: "Local development helper only: finds the newest email to {{email}} in Mailpit and stores userId + token. In the app the user taps the link in the email (https://<web>/{locale}/verify-email?userId=…&token=…) — handle it as a deep link and call Verify email.",
          query: [{ key: "query", value: "to:\"{{email}}\"" }],
          events: test(`
const list = pm.response.json().messages || [];
pm.test("email found", () => pm.expect(list.length).to.be.above(0));
if (list.length) {
  pm.sendRequest(pm.collectionVariables.get("mailpitUrl") + "/api/v1/message/" + list[0].ID, (err, res) => {
    // The Postman sandbox has no URL global: read the query values with a regex.
    const link = res.json().Text.match(/https?:\\/\\/\\S+/)[0];
    const param = (name) => { const m = link.match(new RegExp("[?&]" + name + "=([^&\\\\s]+)")); return m ? decodeURIComponent(m[1]) : ""; };
    if (param("userId")) pm.collectionVariables.set("userId", param("userId"));
    if (link.includes("verify-email")) pm.collectionVariables.set("verifyToken", param("token"));
    if (link.includes("reset-password")) pm.collectionVariables.set("resetToken", param("token"));
    pm.test("link parsed", () => pm.expect(param("token")).to.not.be.empty);
  });
}`),
        }),
        req("Verify email", "POST", "/api/v1/auth/verify-email", {
          auth: noauth,
          body: json({ userId: "{{userId}}", token: "{{verifyToken}}" }),
          description: "204. Idempotent. 400 token_invalid.",
          events: test(`pm.test("204", () => pm.response.to.have.status(204));`),
        }),
        req("Resend verification", "POST", "/api/v1/auth/resend-verification", {
          auth: noauth,
          body: json({ email: "{{email}}" }),
          description: "Always 202.",
        }),
        req("Sign in (token)", "POST", "/api/v1/auth/token", {
          auth: noauth,
          body: json({ email: "{{email}}", password: "{{password}}", deviceName: "{{deviceName}}" }),
          description: "200 → {data: {accessToken, tokenType: 'Bearer', expiresIn: 900, refreshToken, refreshTokenExpiresAt}}. 401 invalid_credentials (wrong email/password or locked out — indistinguishable). 403 email_not_confirmed. 429 too many attempts. deviceName is shown in the signed-in devices list.",
          events: test(saveTokens),
        }),
        req("Refresh token", "POST", "/api/v1/auth/token/refresh", {
          auth: noauth,
          body: json({ refreshToken: "{{refreshToken}}" }),
          description: "Returns a NEW pair; the old refresh token stops working. 401 invalid_refresh_token → sign in again. Never run two refreshes in parallel.",
          events: test(saveTokens),
        }),
        req("Sign out (revoke refresh token)", "POST", "/api/v1/auth/token/revoke", {
          auth: noauth,
          body: json({ refreshToken: "{{refreshToken}}" }),
          description: "204, idempotent. Ends the session; the access token stops working within seconds. Then delete both tokens on the device.",
          events: test(`
pm.test("204", () => pm.response.to.have.status(204));
pm.collectionVariables.set("accessToken", "");
pm.collectionVariables.set("refreshToken", "");`),
        }),
        req("Sign out (bearer)", "POST", "/api/v1/auth/logout", {
          description: "Alternative sign-out using the access token. 204, idempotent.",
        }),
        req("Forgot password", "POST", "/api/v1/auth/forgot-password", {
          auth: noauth,
          body: json({ email: "{{email}}" }),
          description: "Always 202. The email links to https://<web>/{locale}/reset-password?email=…&token=… (1 h). Use the Mailpit helper above to grab resetToken locally.",
        }),
        req("Reset password", "POST", "/api/v1/auth/reset-password", {
          auth: noauth,
          body: json({ email: "{{email}}", token: "{{resetToken}}", newPassword: "{{newPassword}}" }),
          description: "204. Signs out ALL sessions (web and app). 400 token_invalid, password_too_short, password_too_long.",
        }),
      ],
    },
    {
      name: "Account",
      description: "Requires Authorization: Bearer.",
      item: [
        req("Me", "GET", "/api/v1/me", {
          description: "data: {id, email, handle, displayName, locale, emailConfirmed, roles[], avatar: {small, large} | null}. 401 when not signed in.",
          events: test(`
pm.test("200", () => pm.response.to.have.status(200));
if (pm.response.code === 200) pm.collectionVariables.set("userId", pm.response.json().data.id);`),
        }),
        req("Update profile", "PATCH", "/api/v1/me/profile", { body: json({ displayName: "New name", locale: "en" }) }),
        req("Change password", "POST", "/api/v1/me/password", {
          body: json({ currentPassword: "{{password}}", newPassword: "{{newPassword}}" }),
          description: "204. Signs out every OTHER session (including other app devices); this one stays valid.",
        }),
        req("List sessions (devices)", "GET", "/api/v1/me/sessions", {
          description: "data[]: {id, createdAt, lastSeenAt, userAgent (deviceName for apps), isCurrent}; meta {page, pageSize, total, hasMore}.",
          events: test(`
const other = (pm.response.json().data || []).find((s) => !s.isCurrent);
if (other) pm.collectionVariables.set("sessionId", other.id);`),
        }),
        req("Revoke a session", "DELETE", "/api/v1/me/sessions/{{sessionId}}", { description: "204; 404 session_not_found." }),
        req("Revoke other sessions", "POST", "/api/v1/me/sessions/revoke-others", { description: "200 {data: {revoked: n}}." }),
        req("Set account picture", "PUT", "/api/v1/me/avatar", {
          headers: [{ key: "Content-Type", value: "image/jpeg" }],
          body: { mode: "file", file: { src: "" } },
          description: "Any signed-in user (not only artists). Raw body = the image (JPEG/PNG/WebP, ≥ 200×200 px, ≤ 10 MB); cropped square, metadata removed. 200 → data: {small, large} URLs (also in Me.avatar). Errors: image_invalid, image_too_small, image_too_large. Rate limit: 30/hour per IP.",
        }),
        req("Account picture", "GET", "/api/v1/users/{{userId}}/avatar/small", {
          auth: noauth,
          description: "image/jpeg, public. 'small' or 'large'. Prefer the URLs in Me.avatar / playlist owner (they change when the picture changes). 404 without a picture.",
        }),
        req("Remove account picture", "DELETE", "/api/v1/me/avatar", { description: "204, idempotent." }),
      ],
    },
    {
      name: "Catalog",
      item: [
        req("Genres", "GET", "/api/v1/genres", { auth: noauth, description: "Public reference data: data[] {slug, name}, meta." }),
        req("New releases", "GET", "/api/v1/tracks", { auth: noauth, query: [{ key: "page", value: "1" }, { key: "pageSize", value: "20" }], description: "Public, ready tracks, newest first. data[] + meta {page, pageSize, total, hasMore}; load the next page while hasMore." }),
        req("Track by id", "GET", "/api/v1/tracks/{{trackId}}", {
          description: "Public/unlisted tracks for everyone; private/processing tracks only for the owner (else 404). Includes status, durationMs, playable.",
        }),
        req("Artist by slug", "GET", "/api/v1/artists/{{artistSlug}}", { auth: noauth, description: "Artist profile + public tracks. 404 if unknown." }),
        req("My artist profile", "GET", "/api/v1/me/artist", {
          description: "404 if the user has not created one yet. data.avatar / data.banner = {small, large} URLs or null.",
          events: test(`if (pm.response.code === 200) pm.collectionVariables.set("artistId", pm.response.json().data.id);`),
        }),
        req("Become an artist", "POST", "/api/v1/me/artist", {
          body: json({ bio: null }),
          description: "201. Name, URL slug and picture are the account's display name, handle and picture (ADR-0013) — set artistSlug to your handle. Grants the Artist role (needed to create tracks). 409 artist_exists if the user already has one.",
          events: test(`if (pm.response.code === 201) pm.collectionVariables.set("artistId", pm.response.json().data.id);`),
        }),
        req("My tracks", "GET", "/api/v1/me/tracks", { query: [{ key: "page", value: "1" }, { key: "pageSize", value: "50" }], description: "The artist's own tracks, any status, newest first (paged)." }),
        req("Create track", "POST", "/api/v1/tracks", {
          body: json({ title: "My first track", description: null, genreSlug: "ambient", visibility: "Private" }),
          description: "201 → track in status Draft (no audio). Prefer Media → 'Upload a new track', which does it in one request. visibility: Public | Unlisted | Private. 403 if the user is not an artist.",
          events: test(`
pm.test("201", () => pm.response.to.have.status(201));
if (pm.response.code === 201) pm.collectionVariables.set("trackId", pm.response.json().data.id);`),
        }),
        req("Update track", "PATCH", "/api/v1/tracks/{{trackId}}", {
          body: json({ title: "Renamed track", description: "Plain text", genreSlug: "ambient", visibility: "Public" }),
          description: "Owner only (others get 404). Send the full object.",
        }),
        req("Delete track", "DELETE", "/api/v1/tracks/{{trackId}}", { description: "204. Owner only." }),
      ],
    },
    {
      name: "Albums",
      item: [
        req("Create album", "POST", "/api/v1/albums", {
          body: json({ title: "My first EP", kind: "Ep", description: null, releaseDate: "2026-10-20", visibility: "Public" }),
          description: "Artist only. kind: Album | Ep | Single; visibility: Public | Unlisted | Private. 201 → data: AlbumDto.",
          events: test(`
pm.test("201", () => pm.response.to.have.status(201));
if (pm.response.code === 201) pm.collectionVariables.set("albumId", pm.response.json().data.id);`),
        }),
        req("Album", "GET", "/api/v1/albums/{{albumId}}", { auth: noauth, description: "Album + tracks the viewer can see, in order. Cover = its first track cover. Private: owner only (404)." }),
        req("Set album tracklist", "PUT", "/api/v1/albums/{{albumId}}/tracks", {
          body: json({ trackIds: ["{{trackId}}"] }),
          description: "Owner only. The complete tracklist in order (your own tracks, ≤ 100). Tracks left out leave the album.",
        }),
        req("Update album", "PATCH", "/api/v1/albums/{{albumId}}", {
          body: json({ title: "My first EP (deluxe)", kind: "Ep", description: null, releaseDate: "2026-10-20", visibility: "Public" }),
          description: "Owner only. Send the full object.",
        }),
        req("My albums", "GET", "/api/v1/me/albums", { description: "The signed-in artist's albums (any visibility)." }),
        req("Delete album", "DELETE", "/api/v1/albums/{{albumId}}", { description: "204. Tracks are kept." }),
      ],
    },
    {
      name: "Search",
      item: [
        req("Search (overview)", "GET", "/api/v1/search", {
          auth: noauth,
          query: [{ key: "q", value: "{{q}}" }],
          description: "Top 6 tracks, artists and albums with totals. Accent-insensitive (\"son tung\" finds \"Sơn Tùng\"), prefix and typo tolerant. Only listed content (Ready public tracks, public albums).",
        }),
        req("Search tracks", "GET", "/api/v1/search/tracks", {
          auth: noauth,
          query: [{ key: "q", value: "{{q}}" }, { key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Paged: data[] TrackDto + meta {page, pageSize, total, hasMore}.",
        }),
        req("Search artists", "GET", "/api/v1/search/artists", {
          auth: noauth,
          query: [{ key: "q", value: "{{q}}" }, { key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Paged: data[] ArtistDto.",
        }),
        req("Search albums", "GET", "/api/v1/search/albums", {
          auth: noauth,
          query: [{ key: "q", value: "{{q}}" }, { key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Paged: data[] AlbumDto (public albums only).",
        }),
        req("Search playlists", "GET", "/api/v1/playlists", {
          auth: noauth,
          query: [{ key: "q", value: "{{q}}" }, { key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Public playlists with tracks; without q: newest first.",
        }),
      ],
    },
    {
      name: "Playlists",
      item: [
        req("Create playlist", "POST", "/api/v1/playlists", {
          body: json({ title: "Chill", description: null, visibility: "Private" }),
          description: "Any signed-in user. visibility: Public | Private. 201 → data: PlaylistDto.",
          events: test(`
pm.test("201", () => pm.response.to.have.status(201));
if (pm.response.code === 201) pm.collectionVariables.set("playlistId", pm.response.json().data.id);`),
        }),
        req("Add track", "POST", "/api/v1/playlists/{{playlistId}}/tracks", {
          body: json({ trackId: "{{trackId}}" }),
          description: "Owner only; any track you can see. Already there → no-op. 400 playlist_full at 500.",
        }),
        req("Playlist", "GET", "/api/v1/playlists/{{playlistId}}", { description: "Private: owner only (404). coverUrls = up to 4 track covers for a mosaic." }),
        req("Playlist tracks", "GET", "/api/v1/playlists/{{playlistId}}/tracks", { description: "In order, without tracks the viewer can't see." }),
        req("Reorder", "PUT", "/api/v1/playlists/{{playlistId}}/tracks/order", {
          body: json({ trackIds: ["{{trackId}}"] }),
          description: "Owner only. The tracks you see, in their new order; they swap among their current positions (hidden tracks keep theirs).",
        }),
        req("Remove track", "DELETE", "/api/v1/playlists/{{playlistId}}/tracks/{{trackId}}", { description: "204." }),
        req("My playlists", "GET", "/api/v1/me/playlists", {
          query: [{ key: "containsTrack", value: "{{trackId}}", description: "Optional: fills containsTrack on each" }],
          description: "Recently updated first (paged).",
        }),
        req("Update playlist", "PATCH", "/api/v1/playlists/{{playlistId}}", {
          body: json({ title: "Chill vibes", description: "Late night", visibility: "Public" }),
          description: "Owner only. Send the full object.",
        }),
        req("Delete playlist", "DELETE", "/api/v1/playlists/{{playlistId}}", { description: "204." }),
      ],
    },
    {
      name: "Media",
      item: [
        req("Upload a new track (details + audio + cover)", "POST", "/api/v1/track-uploads", {
          body: {
            mode: "formdata",
            formdata: [
              { key: "title", value: "My new track", type: "text" },
              { key: "visibility", value: "Private", type: "text", description: "Public | Unlisted | Private" },
              { key: "genreSlug", value: "ambient", type: "text", description: "Optional" },
              { key: "description", value: "", type: "text", disabled: true, description: "Optional" },
              { key: "artwork", type: "file", src: "", description: "Optional cover: JPEG/PNG/WebP, ≥ 200×200 px, ≤ 10 MB" },
              { key: "audio", type: "file", src: "", description: "Required. Send it last." },
            ],
          },
          description: lines(`
One request creates the track and starts processing — all or nothing (on any error no track exists).
multipart/form-data with text fields first, then artwork (optional), then audio. 202 → data: upload status; Location: /api/v1/tracks/{id}.
Errors (400, field in errors): track_title_invalid, track_description_too_long, track_visibility_invalid, genre_unknown, audio_required, unsupported_file_type, file_too_large, artwork_invalid, artwork_too_small, artwork_too_large; multipart_required / multipart_invalid; 403 artist_required.
Flutter (dio): FormData.fromMap({'title': …, 'visibility': 'Public', 'artwork': await MultipartFile.fromFile(coverPath), 'audio': await MultipartFile.fromFile(path)}) with onSendProgress.
Rate limit: 30 uploads/hour per IP.`).join("\n"),
          events: test(`
pm.test("202", () => pm.response.to.have.status(202));
if (pm.response.code === 202) pm.collectionVariables.set("trackId", pm.response.json().data.trackId);`),
        }),
        req("Set / replace cover", "PUT", "/api/v1/tracks/{{trackId}}/artwork", {
          headers: [{ key: "Content-Type", value: "image/jpeg" }],
          body: { mode: "file", file: { src: "" } },
          description: "Owner only. Raw body = the image (JPEG/PNG/WebP, ≥ 200×200 px, ≤ 10 MB). 200 → data: {small, large} URLs. Cropped to a square; metadata removed.",
        }),
        req("Remove cover", "DELETE", "/api/v1/tracks/{{trackId}}/artwork", { description: "204. Owner only." }),
        req("Cover image", "GET", "/api/v1/tracks/{{trackId}}/artwork/small", {
          description: "image/jpeg. 'small' (≈320 px) or 'large' (≈1200 px). Use the URLs in track.artwork (they change when the cover changes). Anyone who can see the track; 404 otherwise or without a cover.",
        }),
        req("Set artist banner", "PUT", "/api/v1/me/artist/banner", {
          headers: [{ key: "Content-Type", value: "image/jpeg" }],
          body: { mode: "file", file: { src: "" } },
          description: "Signed-in artist. Raw body = image (JPEG/PNG/WebP, 3:1, ≥ 600×200, ≤ 10 MB); 800/1600 px wide renditions. 200 → data: {small, large}. Errors: image_invalid, image_too_small, image_too_large; 404 artist_not_found. The artist picture is the account picture (PUT /me/avatar).",
        }),
        req("Remove artist banner", "DELETE", "/api/v1/me/artist/banner", { description: "204." }),
        req("Artist banner", "GET", "/api/v1/artists/{{artistId}}/banner/small", {
          auth: noauth,
          description: "image/jpeg, public. /banner/{small|large}. Prefer the URLs in ArtistDto.banner. 404 without a banner. The artist picture is the account picture (ArtistDto.avatar).",
        }),
        req("Upload audio", "PUT", "/api/v1/tracks/{{trackId}}/upload", {
          headers: [
            { key: "Content-Type", value: "application/octet-stream" },
            { key: "X-Upload-Length", value: "{{uploadLength}}", description: "Exact file size in bytes (required)" },
            { key: "X-File-Name", value: "song.mp3", description: "Optional, URL-encoded; display only" },
          ],
          body: { mode: "file", file: { src: "" } },
          description: lines(`
For an existing Draft, or again after Failed/Cancelled — new tracks use "Upload a new track". Raw request body = the audio file (NOT multipart). Pick the file under Body → binary and set {{uploadLength}} to its size in bytes.
MP3/WAV/FLAC/AAC/M4A/OGG/Opus, ≤ 200 MB, 1 s–2 h. 202 → processing starts (track status Processing → Ready/Failed).
Errors: upload_length_required, file_too_large, upload_incomplete (size mismatch / connection dropped), unsupported_file_type, track_not_uploadable / upload_in_progress (409). Processing failures (unsupported_format, no_audio_stream, duration_too_short|long, processing_*) appear in track.processingError.
Flutter (dio): dio.put(url, data: file.openRead(), options: Options(headers: {'Content-Type': 'application/octet-stream', 'X-Upload-Length': await file.length(), 'X-File-Name': Uri.encodeComponent(name)}), onSendProgress: …).
Rate limit: 30 uploads/hour per IP.`).join("\n"),
        }),
        req("Upload status", "GET", "/api/v1/tracks/{{trackId}}/upload", { description: "Owner only: status (Uploaded / Processing / Completed / Failed / Cancelled), attempts, error code. Poll every 2–5 s, or poll the track." }),
        req("Cancel upload", "DELETE", "/api/v1/tracks/{{trackId}}/upload", { description: "204 while queued/processing; 409 if already finished." }),
        req("Playback URLs (for the player)", "GET", "/api/v1/tracks/{{trackId}}/playback", {
          description: lines(`
200 → data: {trackId, streamUrl, waveformUrl, contentType: "audio/mp4", sizeBytes, durationMs, expiresAt}. Cache-Control: no-store.
streamUrl/waveformUrl are relative to the base URL and carry ?token=vzp_… (valid ~6 h): hand baseUrl + streamUrl to the player with NO headers
(just_audio: AudioSource.uri(Uri.parse(baseUrl + streamUrl)) — works with background playback, iOS, casting).
Public tracks: works without sign-in. Private tracks: owner only (bearer). 404 track_not_found / media_not_ready.
When the player gets 401 playback_token_invalid (expired), call this endpoint again and resume at the same position.`).join("\n"),
          events: test(`
pm.test("200", () => pm.response.to.have.status(200));
if (pm.response.code === 200) {
  const d = pm.response.json().data;
  pm.collectionVariables.set("streamUrl", d.streamUrl);
  pm.collectionVariables.set("waveformUrl", d.waveformUrl);
}`),
        }),
        req("Stream audio (player URL)", "GET", "{{streamUrl}}", {
          auth: noauth,
          headers: [{ key: "Range", value: "bytes=0-", description: "Players send ranges; 206 Partial Content / 416" }],
          description: "What the player requests: the URL from \"Playback URLs\", no Authorization header. AAC (audio/mp4), HTTP Range. 401 playback_token_invalid when expired.",
        }),
        req("Stream audio (credentials)", "GET", "/api/v1/tracks/{{trackId}}/stream", {
          headers: [{ key: "Range", value: "bytes=0-", description: "Players send ranges; 206 Partial Content / 416" }],
          description: "Same stream authorised by bearer/cookie instead of a token (the web app uses this). Public tracks need no auth. 404 when not visible or not ready.",
        }),
        req("Waveform", "GET", "/api/v1/tracks/{{trackId}}/waveform", { description: "data: {version, durationMs, peaks: float[~1000] in 0..1}. Same visibility rules as streaming; also accepts ?token= (use waveformUrl)." }),
      ],
    },
    {
      name: "Social",
      description: "Likes, follows and timed comments (M4). Writes are rate-limited (60/min per account).",
      item: [
        req("Track engagement", "GET", "/api/v1/tracks/{{trackId}}/engagement", {
          description: "data: {likeCount, commentCount, liked}. Anyone who can see the track (liked=false when signed out).",
        }),
        req("Like track", "PUT", "/api/v1/tracks/{{trackId}}/like", {
          description: "Idempotent. Track must be playable for you. 200 → engagement. The artist gets a notification (not for their own tracks).",
        }),
        req("Unlike track", "DELETE", "/api/v1/tracks/{{trackId}}/like", { description: "Idempotent. 200 → engagement." }),
        req("My liked track ids", "GET", "/api/v1/me/likes/ids", { description: "All ids (≤ 5000, newest first) — mark hearts in lists without one request per track." }),
        req("List comments", "GET", "/api/v1/tracks/{{trackId}}/comments", {
          query: [{ key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Top-level comments newest first (paged); each has replies[] oldest first. timestampMs = moment of the track (ms) or null. Plain text — render as text.",
        }),
        req("Post a comment", "POST", "/api/v1/tracks/{{trackId}}/comments", {
          body: json({ body: "Love this drop!", timestampMs: 42000, parentId: null }),
          description: "1–1000 chars. timestampMs pins it to the waveform (top-level only). parentId → reply (a reply to a reply attaches to its top-level comment). 201. Errors: comment_body_invalid, comment_timestamp_invalid, comment_not_found (parent).",
          events: test(`
pm.test("201", () => pm.response.to.have.status(201));
if (pm.response.code === 201) pm.collectionVariables.set("commentId", pm.response.json().data.id);`),
        }),
        req("Edit comment", "PATCH", "/api/v1/comments/{{commentId}}", { body: json({ body: "Edited text" }), description: "Author only (404 otherwise)." }),
        req("Delete comment", "DELETE", "/api/v1/comments/{{commentId}}", { description: "204. Author, the track's owner or a moderator; 404 for others." }),
        req("Follow summary", "GET", "/api/v1/artists/{{artistId}}/followers/summary", { description: "data: {followerCount, following}." }),
        req("Follow artist", "PUT", "/api/v1/artists/{{artistId}}/follow", { description: "Idempotent. 400 follow_self for your own profile. The artist gets a notification." }),
        req("Unfollow artist", "DELETE", "/api/v1/artists/{{artistId}}/follow", { description: "Idempotent." }),
      ],
    },
    {
      name: "Library & charts",
      item: [
        req("Liked tracks", "GET", "/api/v1/me/likes", {
          query: [{ key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "data: [{likedAt, track: TrackDto}], newest like first. Tracks you can no longer see are left out (total still counts them).",
        }),
        req("Listening history", "GET", "/api/v1/me/history", {
          query: [{ key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Recently played: [{playedAt, track}], one entry per track, most recent first.",
        }),
        req("Followed artists", "GET", "/api/v1/me/following", { description: "[{id, slug, name, avatarUrl, followedAt}] newest first (paged)." }),
        req("Trending", "GET", "/api/v1/tracks/trending", {
          auth: noauth,
          query: [{ key: "page", value: "1" }, { key: "pageSize", value: "20" }],
          description: "Chart: valid listens + 2 × likes over the last 7 days, recent days weigh more. Same for everyone (not personalised). Top 100, paged.",
        }),
        req("Related tracks", "GET", "/api/v1/tracks/{{trackId}}/related", {
          auth: noauth,
          description: "≤ 12 public tracks by the same artist or in the same genre (label them as such in the UI — not recommendations).",
        }),
        req("Track stats", "GET", "/api/v1/tracks/{{trackId}}/stats", { auth: noauth, description: "data: {plays (started), listens (valid)}, all time." }),
      ],
    },
    {
      name: "Analytics",
      item: [
        req("Report playback", "POST", "/api/v1/plays", {
          body: json({
            clientId: null,
            events: [
              { id: "{{$guid}}", sessionId: "{{playSessionId}}", trackId: "{{trackId}}", type: "started", positionMs: 0, listenedMs: 0, clientTimestamp: null },
            ],
          }),
          description: lines(`
Send from the player: "started" when a track starts (new sessionId per start), "progress" every ~15 s of actual listening, "completed" at the end.
listenedMs = playback time accumulated in this session (seeks don't count); the server caps it by wall-clock time since the session started.
A valid listen = ≥ 30 s (half of tracks under 60 s), once per listener and track per 30 minutes. Event ids are idempotency keys: retry a failed batch with the same ids.
Signed-out players may send clientId (a random UUID kept on the device) so their listens de-duplicate. 1–50 events per batch; 202 → {accepted}. Rate limit 60 batches/min.`).join("\n"),
          events: [
            { listen: "prerequest", script: { type: "text/javascript", exec: [`if (!pm.collectionVariables.get("playSessionId")) pm.collectionVariables.set("playSessionId", crypto.randomUUID());`] } },
          ],
        }),
      ],
    },
    {
      name: "Notifications",
      description: "Realtime: SignalR hub {{baseUrl}}/api/v1/hubs/notifications (apps: ?access_token={{accessToken}}) sends 'unread' with {count}; then refetch.",
      item: [
        req("My notifications", "GET", "/api/v1/me/notifications", {
          query: [{ key: "unreadOnly", value: "false" }, { key: "page", value: "1" }],
          description: "Newest first. type: TrackLiked, ArtistFollowed, CommentPosted, CommentReplied, TrackReady, TrackFailed, ReportResolved, ContentRemoved. Link with trackId / artistSlug; code = error/outcome/reason.",
        }),
        req("Unread count", "GET", "/api/v1/me/notifications/unread-count", { description: "data: {count}." }),
        req("Mark one read", "POST", "/api/v1/me/notifications/{{notificationId}}/read", { description: "204; 404 if it isn't yours." }),
        req("Mark all read", "POST", "/api/v1/me/notifications/read-all", { description: "204." }),
      ],
    },
    {
      name: "Moderation",
      item: [
        req("Report content", "POST", "/api/v1/reports", {
          body: json({ subjectType: "Track", subjectId: "{{trackId}}", reason: "Copyright", details: "Original: https://example.com/…" }),
          description: "subjectType: Track | Comment | Playlist; reason: Copyright | Abuse | Spam | Other. 201. Errors: report_own_content (400), report_subject_not_found (404), report_exists (409 — one open report per item).",
        }),
        req("Report queue (moderators)", "GET", "/api/v1/moderation/reports", {
          query: [{ key: "status", value: "Open" }, { key: "page", value: "1" }],
          description: "Moderator/Admin. status=Open (oldest first) | Actioned | Rejected; omit for all. 403 for other users.",
        }),
        req("Resolve report", "POST", "/api/v1/moderation/reports/{{reportId}}/resolve", {
          body: json({ action: "remove", note: "Confirmed copyright claim" }),
          description: "Moderator/Admin. action: remove (takes the content down and settles every open report on it) | dismiss. Audited; reporters and the owner are notified. 409 report_not_open.",
        }),
        req("Restore content", "POST", "/api/v1/moderation/content/Track/{{trackId}}/restore", { description: "Moderator/Admin. Track or Playlist. 204. Audited." }),
      ],
    },
    {
      name: "Admin",
      description: "Admin role only (403 otherwise).",
      item: [
        req("Users", "GET", "/api/v1/admin/users", { query: [{ key: "q", value: "" }, { key: "role", value: "" }, { key: "page", value: "1" }] }),
        req("Grant role", "PUT", "/api/v1/admin/users/{{targetUserId}}/roles/Moderator", { description: "Moderator or Admin. Idempotent, audited, effective immediately." }),
        req("Revoke role", "DELETE", "/api/v1/admin/users/{{targetUserId}}/roles/Moderator", { description: "400 role_self_admin when removing your own Admin." }),
        req("All tracks", "GET", "/api/v1/admin/tracks", {
          query: [{ key: "q", value: "" }, { key: "takenDown", value: "" }, { key: "page", value: "1" }],
          description: "Moderators too. Every non-deleted track, any status/visibility.",
        }),
        req("Processing jobs", "GET", "/api/v1/admin/jobs", { query: [{ key: "status", value: "" }, { key: "page", value: "1" }] }),
        req("Job summary", "GET", "/api/v1/admin/jobs/summary"),
        req("Outbox summary", "GET", "/api/v1/admin/outbox", { description: "{pending, deadLettered, processedLastDay}." }),
        req("Audit log", "GET", "/api/v1/admin/audit-log", { query: [{ key: "page", value: "1" }] }),
      ],
    },
    {
      name: "Web (cookie) only",
      description: "Browser flow, for reference. Apps should use the token endpoints instead. Postman keeps the cookies automatically.",
      item: [
        req("CSRF token", "GET", "/api/v1/auth/csrf", {
          auth: noauth,
          events: test(`pm.collectionVariables.set("csrfToken", pm.response.json().data.token);`),
        }),
        req("Login (cookie)", "POST", "/api/v1/auth/login", {
          auth: noauth,
          headers: [{ key: "X-CSRF-TOKEN", value: "{{csrfToken}}" }],
          body: json({ email: "{{email}}", password: "{{password}}", rememberMe: false }),
          description: "204 + session cookie. Fetch a new CSRF token afterwards (tokens are bound to the identity).",
        }),
        req("Logout (cookie)", "POST", "/api/v1/auth/logout", { auth: noauth, headers: [{ key: "X-CSRF-TOKEN", value: "{{csrfToken}}" }] }),
      ],
    },
    {
      name: "Health",
      item: [
        req("Live", "GET", "/health/live", { auth: noauth }),
        req("Ready", "GET", "/health/ready", { auth: noauth, description: "Only reachable on the API port (5080) — the proxy routes /api only." }),
      ],
    },
  ],
};

// Mailpit helper uses its own host.
const folder = (name) => collection.item.find((f) => f.name === name);
const mailpit = folder("Auth (app)").item.find((r) => r.name.startsWith("DEV: read")).request;
mailpit.url.raw = "{{mailpitUrl}}/api/v1/search?query=to:\"{{email}}\"";
mailpit.url.host = ["{{mailpitUrl}}"];
// Health lives on the API port, not behind the proxy.
for (const h of folder("Health").item) {
  h.request.url.raw = h.request.url.raw.replace("{{baseUrl}}", "http://localhost:5080");
  h.request.url.host = ["http://localhost:5080"];
}

fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(collection, null, 2) + "\n");
console.log("wrote", out, collection.item.reduce((n, f) => n + f.item.length, 0), "requests");
