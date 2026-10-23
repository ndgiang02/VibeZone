"use client";

import { Disc3, Pencil, Play, Shuffle } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { useAlbum } from "@/features/catalog/api";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, isNotFound } from "@/features/tracks/api";
import { artHue, TrackArt } from "@/features/tracks/track-art";
import { TrackList } from "@/features/tracks/track-list";

/** Album page: cover, title, artist, release info, play/shuffle, and the tracklist in album order. */
export function AlbumView({ albumId }: { albumId: string }) {
  const t = useTranslations("album");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const format = useFormatter();
  const album = useAlbum(albumId);
  const playQueue = usePlayerStore((s) => s.playQueue);

  if (album.isPending) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-72 rounded-3xl" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  if (album.isError) {
    return isNotFound(album.error) ? (
      <ErrorState title={t("notFoundTitle")} description={t("notFoundBody")} />
    ) : (
      <ErrorState
        title={tErrors(errorCode(album.error))}
        onRetry={() => void album.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  const { album: info, tracks } = album.data;
  const playable = tracks.filter((track) => track.playable).map(toPlayerTrack);
  const totalSeconds = tracks.reduce((sum, track) => sum + (durationMsOf(track) ?? 0), 0) / 1000;
  const hue = artHue(info.id);

  function shufflePlay() {
    const store = usePlayerStore.getState();
    if (!store.shuffle) store.toggleShuffle();
    store.playQueue(playable, Math.floor(Math.random() * playable.length));
  }

  return (
    <div className="flex flex-col gap-8">
      <header
        className="relative isolate overflow-hidden rounded-3xl border p-6 md:p-10"
        style={{
          background: `radial-gradient(120% 140% at 0% 0%, oklch(0.42 0.11 ${hue} / 0.95) 0%, oklch(0.2 0.04 ${hue}) 55%, #0b0b0b 100%)`,
        }}
      >
        <div className="flex flex-col gap-6 sm:flex-row sm:items-end md:gap-8">
          <TrackArt
            id={info.id}
            artwork={info.cover}
            size="large"
            className="size-44 animate-in rounded-2xl shadow-elevated duration-500 zoom-in-95 fade-in md:size-56"
          />
          <div className="flex min-w-0 flex-col gap-3">
            <p className="flex items-center gap-2 text-xs font-semibold tracking-[0.2em] text-white/70 uppercase">
              <Disc3 className="size-3.5" aria-hidden="true" />
              {t(`kind.${info.kind}`)}
              {info.visibility !== "Public" ? ` · ${tTrack(`visibility.${info.visibility}`)}` : null}
            </p>
            <h1 className="text-4xl font-bold tracking-tight break-words text-white md:text-5xl">
              {info.title}
            </h1>
            <div className="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm text-white/80">
              <Link
                href={`/artists/${info.artist.slug}`}
                className="flex items-center gap-2 font-semibold text-white hover:underline"
              >
                <ArtistAvatar
                  id={info.artist.id}
                  name={info.artist.name}
                  src={info.artist.avatarUrl}
                  className="size-7 text-[0.625rem]"
                />
                {info.artist.name}
              </Link>
              {info.releaseDate ? (
                <span>
                  {t("released", {
                    date: format.dateTime(new Date(info.releaseDate), { dateStyle: "medium" }),
                  })}
                </span>
              ) : null}
              {tracks.length > 0 ? (
                <span className="tabular-nums">
                  {t("stats", { count: tracks.length, duration: formatTime(totalSeconds) })}
                </span>
              ) : null}
            </div>
            <div className="mt-2 flex flex-wrap items-center gap-3">
              {playable.length > 0 ? (
                <>
                  <button
                    type="button"
                    onClick={() => playQueue(playable)}
                    className="inline-flex h-11 items-center gap-2 rounded-full bg-primary pr-6 pl-5 text-sm font-semibold text-primary-foreground shadow-[0_10px_30px_-8px_var(--accent-glow)] transition-[background-color,transform] duration-200 hover:scale-[1.03] hover:bg-primary-hover active:scale-95"
                  >
                    <Play className="size-4 fill-current" aria-hidden="true" />
                    {t("playAll")}
                  </button>
                  <button
                    type="button"
                    onClick={shufflePlay}
                    className="inline-flex h-11 items-center gap-2 rounded-full border border-white/20 bg-black/20 px-5 text-sm font-medium text-white backdrop-blur transition-colors hover:border-white/40 hover:bg-black/30"
                  >
                    <Shuffle className="size-4" aria-hidden="true" />
                    {t("shufflePlay")}
                  </button>
                </>
              ) : null}
              {info.isOwner ? (
                <Link
                  href="/studio"
                  className="inline-flex h-11 items-center gap-2 rounded-full border border-white/20 px-5 text-sm font-medium text-white transition-colors hover:bg-white/10"
                >
                  <Pencil className="size-4" aria-hidden="true" />
                  {t("manage")}
                </Link>
              ) : null}
            </div>
          </div>
        </div>
      </header>

      {info.description ? (
        <p className="max-w-3xl text-sm whitespace-pre-line text-muted-foreground">{info.description}</p>
      ) : null}

      <section aria-labelledby="album-tracks" className="flex flex-col gap-3">
        <h2 id="album-tracks" className="text-xl font-semibold tracking-tight">
          {t("tracks")}
        </h2>
        {tracks.length === 0 ? (
          <EmptyState icon={Disc3} title={t("emptyTitle")} description={t("emptyBody")} />
        ) : (
          <TrackList
            tracks={tracks}
            showArtist={false}
            numbered={(track, i) => Number(track.trackNumber ?? i + 1)}
          />
        )}
      </section>
    </div>
  );
}
