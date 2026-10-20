"use client";

import { useTranslations } from "next-intl";
import { useId, useState, type ReactNode } from "react";
import { cn } from "@/lib/utils";

export const GRID = "grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 2xl:grid-cols-6";
/** Collapsed: two rows on phones/tablets, one row on desktop — always whole rows. */
const COLLAPSED =
  "max-sm:[&>li:nth-child(n+5)]:hidden sm:max-lg:[&>li:nth-child(n+7)]:hidden lg:max-2xl:[&>li:nth-child(n+5)]:hidden 2xl:[&>li:nth-child(n+7)]:hidden";
/** The smallest collapsed count; with more items than this "View all" has something to reveal. */
const MIN_COLLAPSED = 4;

/**
 * Titled home section with an optional "View all / Show less" toggle that reveals the rest of the items
 * already loaded (there are no dedicated listing pages yet).
 */
export function MediaSection<T>({
  title,
  badge,
  note,
  items,
  renderItem,
  getKey,
  className,
  busy,
  fallback,
}: {
  title: string;
  badge?: ReactNode;
  note?: string;
  items: T[];
  renderItem: (item: T) => ReactNode;
  getKey: (item: T) => string;
  className?: string;
  busy?: boolean;
  /** Rendered instead of the grid (loading, error or empty state). */
  fallback?: ReactNode;
}) {
  const t = useTranslations("home");
  const headingId = useId();
  const [expanded, setExpanded] = useState(false);

  return (
    <section aria-labelledby={headingId} aria-busy={busy} className={cn("flex flex-col gap-4", className)}>
      <div className="flex items-end justify-between gap-4">
        <div className="min-w-0">
          <h2 id={headingId} className="flex items-center gap-2 text-xl font-semibold tracking-tight">
            {title}
            {badge}
          </h2>
          {note ? <p className="mt-0.5 text-xs text-subtle-foreground">{note}</p> : null}
        </div>
        {!fallback && items.length > MIN_COLLAPSED ? (
          <button
            type="button"
            aria-expanded={expanded}
            onClick={() => setExpanded((value) => !value)}
            className="shrink-0 rounded-full px-3 py-1 text-sm font-medium text-muted-foreground transition-colors hover:bg-surface-hover hover:text-primary"
          >
            {expanded ? t("showLess") : t("viewAll")}
          </button>
        ) : null}
      </div>
      {fallback ?? (
        <ul className={cn(GRID, !expanded && COLLAPSED)}>
          {items.map((item, i) => (
            <li
              key={getKey(item)}
              className="animate-in duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
              style={{ animationDelay: `${Math.min(i, 10) * 40}ms` }}
            >
              {renderItem(item)}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function CardGridSkeleton({ label }: { label: string }) {
  return (
    <ul className={cn(GRID, COLLAPSED)} aria-label={label}>
      {Array.from({ length: 6 }, (_, i) => (
        <li key={i} className="flex flex-col gap-3 rounded-xl bg-surface-card p-3">
          <div className="aspect-square w-full animate-pulse rounded-lg bg-surface-hover" />
          <div className="h-3.5 w-3/4 animate-pulse rounded bg-surface-hover" />
          <div className="h-3 w-1/2 animate-pulse rounded bg-surface-hover" />
        </li>
      ))}
    </ul>
  );
}
