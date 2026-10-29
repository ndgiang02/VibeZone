# Security & Threat Model

Status: **M0 — initial threat model.** Revisit at the end of every milestone and whenever a new
trust boundary, external integration or data class is introduced. This document lists known
risks and planned controls; it is not a claim that the system is secure.

## 1. Assets

| Asset | Why it matters |
|---|---|
| User accounts & credentials (password hashes, sessions, OAuth links) | Account takeover, impersonation |
| Personal data (email, IP-derived data, listening history) | Privacy, legal obligations |
| Unreleased / private audio and originals | Leaks harm artists; copyright exposure |
| Upload pipeline & worker compute | Abuse for DoS, RCE via media parsers |
| Admin & moderation capabilities | Full-platform compromise |
| Integrity of counts (plays, likes, trending) | Manipulation, fraud |
| Infrastructure secrets (DB, S3, SMTP, OAuth client secrets) | Lateral movement, data theft |

## 2. Actors

Anonymous visitor · authenticated listener · artist · moderator · admin · automated bot/scraper ·
attacker with a stolen session · malicious uploader · compromised dependency/maintainer ·
insider with infrastructure access.

## 3. Trust boundaries

1. Browser ↔ public origin (reverse proxy / Next.js) — untrusted input, TLS. Apps and integrations use the
   separate API site (`api.<domain>`, ADR-0011): same API, same rate limits and authorization; it serves
   `/api/*` only and has no CORS, so other origins cannot use it with browser cookies.
2. Next.js server ↔ API — SSR forwards only the user's cookie; API re-authorises everything.
   Native apps (Flutter) ↔ API — bearer access token + rotating refresh token (ADR-0007); the device
   holds the refresh token and must keep it in OS secure storage.
3. API ↔ PostgreSQL / Redis / object storage — credentials from env/secret manager, private network.
4. Worker ↔ uploaded media — **hostile input parsed by FFmpeg**; highest-risk boundary.
5. API/Worker ↔ external services (SMTP, OAuth providers) — outbound only.
6. CI/CD ↔ registry/production — supply chain. Images are built only by CI on `main`, signed keylessly (cosign) with
   SBOM/provenance; the deploy verifies the signatures, reaches AWS via GitHub OIDC (role limited to SSM Run Command
   on the one instance, `production` environment only) and the host reads secrets from SSM Parameter Store with its
   instance role. No long-lived AWS keys, no inbound SSH (ADR-0014).

## 4. STRIDE analysis

