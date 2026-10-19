"use client";

import { getNonce } from "get-nonce";
import { useTranslations } from "next-intl";
import { useEffect, useRef } from "react";
import WaveSurfer from "wavesurfer.js";
import { Skeleton } from "@/components/states/states";
import { engine } from "@/features/player/engine";
import { toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { type Track, useWaveform } from "./api";

function cssColor(name: string, fallback: string) {
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
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
 */
export function Waveform({ track }: { track: Track }) {
  const t = useTranslations("track");
  const container = useRef<HTMLDivElement>(null);
  const waveform = useWaveform(track.id, track.playable);
  const bound = usePlayerStore((s) => s.queue[s.index]?.id === track.id && s.status !== "idle");

  useEffect(() => {
    const element = container.current;
    const data = waveform.data;
    if (!element || !data || data.peaks.length === 0) return;

    const audio = engine.element();
    const attach = bound && audio !== null && engine.hasSource(track.id);
    const ws = WaveSurfer.create({
      container: element,
      height: 96,
      waveColor: cssColor("--subtle-foreground", "#71717a"),
      progressColor: cssColor("--primary", "#ff6b3d"),
      cursorColor: "transparent",
      barWidth: 2,
      barGap: 1,
      barRadius: 2,
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
  if (waveform.isPending) return <Skeleton className="h-24 w-full" />;
  if (!waveform.data) {
    return <p className="text-sm text-muted-foreground">{t("waveformUnavailable")}</p>;
  }

  return (
    <div
      ref={container}
      role="img"
      aria-label={t("waveformLabel", { title: track.title })}
      className="w-full"
    />
  );
}
