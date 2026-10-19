import createClient from "openapi-fetch";
import type { paths } from "./schema";

/**
 * Typed client for the VibeZone API, generated from openapi/vibezone-api.json
 * (`npm run api:generate`).
 *
 * Browser: same-origin `/api/*` (cookies stay first-party, ADR-0002).
 * Server: calls the API directly at API_INTERNAL_URL.
 */
function baseUrl() {
  if (typeof window !== "undefined") {
    return window.location.origin;
  }
  return process.env.API_INTERNAL_URL ?? "http://localhost:5080";
}

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS", "TRACE"]);
const CSRF_HEADER = "X-CSRF-TOKEN";

let csrfToken: Promise<string> | null = null;

/** Antiforgery tokens are bound to the signed-in identity; drop the cached one whenever that changes. */
export function resetCsrfToken() {
  csrfToken = null;
}

/** Shared with non-fetch transports (XHR uploads need progress events). */
export function getCsrfToken(): Promise<string> {
  csrfToken ??= globalThis
    .fetch(new Request(new URL("/api/v1/auth/csrf", baseUrl()), { cache: "no-store" }))
    .then(async (response) => {
      if (!response.ok) throw new ApiError(response.status);
      return ((await response.json()) as { token: string }).token;
    })
    .catch((error: unknown) => {
      csrfToken = null;
      throw error;
    });
  return csrfToken;
}

async function isCsrfRejection(response: Response) {
  if (response.status !== 400) return false;
  const problem = (await response
    .clone()
    .json()
    .catch(() => null)) as ProblemDetails | null;
  return problem?.code === "csrf_invalid";
}

async function sendWithCsrf(request: Request) {
  request.headers.set(CSRF_HEADER, await getCsrfToken());
  return globalThis.fetch(request);
}

/**
 * Adds the CSRF header to unsafe requests. If the server rejects the token (the session changed
 * server-side, e.g. it was revoked), fetch a fresh token and retry exactly once.
 */
async function apiFetch(request: Request): Promise<Response> {
  if (SAFE_METHODS.has(request.method)) {
    return globalThis.fetch(request);
  }

  const retry = request.clone();
  let response = await sendWithCsrf(request);
  if (await isCsrfRejection(response)) {
    resetCsrfToken();
    response = await sendWithCsrf(retry);
  }

  const path = new URL(request.url).pathname;
  if (path === "/api/v1/auth/login" || path === "/api/v1/auth/logout") {
    resetCsrfToken();
  }
  return response;
}

export const api = createClient<paths>({
  baseUrl: baseUrl(),
  // Resolved per call (not at import) so instrumentation/test doubles installed later apply.
  fetch: (request) => apiFetch(request),
});

export type ProblemDetails = {
  title?: string;
  status?: number;
  code?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
};

/** Thrown for non-2xx responses so TanStack Query surfaces them as errors. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem?: ProblemDetails,
  ) {
    super(problem?.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
  }

  /** Stable error code for translation (falls back by status). */
  get code(): string {
    if (this.problem?.code) return this.problem.code;
    if (this.status === 429) return "rate_limited";
    return "unknown";
  }

  /** Field-level error codes keyed by request field name (camelCase). */
  get fieldErrors(): Record<string, string> {
    return Object.fromEntries(
      Object.entries(this.problem?.errors ?? {}).flatMap(([field, codes]) =>
        codes[0] ? [[field, codes[0]]] : [],
      ),
    );
  }
}

export function unwrap<T>(result: { data?: T; error?: unknown; response: Response }): T {
  if (result.response.ok && result.data !== undefined) {
    return result.data;
  }
  throw new ApiError(result.response.status, result.error as ProblemDetails | undefined);
}

/** For endpoints that answer 202/204 without a body. */
export function ensureOk(result: { error?: unknown; response: Response }): void {
  if (!result.response.ok) {
    throw new ApiError(result.response.status, result.error as ProblemDetails | undefined);
  }
}
