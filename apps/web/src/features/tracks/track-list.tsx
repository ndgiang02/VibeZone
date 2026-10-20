"use client";

import { ListPlus, Lock } from "lucide-react";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { AddToPlaylistMenu } from "@/features/playlists/add-to-playlist-menu";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, type Track } from "./api";
import { MiniWaveform } from "./mini-waveform";
import { PlayButton } from "./play-button";
import { TrackArt } from "./track-art";

/**
 * Track list widget: number/play, artwork, title, the track's waveform (click to play from a point),
 * duration and add-to-queue. Playing one queues the whole (playable) list. On phones the waveform wraps
 * under the title.
 */
export function TrackList({
  tracks,
  showArtist = true,
  highlightId,
  numbered,
  actions,
}: {
  tracks: Track[];
  showArtist?: boolean;
  /** Overrides the row number (e.g. album track numbers). */
  numbered?: (track: Track, index: number) => number;
  /** Extra per-row buttons (e.g. "remove from playlist"). */
  actions?: (track: Track) => ReactNode;
  /** Marks a row as "this page's track" (e.g. on the track page's "More from" list). */
  highlightId?: string;
}) {
  const t = useTranslations("player");
  const tTrack = useTranslations("track");
  const currentId = usePlayerStore((s) => s.queue[s.index]?.id);
  const enqueue = usePlayerStore((s) => s.enqueue);

  return (
    <ol className="flex flex-col gap-1">
      {tracks.map((track, i) => {
        const duration = durationMsOf(track);
        const isCurrent = currentId === track.id;
        return (
          <li
            key={track.id}
            className={cn(
              "group grid animate-in grid-cols-[2.25rem_3rem_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1 rounded-xl p-2 transition-colors duration-200 fill-mode-both fade-in slide-in-from-bottom-1 hover:bg-surface-hover/70 sm:grid-cols-[2.25rem_3rem_minmax(0,13rem)_minmax(0,1fr)_auto] sm:gap-x-4",
              isCurrent ? "bg-primary-subtle" : highlightId === track.id && "bg-surface-elevated",
            )}
            // Staggered entrance for the first rows; the rest arrive together.
            style={{ animationDelay: `${Math.min(i, 12) * 30}ms`, animationDuration: "350ms" }}
          >
            <div className="relative flex size-9 items-center justify-center">
              <span
                aria-hidden="true"
                className={cn(
                  "text-sm text-subtle-foreground tabular-nums transition-opacity duration-150",
                  track.playable &&
                    "group-focus-within:opacity-0 group-hover:opacity-0 pointer-coarse:opacity-0",
                  isCurrent && "opacity-0",
                )}
              >
                {numbered ? numbered(track, i) : i + 1}
              </span>
              <span
                className={cn(
                  "absolute inset-0 flex items-center justify-center transition-opacity duration-150",
                  isCurrent
                    ? "opacity-100"
                    : "opacity-0 group-focus-within:opacity-100 group-hover:opacity-100 pointer-coarse:opacity-100",
                )}
              >
                <PlayButton track={track} context={tracks} size="icon-sm" className="size-9" />
              </span>
            </div>

            <TrackArt id={track.id} artwork={track.artwork} className="size-12 rounded-md" />

            <div className="min-w-0">
              <Link
                href={`/tracks/${track.id}`}
                className={cn("block truncate font-medium hover:underline", isCurrent && "text-primary")}
              >
                {track.title}
              </Link>
              <p className="truncate text-sm text-muted-foreground">
                {track.visibility !== "Public" && track.isOwner ? (
                  <Lock
                    className="mr-1 inline size-3 align-[-1px]"
                    aria-label={tTrack(`visibility.${track.visibility}`)}
                  />
                ) : null}
                {showArtist ? (
                  <Link href={`/artists/${track.artist.slug}`} className="hover:text-foreground">
                    {track.artist.name}
                  </Link>
                ) : null}
                {showArtist && track.genre ? " · " : null}
                {track.genre?.name}
              </p>
            </div>

            <MiniWaveform
              track={track}
              context={tracks}
              className="max-sm:col-start-3 max-sm:col-end-5 max-sm:row-start-2 max-sm:h-8"
            />

            <div className="flex items-center gap-1 justify-self-end">
              <span className="hidden w-12 text-right text-sm text-subtle-foreground tabular-nums sm:block">
                {duration ? formatTime(duration / 1000) : ""}
              </span>
              {track.playable ? (
                <button
                  type="button"
                  aria-label={t("addToQueue", { title: track.title })}
                  onClick={() => enqueue(toPlayerTrack(track))}
                  className="flex size-8 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-surface-hover hover:text-foreground"
                >
                  <ListPlus className="size-4" aria-hidden="true" />
                </button>
              ) : null}
              <AddToPlaylistMenu track={track} />
              {actions?.(track)}
            </div>
          </li>
        );
      })}
    </ol>
  );
}
