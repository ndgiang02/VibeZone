# ADR-0011: A separate domain for the API (apps), web keeps its same-origin /api

- Status: Accepted
- Date: 2026-10-21
- Related: ADR-0002 (cookie sessions on a single origin), ADR-0007 (native app tokens), ADR-0001 (M6 split)

## Context

Web and API were served from one host: the proxy routes `/api/*` to the API and everything else to the web
app. The owner wants the API under its own name so apps and integrations do not look like they talk to the
website, and so an API gateway can be put in front of the API later without changing clients.

Moving the browser to a cross-origin API would undo ADR-0002: CORS with credentials, a domain-wide cookie
instead of `__Host-` (SSR would no longer see a host-only API cookie), wider CSP, credentialed media requests.

## Decision

- The edge proxy serves two sites (`infra/caddy/Caddyfile`, `WEB_SITE` / `API_SITE`):
  - **Web site** (`vibezone.vn`): unchanged — `/api/*`, `/hubs/*` → API, the rest → Next.js. Browsers keep a
    single origin; ADR-0002 stays as is.
  - **API site** (`api.vibezone.vn`): `/api/*`, `/hubs/*` → API, anything else 404. This is the documented base
    URL for native apps, Postman and integrations. Bearer tokens only in practice: no CORS is configured, so
    browsers cannot call it with cookies from other origins.
- Locally the sites are ports: web `WEB_PORT` (3000), API `API_PUBLIC_PORT` (3001). `API_PORT` (5080) stays the
  API container itself for health and debugging.
- The API returns relative media URLs (playback, artwork, avatars), so they work on either site.

## Consequences

- + Apps depend on `api.<domain>` only; a gateway (or a split service, M6) can take over that name later.
- + No change to browser security (cookies, CSRF, CSP).
- − Two hosts route to the same API; the API site has no web pages, and `/api` on the web site is for the
  browser. Rate limits are per IP across both.
- − Production needs TLS certificates for both names (Caddy obtains them when `auto_https` is enabled, M5).
