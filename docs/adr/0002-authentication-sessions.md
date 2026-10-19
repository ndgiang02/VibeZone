# ADR-0002: Cookie-based sessions on a single origin

- Status: Accepted (M0)
- Date: 2026-10-19

## Context

The web app (Next.js) and API (ASP.NET Core) are separate processes. Requirements: no long-lived
tokens in localStorage, revocable sessions, CSRF protection, optional OAuth login, SSR that can
render per-user data.

## Decision

- **ASP.NET Core Identity** for users, password hashing, lockout, email confirmation, reset tokens,
  external logins (OAuth/OIDC providers enabled only when configured).
- **Cookie authentication** issued by the API, served to the browser on the **web origin**:
  production reverse proxy routes `/api/*` and `/hubs/*` to the API; in development Next.js
  `rewrites` do the same. Cookie: `__Host-vz_session`, `HttpOnly`, `Secure`, `SameSite=Lax`,
  `Path=/`, sliding 14 days with a 30-day absolute cap (tunable).
- The cookie carries a **session id** claim backed by `identity.user_sessions`; each request
  validates the session is not revoked (short in-memory cache, Redis later if multi-instance).
  Enables "sign out other devices" and revocation on password change.
- **CSRF**: ASP.NET Core antiforgery; the web app fetches a request token and sends it in
  `X-CSRF-TOKEN` on unsafe methods. Plus `SameSite=Lax` and Origin/Referer validation.
- No JWT/refresh tokens for the browser. Native apps use bearer + rotating refresh tokens bound to
  the same sessions — see ADR-0007 (OIDC/OpenIddict not needed for a first-party app).

## Consequences

- + No token handling in JavaScript; XSS cannot read the session.
- + Server-side revocation and session listing.
- − All API traffic for browsers must go through the same origin (proxy hop for streaming in dev;
  in production the reverse proxy streams directly to the API).
- − SSR must forward the incoming `Cookie` header to the API and never cache personalised responses.

## Implementation notes (M2a)

- Cookies: `__Host-vz_session` (session) and `__Host-vz_af` (antiforgery, `SameSite=Strict`).
  `Security:SecureCookies=false` (Development and the local Compose stack only, plain HTTP on
  localhost) switches to unprefixed, non-`Secure` names: ASP.NET antiforgery refuses to issue a Secure
  cookie on a non-HTTPS request (and some browsers, e.g. Safari, don't treat http://localhost as secure). The API logs a warning
  when this is off. Behind a real domain, terminate TLS and list the proxy in `ReverseProxy:KnownProxies`.
- The session store is checked on every authenticated request with a 30 s per-instance cache;
  revocations on the same instance evict immediately. Roles are loaded with the session, so role
  changes apply without re-login.
- Antiforgery tokens are bound to the identity. Clients refetch after login/logout, and on a
  `csrf_invalid` response (session changed server-side) refetch once and retry the request.
- Local Compose stack mirrors production routing with a Caddy reverse proxy (`infra/caddy/Caddyfile`,
  service `proxy` on `WEB_PORT`): `/api/*` and `/hubs/*` stream directly to the API, the rest to web.
  The Next.js `/api` rewrite remains only for host development (`npm run dev`), where Next buffers
  proxied request bodies (`proxyClientMaxBodySize` raised to 210 MB for uploads).
- Data Protection keys (cookies, antiforgery, Identity tokens) are persisted in
  `infra.data_protection_keys` so restarts and multiple instances keep sessions valid. Key-ring
  encryption at rest is an M5 production task.
