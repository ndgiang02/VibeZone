"use client";

import { useTranslations } from "next-intl";
import { useEffect, useId, useMemo, useRef, useState, type KeyboardEvent, type MouseEvent } from "react";
import { cn } from "@/lib/utils";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, type Track, useWaveform } from "./api";

const BARS = 64;
const WIDTH = BARS * 4;
const HEIGHT = 40;
/** Bars rise from this line; below it sits their dim reflection. */
const BASELINE = 26;

/** Max peak per bucket, scaled so the loudest bar fills the height (peaks are 0–1 from the worker). */
function toBars(peaks: number[]) {
  const bucket = peaks.length / BARS;
  const bars = Array.from({ length: BARS }, (_, i) => {
    const slice = peaks.slice(
      Math.floor(i * bucket),
      Math.max(Math.floor((i + 1) * bucket), Math.floor(i * bucket) + 1),
    );
    return slice.reduce((max, v) => Math.max(max, Math.abs(v)), 0);
  });
  const loudest = Math.max(...bars, 0.0001);
  return bars.map((v) => v / loudest);
}

/** True once the element has been near the viewport (then stays true). */
function useNearViewport<T extends Element>() {
  const ref = useRef<T>(null);
  const [near, setNear] = useState(false);
  useEffect(() => {
    const element = ref.current;
    if (!element || near || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(
      (entries) => entries.some((e) => e.isIntersecting) && setNear(true),
      {
        rootMargin: "200px",
      },
    );
    observer.observe(element);
    return () => observer.disconnect();
  }, [near]);
  return [ref, near] as const;
}

/**
 * Compact waveform for track lists, drawn from the worker's peaks (never synthesised; loaded when the row
 * scrolls into view). Shows live progress for the current track; clicking seeks — or starts this track,
 * with the list as its queue, at that point. Commands go through the global player store only.
 */
export function MiniWaveform({
  track,
  context,
  className,
}: {
  track: Track;
  context: Track[];
  className?: string;
}) {
  const t = useTranslations();
  const [ref, near] = useNearViewport<HTMLDivElement>();
  const waveform = useWaveform(track.id, track.playable && near);
  const isCurrent = usePlayerStore((s) => s.queue[s.index]?.id === track.id);
  const position = usePlayerStore((s) => (s.queue[s.index]?.id === track.id ? s.currentTime : 0));
  const liveDuration = usePlayerStore((s) => (s.queue[s.index]?.id === track.id ? s.duration : 0));
  const [hover, setHover] = useState<number | null>(null);
  const clipId = `wf${useId().replace(/[^a-zA-Z0-9]/g, "")}`;

  const bars = useMemo(() => (waveform.data ? toBars(waveform.data.peaks) : null), [waveform.data]);
  const duration = (durationMsOf(track) ?? waveform.data?.durationMs ?? 0) / 1000 || liveDuration;
  const progress = duration > 0 ? Math.min(position / duration, 1) : 0;

  function playFrom(seconds: number) {
    const store = usePlayerStore.getState();
    if (!isCurrent) {
      store.playTrack(toPlayerTrack(track), context.filter((item) => item.playable).map(toPlayerTrack));
    }
    store.seek(seconds);
  }

  function fractionAt(event: MouseEvent<HTMLDivElement>) {
    const rect = event.currentTarget.getBoundingClientRect();
    return Math.min(Math.max((event.clientX - rect.left) / rect.width, 0), 1);
  }

  function onKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    const store = usePlayerStore.getState();
    if (event.key === "Enter" || event.key === " ") {
      if (isCurrent) store.togglePlay();
      else playFrom(0);
    } else if (isCurrent && (event.key === "ArrowLeft" || event.key === "ArrowRight")) {
      store.seekBy(event.key === "ArrowLeft" ? -5 : 5);
    } else if (isCurrent && event.key === "Home") {
      store.seek(0);
    } else {
      return;
    }
    // Handled here: keeps the global player shortcuts from acting on the same key.
    event.preventDefault();
  }

  // Keeps its grid cell in lists even when there is nothing to draw.
  if (!track.playable) return <div aria-hidden="true" className={cn("h-10", className)} />;

  return (
    <div ref={ref} className={cn("relative h-10 min-w-0", className)}>
      {bars ? (
        <div
          role="slider"
          tabIndex={0}
          aria-label={t("track.waveformLabel", { title: track.title })}
          aria-valuemin={0}
          aria-valuemax={Math.round(duration)}
          aria-valuenow={Math.round(position)}
          aria-valuetext={t("player.position", {
            current: formatTime(position),
            total: formatTime(duration),
          })}
          onClick={(event) => playFrom(fractionAt(event) * duration)}
          onPointerMove={(event) => setHover(fractionAt(event))}
          onPointerLeave={() => setHover(null)}
          onKeyDown={onKeyDown}
          className="group/wave relative size-full cursor-pointer rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring/60"
        >
          <svg
            viewBox={`0 0 ${WIDTH} ${HEIGHT}`}
            preserveAspectRatio="none"
            aria-hidden="true"
            className="size-full"
          >
            <defs>
              <clipPath id={`${clipId}p`}>
                <rect width={progress * WIDTH} height={HEIGHT} />
              </clipPath>
              <clipPath id={`${clipId}h`}>
                <rect width={(hover ?? 0) * WIDTH} height={HEIGHT} />
              </clipPath>
            </defs>
            <Bars bars={bars} className={isCurrent ? "fill-surface-hover" : "fill-subtle-foreground/55"} />
            {hover != null ? (
              <Bars bars={bars} className="fill-muted-foreground/70" clipPath={`url(#${clipId}h)`} />
            ) : null}
            {isCurrent ? <Bars bars={bars} className="fill-primary" clipPath={`url(#${clipId}p)`} /> : null}
          </svg>
          {hover != null && duration > 0 ? (
            <span
              aria-hidden="true"
              className="pointer-events-none absolute -top-6 -translate-x-1/2 rounded bg-surface-elevated px-1.5 py-0.5 text-[0.625rem] text-foreground tabular-nums shadow-elevated"
              style={{ left: `${hover * 100}%` }}
            >
              {formatTime(hover * duration)}
            </span>
          ) : null}
        </div>
      ) : waveform.isPending && track.playable ? (
        <div aria-hidden="true" className="flex size-full items-center">
          <div className="h-1 w-full animate-pulse rounded-full bg-surface-hover" />
        </div>
      ) : (
        <div aria-hidden="true" className="flex size-full items-center">
          <div className="h-px w-full border-t border-dashed border-border" />
        </div>
      )}
    </div>
  );
}

/** Each bar rises from the baseline, with a shorter, faded mirror image below it. */
function Bars({ bars, className, clipPath }: { bars: number[]; className: string; clipPath?: string }) {
  return (
    <g className={className} clipPath={clipPath}>
      {bars.map((value, i) => {
        const height = Math.max(1.5, value * (BASELINE - 1));
        return <rect key={i} x={i * 4 + 0.8} y={BASELINE - height} width={2.4} height={height} rx={1.2} />;
      })}
      <g opacity={0.3}>
        {bars.map((value, i) => {
          const height = Math.max(1, value * (HEIGHT - BASELINE - 2));
          return <rect key={i} x={i * 4 + 0.8} y={BASELINE + 1} width={2.4} height={height} rx={1.2} />;
        })}
      </g>
    </g>
  );
}
