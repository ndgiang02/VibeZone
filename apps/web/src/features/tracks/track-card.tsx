"use client";

import { ListPlus } from "lucide-react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { AddToPlaylistMenu } from "@/features/playlists/add-to-playlist-menu";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, type Track } from "./api";
import { PlayButton } from "./play-button";
import { TrackArt } from "./track-art";

/** Grid card for a real track: artwork with hover play/queue actions; playing queues the whole grid. */
export function TrackCard({ track, context }: { track: Track; context: Track[] }) {
  const t = useTranslations("player");
  const isCurrent = usePlayerStore((s) => s.queue[s.index]?.id === track.id);
  const enqueue = usePlayerStore((s) => s.enqueue);
  const duration = durationMsOf(track);

  return (
    <article
      className={cn(
        "group relative flex flex-col gap-3 rounded-xl bg-surface-card p-3 transition-[background-color,transform] duration-200 hover:-translate-y-0.5 hover:bg-surface-elevated motion-reduce:hover:translate-y-0",
        isCurrent && "ring-1 ring-primary-border",
      )}
    >
      <div className="relative">
        <TrackArt id={track.id} artwork={track.artwork} className="aspect-square w-full rounded-lg" />
        {track.playable ? (
          <div className="absolute right-2 bottom-2 flex items-center gap-1.5 opacity-0 transition-[opacity,transform] duration-200 group-focus-within:opacity-100 group-hover:opacity-100 pointer-coarse:opacity-100">
            <button
              type="button"
              aria-label={t("addToQueue", { title: track.title })}
              onClick={() => enqueue(toPlayerTrack(track))}
              className="flex size-8 items-center justify-center rounded-full bg-black/60 text-white backdrop-blur transition-colors hover:bg-black/80"
            >
              <ListPlus className="size-4" aria-hidden="true" />
            </button>
            <AddToPlaylistMenu
              track={track}
              className="bg-black/60 text-white backdrop-blur hover:bg-black/80 hover:text-white"
            />
            <PlayButton
              track={track}
              context={context}
              size="icon-lg"
              className="size-10 shadow-[0_8px_24px_-6px_var(--accent-glow)] hover:bg-primary-hover"
            />
          </div>
        ) : null}
      </div>
      <div className="min-w-0">
        <Link
          href={`/tracks/${track.id}`}
          className={cn("block truncate text-sm font-semibold hover:underline", isCurrent && "text-primary")}
        >
          {track.title}
        </Link>
        <p className="truncate text-xs text-muted-foreground">
          <Link href={`/artists/${track.artist.slug}`} className="transition-colors hover:text-foreground">
            {track.artist.name}
          </Link>
          {track.genre ? ` · ${track.genre.name}` : null}
          {duration ? ` · ${formatTime(duration / 1000)}` : null}
        </p>
      </div>
    </article>
  );
}
