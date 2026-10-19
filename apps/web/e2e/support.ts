import { expect, type APIRequestContext, type Page } from "@playwright/test";

export const MAILPIT_URL = process.env.MAILPIT_URL ?? "http://localhost:8025";
export const PASSWORD = "correct horse battery staple";

/** Steps that wait on the API (password hashing, first DB/key-ring access on a cold server). */
export const API_TIMEOUT = 15_000;

export function newUser(prefix = "e2e") {
  const id = `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 7)}`;
  return { email: `${prefix}-${id}@example.test`, handle: `${prefix}${id}`.slice(0, 30), password: PASSWORD };
}

type MailSummary = { ID: string; Subject: string };

/** Polls the real Mailpit inbox for a message to `to` and returns the single call-to-action link. */
export async function waitForEmailLink(request: APIRequestContext, to: string, subjectContains: string) {
  let link: string | undefined;
  await expect
    .poll(
      async () => {
        const search = await request.get(`${MAILPIT_URL}/api/v1/search`, { params: { query: `to:"${to}"` } });
        const { messages } = (await search.json()) as { messages: MailSummary[] };
        const match = messages.find((m) => m.Subject.includes(subjectContains));
        if (!match) return null;
        const message = (await (await request.get(`${MAILPIT_URL}/api/v1/message/${match.ID}`)).json()) as {
          Text: string;
        };
        link = message.Text.match(/https?:\/\/\S+/)?.[0];
        return link ?? null;
      },
      { timeout: 20_000, message: `email "${subjectContains}" to ${to}` },
    )
    .not.toBeNull();
  return link!;
}

/** Fails the test if the browser reports any Content-Security-Policy violation. */
export function failOnCspViolations(page: Page) {
  const violations: string[] = [];
  page.on("console", (message) => {
    if (/Content Security Policy|Content-Security-Policy/i.test(message.text())) {
      violations.push(message.text());
    }
  });
  return () => expect(violations, "CSP violations").toEqual([]);
}
