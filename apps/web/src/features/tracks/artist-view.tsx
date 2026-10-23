"use client";

import { CalendarDays, Music, Play, Shuffle } from "lucide-react";
import { useState } from "react";
import { useFormatter, useTranslations } from "next-intl";
import { ArtistAvatar, artistHue } from "@/components/media/artist-avatar";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { AlbumCard } from "@/features/albums/album-card";
import { GRID } from "@/features/home/media-section";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { useMe } from "@/features/auth/api";
import { FollowButton } from "@/features/social/follow-button";
import { durationMsOf, isNotFound, useArtistProfile, useMyArtist } from "./api";
import { TrackList } from "./track-list";

export function ArtistView({ slug }: { slug: string }) {
  const t = useTranslations("artist");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const format = useFormatter();
  const profile = useArtistProfile(slug);
  const me = useMe();
  const myArtist = useMyArtist(Boolean(me.data));
  const playQueue = usePlayerStore((s) => s.playQueue);

  if (profile.isPending) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-72 rounded-3xl" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  if (profile.isError) {
    return isNotFound(profile.error) ? (
      <ErrorState title={t("notFoundTitle")} description={t("notFoundBody")} />
    ) : (
      <ErrorState
        title={tErrors(errorCode(profile.error))}
        onRetry={() => void profile.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  const { artist, tracks } = profile.data;
  const playable = tracks.filter((track) => track.playable).map(toPlayerTrack);
  const totalSeconds = tracks.reduce((sum, track) => sum + (durationMsOf(track) ?? 0), 0) / 1000;
  const hue = artistHue(artist.id);

  function shufflePlay() {
    const store = usePlayerStore.getState();
    if (!store.shuffle) store.toggleShuffle();
    store.playQueue(playable, Math.floor(Math.random() * playable.length));
  }

  return (
    <div className="flex flex-col gap-8">
      <header
        className="relative isolate overflow-hidden rounded-3xl border p-6 md:min-h-72 md:p-10"
        style={{
          background: `radial-gradient(120% 140% at 0% 0%, oklch(0.45 0.12 ${hue} / 0.95) 0%, oklch(0.2 0.04 ${hue}) 50%, #0b0b0b 100%)`,
        }}
      >
        {artist.banner ? <ArtistBanner src={artist.banner.large} /> : null}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-24 right-0 -z-10 size-96 rounded-full bg-primary/15 blur-3xl"
        />
        <div className="flex flex-col gap-6 sm:flex-row sm:items-end md:gap-8">
          <ArtistAvatar
            id={artist.id}
            name={artist.name}
            src={artist.avatar?.large}
            className="size-32 text-4xl shadow-elevated ring-4 ring-black/30 md:size-44 md:text-5xl"
          />
          <div className="flex min-w-0 flex-col gap-3">
            <p className="text-xs font-semibold tracking-[0.2em] text-white/70 uppercase">{t("label")}</p>
            <h1 className="text-4xl font-bold tracking-tight break-words text-white md:text-6xl">
              {artist.name}
            </h1>
            {tracks.length > 0 ? (
              <p className="text-sm text-white/80 tabular-nums">
                {t("stats", { count: tracks.length, duration: formatTime(totalSeconds) })}
              </p>
            ) : null}
            <FollowButton artist={artist} isOwner={myArtist.data?.id === artist.id} />
            {playable.length > 0 ? (
              <div className="mt-2 flex flex-wrap items-center gap-3">
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
              </div>
            ) : null}
          </div>
        </div>
      </header>

      {profile.data.albums.length > 0 ? (
        <section aria-labelledby="artist-albums" className="flex flex-col gap-3">
          <h2 id="artist-albums" className="text-xl font-semibold tracking-tight">
            {t("albums")}
          </h2>
          <ul className={GRID}>
            {profile.data.albums.map((album, i) => (
              <li
                key={album.id}
                className="animate-in duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
                style={{ animationDelay: `${Math.min(i, 10) * 40}ms` }}
              >
                <AlbumCard album={album} showArtist={false} />
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <section aria-labelledby="artist-tracks" className="flex min-w-0 flex-col gap-3">
          <h2 id="artist-tracks" className="text-xl font-semibold tracking-tight">
            {t("tracks")}
          </h2>
          {tracks.length === 0 ? (
            <EmptyState icon={Music} title={t("noTracksTitle")} description={t("noTracksBody")} />
          ) : (
            <TrackList tracks={tracks} showArtist={false} />
          )}
        </section>

        <aside
          aria-labelledby="artist-about"
          className="flex h-fit flex-col gap-3 rounded-2xl border bg-surface-card p-5 xl:sticky xl:top-[calc(var(--topbar-height)+0.5rem)]"
        >
          <h2 id="artist-about" className="font-semibold">
            {t("about")}
          </h2>
          {/* Plain text: rendered as text, never as HTML. */}
          <p className="text-sm whitespace-pre-line text-muted-foreground">{artist.bio || t("noBio")}</p>
          <p className="flex items-center gap-2 text-xs text-subtle-foreground">
            <CalendarDays className="size-3.5" aria-hidden="true" />
            {t("joined", { date: format.dateTime(new Date(artist.createdAt), { dateStyle: "medium" }) })}
          </p>
        </aside>
      </div>
    </div>
  );
}

/** The artist's 3:1 banner behind the header, fading in, darkened at the bottom so the text stays readable. */
function ArtistBanner({ src }: { src: string }) {
  const [loaded, setLoaded] = useState(false);
  return (
    <div aria-hidden="true" className="absolute inset-0 -z-10">
      {/* eslint-disable-next-line @next/next/no-img-element -- served by our API */}
      <img
        src={src}
        alt=""
        decoding="async"
        ref={(img) => {
          if (img?.complete && img.naturalWidth > 0 && !loaded) setLoaded(true);
        }}
        onLoad={() => setLoaded(true)}
        className={`size-full object-cover transition-[opacity,scale] duration-700 ease-out ${loaded ? "scale-100 opacity-100" : "scale-105 opacity-0"}`}
      />
      <div className="absolute inset-0 bg-linear-to-t from-black/90 via-black/45 to-black/10" />
    </div>
  );
}
