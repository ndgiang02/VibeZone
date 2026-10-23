"use client";

import {
  Disc3,
  Loader2,
  Pause,
  Play,
  Repeat,
  Repeat1,
  RotateCcw,
  Shuffle,
  SkipBack,
  SkipForward,
} from "lucide-react";
import { useTranslations } from "next-intl";
import type { CSSProperties, ReactNode } from "react";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { TrackArt } from "@/features/tracks/track-art";
import { engine } from "./engine";
import { formatTime } from "./format";
import { selectCurrentTrack, usePlayerStore } from "./store";

/**
 * Large "now playing" card for the home page's side panel. A second view of the global player store — it
 * sends the same commands as the player bar and never touches media itself.
 */
export function NowPlayingCard() {
  const t = useTranslations("nowPlaying");
  const tPlayer = useTranslations("player");
  const track = usePlayerStore(selectCurrentTrack);
  const status = usePlayerStore((s) => s.status);
  const currentTime = usePlayerStore((s) => s.currentTime);
  const duration = usePlayerStore((s) => s.duration);
  const shuffle = usePlayerStore((s) => s.shuffle);
  const repeat = usePlayerStore((s) => s.repeat);
  const actions = usePlayerStore.getState();

  const playing = status === "playing";
  const loading = status === "loading";
  const RepeatIcon = repeat === "one" ? Repeat1 : Repeat;
  const progress = duration > 0 ? (Math.min(currentTime, duration) / duration) * 100 : 0;

  return (
    <section aria-labelledby="now-playing-title" className="rounded-2xl border bg-surface-card p-5">
      <h2 id="now-playing-title" className="mb-4 text-base font-semibold">
        {t("title")}
      </h2>

      {!track ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed px-4 py-10 text-center">
          <Disc3 className="size-10 text-subtle-foreground" aria-hidden="true" />
          <p className="font-medium">{t("emptyTitle")}</p>
          <p className="text-sm text-muted-foreground">{t("emptyBody")}</p>
        </div>
      ) : (
        <>
          <div className="relative mx-auto w-full max-w-60">
            <div
              aria-hidden="true"
              className={cn(
                "absolute inset-4 rounded-full bg-primary/40 blur-3xl transition-opacity duration-500",
                playing ? "opacity-100" : "opacity-50",
              )}
            />
            <TrackArt
              key={track.id}
              id={track.id}
              artwork={track.artwork}
              size="large"
              className={cn(
                "relative aspect-square w-full animate-in rounded-2xl shadow-elevated transition-transform duration-500 zoom-in-95 fade-in",
                playing ? "scale-100" : "scale-[0.97]",
              )}
            />
          </div>

          <div className="mt-5 min-w-0 text-center">
            <Link
              href={`/tracks/${track.id}`}
              className="block truncate text-lg font-semibold hover:underline"
            >
              {track.title}
            </Link>
            <Link
              href={`/artists/${track.artistSlug}`}
              className="block truncate text-sm text-muted-foreground transition-colors hover:text-foreground"
            >
              {track.artistName}
            </Link>
          </div>

          <div className="mt-4 flex flex-col gap-1.5">
            <input
              type="range"
              min={0}
              max={duration || 0}
              step={1}
              value={Math.min(currentTime, duration || 0)}
              disabled={!duration}
              onChange={(e) => actions.seek(Number(e.target.value))}
              aria-label={tPlayer("seek")}
              aria-valuetext={tPlayer("position", {
                current: formatTime(currentTime),
                total: formatTime(duration),
              })}
              className="vz-range w-full cursor-pointer"
              style={{ "--fill": `${progress}%` } as CSSProperties}
            />
            <div className="flex justify-between text-xs text-muted-foreground tabular-nums">
              <span>{formatTime(currentTime)}</span>
              <span>{formatTime(duration)}</span>
            </div>
          </div>

          <div className="mt-3 flex items-center justify-between">
            <ControlButton
              label={tPlayer("shuffle")}
              pressed={shuffle}
              active={shuffle}
              onClick={actions.toggleShuffle}
            >
              <Shuffle className="size-4" aria-hidden="true" />
            </ControlButton>
            <ControlButton label={tPlayer("previous")} onClick={actions.previous}>
              <SkipBack className="size-5" aria-hidden="true" />
            </ControlButton>
            {status === "error" ? (
              <button
                type="button"
                aria-label={tPlayer("retry")}
                title={tPlayer("error")}
                onClick={() => engine.reload(currentTime)}
                className="flex size-14 items-center justify-center rounded-full border text-foreground transition-colors hover:bg-surface-hover"
              >
                <RotateCcw className="size-5" aria-hidden="true" />
              </button>
            ) : (
              <button
                type="button"
                aria-label={playing || loading ? tPlayer("pause") : tPlayer("play")}
                onClick={actions.togglePlay}
                className="flex size-14 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-[0_8px_28px_-6px_var(--accent-glow)] transition-[background-color,transform] duration-200 hover:scale-105 hover:bg-primary-hover active:scale-95"
              >
                {loading ? (
                  <Loader2 className="size-6 animate-spin" aria-hidden="true" />
                ) : playing ? (
                  <Pause className="size-6 fill-current" aria-hidden="true" />
                ) : (
                  <Play className="ml-0.5 size-6 fill-current" aria-hidden="true" />
                )}
              </button>
            )}
            <ControlButton label={tPlayer("next")} onClick={() => actions.next()}>
              <SkipForward className="size-5" aria-hidden="true" />
            </ControlButton>
            <ControlButton
              label={tPlayer(`repeat.${repeat}`)}
              active={repeat !== "off"}
              onClick={actions.cycleRepeat}
            >
              <RepeatIcon className="size-4" aria-hidden="true" />
            </ControlButton>
          </div>
          {status === "error" ? (
            <p role="alert" className="mt-3 text-center text-xs text-destructive">
              {tPlayer("error")}
            </p>
          ) : null}
        </>
      )}
    </section>
  );
}

function ControlButton({
  label,
  onClick,
  active = false,
  pressed,
  children,
}: {
  label: string;
  onClick: () => void;
  active?: boolean;
  pressed?: boolean;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      aria-pressed={pressed}
      onClick={onClick}
      className={cn(
        "flex size-10 items-center justify-center rounded-full transition-colors duration-200 hover:bg-surface-hover",
        active ? "text-primary" : "text-muted-foreground hover:text-foreground",
      )}
    >
      {children}
    </button>
  );
}
