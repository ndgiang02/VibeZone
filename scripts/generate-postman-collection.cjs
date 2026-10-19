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
  const t = pm.response.json();
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
      const t = res.json();
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
    { key: "baseUrl", value: "http://localhost:3000", description: "Caddy proxy of the local stack. Android emulator: http://10.0.2.2:3000 (from the app)." },
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
    { key: "trackId", value: "" },
    { key: "sessionId", value: "" },
    { key: "uploadLength", value: "0", description: "Exact size in bytes of the file selected in 'Upload audio'." },
    { key: "csrfToken", value: "" },
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
          description: "200 → {accessToken, tokenType: 'Bearer', expiresIn: 900, refreshToken, refreshTokenExpiresAt}. 401 invalid_credentials (wrong email/password or locked out — indistinguishable). 403 email_not_confirmed. 429 too many attempts. deviceName is shown in the signed-in devices list.",
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
          description: "{id, email, handle, displayName, locale, emailConfirmed, roles[]}. 401 when not signed in.",
          events: test(`pm.test("200", () => pm.response.to.have.status(200));`),
        }),
        req("Update profile", "PATCH", "/api/v1/me/profile", { body: json({ displayName: "New name", locale: "en" }) }),
        req("Change password", "POST", "/api/v1/me/password", {
          body: json({ currentPassword: "{{password}}", newPassword: "{{newPassword}}" }),
          description: "204. Signs out every OTHER session (including other app devices); this one stays valid.",
        }),
        req("List sessions (devices)", "GET", "/api/v1/me/sessions", {
          description: "items[]: {id, createdAt, lastSeenAt, userAgent (deviceName for apps), isCurrent}.",
          events: test(`
const other = (pm.response.json().items || []).find((s) => !s.isCurrent);
if (other) pm.collectionVariables.set("sessionId", other.id);`),
        }),
        req("Revoke a session", "DELETE", "/api/v1/me/sessions/{{sessionId}}", { description: "204; 404 session_not_found." }),
        req("Revoke other sessions", "POST", "/api/v1/me/sessions/revoke-others", { description: "200 {revoked: n}." }),
      ],
    },
    {
      name: "Catalog",
      item: [
        req("Genres", "GET", "/api/v1/genres", { auth: noauth, description: "Public reference data: items[] {id, slug, name}." }),
        req("New releases", "GET", "/api/v1/tracks", { auth: noauth, query: [{ key: "limit", value: "20" }], description: "Public, ready tracks, newest first." }),
        req("Track by id", "GET", "/api/v1/tracks/{{trackId}}", {
          description: "Public/unlisted tracks for everyone; private/processing tracks only for the owner (else 404). Includes status, durationMs, playable.",
        }),
        req("Artist by slug", "GET", "/api/v1/artists/{{artistSlug}}", { auth: noauth, description: "Artist profile + public tracks. 404 if unknown." }),
        req("My artist profile", "GET", "/api/v1/me/artist", { description: "404 if the user has not created one yet." }),
        req("Become an artist", "POST", "/api/v1/me/artist", {
          body: json({ slug: "{{artistSlug}}", name: "App Artist", bio: null }),
          description: "201. Grants the Artist role (needed to create tracks). 409 if the slug is taken or the user already has one.",
        }),
        req("My tracks", "GET", "/api/v1/me/tracks", { description: "All the artist's own tracks, any status." }),
        req("Create track", "POST", "/api/v1/tracks", {
          body: json({ title: "My first track", description: null, genreSlug: "ambient", visibility: "Private" }),
          description: "201 → track in status Draft; then upload audio. visibility: Public | Unlisted | Private. 403 if the user is not an artist.",
          events: test(`
pm.test("201", () => pm.response.to.have.status(201));
if (pm.response.code === 201) pm.collectionVariables.set("trackId", pm.response.json().id);`),
        }),
        req("Update track", "PATCH", "/api/v1/tracks/{{trackId}}", {
          body: json({ title: "Renamed track", description: "Plain text", genreSlug: "ambient", visibility: "Public" }),
          description: "Owner only (others get 404). Send the full object.",
        }),
        req("Delete track", "DELETE", "/api/v1/tracks/{{trackId}}", { description: "204. Owner only." }),
      ],
    },
    {
      name: "Media",
      item: [
        req("Upload audio", "PUT", "/api/v1/tracks/{{trackId}}/upload", {
          headers: [
            { key: "Content-Type", value: "application/octet-stream" },
            { key: "X-Upload-Length", value: "{{uploadLength}}", description: "Exact file size in bytes (required)" },
            { key: "X-File-Name", value: "song.mp3", description: "Optional, URL-encoded; display only" },
          ],
          body: { mode: "file", file: { src: "" } },
          description: lines(`
Raw request body = the audio file (NOT multipart). Pick the file under Body → binary and set {{uploadLength}} to its size in bytes.
MP3/WAV/FLAC/AAC/M4A/OGG/Opus, ≤ 200 MB, 1 s–2 h. 202 → processing starts (track status Processing → Ready/Failed).
Errors: upload_length_required, file_too_large, upload_incomplete (size mismatch / connection dropped), unsupported_file_type, track_not_uploadable / upload_in_progress (409). Processing failures (unsupported_format, no_audio_stream, duration_too_short|long, processing_*) appear in track.processingError.
Flutter (dio): dio.put(url, data: file.openRead(), options: Options(headers: {'Content-Type': 'application/octet-stream', 'X-Upload-Length': await file.length(), 'X-File-Name': Uri.encodeComponent(name)}), onSendProgress: …).
Rate limit: 30 uploads/hour per IP.`).join("\n"),
        }),
        req("Upload status", "GET", "/api/v1/tracks/{{trackId}}/upload", { description: "Owner only: status (Uploaded / Processing / Completed / Failed / Cancelled), attempts, error code. Poll every 2–5 s, or poll the track." }),
        req("Cancel upload", "DELETE", "/api/v1/tracks/{{trackId}}/upload", { description: "204 while queued/processing; 409 if already finished." }),
        req("Stream audio", "GET", "/api/v1/tracks/{{trackId}}/stream", {
          headers: [{ key: "Range", value: "bytes=0-", description: "Players send ranges; 206 Partial Content / 416" }],
          description: "AAC (audio/mp4) with HTTP Range support. Public tracks need no auth; private ones need the owner's bearer token — with just_audio pass headers: {'Authorization': 'Bearer …'} on AudioSource.uri. 404 when not visible or not ready.",
        }),
        req("Waveform", "GET", "/api/v1/tracks/{{trackId}}/waveform", { description: "{version, durationMs, peaks: float[~1000] in 0..1}. Same visibility rules as streaming." }),
      ],
    },
    {
      name: "Web (cookie) only",
      description: "Browser flow, for reference. Apps should use the token endpoints instead. Postman keeps the cookies automatically.",
      item: [
        req("CSRF token", "GET", "/api/v1/auth/csrf", {
          auth: noauth,
          events: test(`pm.collectionVariables.set("csrfToken", pm.response.json().token);`),
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
const mailpit = collection.item[0].item[1].request;
mailpit.url.raw = "{{mailpitUrl}}/api/v1/search?query=to:\"{{email}}\"";
mailpit.url.host = ["{{mailpitUrl}}"];
// Health lives on the API port, not behind the proxy.
for (const h of collection.item[5].item) {
  h.request.url.raw = h.request.url.raw.replace("{{baseUrl}}", "http://localhost:5080");
  h.request.url.host = ["http://localhost:5080"];
}

fs.mkdirSync(path.dirname(out), { recursive: true });
fs.writeFileSync(out, JSON.stringify(collection, null, 2) + "\n");
console.log("wrote", out, collection.item.reduce((n, f) => n + f.item.length, 0), "requests");
