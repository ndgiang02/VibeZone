"use client";

import { Music } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { type Genre, useGenres } from "./use-genres";

export function GenreBrowser() {
  const t = useTranslations("home.genres");
  const tCommon = useTranslations("common");
  const { data, isPending, isError, refetch, isRefetching } = useGenres();

  return (
    <section
      aria-labelledby="genres-heading"
      aria-busy={isPending || isRefetching}
      className="flex flex-col gap-5"
    >
      <h2 id="genres-heading" className="text-title font-semibold">
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
  return (
    <div
      className="relative flex aspect-[4/3] items-end overflow-hidden rounded-xl p-3 shadow-card"
      style={{ background: tileGradient(genre.slug) }}
    >
      <span className="relative text-base font-semibold text-white drop-shadow-sm">{genre.name}</span>
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

/** Deterministic, decorative per-genre colour so tiles are distinguishable without artwork. */
function tileGradient(slug: string) {
  let hash = 0;
  for (const char of slug) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }
  const hue = hash % 360;
  return `linear-gradient(135deg, oklch(0.55 0.15 ${hue}) 0%, oklch(0.3 0.08 ${(hue + 40) % 360}) 100%)`;
}
