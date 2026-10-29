"use client";

import { getNonce } from "get-nonce";
import { useTranslations } from "next-intl";
import { useEffect, useRef, useState, type PointerEvent } from "react";
import WaveSurfer from "wavesurfer.js";
import { Skeleton } from "@/components/states/states";
import { engine } from "@/features/player/engine";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { type Track, useWaveform } from "./api";

/** Matches the skeleton (h-28). */
const HEIGHT = 112;

/** A design token as rgba() with the given alpha (canvas fill styles can't use CSS variables or color-mix). */
function tokenColor(name: string, fallback: string, alpha = 1) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback;
  const ctx = document.createElement("canvas").getContext("2d");
  if (!ctx) return value;
  ctx.fillStyle = value;
  ctx.fillRect(0, 0, 1, 1);
  const [r, g, b] = ctx.getImageData(0, 0, 1, 1).data;
  return `rgba(${r}, ${g}, ${b}, ${alpha})`;
}

/**
 * Vertical gradient stops for centred bars: the upper half is the waveform (bright at the crest, a touch
 * softer at the centre line), the lower half its dim reflection. wavesurfer spreads the stops evenly.
 */
function mirrored(crest: string, body: string, reflection: string) {
  return Array.from({ length: 21 }, (_, i) => (i <= 4 ? crest : i <= 10 ? body : reflection));
}

/**
 * Interactive waveform from the peaks computed by the worker (never synthesised).
 *
 * wavesurfer.js is used as a renderer around the app's single audio engine:
 * - for the track that is loaded in the engine, it is bound to the shared <audio> element so progress
 *   and clicks follow real playback. We pass `url` equal to the element's current src: with peaks and
 *   no url, wavesurfer 8 calls setSrc("") which would *remove* the shared element's source;
 *   setSrc(sameUrl) returns early instead. With an external media element, destroy() leaves it alone.
 * - for any other track it renders statically; clicking starts that track at the clicked position.
 *
 * The hover line and time labels are React overlays (no wavesurfer plugins: they inject styles the CSP
 * nonce doesn't cover).
 */
export function Waveform({ track }: { track: Track }) {
  const t = useTranslations("track");
  const container = useRef<HTMLDivElement>(null);
  const waveform = useWaveform(track.id, track.playable);
  const bound = usePlayerStore((s) => s.queue[s.index]?.id === track.id && s.status !== "idle");
  const position = usePlayerStore((s) => (s.queue[s.index]?.id === track.id ? s.currentTime : 0));
  const [hover, setHover] = useState<number | null>(null);
  const duration = (waveform.data?.durationMs ?? 0) / 1000;

  useEffect(() => {
    const element = container.current;
    const data = waveform.data;
    if (!element || !data || data.peaks.length === 0) return;

    const audio = engine.element();
    const attach = bound && audio !== null && engine.hasSource(track.id);
    const ws = WaveSurfer.create({
      container: element,
      height: HEIGHT,
      waveColor: mirrored(
        tokenColor("--muted-foreground", "#a0a0a0", 0.75),
        tokenColor("--subtle-foreground", "#6f6f6f", 0.9),
        tokenColor("--subtle-foreground", "#6f6f6f", 0.3),
      ),
      progressColor: mirrored(
        tokenColor("--primary", "#a8d64a"),
        tokenColor("--primary", "#a8d64a", 0.85),
        tokenColor("--primary", "#a8d64a", 0.3),
      ),
      cursorColor: tokenColor("--foreground", "#ffffff", 0.85),
      cursorWidth: attach ? 2 : 0,
      barWidth: 3,
      barGap: 2,
      barRadius: 3,
      barMinHeight: 2,
      normalize: true,
      interact: true,
      dragToSeek: attach,
      peaks: [data.peaks],
      duration: data.durationMs / 1000,
      cspNonce: getNonce(),
      ...(attach ? { media: audio, url: audio.src } : {}),
    });

    const unsubscribe = attach
      ? undefined
      : ws.on("interaction", (time) => {
          const store = usePlayerStore.getState();
          store.playTrack(toPlayerTrack(track));
          store.seek(time);
        });

    return () => {
      unsubscribe?.();
      ws.destroy();
    };
    // Re-create when the shared engine starts/stops playing this track.
  }, [bound, track, waveform.data]);

  if (!track.playable) return null;
  if (waveform.isPending) return <Skeleton className="h-28 w-full rounded-xl" />;
  if (!waveform.data) {
    return <p className="text-sm text-muted-foreground">{t("waveformUnavailable")}</p>;
  }

  function onPointerMove(event: PointerEvent<HTMLDivElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    setHover(Math.min(Math.max((event.clientX - rect.left) / rect.width, 0), 1));
  }

  return (
    <div
      className="relative w-full cursor-pointer select-none"
      onPointerMove={onPointerMove}
      onPointerLeave={() => setHover(null)}
    >
      {/* Faint centre line: the axis the waveform and its reflection meet on. */}
      <div aria-hidden="true" className="absolute inset-x-0 top-1/2 h-px bg-border/60" />
      <div
        ref={container}
        role="img"
        aria-label={t("waveformLabel", { title: track.title })}
        className="relative w-full"
      />
      {hover != null && duration > 0 ? (
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-y-0 w-px bg-foreground/50"
          style={{ left: `${hover * 100}%` }}
        >
          <span className="absolute top-1 left-1/2 -translate-x-1/2 rounded-md bg-surface-elevated px-1.5 py-0.5 text-[0.6875rem] text-foreground tabular-nums shadow-elevated">
            {formatTime(hover * duration)}
          </span>
        </div>
      ) : null}
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-x-0 bottom-1 flex justify-between px-1 text-[0.6875rem] tabular-nums"
      >
        <span
          className={
            bound
              ? "rounded bg-background/80 px-1 text-primary"
              : "rounded bg-background/80 px-1 text-transparent"
          }
        >
          {formatTime(position)}
        </span>
        <span className="rounded bg-background/80 px-1 text-muted-foreground">{formatTime(duration)}</span>
      </div>
    </div>
  );
}
