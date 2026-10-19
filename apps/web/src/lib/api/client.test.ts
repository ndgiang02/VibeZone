import { describe, expect, it, vi } from "vitest";
import { jsonResponse } from "@/test/render";
import { api, ApiError, unwrap } from "./client";

type Call = { method: string; path: string; csrf: string | null };

/** Fake API: issues numbered CSRF tokens; `respond` decides the answer for other requests. */
function fakeServer(respond: (call: Call, attempt: number) => Response) {
  let issued = 0;
  const calls: Call[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>(async (input) => {
      const request = input as Request;
      const path = new URL(request.url).pathname;
      if (path === "/api/v1/auth/csrf") {
        issued += 1;
        return jsonResponse({ token: `token-${issued}` });
      }
      const call = { method: request.method, path, csrf: request.headers.get("X-CSRF-TOKEN") };
      calls.push(call);
      return respond(call, calls.length);
    }),
  );
  return { calls, issued: () => issued };
}

describe("api client CSRF handling", () => {
  it("sends no token on safe requests", async () => {
    const server = fakeServer(() => jsonResponse({ items: [] }));

    await api.GET("/api/v1/genres");

    expect(server.calls).toEqual([{ method: "GET", path: "/api/v1/genres", csrf: null }]);
    expect(server.issued()).toBe(0);
  });

  it("fetches a token once and attaches it to unsafe requests", async () => {
    const server = fakeServer(() => new Response(null, { status: 202 }));

    await api.POST("/api/v1/auth/forgot-password", { body: { email: "a@example.test", locale: "en" } });
    await api.POST("/api/v1/auth/forgot-password", { body: { email: "b@example.test", locale: "en" } });

    expect(server.calls.map((c) => c.csrf)).toEqual(["token-1", "token-1"]);
    expect(server.issued()).toBe(1);
  });

  it("refreshes the token and retries exactly once when the server rejects it", async () => {
    const server = fakeServer((_, attempt) =>
      attempt === 1
        ? jsonResponse({ status: 400, code: "csrf_invalid" }, 400)
        : new Response(null, { status: 204 }),
    );

    const result = await api.POST("/api/v1/me/sessions/revoke-others");

    expect(result.response.status).toBe(204);
    expect(server.calls.map((c) => c.csrf)).toEqual(["token-1", "token-2"]);
  });

  it("does not loop when the retry is rejected too", async () => {
    const server = fakeServer(() => jsonResponse({ status: 400, code: "csrf_invalid" }, 400));

    const result = await api.POST("/api/v1/auth/logout");

    expect(result.response.status).toBe(400);
    expect(server.calls).toHaveLength(2);
  });

  it("drops the token after login because it is bound to the identity", async () => {
    const server = fakeServer(() => new Response(null, { status: 204 }));

    await api.POST("/api/v1/auth/login", {
      body: { email: "a@example.test", password: "x", rememberMe: false },
    });
    await api.POST("/api/v1/auth/logout");

    expect(server.calls.map((c) => c.csrf)).toEqual(["token-1", "token-2"]);
  });
});

describe("ApiError", () => {
  it("exposes the problem code and field errors", () => {
    const error = new ApiError(400, { code: "handle_taken", errors: { handle: ["handle_taken"] } });

    expect(error.code).toBe("handle_taken");
    expect(error.fieldErrors).toEqual({ handle: "handle_taken" });
  });

  it("maps 429 without a code to rate_limited", () => {
    expect(new ApiError(429).code).toBe("rate_limited");
  });

  it("unwrap throws ApiError for non-2xx responses", () => {
    expect(() => unwrap({ error: { code: "x" }, response: new Response(null, { status: 404 }) })).toThrow(
      ApiError,
    );
  });
});
