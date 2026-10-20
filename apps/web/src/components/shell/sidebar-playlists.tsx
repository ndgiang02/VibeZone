"use client";

import { Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { useMyPlaylists } from "@/features/playlists/api";
import { PlaylistCover } from "@/features/playlists/playlist-card";

const MAX_SHOWN = 8;

/** The signed-in user's recent playlists as sidebar links (nothing for visitors). */
export function SidebarPlaylists() {
  const t = useTranslations("playlist");
  const me = useMe();
  const playlists = useMyPlaylists(Boolean(me.data));
  const pathname = usePathname();

  if (!me.data || !playlists.data) return null;

  return (
    <ul className="flex flex-col gap-0.5">
      {playlists.data.slice(0, MAX_SHOWN).map((playlist) => {
        const href = `/playlists/${playlist.id}`;
        return (
          <li key={playlist.id} className="animate-in duration-300 fade-in">
            <Link
              href={href}
              aria-current={pathname === href ? "page" : undefined}
              className={cn(
                "flex h-10 items-center gap-3 rounded-lg px-2 text-sm text-muted-foreground transition-colors duration-200 hover:bg-surface-hover hover:text-foreground",
                pathname === href && "bg-surface-hover text-foreground",
              )}
            >
              <PlaylistCover playlist={playlist} className="size-7 rounded-md" />
              <span className="truncate">{playlist.title}</span>
            </Link>
          </li>
        );
      })}
      <li>
        <Link
          href="/library"
          className="flex h-10 items-center gap-3 rounded-lg px-2 text-sm text-muted-foreground transition-colors duration-200 hover:bg-surface-hover hover:text-foreground"
        >
          <span className="flex size-7 items-center justify-center rounded-md border border-dashed">
            <Plus className="size-3.5" aria-hidden="true" />
          </span>
          {t("create")}
        </Link>
      </li>
    </ul>
  );
}
