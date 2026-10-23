"use client";

import { Search, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { useEffect, useRef } from "react";
import { usePathname, useRouter } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { useSearchStore } from "@/stores/search-store";

const DEBOUNCE_MS = 300;

/**
 * Search field in the top bar. Typing opens /search and keeps ?q= in sync (debounced, replacing history
 * entries while on the page); Enter searches immediately; Escape clears.
 */
export function SearchBox({ className }: { className?: string }) {
  const t = useTranslations("search");
  const query = useSearchStore((s) => s.query);
  const setQuery = useSearchStore((s) => s.setQuery);
  const pathname = usePathname();
  const router = useRouter();
  const urlQuery = useSearchParams().get("q") ?? "";
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const onSearchPage = pathname === "/search";

  // Opening /search?q=… directly (link, reload, back/forward) shows that query in the box.
  useEffect(() => {
    if (onSearchPage) setQuery(urlQuery);
  }, [onSearchPage, urlQuery, setQuery]);

  useEffect(() => () => void (timer.current && clearTimeout(timer.current)), []);

  function go(value: string, immediate: boolean) {
    if (timer.current) clearTimeout(timer.current);
    const navigate = () => {
      const q = value.trim();
      if (!q) return;
      const target = { pathname: "/search" as const, query: { q } };
      if (onSearchPage) router.replace(target);
      else router.push(target);
    };
    if (immediate) navigate();
    else timer.current = setTimeout(navigate, DEBOUNCE_MS);
  }

  return (
    <form
      role="search"
      className={cn("relative", className)}
      onSubmit={(event) => {
        event.preventDefault();
        go(query, true);
      }}
    >
      <Search
        className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-subtle-foreground"
        aria-hidden="true"
      />
      <input
        type="search"
        value={query}
        onChange={(event) => {
          setQuery(event.target.value);
          go(event.target.value, false);
        }}
        onKeyDown={(event) => event.key === "Escape" && setQuery("")}
        placeholder={t("placeholder")}
        aria-label={t("label")}
        autoComplete="off"
        maxLength={100}
        className="h-10 w-full rounded-full border border-transparent bg-surface-elevated pr-10 pl-11 text-sm text-foreground transition-[border-color,box-shadow,background-color] duration-200 outline-none placeholder:text-subtle-foreground hover:bg-surface-hover focus-visible:border-primary focus-visible:bg-surface-elevated focus-visible:shadow-[0_0_0_3px_var(--primary-subtle)] focus-visible:outline-none [&::-webkit-search-cancel-button]:appearance-none"
      />
      {query ? (
        <button
          type="button"
          onClick={() => setQuery("")}
          aria-label={t("clear")}
          className="absolute top-1/2 right-2 flex size-7 -translate-y-1/2 animate-in items-center justify-center rounded-full text-muted-foreground transition-colors duration-150 zoom-in-75 hover:bg-surface-hover hover:text-foreground"
        >
          <X className="size-4" aria-hidden="true" />
        </button>
      ) : null}
    </form>
  );
}
