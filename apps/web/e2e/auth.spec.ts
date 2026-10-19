import { expect, test } from "@playwright/test";
import { API_TIMEOUT, failOnCspViolations, newUser, waitForEmailLink } from "./support";

test.describe("account lifecycle (real API, database and mail server)", () => {
  test("register → confirm email → sign in → settings → sign out (English)", async ({ page, request }) => {
    const assertNoCsp = failOnCspViolations(page);
    const user = newUser();

    await page.goto("/en/register");
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Username").fill(user.handle);
    await page.getByLabel("Display name").fill("E2E Listener");
    await page.getByLabel("Password").fill(user.password);
    await page.getByRole("button", { name: "Create account" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText(user.email, {
      timeout: API_TIMEOUT,
    });

    const link = await waitForEmailLink(request, user.email, "Confirm your VibeZone email");
    expect(new URL(link).pathname).toBe("/en/verify-email");
    await page.goto(link);
    await expect(page.getByRole("heading", { name: "Email confirmed" })).toBeVisible();

    await page.getByRole("link", { name: "Sign in" }).first().click();
    await expect(page).toHaveURL(/\/en\/login$/);
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Password").fill(user.password);
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/en$/);

    await page.getByRole("button", { name: "Account menu" }).first().click();
    await page.getByRole("menuitem", { name: "Settings" }).click();
    await expect(page.getByRole("heading", { name: "Settings", level: 1 })).toBeVisible();
    await expect(page.getByRole("main").getByText(`@${user.handle}`)).toBeVisible();
    await expect(page.getByLabel("Display name")).toHaveValue("E2E Listener");
    await expect(page.getByText("This device")).toBeVisible();

    await page.getByRole("button", { name: "Account menu" }).first().click();
    await page.getByRole("menuitem", { name: "Sign out" }).click();
    await expect(page.getByRole("link", { name: "Sign in" }).first()).toBeVisible();

    // Signed out for real: the settings page sends us to login and keeps the return path.
    await page.goto("/en/settings");
    await expect(page).toHaveURL(/\/en\/login\?next=%2Fsettings$/);
    assertNoCsp();
  });

  test("Vietnamese UI gets a Vietnamese email, and wrong passwords get a generic error", async ({
    page,
    request,
  }) => {
    const user = newUser("vi");

    await page.goto("/vi/register");
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Tên người dùng").fill(user.handle);
    await page.getByLabel("Mật khẩu").fill(user.password);
    await page.getByRole("button", { name: "Tạo tài khoản" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText("Kiểm tra hộp thư", {
      timeout: API_TIMEOUT,
    });

    const link = await waitForEmailLink(request, user.email, "Xác nhận email VibeZone");
    expect(new URL(link).pathname).toBe("/vi/verify-email");
    await page.goto(link);
    await expect(page.getByRole("heading", { name: "Đã xác nhận email" })).toBeVisible();

    await page.goto("/vi/login");
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Mật khẩu").fill("sai mật khẩu rồi");
    await page.getByRole("button", { name: "Đăng nhập" }).click();
    await expect(page.getByRole("main").getByRole("alert")).toHaveText("Email hoặc mật khẩu không đúng.");
  });

  test("forgot password → email link → new password works and the old one doesn't", async ({
    page,
    request,
  }) => {
    const user = newUser("reset");
    // Create and confirm the account through the UI-facing API flow.
    await page.goto("/en/register");
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Username").fill(user.handle);
    await page.getByLabel("Password").fill(user.password);
    await page.getByRole("button", { name: "Create account" }).click();
    await page.goto(await waitForEmailLink(request, user.email, "Confirm"));
    await expect(page.getByRole("heading", { name: "Email confirmed" })).toBeVisible();

    await page.goto("/en/forgot-password");
    await page.getByLabel("Email").fill(user.email);
    await page.getByRole("button", { name: "Send reset link" }).click();
    await expect(page.getByRole("main").getByRole("status")).toContainText(user.email, {
      timeout: API_TIMEOUT,
    });

    await page.goto(await waitForEmailLink(request, user.email, "Reset your VibeZone password"));
    await page.getByLabel("New password", { exact: true }).fill("a brand new passphrase");
    await page.getByLabel("Confirm new password").fill("a brand new passphrase");
    await page.getByRole("button", { name: "Update password" }).click();
    await expect(page.getByRole("heading", { name: "Password updated" })).toBeVisible();

    await page.goto("/en/login");
    await page.getByLabel("Email").fill(user.email);
    await page.getByLabel("Password").fill(user.password);
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page.getByRole("main").getByRole("alert")).toHaveText("Email or password is incorrect.");

    await page.getByLabel("Password").fill("a brand new passphrase");
    await page.getByRole("button", { name: "Sign in" }).click();
    await expect(page).toHaveURL(/\/en$/);
  });

  test("language switcher keeps the current page", async ({ page }) => {
    await page.goto("/vi/login");
    await page.getByRole("button", { name: "Ngôn ngữ" }).first().click();
    await page.getByRole("menuitem", { name: "English" }).click();

    await expect(page).toHaveURL(/\/en\/login$/);
    await expect(page.getByRole("heading", { name: "Welcome back" })).toBeVisible();
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
  });
});
