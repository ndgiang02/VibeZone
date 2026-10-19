"use client";

import { Loader2, Pause, Play } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import type { Track } from "./api";

/** Sends the track (and its list as the queue) to the global player; shows its live state. */
export function PlayButton({
  track,
  context,
  size = "icon",
  className,
}: {
  track: Track;
  context?: Track[];
  size?: "icon" | "icon-lg" | "icon-sm";
  className?: string;
}) {
  const t = useTranslations("player");
  const isCurrent = usePlayerStore((s) => s.queue[s.index]?.id === track.id);
  const status = usePlayerStore((s) => s.status);
  const playTrack = usePlayerStore((s) => s.playTrack);
  const active = isCurrent && (status === "playing" || status === "loading");

  if (!track.playable) return null;

  return (
    <Button
      size={size}
      className={cn("shrink-0 rounded-full", className)}
      aria-label={active ? t("pauseTrack", { title: track.title }) : t("playTrack", { title: track.title })}
      aria-pressed={active}
      onClick={() => playTrack(toPlayerTrack(track), context?.filter((t) => t.playable).map(toPlayerTrack))}
    >
      {isCurrent && status === "loading" ? (
        <Loader2 className="size-4 animate-spin" aria-hidden="true" />
      ) : active ? (
        <Pause className="size-4" aria-hidden="true" />
      ) : (
        <Play className="size-4" aria-hidden="true" />
      )}
    </Button>
  );
}
