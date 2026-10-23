"use client";

import { Lock } from "lucide-react";
import { useTranslations } from "next-intl";
import { Link } from "@/i18n/navigation";
import { TrackArt } from "@/features/tracks/track-art";
import type { Album } from "@/features/catalog/api";

/** Grid card linking to the album page: cover (from its first track), title, type and year. */
export function AlbumCard({ album, showArtist = true }: { album: Album; showArtist?: boolean }) {
  const t = useTranslations("album");
  const tTrack = useTranslations("track");
  const year = album.releaseDate ? new Date(album.releaseDate).getFullYear() : null;

  return (
    <Link
      href={`/albums/${album.id}`}
      className="group flex flex-col gap-3 rounded-xl bg-surface-card p-3 transition-[background-color,transform] duration-200 hover:-translate-y-0.5 hover:bg-surface-elevated motion-reduce:hover:translate-y-0"
    >
      <TrackArt id={album.id} artwork={album.cover} className="aspect-square w-full rounded-lg shadow-card" />
      <div className="min-w-0">
        <p className="flex items-center gap-1.5 truncate text-sm font-semibold">
          {album.visibility !== "Public" ? (
            <Lock
              className="size-3.5 shrink-0 text-subtle-foreground"
              aria-label={tTrack(`visibility.${album.visibility}`)}
            />
          ) : null}
          <span className="truncate">{album.title}</span>
        </p>
        <p className="truncate text-xs text-muted-foreground">
          {[year, t(`kind.${album.kind}`), showArtist ? album.artist.name : null].filter(Boolean).join(" · ")}
        </p>
      </div>
    </Link>
  );
}
