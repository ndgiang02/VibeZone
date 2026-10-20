import { expect, test, type Page } from "@playwright/test";
import { API_TIMEOUT, failOnCspViolations, newUser, waitForEmailLink } from "./support";

/**
 * Upload → processing → playback through the real API, object storage and media worker (FFmpeg).
 * Needs the worker running against the same database/storage as the API (see README → E2E).
 */

/** 16-bit PCM mono WAV: a tone with a slow swell, generated here so no binary fixtures are needed. */
function toneWav(seconds: number, frequency: number, sampleRate = 22_050) {
  const frames = seconds * sampleRate;
  const wav = Buffer.alloc(44 + frames * 2);
  wav.write("RIFF", 0, "ascii");
  wav.writeUInt32LE(36 + frames * 2, 4);
  wav.write("WAVEfmt ", 8, "ascii");
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20); // PCM
  wav.writeUInt16LE(1, 22); // mono
  wav.writeUInt32LE(sampleRate, 24);
  wav.writeUInt32LE(sampleRate * 2, 28);
  wav.writeUInt16LE(2, 32);
  wav.writeUInt16LE(16, 34);
  wav.write("data", 36, "ascii");
  wav.writeUInt32LE(frames * 2, 40);
  for (let i = 0; i < frames; i++) {
    const envelope = 0.3 + 0.6 * Math.abs(Math.sin((Math.PI * i) / sampleRate));
    wav.writeInt16LE(
      Math.round(Math.sin((2 * Math.PI * frequency * i) / sampleRate) * envelope * 32767),
      44 + i * 2,
    );
  }
  return wav;
}

const audioState = (page: Page) =>
  page.locator("audio").evaluate((a: HTMLAudioElement) => ({ paused: a.paused, time: a.currentTime }));

/** One step: audio + details in a single request (ADR-0009). */
async function uploadTrack(page: Page, title: string, seconds: number, frequency: number) {
  await page.getByLabel(/Choose an audio file/).setInputFiles({
    name: `${title}.wav`,
    mimeType: "audio/wav",
    buffer: toneWav(seconds, frequency),
  });
  await expect(page.getByLabel("Title")).toHaveValue(title); // suggested from the file name
  await page.getByLabel("Visibility").selectOption("Public");
  await page.getByRole("button", { name: "Upload", exact: true }).click();
  await expect(page.getByText("Uploaded!")).toBeVisible({ timeout: API_TIMEOUT });
  await page.getByRole("button", { name: "Upload another" }).click();
  // The worker converts it → the list polls until the track is playable.
  const item = page
    .getByRole("listitem")
    .filter({ has: page.getByRole("link", { name: title, exact: true }) });
  await expect(item.getByRole("button", { name: `Play ${title}` })).toBeVisible({ timeout: 120_000 });
}

test("upload in Studio → Ready → play, pause, seek and next with the global player", async ({
  page,
  request,
  isMobile,
}) => {
  test.setTimeout(240_000);
  const assertNoCsp = failOnCspViolations(page);
  const user = newUser("pl");
  const slug = `a-${user.handle}`.toLowerCase().slice(0, 40);

  // Account (API + real email), then sign in through the UI.
  const register = await request.post("/api/v1/auth/register", {
    data: {
      email: user.email,
      handle: user.handle,
      password: user.password,
      displayName: "E2E Artist",
      locale: "en",
    },
  });
  expect(register.status()).toBe(202);
  await page.goto(await waitForEmailLink(request, user.email, "Confirm your VibeZone email"));
  await expect(page.getByRole("heading", { name: "Email confirmed" })).toBeVisible();
  await page.goto("/en/login");
  await page.getByLabel("Email").fill(user.email);
  await page.getByLabel("Password").fill(user.password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/en$/);

  await page.goto("/en/studio");
  await page.getByLabel("Artist name").fill("E2E Artist");
  await page.getByLabel("Profile URL").fill(slug);
  await page.getByRole("button", { name: "Create artist profile" }).click();
  await expect(page.getByRole("heading", { name: "Upload a track" })).toBeVisible({ timeout: API_TIMEOUT });

  await uploadTrack(page, "Morning tone", 40, 440);
  await uploadTrack(page, "Evening tone", 30, 660);

  // The artist page lists both (newest first) and plays them as one queue.
  await page.goto(`/en/artists/${slug}`);
  await page.getByRole("button", { name: "Play Evening tone" }).click();
  const player = page.getByRole("region", { name: "Player" });
  await expect(player.getByRole("link", { name: "Evening tone" })).toBeVisible();
  await expect.poll(async () => (await audioState(page)).time, { timeout: 20_000 }).toBeGreaterThan(1);

  // Pause / resume from the player bar.
  await player.getByRole("button", { name: "Pause", exact: true }).click();
  await expect.poll(async () => (await audioState(page)).paused).toBe(true);
  await player.getByRole("button", { name: "Play", exact: true }).click();
  await expect.poll(async () => (await audioState(page)).paused).toBe(false);

  // Seek: the slider on desktop (hidden on small screens), keyboard shortcut everywhere.
  if (!isMobile) {
    await player.getByRole("slider", { name: "Seek" }).fill("20");
    await expect.poll(async () => (await audioState(page)).time).toBeGreaterThanOrEqual(20);
  }
  const beforeShortcut = (await audioState(page)).time;
  await page.locator("body").press("ArrowLeft");
  await expect.poll(async () => (await audioState(page)).time).toBeLessThan(beforeShortcut - 2);

  // Keyboard-only: Space toggles playback.
  await page.locator("body").press(" ");
  await expect.poll(async () => (await audioState(page)).paused).toBe(true);
  await page.locator("body").press(" ");
  await expect.poll(async () => (await audioState(page)).paused).toBe(false);

  // Playback survives client-side navigation (one audio engine in the app shell).
  const beforeNavigation = (await audioState(page)).time;
  await page.getByRole("link", { name: "Home" }).first().click();
  await expect(page).toHaveURL(/\/en$/);
  await expect(player.getByRole("link", { name: "Evening tone" })).toBeVisible();
  await expect.poll(async () => (await audioState(page)).time).toBeGreaterThan(beforeNavigation);
  await expect(page.locator("audio")).toHaveCount(1);

  // Next track in the queue.
  await player.getByRole("button", { name: "Next track" }).click();
  await expect(player.getByRole("link", { name: "Morning tone" })).toBeVisible();
  await expect.poll(async () => (await audioState(page)).time, { timeout: 20_000 }).toBeGreaterThan(1);

  assertNoCsp();
});
