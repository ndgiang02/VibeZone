"use client";

import { ListMusic, Lock } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { artHue } from "@/features/tracks/track-art";
import type { Playlist } from "./api";

/**
 * Playlist cover: a 2×2 mosaic of its first track covers (or one cover / a gradient when it has fewer).
 * Covers that fail to load (e.g. a track made private since) simply drop out.
 */
export function PlaylistCover({ playlist, className }: { playlist: Playlist; className?: string }) {
  const [failed, setFailed] = useState<string[]>([]);
  const urls = playlist.coverUrls.filter((u) => !failed.includes(u));
  const hue = artHue(playlist.id);
  const tiles = urls.length >= 4 ? urls.slice(0, 4) : urls.slice(0, 1);

  return (
    <div
      aria-hidden="true"
      className={cn(
        "relative grid shrink-0 overflow-hidden rounded-lg",
        tiles.length === 4 && "grid-cols-2",
        className,
      )}
      style={{
        background: `linear-gradient(135deg, oklch(0.55 0.14 ${hue}), oklch(0.25 0.06 ${(hue + 40) % 360}))`,
      }}
    >
      {tiles.length === 0 ? <ListMusic className="m-auto size-2/5 text-white/70" /> : null}
      {tiles.map((url) => (
        // eslint-disable-next-line @next/next/no-img-element -- served by our API
        <img
          key={url}
          src={url}
          alt=""
          loading="lazy"
          decoding="async"
          onError={() => setFailed((f) => [...f, url])}
          className="size-full animate-in object-cover duration-500 fade-in"
        />
      ))}
    </div>
  );
}

/** Grid card linking to the playlist page. */
export function PlaylistCard({ playlist }: { playlist: Playlist }) {
  const t = useTranslations("playlist");
  return (
    <Link
      href={`/playlists/${playlist.id}`}
      className="group flex flex-col gap-3 rounded-xl bg-surface-card p-3 transition-[background-color,transform] duration-200 hover:-translate-y-0.5 hover:bg-surface-elevated motion-reduce:hover:translate-y-0"
    >
      <PlaylistCover playlist={playlist} className="aspect-square w-full" />
      <div className="min-w-0">
        <p className="flex items-center gap-1.5 truncate text-sm font-semibold">
          {playlist.visibility === "Private" ? (
            <Lock className="size-3.5 shrink-0 text-subtle-foreground" aria-label={t("visibility.Private")} />
          ) : null}
          <span className="truncate">{playlist.title}</span>
        </p>
        <p className="truncate text-xs text-muted-foreground">
          {t("by", { name: playlist.owner.displayName || playlist.owner.handle })} ·{" "}
          {t("stats", { count: Number(playlist.trackCount) })}
        </p>
      </div>
    </Link>
  );
}