| # | Threat | Where | Mitigation (milestone) |
|---|---|---|---|
| S1 | Credential stuffing / brute force | Login, reset | Identity lockout, per-IP + per-account rate limits, uniform responses, password policy + breached-password check optional (M2) |
| S2 | Session theft via XSS | Browser | HttpOnly `__Host-` cookie, no tokens in localStorage, strict CSP, React escaping, no `dangerouslySetInnerHTML` on user content (M2) |
| S3 | Session fixation / stale sessions | Auth | New session on login, server-side session records, revoke on password change/logout-all, security-stamp validation (M2) |
| S4 | OAuth account linking abuse | OAuth | Link only verified emails; state/PKCE handled by ASP.NET handlers; explicit confirmation to link (M2, if configured) |
| T1 | CSRF on state-changing endpoints | API | SameSite=Lax cookie + antiforgery token header on all unsafe methods + Origin check (M2) |
| T2 | Tampering with play counts | Analytics | Server-side valid-listen rules, idempotency keys, rate limits per user/IP/session, anomaly flags (M4/M5) |
| T3 | Mass assignment | API | Explicit request DTOs, never bind to entities (M1+) |
| T4 | SQL injection | DB | EF Core parameterisation; raw SQL only via `FromSql` interpolation; FTS input via `websearch_to_tsquery` (M1+) |
| R1 | Admin/moderator actions disputed | Moderation | Append-only `audit_logs` for role changes, takedowns, report decisions, account actions (M4) |
| I1 | IDOR on private tracks/playlists/upload jobs | API | Resource-based authorization in Application layer; tests for cross-user access; 404 (not 403) for invisible resources (M2–M4) |
| I2 | Private audio exposed via storage | S3 | Private bucket, no anonymous policy (verified in M0), server-generated keys, access only through authorised endpoints / short-lived signed URLs (M3) |
| I3 | User enumeration | Register/login/reset | Identical responses and comparable timing; reset/verification always "if an account exists…" (M2) |
| I4 | Secrets / PII in logs | Logging | Serilog destructuring policies, redaction of `Authorization`, `Cookie`, `Set-Cookie`, query tokens, signed URLs; IPs hashed (M1) |
| I5 | Secrets committed | Repo | `.env` ignored, `.env.example` placeholders only, secret scanning in CI (M0 hygiene, M5 CI) |
| I6 | Over-permissive CORS | API | Same-origin by design; CORS disabled unless an explicit allow-list is configured (M1) |
| I7 | SSRF | Any URL fetch | No server-side URL fetching in MVP. If added (e.g. avatar import), allow-list + resolved-IP checks (n/a) |
| D1 | Upload flooding / huge files | Upload | Auth required, per-user quotas, request size limit enforced while streaming, rate limit (M3) |
| D2 | Decompression/parser bombs, long transcodes | Worker | `ffprobe` gate (duration ≤ limit, allowed codecs/containers), FFmpeg timeouts, CPU/memory limits on the worker container, max concurrent jobs (M3) |
| D3 | Range-request abuse | Streaming | Validate ranges, reject excessive multi-range, rate limit per session (M3) |
| D4 | Expensive search queries | Search | Query length limits, pagination caps, statement timeout (M2) |
| E1 | Command injection via FFmpeg | Worker | `ProcessStartInfo.ArgumentList` only, no shell, server-generated file paths, fixed argument templates (M3) |
| E2 | Malicious media exploits FFmpeg | Worker | Up-to-date FFmpeg, non-root container, read-only rootfs, no network egress except storage, `no-new-privileges`, seccomp default (M3/M5) |
| E3 | Privilege escalation to admin | Admin | Role checks server-side on every admin endpoint (policy-based), admin role assignment only via CLI/seeding + audited (M4) |
| E4 | Stored XSS via comments/bios/titles | Web | Plain-text storage, output encoding, length limits, URL allow-listing for profile links (M2/M4) |
| E5 | Container breakout / over-privileged containers | Infra | Non-root images, minimal base, `no-new-privileges`, dropped capabilities, ports bound to localhost in dev (M0 dev, M5 prod) |
| E6 | Dependency compromise | Supply chain | Lockfiles, pinned image tags, `dotnet list package --vulnerable`, `npm audit`, Dependabot, SBOM (M5) |

## 5. Security rules (non-negotiable)

- Authorization is enforced on the server for every request; the UI only hides affordances.
- Passwords hashed by ASP.NET Core Identity (PBKDF2, default iteration count or higher).
- Cookie auth: `HttpOnly`, `Secure`, `SameSite=Lax`, `__Host-` prefix, sliding expiration with an
  absolute cap, server-side revocation.
- No secrets in source, images or logs. Configuration via environment / secret manager.
- Never log: passwords, tokens, cookies, reset/verification links, signed URLs, full IPs.
- Copyright: reports with reason `copyright` are prioritised; moderators can unpublish a track
  (status preserved, audio inaccessible) pending review; actions audited. A DMCA-style process
  document will accompany M4.

## 6. Local development posture (M0, verified)

- All Compose ports bound to `127.0.0.1` only; `no-new-privileges` on all containers.
- Postgres uses SCRAM-SHA-256 and data checksums; superuser is used locally only — production
  will use separate migration and runtime roles.
- MinIO: root credentials only for bootstrap; the app uses a bucket-scoped user that cannot create
  buckets or call admin APIs; anonymous access denied (403).
- Redis requires a password; unauthenticated commands are rejected. (The password is visible in
  `docker inspect`; acceptable for local dev only.)
- Verified by `scripts/verify-local-infra.sh`.

## 7. API & web baseline (M1, verified)

