"use client";

import { ListMusic, Loader2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { errorCode } from "@/lib/api/errors";
import { GenreBrowser } from "@/features/catalog/genre-browser";
import { GRID } from "@/features/home/media-section";
import { usePublicPlaylists } from "@/features/playlists/api";
import { PlaylistCard } from "@/features/playlists/playlist-card";
import { NewReleases } from "@/features/tracks/new-releases";

/** Browse without a query: genres, community (public) playlists, newest releases. Chronological, not personalised. */
export function DiscoverView() {
  const t = useTranslations("discover");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const playlists = usePublicPlaylists("", 12);
  const items = playlists.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div className="flex flex-col gap-10">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>

      <GenreBrowser />

      <section aria-labelledby="discover-playlists" className="flex flex-col gap-3">
        <h2 id="discover-playlists" className="text-xl font-semibold tracking-tight">
          {t("playlists")}
        </h2>
        {playlists.isPending ? (
          <div className={GRID} aria-busy="true" aria-label={tCommon("loading")}>
            {Array.from({ length: 6 }, (_, i) => (
              <Skeleton key={i} className="aspect-[4/5] rounded-xl" />
            ))}
          </div>
        ) : playlists.isError ? (
          <ErrorState
            title={tErrors(errorCode(playlists.error))}
            onRetry={() => void playlists.refetch()}
            retryLabel={tCommon("retry")}
          />
        ) : items.length === 0 ? (
          <EmptyState icon={ListMusic} title={t("playlistsEmpty")} />
        ) : (
          <>
            <ul className={GRID}>
              {items.map((playlist, i) => (
                <li
                  key={playlist.id}
                  className="animate-in duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
                  style={{ animationDelay: `${Math.min(i % 12, 10) * 40}ms` }}
                >
                  <PlaylistCard playlist={playlist} />
                </li>
              ))}
            </ul>
            {playlists.hasNextPage ? (
              <Button
                variant="outline"
                size="sm"
                className="self-start rounded-full"
                disabled={playlists.isFetchingNextPage}
                onClick={() => void playlists.fetchNextPage()}
              >
                {playlists.isFetchingNextPage ? (
                  <Loader2 className="size-3.5 animate-spin" aria-hidden="true" />
                ) : null}
                {tCommon("showMore")}
              </Button>
            ) : null}
          </>
        )}
      </section>

      <NewReleases />
    </div>
  );
}
