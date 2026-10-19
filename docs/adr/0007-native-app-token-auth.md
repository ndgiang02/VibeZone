# ADR-0007: Token authentication for native apps (email + password)

- Status: Accepted
- Date: 2026-10-19
- Supersedes: the "add OIDC for mobile" note in ADR-0002 (deferred, see below)

## Context

The project owner has a Flutter app that must use the same API as the web app. Cookie sessions +
antiforgery (ADR-0002) suit browsers but not native HTTP clients: there is no shared cookie jar with a
browser, antiforgery requires a cookie/token dance, and long-lived sign-in needs a refresh mechanism.
Social logins (Google/Apple/Facebook) were discussed and **deferred** by the owner; first-party
email + password sign-in is needed now.

## Decision

- **Endpoints** (`/api/v1/auth/token…`, Identity module, rate policy `auth`, CSRF-exempt, `Cache-Control: no-store`):
  - `POST /auth/token` `{email, password, deviceName?}` → `{accessToken, tokenType: "Bearer", expiresIn, refreshToken, refreshTokenExpiresAt}`.
    Same rules as cookie login: lock-out, generic `invalid_credentials`, `email_not_confirmed` only after a correct password.
  - `POST /auth/token/refresh` `{refreshToken}` → a new pair (**rotation**). Unknown/expired → 401 `invalid_refresh_token`.
  - `POST /auth/token/revoke` `{refreshToken}` → 204, idempotent. `POST /auth/logout` with a bearer token also works.
- **One session model.** Each token sign-in creates a row in `identity.user_sessions` (the same table as
  cookie sessions) with `refresh_token_hash`. So app devices appear in "signed-in devices", can be
  revoked from the web, and are revoked by password change/reset like every other session.
- **Access token**: opaque `vza_…`, 15 minutes, protected with ASP.NET Core Data Protection (the key ring
  already shared through Postgres) and carrying only user id, session id, handle and expiry.
  Every request still validates the session (≤ 30 s cache) and loads roles, exactly like the cookie, so
  revocation is effective within seconds, not at token expiry. JWT was considered and rejected: its
  benefit (stateless validation) is unused because sessions are checked anyway, and it would add a
  separate signing secret to manage and rotate.
- **Refresh token**: opaque `vzr_…` (256 random bits), stored only as SHA-256; valid for the session's
  absolute lifetime (30 days). Each refresh token works **once**: the swap is a compare-and-swap on the
  hash. Presenting the token that was just rotated out (`previous_refresh_token_hash`) is treated as
  theft and **revokes the session** (reuse detection; logged without token values).
- **Scheme selection**: default scheme `vz-auth` forwards to the bearer handler when an
  `Authorization: Bearer` header is present, otherwise to the cookie. A request never uses both; an
  invalid bearer token is 401 (`WWW-Authenticate: Bearer error="invalid_token"`) even if a valid cookie
  is also sent.
- **CSRF**: requests with a bearer header skip antiforgery (cookies are ignored for them, and a
  cross-site page cannot set `Authorization` without CORS, which stays disabled). Account endpoints that
  neither read nor set the cookie — register, verify-email, resend-verification, forgot/reset-password,
  token endpoints — are exempt so apps can call them; login/logout keep requiring the token.

## Consequences

- + Web and app share accounts, sessions, rules, rate limits and tests; one revocation path.
- + No new dependency and no new secret (Data Protection keys are already managed and persisted).
- − Strict rotation: an app that refreshes twice in parallel with the same token gets signed out.
  Clients must serialise refreshes (single-flight); documented in the Postman collection.
- − Only the last rotated-out token is remembered; replaying an older one is rejected but does not
  revoke the session.
- − Mobile sessions end after 30 days (absolute) like browser sessions; revisit sliding expiry if the
  owner wants "stay signed in forever".
- − Rate limiting is per IP (10/min for auth endpoints): many users behind one carrier NAT can hit it.
  Revisit with per-account limits when real traffic exists.
- Native apps must keep the refresh token in secure storage (Keychain / Android Keystore via
  `flutter_secure_storage`), never in plain preferences.
- Social login later: verify the provider's ID token server-side (`POST /auth/token/{provider}`) and
  issue the same token pair; Apple Sign-In becomes mandatory on iOS once any social login is offered.