- API responses: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Content-Security-Policy: default-src none; frame-ancestors none`, `Referrer-Policy: no-referrer`,
  `Cross-Origin-Resource-Policy: same-origin`; no `Server` header; HSTS outside Development.
- CORS is not registered: cross-origin preflights receive no grant (integration test).
- Global per-IP fixed-window rate limit (default 600/min, 429 Problem Details); health probes exempt.
  `X-Forwarded-For` is honoured only from proxies listed in `ReverseProxy:KnownProxies`.
- Errors are RFC 9457 Problem Details with a trace id only; health responses expose status names only.
- Fail-fast configuration validation; connection string only via environment / user-secrets.
- Logs: request path (no query string), status, timing, trace ids; no headers, bodies or SQL
  parameters. Verified that the DB password never appears in API logs (host and container runs).
- Web: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`; no
  `X-Powered-By` (Playwright test). Both containers run as non-root.

## 8. Identity & web hardening (M2a, verified)

- Passwords: ASP.NET Core Identity hashing; policy is length-based (10–128, NIST 800-63B style).
- Anti-enumeration: register, resend-verification and forgot-password answer `202` identically
  whether or not the address exists (owner gets an "account exists" email instead); unknown email,
  wrong password and lock-out all return the same `invalid_credentials`; unknown accounts burn the
  same PBKDF2 cost as real ones. `email_not_confirmed` is only returned after a correct password.
- Lock-out after 5 failures (15 min); a successful password reset clears it.
- Rate limits: strict per-IP policy on login/register/verify/resend/forgot/reset/change-password
  (default 10/min) on top of the global limit; per-address email throttle (3 per purpose per hour).
- Sessions: server-side records, revocable individually / all others / all (password reset), 30-day
  absolute lifetime, validated every request (≤ 30 s staleness on other instances).
- CSRF: antiforgery header on every unsafe `/api` request (verified: missing token, token without
  its cookie, and pre-login token after sign-in are all rejected before any side effect).
- Email links are built from configured `App:PublicBaseUrl` (never from request headers); tokens are
  single-use (reset) or idempotent (verification), with 1 h / 24 h lifetimes; logs never contain
  recipients, tokens or links.
- Web: strict per-request nonce CSP (ADR-0006); e2e tests fail on any CSP violation; open-redirect
  guard on `?next=`; localized error codes only (no server messages rendered as HTML).

### Native-app tokens (ADR-0007, verified)

- `POST /auth/token` applies the cookie-login rules (lock-out, `invalid_credentials`, confirmed email).
- Access tokens: opaque, Data Protection–protected, 15 min, and the session is still checked on every
  request, so revocation (logout, device removal, password change/reset) takes effect within ≤ 30 s.
- Refresh tokens: 256-bit random, stored as SHA-256 only, single use (compare-and-swap rotation);
  replaying the previous token revokes the session. Token responses are `Cache-Control: no-store`.
- A bearer request never falls back to the cookie; such requests skip antiforgery (no ambient
  credentials; CORS stays disabled). Register/verify/resend/forgot/reset are CSRF-exempt because they
  neither read nor set the cookie (they already answer uniformly and are rate limited); login/logout
  still require the antiforgery token (login CSRF).
- Tests: `TokenAuthTests` (13), `CsrfTests`; manual smoke through the Caddy proxy (see progress.md).

## 9. Residual risks / open items

- Token clients: strict refresh rotation signs out an app that refreshes twice in parallel (clients
  must single-flight); per-IP auth rate limit may hit users behind carrier NAT; a stolen refresh
  token is usable until the legitimate app refreshes (then reuse detection ends the session).

- **Insecure-cookie switch** (`Security:SecureCookies=false`) is required for the plain-HTTP local
  stack; misconfiguring it in a real deployment would drop the Secure flag. The API logs a warning;
  M5 adds a startup guard for non-local environments.
- Session revocation and rate limits are per instance until Redis is introduced for scale-out.
- The email queue is in memory: emails queued during a crash are lost (user can resend). The durable
  outbox exists since M4 (ADR-0012) but account emails don't go through it yet.
- Data Protection key ring is stored unencrypted in Postgres (M5: certificate/KMS encryption).
- No breached-password (HIBP) check yet; no MFA yet.
- `npm audit` reports GHSA-vfj7-8cjw-p6xm (`braces` ≤ 3.0.3, no patched release exists) through
  dev-only tooling (`eslint-config-next`, `shadcn` CLI). Not shipped in the production bundle
  (`npm audit --omit=dev` is clean; CI enforces that) and only processes repository-defined globs.
  Re-check when upstream publishes a fix.
