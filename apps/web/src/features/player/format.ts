import { durationMsOf, type Track } from "@/features/tracks/api";
import type { PlayerTrack } from "./store";

/** 0:07, 3:45, 1:02:03 */
export function formatTime(seconds: number) {
  if (!Number.isFinite(seconds) || seconds < 0) return "0:00";
  const total = Math.floor(seconds);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = String(total % 60).padStart(2, "0");
  return h > 0 ? `${h}:${String(m).padStart(2, "0")}:${s}` : `${m}:${s}`;
}

export function toPlayerTrack(track: Track): PlayerTrack {
  return {
    id: track.id,
    title: track.title,
    artistName: track.artist.name,
    artistSlug: track.artist.slug,
    durationMs: durationMsOf(track),
  };
}
