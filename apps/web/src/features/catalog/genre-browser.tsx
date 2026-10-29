"use client";

import { Music } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { TrackArt } from "@/features/tracks/track-art";
import { cn } from "@/lib/utils";
import { type Genre, useGenres } from "./use-genres";

export function GenreBrowser() {
  const t = useTranslations("home.genres");
  const tCommon = useTranslations("common");
  const { data, isPending, isError, refetch, isRefetching } = useGenres();

  return (
    <section
      aria-labelledby="genres-heading"
      aria-busy={isPending || isRefetching}
      className="flex flex-col gap-4"
    >
      <h2 id="genres-heading" className="text-xl font-semibold tracking-tight">
        {t("title")}
      </h2>

      {isPending ? (
        <GenreGridSkeleton label={t("loading")} />
      ) : isError ? (
        <ErrorState
          title={t("errorTitle")}
          description={t("errorBody")}
          onRetry={() => void refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : data.length === 0 ? (
        <EmptyState icon={Music} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6">
          {data.map((genre) => (
            <li key={genre.slug}>
              <GenreTile genre={genre} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function GenreTile({ genre }: { genre: Genre }) {
  const covers = genre.covers ?? [];
  return (
    <div
      className="group relative flex aspect-[4/3] items-start overflow-hidden rounded-xl p-3 shadow-card transition-transform duration-200 hover:-translate-y-0.5 motion-reduce:hover:translate-y-0"
      style={{ background: tileGradient(genre.slug) }}
    >
      {covers.length > 0 ? (
        <CoverStack covers={covers} />
      ) : (
        <Music
          aria-hidden="true"
          className="absolute -right-2 -bottom-3 size-[48%] rotate-[18deg] text-white/15 transition-transform duration-300 group-hover:rotate-[10deg] motion-reduce:transition-none"
        />
      )}
      <span className="relative z-10 text-base font-semibold text-white drop-shadow-sm">{genre.name}</span>
    </div>
  );
}

/**
 * Up to three real covers from the genre's newest tracks, fanned out like a stack of records (the newest on
 * top). Hovering spreads the fan; reduced motion keeps it still.
 */
const FAN = [
  "z-30 rotate-[8deg] group-hover:translate-x-1 group-hover:rotate-[12deg]",
  "z-20 -translate-x-[22%] rotate-[-2deg] group-hover:-translate-x-[30%] group-hover:rotate-[-6deg]",
  "z-10 -translate-x-[44%] rotate-[-12deg] group-hover:-translate-x-[58%] group-hover:rotate-[-18deg]",
];

function CoverStack({ covers }: { covers: string[] }) {
  return (
    <div aria-hidden="true" className="absolute right-3 bottom-3 size-[46%]">
      {covers.slice(0, FAN.length).map((url, i) => (
        <TrackArt
          key={url}
          id={url}
          artwork={{ small: url, large: url }}
          className={cn(
            "absolute inset-0 size-full rounded-md shadow-elevated ring-1 ring-black/20 transition-transform duration-300 motion-reduce:transition-none",
            FAN[i],
          )}
        />
      ))}
    </div>
  );
}

function GenreGridSkeleton({ label }: { label: string }) {
  return (
    <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-6" aria-label={label}>
      {Array.from({ length: 12 }, (_, i) => (
        <li key={i}>
          <Skeleton className="aspect-[4/3] rounded-xl" />
        </li>
      ))}
    </ul>
  );
}

function slugHash(slug: string) {
  let hash = 0;
  for (const char of slug) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }
  return hash;
}

/** Deterministic, decorative per-genre colour so tiles are distinguishable. */
function tileGradient(slug: string) {
  const hue = slugHash(slug) % 360;
  return `linear-gradient(135deg, oklch(0.55 0.15 ${hue}) 0%, oklch(0.3 0.08 ${(hue + 40) % 360}) 100%)`;
}
