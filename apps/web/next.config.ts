import type { NextConfig } from "next";
import createNextIntlPlugin from "next-intl/plugin";

// Where the Next.js server forwards /api/* (dev and simple deployments). In production the
// reverse proxy routes /api/* straight to the API (ADR-0002); this value is fixed at build time.
const apiInternalUrl = process.env.API_INTERNAL_URL ?? "http://localhost:5080";

// Content-Security-Policy is set per request with a nonce in src/proxy.ts (ADR-0006).
const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
];

const nextConfig: NextConfig = {
  output: "standalone",
  poweredByHeader: false,
  experimental: {
    // The /api rewrite proxies audio uploads (up to 200 MB, Media:MaxUploadBytes) and long streams.
    // Next buffers proxied request bodies and silently forwards only the first 10 MB by default —
    // with the full Content-Length still declared, so the API waits forever. Allow the max upload
    // (+ margin) and a long timeout. This buffers uploads in the web server's memory, acceptable
    // locally; production routes /api at the reverse proxy and never through Next (ADR-0002).
    proxyClientMaxBodySize: "210mb",
    proxyTimeout: 15 * 60 * 1000,
  },
  turbopack: {
    rules: {
      "*.css": {
        loaders: ["@tailwindcss/turbopack"],
        as: "*.css",
      },
    },
  },
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiInternalUrl}/api/:path*` }];
  },
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

const withNextIntl = createNextIntlPlugin("./src/i18n/request.ts");
export default withNextIntl(nextConfig);
