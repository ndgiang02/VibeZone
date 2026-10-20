"use client";

import { useTranslations } from "next-intl";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { Skeleton } from "@/components/states/states";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { demoArtists } from "@/features/showcase/data";
import { DemoBadge } from "@/features/showcase/demo-badge";
import { showcaseEnabled } from "@/features/showcase/showcase";
import { type Track, useNewReleases } from "@/features/tracks/api";
import { HOME_RELEASES } from "@/features/tracks/new-releases";

const MAX_ARTISTS = 5;

/** Distinct artists of the given tracks, in order of first appearance. */
export function artistsOf(tracks: Track[]) {
  const seen = new Map<string, Track["artist"]>();
  for (const track of tracks) if (!seen.has(track.artist.id)) seen.set(track.artist.id, track.artist);
  return [...seen.values()];
}

/**
 * Artists behind the newest releases (real data, linking to their pages). With an empty catalog the
 * showcase shows badged sample names (no links, no follower numbers).
 * `list` = side panel on desktop; `rail` = horizontal scroller on smaller screens.
 */
export function ArtistPanel({ variant, className }: { variant: "list" | "rail"; className?: string }) {
  const t = useTranslations();
  const releases = useNewReleases(HOME_RELEASES);
  const real = artistsOf(releases.data ?? []).slice(0, MAX_ARTISTS);
  const demo = releases.isSuccess && real.length === 0 && showcaseEnabled;
  const items = demo
    ? demoArtists.map((a) => ({ id: a.id, name: a.name, href: null, avatarUrl: null }))
    : real.map((a) => ({ id: a.id, name: a.name, href: `/artists/${a.slug}`, avatarUrl: a.avatarUrl }));

  if (releases.isError || (releases.isSuccess && items.length === 0 && variant === "rail")) return null;

  return (
    <section aria-labelledby={`artists-${variant}`} className={className}>
      <h2 id={`artists-${variant}`} className="mb-3 flex items-center gap-2 text-base font-semibold">
        {t("home.artists.title")}
        {demo ? <DemoBadge /> : null}
      </h2>

      {releases.isPending ? (
        <div className={cn("flex gap-3", variant === "list" && "flex-col")} aria-label={t("common.loading")}>
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className={variant === "list" ? "h-14" : "size-24 shrink-0 rounded-full"} />
          ))}
        </div>
      ) : items.length === 0 ? (
        <p className="rounded-xl border border-dashed p-4 text-sm text-muted-foreground">
          {t("home.artists.empty")}
        </p>
      ) : (
        <ul
          className={cn(
            variant === "list"
              ? "flex flex-col gap-1"
              : "vz-no-scrollbar -mx-4 flex snap-x gap-4 overflow-x-auto px-4 pb-1 md:-mx-6 md:px-6",
          )}
        >
          {items.map((artist) => {
            const body = (
              <>
                <ArtistAvatar
                  id={artist.id}
                  name={artist.name}
                  src={artist.avatarUrl}
                  className={variant === "list" ? "size-11" : "size-20 text-xl"}
                />
                <span className={cn("min-w-0", variant === "rail" && "w-24 text-center")}>
                  <span className="block truncate text-sm font-medium">{artist.name}</span>
                  <span className="block truncate text-xs text-muted-foreground">{t("artist.label")}</span>
                </span>
              </>
            );
            const layout = cn(
              "flex items-center rounded-xl transition-colors duration-200",
              variant === "list" ? "gap-3 p-2" : "snap-start flex-col gap-2 p-1",
            );
            return (
              <li key={artist.id} className={cn(variant === "rail" && "shrink-0")}>
                {artist.href ? (
                  <Link href={artist.href} className={cn(layout, "hover:bg-surface-hover")}>
                    {body}
                  </Link>
                ) : (
                  <div className={layout}>{body}</div>
                )}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
