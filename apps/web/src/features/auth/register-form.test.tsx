import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { emptyResponse, jsonResponse, renderWithProviders } from "@/test/render";
import { RegisterForm } from "./register-form";
import { safeNextPath } from "./schemas";

function stubRegister(response: () => Response) {
  const bodies: unknown[] = [];
  vi.stubGlobal(
    "fetch",
    vi.fn<typeof fetch>(async (input) => {
      const request = input as Request;
      if (new URL(request.url).pathname === "/api/v1/auth/csrf") return jsonResponse({ token: "t" });
      bodies.push(await request.clone().json());
      return response();
    }),
  );
  return bodies;
}

async function fill() {
  await userEvent.type(screen.getByLabelText("Email"), "new@example.test");
  await userEvent.type(screen.getByLabelText("Username"), "  DJ_Vibe ");
  await userEvent.type(screen.getByLabelText("Password"), "a long passphrase");
  await userEvent.click(screen.getByRole("button", { name: "Create account" }));
}

describe("RegisterForm", () => {
  it("submits normalized values with the UI locale and shows the check-your-inbox state", async () => {
    const bodies = stubRegister(() => emptyResponse(202));
    renderWithProviders(<RegisterForm />);

    await fill();

    expect(await screen.findByRole("status")).toHaveTextContent("new@example.test");
    expect(bodies).toEqual([
      {
        email: "new@example.test",
        handle: "dj_vibe",
        password: "a long passphrase",
        displayName: null,
        locale: "en",
      },
    ]);
  });

  it("puts a server field error on the matching field", async () => {
    stubRegister(() =>
      jsonResponse({ status: 409, code: "handle_taken", errors: { handle: ["handle_taken"] } }, 409),
    );
    renderWithProviders(<RegisterForm />);

    await fill();

    expect(await screen.findByText("This username is already taken.")).toBeInTheDocument();
    expect(screen.getByLabelText("Username")).toHaveAttribute("aria-invalid", "true");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("enforces the password minimum client-side", async () => {
    const bodies = stubRegister(() => emptyResponse(202));
    renderWithProviders(<RegisterForm />);

    await userEvent.type(screen.getByLabelText("Email"), "new@example.test");
    await userEvent.type(screen.getByLabelText("Username"), "djvibe");
    await userEvent.type(screen.getByLabelText("Password"), "short");
    await userEvent.click(screen.getByRole("button", { name: "Create account" }));

    expect(await screen.findByText("Use at least 10 characters.")).toBeInTheDocument();
    expect(bodies).toHaveLength(0);
  });
});

describe("safeNextPath", () => {
  it.each([
    ["/settings", "/settings"],
    ["/settings?tab=1", "/settings?tab=1"],
    ["//evil.example", null],
    ["/\\evil.example", null],
    ["https://evil.example", null],
    ["", null],
    [undefined, null],
  ])("%s → %s", (input, expected) => {
    expect(safeNextPath(input)).toBe(expected);
  });
});
