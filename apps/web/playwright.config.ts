import { defineConfig, devices } from "@playwright/test";

/**
 * End-to-end tests against the real stack: production build of the web app + the real API
 * (which needs PostgreSQL with migrations applied and Mailpit — see README).
 * Locally the API reads its connection string from user-secrets; CI passes ConnectionStrings__Database.
 */
const webPort = 3100;
const apiUrl = "http://localhost:5080";

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // Each worker runs a browser next to the API, Next.js and Docker; more than a few starves an 8 GB machine.
  workers: process.env.CI ? 2 : 3,
  reporter: process.env.CI ? [["github"], ["html", { open: "never" }]] : "list",
  use: {
    baseURL: `http://localhost:${webPort}`,
    trace: "retain-on-failure",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] } },
    { name: "mobile", use: { ...devices["Pixel 7"] } },
  ],
  webServer: [
    {
      command: "dotnet run --project ../../src/VibeZone.Api --no-launch-profile",
      url: `${apiUrl}/health/ready`,
      env: {
        ASPNETCORE_ENVIRONMENT: "Development",
        ASPNETCORE_URLS: apiUrl,
        // Email links must point at this test server; SMTP goes to Mailpit (localhost:1025 in Development).
        App__PublicBaseUrl: `http://localhost:${webPort}`,
        RateLimiting__AuthPermitsPerMinute: "200",
      },
      reuseExistingServer: !process.env.CI,
      timeout: 120_000,
      stdout: "ignore",
    },
    {
      command: "npm run start",
      url: `http://localhost:${webPort}`,
      env: { PORT: String(webPort), HOSTNAME: "localhost", API_INTERNAL_URL: apiUrl },
      reuseExistingServer: !process.env.CI,
      timeout: 60_000,
    },
  ],
});
