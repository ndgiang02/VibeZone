# ADR-0006: Nonce-based CSP with per-request rendering; locale-prefixed routing

- Status: Accepted (M2a)
- Date: 2026-10-19

## Context

The web app needed a Content-Security-Policy (security.md residual risk from M1). Next.js applies a
CSP nonce only during server rendering; its docs state nonce-based CSP requires dynamic rendering
and is incompatible with Partial Prerendering (`cacheComponents`, enabled in M1). The alternative —
keeping static shells — forces `script-src 'unsafe-inline'`, which leaves injected inline scripts
executable. M2 also introduces a bilingual UI (vi default, en).

## Decision (chosen by the project owner)

- **Strict CSP with a fresh nonce per request**, set in `src/proxy.ts`:
  `script-src 'self' 'nonce-…' 'strict-dynamic'`, `style-src 'self' 'nonce-…'`,
  `style-src-attr 'unsafe-inline'` (Radix positioning, decorative gradients), `object-src 'none'`,
  `base-uri 'self'`, `form-action 'self'`, `frame-ancestors 'none'`, `connect-src 'self'`,
  `img-src`/`media-src 'self' blob: data:`; `upgrade-insecure-requests` only over HTTPS;
  `'unsafe-eval'`/`'unsafe-inline'` styles only in `next dev`.
- `cacheComponents`/PPR **disabled**; the root layout reads request headers so every page renders per
  request. Next.js stamps the nonce on its scripts; libraries that inject `<style>` at runtime get it
  via `get-nonce` (`src/app/csp-nonce.tsx`).
- **i18n**: next-intl, `localePrefix: "always"` (`/vi/...`, `/en/...`), `vi` default, Accept-Language
  + `NEXT_LOCALE` cookie detection, `[locale]` as root segment (`next/root-params`). Translation keys
  are type-checked; vi/en parity is a unit test. API errors carry stable codes translated client-side.

## Consequences

- + Injected inline scripts/styles are blocked; e2e tests fail on any CSP console violation.
- − No static HTML/CDN caching of pages; every navigation renders on the server. Acceptable: most
  pages are personalised, and data caching happens at the API/TanStack Query layer.
- − Root `loading.tsx` removed: a Suspense boundary above pages would flush a 200 before
  `notFound()` and lose the 404 status.
- Revisit if Next.js gains hash-based CSP for inline scripts compatible with prerendering.
