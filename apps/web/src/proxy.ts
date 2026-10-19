import createIntlMiddleware from "next-intl/middleware";
import type { NextRequest } from "next/server";
import { routing } from "./i18n/routing";

const intl = createIntlMiddleware(routing);
const isDev = process.env.NODE_ENV === "development";

/**
 * Per-request Content-Security-Policy. Only scripts carrying this request's nonce run ('strict-dynamic'
 * lets them load the app's chunks), so injected inline scripts are blocked. Next.js reads the nonce from
 * the request's CSP header and applies it to its own scripts; pages are therefore rendered dynamically.
 */
export function buildCsp(nonce: string, https: boolean) {
  const directives = [
    "default-src 'self'",
    `script-src 'self' 'nonce-${nonce}' 'strict-dynamic'${isDev ? " 'unsafe-eval'" : ""}`,
    // Style elements need the nonce; style *attributes* (Radix positioning, per-genre gradients) are allowed.
    isDev ? "style-src 'self' 'unsafe-inline'" : `style-src 'self' 'nonce-${nonce}'`,
    "style-src-attr 'unsafe-inline'",
    "img-src 'self' blob: data:",
    "media-src 'self' blob:",
    "font-src 'self'",
    `connect-src 'self'${isDev ? " ws:" : ""}`,
    "object-src 'none'",
    "base-uri 'self'",
    "form-action 'self'",
    "frame-ancestors 'none'",
  ];
  if (https) {
    directives.push("upgrade-insecure-requests");
  }
  return directives.join("; ");
}

export default function proxy(request: NextRequest) {
  const nonce = btoa(crypto.randomUUID());
  const https = request.nextUrl.protocol === "https:" || request.headers.get("x-forwarded-proto") === "https";
  const csp = buildCsp(nonce, https);

  // next-intl forwards a copy of these request headers to rendering (rewrite/next responses).
  request.headers.set("x-nonce", nonce);
  request.headers.set("Content-Security-Policy", csp);

  const response = intl(request);
  response.headers.set("Content-Security-Policy", csp);
  return response;
}

export const config = {
  // Pages only: not the API proxy, Next internals or files with an extension.
  matcher: "/((?!api|_next|_vercel|.*\\..*).*)",
};