- GitHub Actions are pinned to major tags (Dependabot updates them since M5); SHA pinning still open.
- Single production host (ADR-0014): Postgres shares the machine with FFmpeg; a worker escape reaches the instance
  role (media bucket, backup writes, SSM `/vibezone/prod` reads). Mitigated by non-root, no-new-privileges and
  CPU/memory limits; a separate host or narrower worker credentials when budget allows.
- `System.Security.Cryptography.Pkcs` is pinned to 10.0.13 (transitive via MailKit 4.18, which references a
  vulnerable 10.0.0). Remove the pin once MailKit ships a fixed dependency.
- MinIO community image is maintained by a third party (see ADR-0003); local-only exposure.
- Signed URLs (M3+) prevent casual hotlinking, **not** copying — there is no DRM.
- Play-count fraud can be reduced, not eliminated.
- FFmpeg remains a large attack surface even with sandboxing.

## 10. Media controls (M3; verified by integration tests, see progress.md 2026-10-20)

- Uploads: authenticated owner only; size enforced while reading (never trusting `Content-Length`),
  declared size must match bytes received, first-bytes signature sniff, server-generated object keys,
  file name stored for display only; per-IP hourly upload limit; one active job per track (DB constraint).
- One-request uploads (`POST /track-uploads`, ADR-0009): multipart read as a stream (never buffered by
  `Request.Form`), text fields capped at 16 KB, unknown or duplicate parts rejected, body limit = audio + cover
  limits; all-or-nothing commit, written objects deleted on failure. The CSRF token is read from the header
  only (the form is never parsed for it).
- Cover images (ADR-0009) — **hostile input parsed by Skia in the API process**: JPEG/PNG/WebP signature
  allow-list before decoding (SVG, GIF, HEIC… rejected), size limit (10 MB), dimensions checked from the
  header (min 200 px side, max 40 MP) before pixels are allocated, re-encoded to JPEG from decoded pixels so
  EXIF/GPS/XMP/comments never reach storage or viewers; original not kept; server-generated keys; served as
  `image/jpeg` with `nosniff` only to viewers who can see the track (invisible → 404), `Cache-Control: private`.
- Artist banner: same pipeline and limits; only the owner of the profile can change it
  (`/me/artist/banner` — no artist id in the URL, so no IDOR surface); served publicly (profiles are public).
  The artist picture and name are the account's (ADR-0013), so they can only change through the account.
- Account pictures: `/me/avatar` (own account only), same pipeline; served publicly by user id.

## 11. Catalog, search and playlists (M2b/M2c, ADR-0010; covered by integration + e2e tests since 2026-10-28)

- Albums and playlists follow the track rule: private → owner (and moderators) only, invisible → 404;
  every write is owner-checked in the Application layer (no ids from the client are trusted for ownership).
- Album tracklists only accept the artist's own tracks; playlists only accept tracks the user can see.
  Rendering a playlist re-checks each track's visibility for the viewer, so a public playlist never
  reveals another artist's private or unready track (only the count of hidden tracks).
- Search: only listed content; query text is length-capped (100), reduced to letter/digit terms before it
  becomes a tsquery, and always passed as a SQL parameter.
- Playlist owners are shown by display name, handle and picture only (`IUserDirectory` exposes nothing else).
- Dev seed (`seed-dev`) refuses to run outside Development; demo accounts use `@example.test` addresses.
- Processing: ffprobe gate (container/codec allow-list, duration bounds) before any transcode; FFmpeg via
  `ArgumentList` with server-generated temp paths only; whole-process-tree kill on timeout/cancel; bounded
  stderr capture; worker container non-root with CPU/memory limits; originals never streamed.
- Streaming/waveform: visibility + Ready check before any byte; invisible tracks → 404;
  `Cache-Control: private` so shared caches never store audio whose visibility may change.
- Playback tokens (ADR-0008): `GET /tracks/{id}/playback` issues `?token=vzp_…` URLs for native players.
  - Opaque and Data Protection–protected; each names one track and the viewer it was issued to, and
    expires (default 6 h).
  - Visibility is re-checked as that viewer on every stream/waveform request, so making a track private
    or deleting it ends access immediately. Foreign, tampered or expired tokens → 401 `playback_token_invalid`.
  - Residual risks: a leaked URL works for its holder until it expires; revoking a session does not
    revoke tokens already issued from it.
  - Mitigations: request logs record paths without query strings, the `Microsoft.AspNetCore` hosting
    logs stay at Warning, and the proxy has no access log. Responses are `Cache-Control: no-store`.
