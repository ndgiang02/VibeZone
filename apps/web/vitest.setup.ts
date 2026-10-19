import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import { resetCsrfToken } from "@/lib/api/client";

vi.mock("@/i18n/navigation", async () => (await import("@/test/navigation-mock")).navigationMock);

afterEach(() => {
  cleanup();
  resetCsrfToken();
  vi.clearAllMocks();
});
