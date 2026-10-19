import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { routerMock } from "@/test/navigation-mock";
import { emptyResponse, jsonResponse, renderWithProviders } from "@/test/render";
import { LoginForm } from "./login-form";

function stubApi(handler: (path: string, body: unknown) => Response) {
  const fetchMock = vi.fn<typeof fetch>(async (input) => {
    const request = input as Request;
    const path = new URL(request.url).pathname;
    if (path === "/api/v1/auth/csrf") return jsonResponse({ token: "t" });
    const body =
      request.method === "GET"
        ? null
        : await request
            .clone()
            .json()
            .catch(() => null);
    return handler(path, body);
  });
  vi.stubGlobal("fetch", fetchMock);
  return fetchMock;
}

async function fillAndSubmit(email = "fan@example.test", password = "a long passphrase") {
  await userEvent.type(screen.getByLabelText("Email"), email);
  await userEvent.type(screen.getByLabelText("Password"), password);
  await userEvent.click(screen.getByRole("button", { name: "Sign in" }));
}

describe("LoginForm", () => {
  it("validates required fields before calling the API", async () => {
    const fetchMock = stubApi(() => emptyResponse(204));
    renderWithProviders(<LoginForm />);

    await userEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(await screen.findAllByText("This field is required.")).toHaveLength(2);
    expect(screen.getByLabelText("Email")).toHaveAttribute("aria-invalid", "true");
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("signs in and returns to a safe next path", async () => {
    stubApi(() => emptyResponse(204));
    renderWithProviders(<LoginForm next="/settings" />);

    await fillAndSubmit();

    await waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith("/settings"));
  });

  it("ignores an external next URL (open-redirect guard)", async () => {
    stubApi(() => emptyResponse(204));
    renderWithProviders(<LoginForm next="//evil.example/phish" />);

    await fillAndSubmit();

    await waitFor(() => expect(routerMock.replace).toHaveBeenCalledWith("/"));
  });

  it("shows the localized generic error for bad credentials", async () => {
    stubApi(() => jsonResponse({ status: 401, code: "invalid_credentials" }, 401));
    renderWithProviders(<LoginForm />, { locale: "vi" });

    await userEvent.type(screen.getByLabelText("Email"), "fan@example.test");
    await userEvent.type(screen.getByLabelText("Mật khẩu"), "wrong password!");
    await userEvent.click(screen.getByRole("button", { name: "Đăng nhập" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Email hoặc mật khẩu không đúng.");
    expect(routerMock.replace).not.toHaveBeenCalled();
  });

  it("offers to resend the verification email when the address is unconfirmed", async () => {
    const fetchMock = stubApi((path) =>
      path === "/api/v1/auth/login"
        ? jsonResponse({ status: 403, code: "email_not_confirmed" }, 403)
        : emptyResponse(202),
    );
    renderWithProviders(<LoginForm />);

    await fillAndSubmit();
    await userEvent.click(await screen.findByRole("button", { name: "Resend verification email" }));

    expect(await screen.findByRole("status")).toHaveTextContent("a new link is on its way");
    const resend = fetchMock.mock.calls.find(
      ([r]) => new URL((r as Request).url).pathname === "/api/v1/auth/resend-verification",
    );
    expect(resend).toBeDefined();
  });

  it("explains rate limiting", async () => {
    stubApi(() => jsonResponse({ status: 429, title: "Too many requests." }, 429));
    renderWithProviders(<LoginForm />);

    await fillAndSubmit();

    expect(await screen.findByRole("alert")).toHaveTextContent("Too many attempts");
  });
});
