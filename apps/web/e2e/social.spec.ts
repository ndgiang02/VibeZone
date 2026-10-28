import { expect, test } from "@playwright/test";
import {
  accessToken,
  becomeArtist,
  createAccount,
  grantRole,
  signIn,
  uniqueWord,
  uploadReadyTrack,
} from "./content";
import { API_TIMEOUT, failOnCspViolations } from "./support";

/**
 * M4 acceptance: like/follow → counts and notifications; report → moderator takedown → unplayable → audited.
 * Notifications travel through the outbox (asynchronous), so their checks poll.
 */

test("like and follow update the counts and notify the artist", async ({ page, browser, request }) => {
  test.setTimeout(180_000);
  const assertNoCsp = failOnCspViolations(page);
  const word = uniqueWord();
  const artist = await createAccount(request, "la", "Liked Artist");
  const token = await accessToken(request, artist);
  const { slug } = await becomeArtist(request, token);
  const title = `${word} Heartbeat`;
  const trackId = await uploadReadyTrack(request, token, title);

  const fan = await createAccount(request, "lf", `Fan ${word}`);
  await signIn(page, fan);

  await page.goto(`/en/tracks/${trackId}`);
  const like = page.getByRole("button", { name: `Like ${title}` }).first();
  // The heart updates optimistically: wait for the server before navigating away (which would abort the request).
  const saved = page.waitForResponse(
    (r) => r.url().endsWith(`/api/v1/tracks/${trackId}/like`) && r.request().method() === "PUT",
  );
  await like.click();
  expect((await saved).status()).toBe(200);
  const unlike = page.getByRole("button", { name: `Unlike ${title}` }).first();
  await expect(unlike).toHaveAttribute("aria-pressed", "true", { timeout: API_TIMEOUT });
  await expect(unlike).toContainText("1");

  await page.goto(`/en/artists/${slug}`);
  await page.getByRole("button", { name: "Follow", exact: true }).click();
  await expect(page.getByRole("button", { name: "Following" })).toHaveAttribute("aria-pressed", "true");
  await expect(page.getByText("1 follower", { exact: true })).toBeVisible();

  // The counts survive a reload.
  await page.reload();
  await expect(page.getByText("1 follower", { exact: true })).toBeVisible({ timeout: API_TIMEOUT });

  // The artist sees both notifications (bell + page). Unread: the like, the follow and "ready to play" from the upload.
  const artistContext = await browser.newContext();
  const artistPage = await artistContext.newPage();
  await signIn(artistPage, artist);
  await expect(async () => {
    await artistPage.reload();
    await expect(artistPage.getByRole("button", { name: "Notifications, 3 unread" })).toBeVisible({
      timeout: 2_000,
    });
  }).toPass({ timeout: 30_000 });
  await artistPage.goto("/en/notifications");
  await expect(artistPage.getByText(`Fan ${word} liked “${title}”`)).toBeVisible({ timeout: API_TIMEOUT });
  await expect(artistPage.getByText(`Fan ${word} started following you`)).toBeVisible();
  await artistContext.close();

  assertNoCsp();
});

test("a reported track taken down by a moderator becomes unplayable and the takedown is audited", async ({
  page,
  browser,
  request,
}) => {
  test.setTimeout(240_000);
  const assertNoCsp = failOnCspViolations(page);
  const word = uniqueWord();
  const artist = await createAccount(request, "ra", "Reported Artist");
  const token = await accessToken(request, artist);
  await becomeArtist(request, token);
  const title = `${word} Borrowed beat`;
  const trackId = await uploadReadyTrack(request, token, title);

  // A listener reports it from the track page.
  const reporter = await createAccount(request, "rr", "Reporter");
  await signIn(page, reporter);
  await page.goto(`/en/tracks/${trackId}`);
  await page.getByRole("button", { name: "Report track" }).click();
  const dialog = page.getByRole("dialog", { name: "Report track" });
  await dialog.getByRole("radio", { name: /Copyright/ }).check();
  await dialog.getByLabel("Details (optional)").fill("Sampled without permission");
  await dialog.getByRole("button", { name: "Send report" }).click();
  await expect(page.getByText("Thanks for reporting")).toBeVisible({ timeout: API_TIMEOUT });

  // Staff (Admin includes the moderator policy and can read the audit log), granted by the operator command.
  const moderator = await createAccount(request, "rm", "Moderator");
  grantRole(moderator.email, "Admin");
  const staffContext = await browser.newContext();
  const staff = await staffContext.newPage();
  await signIn(staff, moderator);
  await staff.goto("/en/moderation");
  const report = staff
    .getByRole("article")
    .filter({ has: staff.getByRole("link", { name: title, exact: true }) });
  await expect(report).toBeVisible({ timeout: API_TIMEOUT });
  staff.once("dialog", (confirm) => void confirm.accept());
  await report.getByLabel("Note (optional, kept in the audit log)").fill("Confirmed copyright claim");
  await report.getByRole("button", { name: "Remove content" }).click();
  // Leaves the Open tab once handled.
  await expect(report).toBeHidden({ timeout: API_TIMEOUT });

  // Unplayable for everyone else: the page is gone, the stream is 404.
  await page.goto(`/en/tracks/${trackId}`);
  await expect(page.getByRole("main").getByRole("alert")).toContainText("Track not found", {
    timeout: API_TIMEOUT,
  });
  expect((await request.get(`/api/v1/tracks/${trackId}/stream`)).status()).toBe(404);
  expect((await page.request.get(`/api/v1/tracks/${trackId}/playback`)).status()).toBe(404);

  // Audited.
  const audit = await staff.request.get("/api/v1/admin/audit-log");
  expect(audit.status()).toBe(200);
  const { data } = (await audit.json()) as { data: { action: string; subjectId: string }[] };
  expect(data).toContainEqual(expect.objectContaining({ action: "content_removed", subjectId: trackId }));
  await staffContext.close();

  // The reporter hears back.
  await expect(async () => {
    await page.goto("/en/notifications");
    await expect(page.getByText(`Thanks — we removed “${title}” after your report.`)).toBeVisible({
      timeout: 2_000,
    });
  }).toPass({ timeout: 30_000 });

  assertNoCsp();
});
