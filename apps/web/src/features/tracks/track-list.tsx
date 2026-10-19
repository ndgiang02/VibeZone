"use client";

import { ListPlus } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, type Track } from "./api";
import { PlayButton } from "./play-button";
import { TrackArt } from "./track-art";

/** A list of tracks; playing one queues the whole (playable) list. */
export function TrackList({ tracks, showArtist = true }: { tracks: Track[]; showArtist?: boolean }) {
  const t = useTranslations("player");
  const currentId = usePlayerStore((s) => s.queue[s.index]?.id);
  const enqueue = usePlayerStore((s) => s.enqueue);

  return (
    <ul className="flex flex-col">
      {tracks.map((track) => {
        const duration = durationMsOf(track);
        return (
          <li
            key={track.id}
            className={cn(
              "flex items-center gap-3 rounded-lg p-2 transition-colors hover:bg-surface-hover/60",
              currentId === track.id && "bg-surface-hover",
            )}
          >
            <TrackArt id={track.id} className="size-12" />
            <div className="min-w-0 flex-1">
              <Link
                href={`/tracks/${track.id}`}
                className={cn(
                  "block truncate font-medium hover:underline",
                  currentId === track.id && "text-primary",
                )}
              >
                {track.title}
              </Link>
              <p className="truncate text-sm text-muted-foreground">
                {showArtist ? (
                  <Link href={`/artists/${track.artist.slug}`} className="hover:text-foreground">
                    {track.artist.name}
                  </Link>
                ) : null}
                {showArtist && track.genre ? " · " : null}
                {track.genre?.name}
              </p>
            </div>
            <span className="hidden text-sm text-subtle-foreground tabular-nums sm:block">
              {duration ? formatTime(duration / 1000) : ""}
            </span>
            {track.playable ? (
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label={t("addToQueue", { title: track.title })}
                onClick={() => enqueue(toPlayerTrack(track))}
              >
                <ListPlus className="size-4" aria-hidden="true" />
              </Button>
            ) : null}
            <PlayButton track={track} context={tracks} />
          </li>
        );
      })}
    </ul>
  );
}
