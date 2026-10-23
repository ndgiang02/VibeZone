"use client";

import { Loader2, Search, SearchX } from "lucide-react";
import { useTranslations } from "next-intl";
import { useSearchParams } from "next/navigation";
import { useState, type ReactNode } from "react";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { AlbumCard } from "@/features/albums/album-card";
import { useSearch, useSearchAll, type Album, type SearchKind } from "@/features/catalog/api";
import { GRID } from "@/features/home/media-section";
import { usePublicPlaylists } from "@/features/playlists/api";
import { PlaylistCard } from "@/features/playlists/playlist-card";
import type { Artist, Track } from "@/features/tracks/api";
import { TrackList } from "@/features/tracks/track-list";

/**
 * Search results for ?q= (the top-bar search box keeps the URL in sync while typing). Server-side search
 * over tracks, artists, albums (Catalog) and public playlists (Playlists); each kind can expand to all
 * results, page by page.
 */
export function SearchView() {
  const t = useTranslations("search");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const q = (useSearchParams().get("q") ?? "").trim();
  const results = useSearch(q);
  const playlists = usePublicPlaylists(q, 6);

  if (!q) {
    return <EmptyState icon={Search} title={t("startTitle")} description={t("startBody")} />;
  }

  if (results.isError) {
    return (
      <ErrorState
        title={tErrors(errorCode(results.error))}
        onRetry={() => void results.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  if (!results.data) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-10 w-72" />
        <Skeleton className="h-40 rounded-2xl" />
        <Skeleton className="h-40 rounded-2xl" />
      </div>
    );
  }

  const data = results.data;
  const playlistItems = playlists.data?.pages[0]?.items ?? [];
  const playlistTotal = playlists.data?.pages[0]?.meta.total ?? 0;
  const nothing =
    Number(data.tracks.total) + Number(data.artists.total) + Number(data.albums.total) + playlistTotal ===
      0 && !playlists.isPending;

  return (
    <section
      aria-labelledby="search-title"
      className={`flex flex-col gap-10 transition-opacity duration-200 ${results.isPlaceholderData ? "opacity-60" : ""}`}
    >
      <div>
        <h1 id="search-title" className="text-2xl font-bold tracking-tight break-words">
          {t("resultsTitle", { query: q })}
        </h1>
        <p className="mt-1 text-sm text-subtle-foreground">{t("scopeNote")}</p>
      </div>

      {nothing ? (
        <EmptyState icon={SearchX} title={t("noResultsTitle")} description={t("noResultsBody")} />
      ) : null}

      {Number(data.tracks.total) > 0 ? (
        <ResultGroup<Track>
          kind="tracks"
          q={q}
          title={t("tracks")}
          total={Number(data.tracks.total)}
          shown={data.tracks.items.length}
          render={(all) => <TrackList tracks={all ?? data.tracks.items} />}
        />
      ) : null}

      {Number(data.artists.total) > 0 ? (
        <ResultGroup<Artist>
          kind="artists"
          q={q}
          title={t("artists")}
          total={Number(data.artists.total)}
          shown={data.artists.items.length}
          render={(allArtists) => (
            <ul className={GRID}>
              {(allArtists ?? data.artists.items).map((artist, i) => (
                <li
                  key={artist.id}
                  className="animate-in duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
                  style={{ animationDelay: `${Math.min(i, 10) * 40}ms` }}
                >
                  <Link
                    href={`/artists/${artist.slug}`}
                    className="flex flex-col items-center gap-3 rounded-xl p-3 text-center transition-colors hover:bg-surface-hover"
                  >
                    <ArtistAvatar
                      id={artist.id}
                      name={artist.name}
                      src={artist.avatar?.large}
                      className="aspect-square h-auto w-full max-w-36 text-3xl shadow-card"
                    />
                    <span className="w-full truncate text-sm font-semibold">{artist.name}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        />
      ) : null}

      {Number(data.albums.total) > 0 ? (
        <ResultGroup<Album>
          kind="albums"
          q={q}
          title={t("albums")}
          total={Number(data.albums.total)}
          shown={data.albums.items.length}
          render={(allAlbums) => (
            <ul className={GRID}>
              {(allAlbums ?? data.albums.items).map((album, i) => (
                <li
                  key={album.id}
                  className="animate-in duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
                  style={{ animationDelay: `${Math.min(i, 10) * 40}ms` }}
                >
                  <AlbumCard album={album} />
                </li>
              ))}
            </ul>
          )}
        />
      ) : null}

      {playlistItems.length > 0 ? (
        <section className="flex flex-col gap-3">
          <GroupHeading title={t("playlists")} total={playlistTotal} />
          <ul className={GRID}>
            {playlists.data?.pages
              .flatMap((p) => p.items)
              .map((playlist) => (
                <li key={playlist.id} className="animate-in duration-500 fade-in slide-in-from-bottom-2">
                  <PlaylistCard playlist={playlist} />
                </li>
              ))}
          </ul>
          {playlists.hasNextPage ? (
            <ShowMore pending={playlists.isFetchingNextPage} onClick={() => void playlists.fetchNextPage()} />
          ) : null}
        </section>
      ) : null}
    </section>
  );
}

function GroupHeading({ title, total }: { title: string; total: number }) {
  const t = useTranslations("search");
  return (
    <div className="flex items-baseline gap-3">
      <h2 className="text-xl font-semibold tracking-tight">{title}</h2>
      <span className="text-sm text-subtle-foreground tabular-nums">{t("total", { count: total })}</span>
    </div>
  );
}

function ShowMore({ pending, onClick }: { pending: boolean; onClick: () => void }) {
  const tCommon = useTranslations("common");
  return (
    <Button
      variant="outline"
      size="sm"
      onClick={onClick}
      disabled={pending}
      className="self-start rounded-full"
    >
      {pending ? <Loader2 className="size-3.5 animate-spin" aria-hidden="true" /> : null}
      {tCommon("showMore")}
    </Button>
  );
}

/** A result kind: the overview items, or — after "Show more" — every result, loaded page by page. */
function ResultGroup<T>({
  kind,
  q,
  title,
  total,
  shown,
  render,
}: {
  kind: SearchKind;
  q: string;
  title: string;
  total: number;
  shown: number;
  render: (all: T[] | undefined) => ReactNode;
}) {
  const [expanded, setExpanded] = useState(false);
  const all = useSearchAll(kind, q, expanded);
  // The three kinds share one hook; the caller says which item type its kind returns.
  const pages = all.data?.pages as { items: unknown[] }[] | undefined;
  const items = expanded ? (pages?.flatMap((p) => p.items) as T[] | undefined) : undefined;

  return (
    <section className="flex flex-col gap-3">
      <GroupHeading title={title} total={total} />
      {render(items)}
      {!expanded && total > shown ? (
        <ShowMore pending={false} onClick={() => setExpanded(true)} />
      ) : expanded && all.hasNextPage ? (
        <ShowMore pending={all.isFetchingNextPage} onClick={() => void all.fetchNextPage()} />
      ) : null}
    </section>
  );
}
