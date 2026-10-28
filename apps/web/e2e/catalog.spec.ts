import { expect, test, type Page } from "@playwright/test";
import { accessToken, becomeArtist, createAccount, signIn, uniqueWord, uploadReadyTrack } from "./content";
import { API_TIMEOUT, failOnCspViolations } from "./support";

/**
 * M2 acceptance: search → track detail, and playlist create → add → reorder → reload.
 * Tracks are uploaded through the API and converted by the real media worker (see content.ts).
 */

const playlistOrder = (page: Page) =>
  page.locator("main ol > li").getByRole("link").filter({ hasText: /^zq/ }).allTextContents();

test("search finds a track without its accents and opens the track page", async ({ page, request }) => {
  test.setTimeout(180_000);
  const assertNoCsp = failOnCspViolations(page);
  const word = uniqueWord();
  const artist = await createAccount(request, "sr", "Search Artist");
  const token = await accessToken(request, artist);
  await becomeArtist(request, token);
  const title = `${word} Mùa thu Hà Nội`;
  const trackId = await uploadReadyTrack(request, token, title);

  await page.goto("/en");
  // Desktop shows the box in the top bar, phones below it — one of the two is visible.
  await page
    .getByRole("searchbox", { name: "Search" })
    .locator("visible=true")
    .fill(`${word} mua thu ha noi`);
  await expect(page).toHaveURL(/\/en\/search\?q=/);
  await expect(page.getByRole("heading", { level: 1 })).toContainText(word);

  await page.getByRole("link", { name: title, exact: true }).first().click();
  await expect(page).toHaveURL(new RegExp(`/en/tracks/${trackId}$`));
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(title, { timeout: API_TIMEOUT });

  assertNoCsp();
});

test("create a playlist, add tracks, reorder, and the order survives a reload", async ({ page, request }) => {
  test.setTimeout(240_000);
  const assertNoCsp = failOnCspViolations(page);
  const word = uniqueWord();
  const artist = await createAccount(request, "pa", "Playlist Artist");
  const token = await accessToken(request, artist);
  await becomeArtist(request, token);
  const titles = ["Alpha", "Bravo", "Charlie"].map((name) => `${word} ${name}`);
  const ids: string[] = [];
  for (const [i, title] of titles.entries()) ids.push(await uploadReadyTrack(request, token, title, 3 + i));

  const listener = await createAccount(request, "pl", "Playlist Listener");
  await signIn(page, listener);

  // Create it in the Library → lands on the (empty) playlist page.
  const name = `Road trip ${word}`;
  await page.goto("/en/library");
  await page.getByLabel("Playlist name").fill(name);
  await page.getByRole("button", { name: "Create playlist" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(name, { timeout: API_TIMEOUT });
  const playlistUrl = page.url();

  // "Add to playlist" from each track page.
  for (const [i, id] of ids.entries()) {
    await page.goto(`/en/tracks/${id}`);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText(titles[i]!);
    // The labelled button in the track header (related-track rows have icon-only ones with the same name).
    await page
      .getByRole("button", { name: "Add to playlist" })
      .filter({ hasText: "Add to playlist" })
      .click();
    await page.getByRole("menuitem", { name }).click();
    await expect(page.getByRole("status").filter({ hasText: `Added to ${name}` })).toBeAttached();
  }

  await page.goto(playlistUrl);
  await expect.poll(() => playlistOrder(page)).toEqual(titles);

  // Move Charlie to the top with the arrow buttons (keyboard-operable).
  const moveUp = page.getByRole("button", { name: `Move ${titles[2]} up` });
  await moveUp.click();
  await expect.poll(() => playlistOrder(page)).toEqual([titles[0], titles[2], titles[1]]);
  await expect(moveUp).toBeEnabled();
  await moveUp.focus();
  await page.keyboard.press("Enter");
  await expect.poll(() => playlistOrder(page)).toEqual([titles[2], titles[0], titles[1]]);
  await expect(moveUp).toBeDisabled();

  await page.reload();
  await expect
    .poll(() => playlistOrder(page), { timeout: API_TIMEOUT })
    .toEqual([titles[2], titles[0], titles[1]]);

  assertNoCsp();
});
