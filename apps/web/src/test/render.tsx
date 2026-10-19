import { QueryClientProvider } from "@tanstack/react-query";
import { render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import en from "../../messages/en.json";
import vi from "../../messages/vi.json";
import { makeQueryClient } from "@/app/providers";

const messages = { en, vi };

/** Renders with the app's real QueryClient defaults and real translations (default: English). */
export function renderWithProviders(ui: ReactElement, { locale = "en" }: { locale?: "en" | "vi" } = {}) {
  const client = makeQueryClient();
  client.setDefaultOptions({ queries: { ...client.getDefaultOptions().queries, retryDelay: 0 } });
  return {
    client,
    ...render(
      <NextIntlClientProvider locale={locale} messages={messages[locale]} timeZone="Asia/Ho_Chi_Minh">
        <QueryClientProvider client={client}>{ui}</QueryClientProvider>
      </NextIntlClientProvider>,
    ),
  };
}

/** Test-only fetch stub returning a JSON response. */
export function jsonResponse(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" },
  });
}

export function emptyResponse(status: number) {
  return new Response(null, { status });
}
