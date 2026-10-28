import { execFileSync } from "node:child_process";
import path from "node:path";
import { expect, type APIRequestContext, type Page } from "@playwright/test";
import { API_TIMEOUT, newUser, waitForEmailLink } from "./support";

/**
 * Test content created through the real API (no database writes): confirmed accounts, artist profiles and
 * Ready tracks (uploaded and converted by the real media worker, like player.spec.ts). Staff roles are granted
 * with the operator command `grant-role` — the same way the first Admin is created in production.
 */

export type Account = ReturnType<typeof newUser> & { displayName: string };

export async function createAccount(
  request: APIRequestContext,
  prefix: string,
  displayName: string,
): Promise<Account> {
  const user = newUser(prefix);
  const register = await request.post("/api/v1/auth/register", {
    data: { ...user, displayName, locale: "en" },
  });
  expect(register.status()).toBe(202);
  const link = new URL(await waitForEmailLink(request, user.email, "Confirm your VibeZone email"));
  const verify = await request.post("/api/v1/auth/verify-email", {
    data: { userId: link.searchParams.get("userId"), token: link.searchParams.get("token") },
  });
  expect(verify.status()).toBe(204);
  return { ...user, displayName };
}

/** An app (bearer) token, so API setup steps don't touch the browser's cookie session. */
export async function accessToken(request: APIRequestContext, account: Account) {
  const response = await request.post("/api/v1/auth/token", {
    data: { email: account.email, password: account.password, deviceName: "Playwright" },
  });
  expect(response.status()).toBe(200);
  const { data } = (await response.json()) as { data: { accessToken: string } };
  return data.accessToken;
}

export async function becomeArtist(request: APIRequestContext, token: string) {
  const response = await request.post("/api/v1/me/artist", {
    headers: { Authorization: `Bearer ${token}` },
    data: { bio: null },
  });
  expect(response.status()).toBe(201);
  const { data } = (await response.json()) as { data: { id: string; slug: string } };
  return data;
}

/** 16-bit PCM mono WAV tone (no binary fixtures). */
export function toneWav(seconds: number, frequency: number, sampleRate = 8_000) {
  const frames = seconds * sampleRate;
  const wav = Buffer.alloc(44 + frames * 2);
  wav.write("RIFF", 0, "ascii");
  wav.writeUInt32LE(36 + frames * 2, 4);
  wav.write("WAVEfmt ", 8, "ascii");
  wav.writeUInt32LE(16, 16);
  wav.writeUInt16LE(1, 20);
  wav.writeUInt16LE(1, 22);
  wav.writeUInt32LE(sampleRate, 24);
  wav.writeUInt32LE(sampleRate * 2, 28);
  wav.writeUInt16LE(2, 32);
  wav.writeUInt16LE(16, 34);
  wav.write("data", 36, "ascii");
  wav.writeUInt32LE(frames * 2, 40);
  for (let i = 0; i < frames; i++) {
    wav.writeInt16LE(
      Math.round(Math.sin((2 * Math.PI * frequency * i) / sampleRate) * 0.5 * 32767),
      44 + i * 2,
    );
  }
  return wav;
}

/** Uploads audio + details in one request (ADR-0009) and waits until the worker made it Ready. */
export async function uploadReadyTrack(
  request: APIRequestContext,
  token: string,
  title: string,
  seconds = 5,
) {
  const headers = { Authorization: `Bearer ${token}` };
  const upload = await request.post("/api/v1/track-uploads", {
    headers,
    multipart: {
      title,
      visibility: "Public",
      audio: { name: "tone.wav", mimeType: "audio/wav", buffer: toneWav(seconds, 330 + seconds * 10) },
    },
  });
  expect(upload.status(), await upload.text()).toBe(202);
  const { data } = (await upload.json()) as { data: { trackId: string } };
  await expect
    .poll(
      async () => {
        const track = await request.get(`/api/v1/tracks/${data.trackId}`, { headers });
        return ((await track.json()) as { data: { status: string } }).data.status;
      },
      { timeout: 120_000, intervals: [1_000], message: `"${title}" becomes Ready` },
    )
    .toBe("Ready");
  return data.trackId;
}

/**
 * `grant-role` on the API host (already built by the Playwright web server), against the same database:
 * user-secrets locally, ConnectionStrings__Database in CI.
 */
export function grantRole(email: string, role: "Moderator" | "Admin") {
  execFileSync(
    "dotnet",
    [
      "run",
      "--project",
      path.resolve(__dirname, "../../../src/VibeZone.Api"),
      "--no-build",
      "--no-launch-profile",
      "--",
      "grant-role",
      email,
      role,
    ],
    { env: { ...process.env, ASPNETCORE_ENVIRONMENT: "Development" }, stdio: "pipe", timeout: 120_000 },
  );
}

export async function signIn(page: Page, account: Account) {
  await page.goto("/en/login");
  await page.getByLabel("Email").fill(account.email);
  await page.getByLabel("Password").fill(account.password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/en$/, { timeout: API_TIMEOUT });
}

/** A unique word for titles, so searches only match this test's content. */
export function uniqueWord() {
  return `zq${Math.random()
    .toString(36)
    .replace(/[^a-z]/g, "")
    .slice(0, 6)}`;
}
