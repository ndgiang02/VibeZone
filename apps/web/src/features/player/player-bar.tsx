"use client";

import {
  ListMusic,
  Loader2,
  Pause,
  Play,
  Repeat,
  Repeat1,
  RotateCcw,
  Shuffle,
  SkipBack,
  SkipForward,
  Volume1,
  Volume2,
  VolumeX,
} from "lucide-react";
import { useTranslations } from "next-intl";
import { useState, type CSSProperties } from "react";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { TrackArt } from "@/features/tracks/track-art";
import { engine } from "./engine";
import { formatTime } from "./format";
import { QueuePanel } from "./queue-panel";
import { selectCurrentTrack, usePlayerStore } from "./store";
import { usePlayerShortcuts } from "./use-player-shortcuts";

/** Global player UI. Renders nothing until something has been queued. */
export function PlayerBar() {
  const t = useTranslations("player");
  const track = usePlayerStore(selectCurrentTrack);
  const status = usePlayerStore((s) => s.status);
  const currentTime = usePlayerStore((s) => s.currentTime);
  const duration = usePlayerStore((s) => s.duration);
  const volume = usePlayerStore((s) => s.volume);
  const muted = usePlayerStore((s) => s.muted);
  const shuffle = usePlayerStore((s) => s.shuffle);
  const repeat = usePlayerStore((s) => s.repeat);
  const actions = usePlayerStore.getState();
  const [queueOpen, setQueueOpen] = useState(false);
  usePlayerShortcuts();

  if (!track) return null;

  const playing = status === "playing";
  const loading = status === "loading";
  const VolumeIcon = muted || volume === 0 ? VolumeX : volume < 0.5 ? Volume1 : Volume2;
  const RepeatIcon = repeat === "one" ? Repeat1 : Repeat;
  const progress = duration > 0 ? (currentTime / duration) * 100 : 0;

  return (
    <>
      {/* In-flow spacer so page content is never hidden behind the fixed bar. */}
      <div className="h-(--player-height)" aria-hidden="true" />
      <section
        aria-label={t("label")}
        className="fixed inset-x-0 bottom-0 z-40 border-t bg-surface/90 shadow-[0_-12px_32px_-16px_rgb(0_0_0/0.8)] backdrop-blur-md max-md:bottom-16 md:left-(--sidebar-width)"
      >
        {/* Thin progress line for small screens (the full slider is desktop-only). */}
        <div className="h-0.5 bg-surface-hover md:hidden" aria-hidden="true">
          <div className="h-full bg-primary transition-[width]" style={{ width: `${progress}%` }} />
        </div>
        <div className="mx-auto flex h-(--player-height) max-w-screen-2xl items-center gap-3 px-3 md:gap-6 md:px-5">
          <div className="flex min-w-0 flex-1 items-center gap-3 md:w-72 md:flex-none">
            <TrackArt
              key={track.id}
              id={track.id}
              artwork={track.artwork}
              className="size-12 animate-in rounded-md duration-300 zoom-in-90 fade-in"
            />
            <div className="min-w-0">
              <Link
                href={`/tracks/${track.id}`}
                className="block truncate text-sm font-medium hover:underline"
              >
                {track.title}
              </Link>
              <Link
                href={`/artists/${track.artistSlug}`}
                className="block truncate text-xs text-muted-foreground hover:text-foreground"
              >
                {track.artistName}
              </Link>
            </div>
          </div>

          <div className="flex flex-col items-center gap-1 md:flex-1">
            <div className="flex items-center gap-1 md:gap-2">
              <Button
                variant="ghost"
                size="icon-sm"
                className={cn("max-md:hidden", shuffle && "text-primary")}
                aria-pressed={shuffle}
                aria-label={t("shuffle")}
                onClick={actions.toggleShuffle}
              >
                <Shuffle className="size-4" aria-hidden="true" />
              </Button>
              <Button
                variant="ghost"
                size="icon-sm"
                className="max-md:hidden"
                aria-label={t("previous")}
                onClick={actions.previous}
              >
                <SkipBack className="size-4" aria-hidden="true" />
              </Button>
              {status === "error" ? (
                <Button
                  size="icon"
                  variant="outline"
                  aria-label={t("retry")}
                  title={t("error")}
                  onClick={() => engine.reload(currentTime)}
                >
                  <RotateCcw className="size-4" aria-hidden="true" />
                </Button>
              ) : (
                <Button
                  size="icon-lg"
                  className="rounded-full shadow-[0_6px_20px_-6px_var(--accent-glow)] hover:bg-primary-hover"
                  aria-label={playing || loading ? t("pause") : t("play")}
                  onClick={actions.togglePlay}
                >
                  {loading ? (
                    <Loader2 className="size-4 animate-spin" aria-hidden="true" />
                  ) : playing ? (
                    <Pause className="size-4" aria-hidden="true" />
                  ) : (
                    <Play className="size-4" aria-hidden="true" />
                  )}
                </Button>
              )}
              <Button variant="ghost" size="icon-sm" aria-label={t("next")} onClick={() => actions.next()}>
                <SkipForward className="size-4" aria-hidden="true" />
              </Button>
              <Button
                variant="ghost"
                size="icon-sm"
                className={cn("max-md:hidden", repeat !== "off" && "text-primary")}
                aria-label={t(`repeat.${repeat}`)}
                onClick={actions.cycleRepeat}
              >
                <RepeatIcon className="size-4" aria-hidden="true" />
              </Button>
            </div>
            <div className="hidden w-full max-w-xl items-center gap-2 text-xs text-muted-foreground tabular-nums md:flex">
              <span className="w-12 text-right">{formatTime(currentTime)}</span>
              <input
                type="range"
                min={0}
                max={duration || 0}
                step={1}
                value={Math.min(currentTime, duration || 0)}
                disabled={!duration}
                onChange={(e) => actions.seek(Number(e.target.value))}
                aria-label={t("seek")}
                aria-valuetext={t("position", {
                  current: formatTime(currentTime),
                  total: formatTime(duration),
                })}
                className="vz-range flex-1 cursor-pointer"
                style={{ "--fill": `${progress}%` } as CSSProperties}
              />
              <span className="w-12">{formatTime(duration)}</span>
            </div>
            {status === "error" ? (
              <p role="alert" className="text-xs text-destructive md:hidden">
                {t("error")}
              </p>
            ) : null}
          </div>

          <div className="flex items-center gap-1 md:w-72 md:justify-end">
            {status === "error" ? (
              <p role="alert" className="mr-2 hidden text-xs text-destructive md:block">
                {t("error")}
              </p>
            ) : null}
            <Button
              variant="ghost"
              size="icon-sm"
              className="max-md:hidden"
              aria-label={muted ? t("unmute") : t("mute")}
              onClick={actions.toggleMute}
            >
              <VolumeIcon className="size-4" aria-hidden="true" />
            </Button>
            <input
              type="range"
              min={0}
              max={1}
              step={0.01}
              value={muted ? 0 : volume}
              onChange={(e) => actions.setVolume(Number(e.target.value))}
              aria-label={t("volume")}
              aria-valuetext={`${Math.round((muted ? 0 : volume) * 100)}%`}
              className="vz-range hidden w-24 cursor-pointer md:block"
              style={{ "--fill": `${(muted ? 0 : volume) * 100}%` } as CSSProperties}
            />
            <Button
              variant="ghost"
              size="icon-sm"
              aria-label={t("queue")}
              aria-expanded={queueOpen}
              onClick={() => setQueueOpen((open) => !open)}
            >
              <ListMusic className="size-4" aria-hidden="true" />
            </Button>
          </div>
        </div>
      </section>
      {queueOpen ? <QueuePanel onClose={() => setQueueOpen(false)} /> : null}
    </>
  );
}
