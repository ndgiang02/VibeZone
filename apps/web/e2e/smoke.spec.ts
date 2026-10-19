import { expect, test } from "@playwright/test";
import { failOnCspViolations } from "./support";

test("home renders genres served by the real API through the /api rewrite", async ({ page }) => {
  const assertNoCsp = failOnCspViolations(page);
  const genresResponse = page.waitForResponse(
    (r) => new URL(r.url()).pathname === "/api/v1/genres" && r.request().method() === "GET",
  );

  await page.goto("/en");

  const response = await genresResponse;
  expect(response.status()).toBe(200);
  // Same origin as the page: the browser never talks to the API host directly.
  expect(new URL(response.url()).origin).toBe(new URL(page.url()).origin);

  const genres = page.getByRole("region", { name: "Browse by genre" });
  await expect(genres.getByText("V-Pop", { exact: true })).toBeVisible();
  await expect(genres.getByRole("listitem")).toHaveCount(23);
  await expect(page.getByRole("heading", { level: 1 })).toContainText("independent sound");
  assertNoCsp();
});

test("Vietnamese is the default language", async ({ page }) => {
  await page.goto("/vi");

  await expect(page.locator("html")).toHaveAttribute("lang", "vi");
  await expect(page.getByRole("heading", { level: 2, name: "Duyệt theo thể loại" })).toBeVisible();
});

test("paths without a locale redirect to one", async ({ page }) => {
  await page.goto("/settings");

  await expect(page).toHaveURL(/\/(vi|en)\/(settings|login)/);
});

test("unknown routes show the localized not-found page with a 404", async ({ page }) => {
  const response = await page.goto("/en/definitely-not-a-page");

  expect(response?.status()).toBe(404);
  await expect(page.getByRole("heading", { name: "Page not found" })).toBeVisible();
  await page.getByRole("link", { name: "Back to home" }).click();
  await expect(page).toHaveURL(/\/en$/);
});

test("skip link moves focus to the main content", async ({ page, isMobile }) => {
  test.skip(isMobile, "Keyboard navigation is checked on desktop.");
  await page.goto("/en");

  await page.keyboard.press("Tab");
  const skip = page.getByRole("link", { name: "Skip to content" });
  await expect(skip).toBeFocused();
  await page.keyboard.press("Enter");
  await expect(page.locator("#main")).toBeFocused();
});

test("pages send security headers including a per-request CSP nonce", async ({ request }) => {
  const first = await request.get("/en");
  const second = await request.get("/en");

  const headers = first.headers();
  expect(headers["x-content-type-options"]).toBe("nosniff");
  expect(headers["x-frame-options"]).toBe("DENY");
  expect(headers["x-powered-by"]).toBeUndefined();

  const csp = headers["content-security-policy"]!;
  expect(csp).toContain("script-src 'self' 'nonce-");
  expect(csp).toContain("'strict-dynamic'");
  expect(csp).toContain("object-src 'none'");
  expect(csp).toContain("frame-ancestors 'none'");
  expect(csp).not.toContain("unsafe-eval");
  const nonce = (value: string) => value.match(/'nonce-([^']+)'/)?.[1];
  expect(nonce(csp)).toBeTruthy();
  expect(nonce(csp)).not.toBe(nonce(second.headers()["content-security-policy"]!));
  // Next.js stamps the same nonce onto its inline scripts.
  expect(await first.text()).toContain(`nonce="${nonce(csp)}"`);
});
