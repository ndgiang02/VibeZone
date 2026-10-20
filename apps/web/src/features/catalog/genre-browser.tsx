"use client";

import { Music } from "lucide-react";
import { useTranslations } from "next-intl";
import { Artwork } from "@/components/media/artwork";
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
  return (
    <div
      className="group relative flex aspect-[4/3] items-start overflow-hidden rounded-xl p-3 shadow-card transition-transform duration-200 hover:-translate-y-0.5 motion-reduce:hover:translate-y-0"
      style={{ background: tileGradient(genre.slug) }}
    >
      <Artwork
        src={tileArtwork(genre.slug)}
        sizes="96px"
        className="absolute -right-3 -bottom-2 size-[45%] rotate-[18deg] rounded-md shadow-elevated transition-transform duration-300 group-hover:rotate-[12deg]"
      />
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

function slugHash(slug: string) {
  let hash = 0;
  for (const char of slug) {
    hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  }
  return hash;
}

/** Decorative illustrations (see /public/artwork) — mood pictures, not covers. */
const TILE_ARTWORK = [
  "aurora",
  "bass-rings",
  "chill-sunset",
  "desert-road",
  "disco-ball",
  "edm-stage",
  "equalizer",
  "forest-mist",
  "lofi-room",
  "neon-grid",
  "night-drive",
  "ocean-waves",
  "palm-sunset",
  "pop-blobs",
  "rain-window",
  "vinyl",
];

function tileArtwork(slug: string) {
  return `/artwork/${TILE_ARTWORK[slugHash(slug) % TILE_ARTWORK.length]}.svg`;
}

/** Deterministic, decorative per-genre colour so tiles are distinguishable. */
function tileGradient(slug: string) {
  const hue = slugHash(slug) % 360;
  return `linear-gradient(135deg, oklch(0.55 0.15 ${hue}) 0%, oklch(0.3 0.08 ${(hue + 40) % 360}) 100%)`;
}
