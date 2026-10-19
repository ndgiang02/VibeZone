import type { AnchorHTMLAttributes, ReactNode } from "react";
import { vi } from "vitest";

/** Stand-in for "@/i18n/navigation" in unit tests (no App Router context exists in jsdom). */
export const routerMock = {
  replace: vi.fn(),
  push: vi.fn(),
  back: vi.fn(),
  refresh: vi.fn(),
  prefetch: vi.fn(),
};

type Href = string | { pathname: string; query?: Record<string, string> };

function toHref(href: Href) {
  if (typeof href === "string") return href;
  const query = href.query ? `?${new URLSearchParams(href.query)}` : "";
  return `${href.pathname}${query}`;
}

export const navigationMock = {
  Link: ({
    href,
    children,
    ...rest
  }: { href: Href; children: ReactNode } & AnchorHTMLAttributes<HTMLAnchorElement>) => (
    <a href={toHref(href)} {...rest}>
      {children}
    </a>
  ),
  useRouter: () => routerMock,
  usePathname: () => "/",
  redirect: vi.fn(),
  getPathname: ({ href }: { href: Href }) => toHref(href),
};
