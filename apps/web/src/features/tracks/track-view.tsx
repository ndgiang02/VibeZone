"use client";

import { CalendarDays, Disc3, Clock3, Headphones, ListPlus, ShieldAlert } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { ErrorState, Skeleton } from "@/components/states/states";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { AddToPlaylistMenu } from "@/features/playlists/add-to-playlist-menu";
import { ReportButton } from "@/features/moderation/report-button";
import { CommentsSection } from "@/features/social/comments-section";
import { LikeButton } from "@/features/social/like-button";
import {
  durationMsOf,
  isNotFound,
  type Track,
  useArtistProfile,
  useRelatedTracks,
  useTrack,
  useTrackStats,
} from "./api";
import { PlayButton } from "./play-button";
import { StatusBadge } from "./status-badge";
import { artHue, TrackArt } from "./track-art";
import { TrackList } from "./track-list";
import { Waveform } from "./waveform";

/** How many of the artist's tracks the "More from" list shows. */
const MORE_FROM_LIMIT = 8;

export function TrackView({ trackId }: { trackId: string }) {
  const t = useTranslations("track");
  const tPlayer = useTranslations("player");
  const tArtist = useTranslations("artist");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const format = useFormatter();
  const track = useTrack(trackId);
  const enqueue = usePlayerStore((s) => s.enqueue);

  if (track.isPending) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-72 rounded-3xl" />
        <Skeleton className="h-36 rounded-2xl" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  if (track.isError) {
    return isNotFound(track.error) ? (
      <ErrorState title={t("notFoundTitle")} description={t("notFoundBody")} />
    ) : (
      <ErrorState
        title={tErrors(errorCode(track.error))}
        onRetry={() => void track.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  const data = track.data;
  const duration = durationMsOf(data);
  const hue = artHue(data.id);

  return (
    <article className="flex flex-col gap-6">
      <header
        className="relative isolate overflow-hidden rounded-3xl border p-6 md:p-8"
        style={{
          background: `linear-gradient(135deg, oklch(0.42 0.12 ${hue} / 0.95) 0%, oklch(0.2 0.04 ${hue}) 55%, #0b0b0b 100%)`,
        }}
      >
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -right-20 -bottom-24 -z-10 size-80 rounded-full bg-primary/15 blur-3xl"
        />
        <div className="flex flex-col gap-6 sm:flex-row sm:items-end md:gap-8">
          <div className="relative shrink-0 self-start sm:self-auto">
            <div aria-hidden="true" className="absolute inset-6 rounded-full bg-black/40 blur-2xl" />
            <TrackArt
              id={data.id}
              artwork={data.artwork}
              size="large"
              className="relative size-44 rounded-2xl shadow-elevated md:size-56"
            />
          </div>

          <div className="flex min-w-0 flex-col gap-3">
            <p className="text-xs font-semibold tracking-[0.2em] text-white/70 uppercase">{t("label")}</p>
            <h1 className="text-3xl font-bold tracking-tight break-words text-white md:text-5xl">
              {data.title}
            </h1>

            <dl className="flex flex-wrap items-center gap-x-3 gap-y-2 text-sm text-white/80">
              <div className="flex items-center gap-2">
                <dt className="sr-only">{tArtist("label")}</dt>
                <dd className="flex items-center gap-2">
                  <ArtistAvatar
                    id={data.artist.id}
                    name={data.artist.name}
                    src={data.artist.avatarUrl}
                    className="size-7 text-[0.625rem]"
                  />
                  <Link
                    href={`/artists/${data.artist.slug}`}
                    className="font-semibold text-white hover:underline"
                  >
                    {data.artist.name}
                  </Link>
                </dd>
              </div>
              {data.publishedAt ? (
                <div className="flex items-center gap-1.5">
                  <dt className="sr-only">{t("published")}</dt>
                  <CalendarDays className="size-3.5 text-white/60" aria-hidden="true" />
                  <dd>{format.dateTime(new Date(data.publishedAt), { dateStyle: "medium" })}</dd>
                </div>
              ) : null}
              {duration ? (
                <div className="flex items-center gap-1.5">
                  <dt className="sr-only">{t("duration")}</dt>
                  <Clock3 className="size-3.5 text-white/60" aria-hidden="true" />
                  <dd className="tabular-nums">{formatTime(duration / 1000)}</dd>
                </div>
              ) : null}
              {data.album ? (
                <div className="flex items-center gap-1.5">
                  <dt className="sr-only">{t("onAlbum")}</dt>
                  <Disc3 className="size-3.5 text-white/60" aria-hidden="true" />
                  <dd>
                    <Link
                      href={`/albums/${data.album.id}`}
                      className="font-medium text-white hover:underline"
                    >
                      {data.album.title}
                    </Link>
                  </dd>
                </div>
              ) : null}
              <PlaysStat trackId={data.id} />
              {data.genre ? (
                <div>
                  <dt className="sr-only">{t("genre")}</dt>
                  <dd className="rounded-full border border-primary-border bg-primary-subtle px-2.5 py-0.5 text-xs font-medium text-primary">
                    {data.genre.name}
                  </dd>
                </div>
              ) : null}
            </dl>

            {data.isOwner ? <StatusBadge track={data} /> : null}
            {data.takenDown ? (
              <p
                role="status"
                className="flex items-center gap-2 rounded-xl border border-destructive/40 bg-destructive/15 px-3 py-2 text-sm text-white"
              >
                <ShieldAlert className="size-4 shrink-0" aria-hidden="true" />
                {t("takenDown")}
              </p>
            ) : null}

            <div className="mt-2 flex items-center gap-3">
              <PlayButton
                track={data}
                size="icon-lg"
                className="size-14 shadow-[0_10px_30px_-8px_var(--accent-glow)] hover:bg-primary-hover [&_svg]:size-6"
              />
              {data.playable ? (
                <button
                  type="button"
                  onClick={() => enqueue(toPlayerTrack(data))}
                  className="inline-flex h-10 items-center gap-2 rounded-full border border-white/20 bg-black/20 px-4 text-sm font-medium text-white backdrop-blur transition-colors hover:border-white/40 hover:bg-black/30"
                >
                  <ListPlus className="size-4" aria-hidden="true" />
                  {tPlayer("addToQueueShort")}
                </button>
              ) : null}
              <AddToPlaylistMenu
                track={data}
                variant="button"
                className="h-10 border-white/20 bg-black/20 text-white backdrop-blur hover:border-white/40 hover:bg-black/30 hover:text-white"
              />
              <LikeButton
                track={data}
                variant="button"
                className="border-white/20 bg-black/20 text-white backdrop-blur hover:border-white/40 hover:bg-black/30"
              />
              {!data.isOwner ? (
                <ReportButton
                  subjectType="Track"
                  subjectId={data.id}
                  className="border-white/20 bg-black/20 text-white/80 backdrop-blur hover:border-white/40 hover:bg-black/30 hover:text-white"
                />
              ) : null}
            </div>
          </div>
        </div>
      </header>

      <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="flex min-w-0 flex-col gap-6">
          {data.playable ? (
            <section aria-label={t("waveformSection")} className="rounded-2xl border bg-surface-card p-5">
              <div className="mb-3 flex flex-wrap items-baseline justify-between gap-2">
                <h2 className="font-semibold">{t("waveformSection")}</h2>
                <p className="text-xs text-subtle-foreground">{t("waveformHint")}</p>
              </div>
              <Waveform track={data} />
            </section>
          ) : (
            <p className="rounded-2xl border border-dashed p-6 text-sm text-muted-foreground">
              {t("notPlayable")}
            </p>
          )}

          {data.description ? (
            <section aria-labelledby="track-about" className="rounded-2xl border bg-surface-card p-5">
              <h2 id="track-about" className="mb-2 font-semibold">
                {t("about")}
              </h2>
              {/* Plain text: rendered as text, never as HTML. */}
              <p className="whitespace-pre-line text-muted-foreground">{data.description}</p>
            </section>
          ) : null}

          <CommentsSection track={data} />

          <MoreFromArtist track={data} />

          <RelatedTracks track={data} />
        </div>

        <ArtistCard track={data} />
      </div>
    </article>
  );
}

/** The artist's public tracks (this one highlighted) with waveforms; hidden when there is nothing else. */
function MoreFromArtist({ track }: { track: Track }) {
  const t = useTranslations("track");
  const tCommon = useTranslations("common");
  const profile = useArtistProfile(track.artist.slug);

  if (profile.isError) return null;
  if (profile.isPending) {
    return (
      <div className="flex flex-col gap-2" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-6 w-48" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  const tracks = profile.data.tracks.slice(0, MORE_FROM_LIMIT);
  if (tracks.length === 0 || (tracks.length === 1 && tracks[0]!.id === track.id)) return null;

  return (
    <section aria-labelledby="more-from" className="flex flex-col gap-3">
      <div className="flex items-baseline justify-between gap-4">
        <h2 id="more-from" className="text-xl font-semibold tracking-tight">
          {t("moreFrom", { artist: track.artist.name })}
        </h2>
        <Link
          href={`/artists/${track.artist.slug}`}
          className="shrink-0 rounded-full px-3 py-1 text-sm font-medium text-muted-foreground transition-colors hover:bg-surface-hover hover:text-primary"
        >
          {t("viewArtist")}
        </Link>
      </div>
      <TrackList tracks={tracks} showArtist={false} highlightId={track.id} />
    </section>
  );
}

function ArtistCard({ track }: { track: Track }) {
  const t = useTranslations();
  const profile = useArtistProfile(track.artist.slug);
  const bio = profile.data?.artist.bio;

  return (
    <aside className="flex h-fit flex-col items-center gap-3 rounded-2xl border bg-surface-card p-6 text-center xl:sticky xl:top-[calc(var(--topbar-height)+0.5rem)]">
      <ArtistAvatar
        id={track.artist.id}
        name={track.artist.name}
        src={profile.data?.artist.avatar?.large ?? track.artist.avatarUrl}
        className="size-24 text-2xl"
      />
      <div className="min-w-0">
        <p className="text-xs font-semibold tracking-[0.15em] text-subtle-foreground uppercase">
          {t("artist.label")}
        </p>
        <Link
          href={`/artists/${track.artist.slug}`}
          className="block truncate text-lg font-semibold hover:underline"
        >
          {track.artist.name}
        </Link>
      </div>
      {profile.isPending ? (
        <Skeleton className="h-12 w-full" />
      ) : bio ? (
        <p className="line-clamp-4 text-sm whitespace-pre-line text-muted-foreground">{bio}</p>
      ) : null}
      <Link
        href={`/artists/${track.artist.slug}`}
        className="mt-1 inline-flex h-9 items-center rounded-full border border-primary-border px-4 text-sm font-medium text-primary transition-colors hover:bg-primary-subtle"
      >
        {t("track.viewArtist")}
      </Link>
    </aside>
  );
}

/** Valid listens (≥ 30 s), all time. Hidden until loaded and on error (secondary info). */
function PlaysStat({ trackId }: { trackId: string }) {
  const t = useTranslations("track");
  const stats = useTrackStats(trackId);
  if (!stats.data) return null;
  return (
    <div className="flex items-center gap-1.5">
      <dt className="sr-only">{t("listensLabel")}</dt>
      <Headphones className="size-3.5 text-white/60" aria-hidden="true" />
      <dd className="tabular-nums">{t("listens", { count: stats.data.listens })}</dd>
    </div>
  );
}

/** Same artist or same genre — labelled as such, not as a personal recommendation. */
function RelatedTracks({ track }: { track: Track }) {
  const t = useTranslations("track");
  const related = useRelatedTracks(track.id);
  // Secondary section: nothing to show while loading, on error or when empty.
  if (!related.data || related.data.length === 0) return null;
  return (
    <section aria-labelledby="related-tracks" className="flex flex-col gap-3">
      <div>
        <h2 id="related-tracks" className="text-xl font-semibold tracking-tight">
          {t("related")}
        </h2>
        <p className="text-xs text-subtle-foreground">{t("relatedHint")}</p>
      </div>
      <TrackList tracks={related.data} />
    </section>
  );
}