- Worker: lease heartbeats and job commits are serialised per job (both update the row version), so a
  long transcode can't lose its result to its own heartbeat.
- Web: covers are `<img>` from our own API (`img-src 'self' blob:`; `blob:` for local previews before
  upload); without a cover a decorative gradient is shown; descriptions/bios rendered as
  plain text; waveform styles injected with the CSP nonce (`cspNonce`).

## 12. Social, analytics, notifications and moderation (M4, ADR-0012; covered by integration + e2e tests since 2026-10-28)

- **Authorization**: likes and comments require the track to be playable for the caller; reading comments,
  engagement and stats requires it to be visible (invisible → 404). Comment edits: author only; deletion: author,
  track owner or moderator (others → 404). Notifications: recipient only (others → 404). Moderation endpoints use
  the `moderator` policy, user/role administration, jobs, outbox and audit log the `admin` policy
  (401 anonymous, 403 wrong role). Role changes are limited to Moderator/Admin, can't remove your own Admin,
  take effect immediately (session cache evicted) and are audited.
- **New data**: comments and report details are user-generated plain text (≤ 1000 chars), rendered as text,
  never HTML. Play events store no IP address; anonymous listeners are keyed by a SHA-256 of a random client id
  the browser keeps (not a credential). Notification payloads hold titles/excerpts/codes only. Admins see
  account emails in `/admin/users` (staff-only data class). Audit metadata holds ids, roles, reasons and the
  moderator's note — never secrets.
- **Abuse limits**: `social` policy (60 writes/min per account, else per IP) on likes, follows, comments and
  reports; `plays` policy (60 batches/min) on play events; batches ≤ 50 events; one open report per reporter
  and item; reporters can't report their own content.
- **Play-count integrity**: event ids are idempotency keys; a session belongs to one listener and one track;
  listened time is capped by wall-clock time since the session started; one valid listen per listener, track and
  30 minutes. A determined client can still script listens (residual risk, see §9).
- **Realtime hub** (`/api/v1/hubs/notifications`): authenticated (cookie or bearer); apps pass the access token as
  `?access_token=` on hub paths only — the request log records paths without query strings, but reverse-proxy
  access logs must not log query strings for `/api/v1/hubs/*`. Exempt from the CSRF filter: server → client only,
  no state-changing hub methods, and cross-site pages can't read negotiate responses. Single instance until a
  SignalR backplane is added.
- **Takedown**: a removed track is unplayable and hidden from everyone but its owner and moderators, including
  search, trending, related, playlists and albums; restore is audited. Removed comments are soft-deleted.
- **Outbox**: events carry ids and display snapshots, never emails or tokens; failures store only the exception
  type.
- **Role bootstrap**: the `grant-role` operator command needs database access (same as the app), only grants
  Moderator/Admin to confirmed accounts, and is audited with `via=cli`. Its log line names the user id, not the email.
- **Staff invitations** (`POST /admin/users`, Admin only, CSRF-protected): creates a Moderator/Admin account with
  **no password** and an unconfirmed email, then emails a single-use link (`/accept-invite?userId&token`, Identity
  user token bound to the security stamp, 24 h). Admins never see or set the password. `POST /auth/accept-invite`
  (rate-limited) only works for an account that has no password yet, sets it, and confirms the email (the link proves
  the mailbox); setting the password changes the security stamp, which spends the token. An existing email is refused
  (`email_taken`, safe to reveal to admins, who can search users) so an invitation can never take over an account.
  Audited as `user_invited`. Until accepted the account can't sign in; an expired link falls back to verify-email +
  forgot-password. Risk accepted: an admin who mistypes an email gives that mailbox's owner the role — the pending
  account shows as "not verified" in the user list and the role can be revoked.
- **Malformed requests**: unreadable JSON, wrongly typed fields and missing bodies return 400 `request_invalid`
  (413 `request_too_large`) and a body the endpoint doesn't accept 415 `unsupported_media_type`, all as Problem
  Details without echoing the request — previously some of these surfaced as 500.
