# ADR-0008: Response envelope and tokenised playback URLs

- Status: Accepted
- Date: 2026-10-20
- Related: ADR-0005 (audio delivery), ADR-0007 (native-app tokens)

## Context

The project owner asked for one response format shared by the web app and the Flutter app. Until now,
success bodies were bare DTOs, or `{items}` for lists, and errors were RFC 9457 Problem Details.
Lists had no paging metadata. The owner chose to **keep Problem Details for errors** and to wrap
**successes** in an envelope.

Playback in the app also needs a URL the native player (just_audio → ExoPlayer/AVPlayer, often in a
background audio service) can fetch on its own:

- An `Authorization` header on a player is awkward. iOS needs a local proxy for headers, cast and
  background players drop them, and the 15-minute access token expires in the middle of a long track.
- Cookies don't exist in the app.

## Decision

### Envelope

- Every successful JSON body is `{ "data": <payload> }`.
- Collections are `{ "data": [ … ], "meta": { "page", "pageSize", "total", "hasMore" } }`.
- Paged endpoints take `page` (1-based) and `pageSize` (default 20, max 50). These are
  `GET /tracks` (new releases) and `GET /me/tracks`.
- Small complete lists (genres, sessions) are reported as a single page.
- Types: `ApiResponse<T>` and `ApiListResponse<T>`, with factories in `ApiResults`
  (`VibeZone.Infrastructure.Web`). In OpenAPI they appear as `ApiResponseOfX` / `ApiListResponseOfX`.
- Errors are unchanged: Problem Details (`application/problem+json`) with a stable `code`,
  `errors[field] = [code]` and `traceId`. Clients branch on the HTTP status, never on the body shape.
- Not wrapped:
  - `204` responses: no body.
  - Binary responses: the stream.
  - Health endpoints: an infrastructure contract.

### Playback URLs

- `GET /api/v1/tracks/{id}/playback` returns `{ data: { trackId, streamUrl, waveformUrl, contentType, sizeBytes, durationMs, expiresAt } }`.
  - It sends `Cache-Control: no-store`.
  - Anonymous callers can use it for public tracks; bearer or cookie callers for their own private tracks.
- The URLs are relative to the API origin and carry `?token=vzp_…`.
  - The token is opaque and protected with the shared Data Protection key ring, the same mechanism as
    ADR-0007's access tokens.
  - It holds the track id, the viewer it was issued to (or none) and an expiry
    (`Media:PlaybackTokenLifetime`, default 6 h: a 2 h maximum track plus pauses).
- `GET /stream` and `GET /waveform` accept the token instead of credentials.
  - **Authorization is re-checked on every request** as that viewer. Making a track private or
    deleting it stops playback immediately; the token only stands in for *who* is listening.
  - An invalid, expired or foreign token (one issued for another track) → `401 playback_token_invalid`.
    The app then asks `/playback` again.
- The web app keeps using same-origin cookie requests; nothing changes there.

## Consequences

- Breaking change for every client that read success bodies. Updated in the same change: the web app
  (`unwrap()` returns `data`), integration tests, the Postman collection and `docs/api/README.md`.
  The project has no external consumers yet.
- Paging uses `COUNT(*)`, which is fine at current sizes. Keyset pagination can replace it on hot
  lists later without changing the envelope.
- A leaked playback URL plays that one track for the person it was issued to, until it expires and
  only while they are still allowed to hear it.
  - Mitigations: short lifetime, HTTPS only in production, request paths are logged without query
    strings (Serilog request logging, `Microsoft.AspNetCore` hosting logs at Warning), and the proxy
    has no access log.
  - Revoking a *session* does not revoke playback tokens already issued from it (at most
    `PlaybackTokenLifetime`). This is accepted, because track visibility is re-checked on every request.
