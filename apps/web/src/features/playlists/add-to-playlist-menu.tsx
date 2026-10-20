"use client";

import { Check, ListMusic, Loader2, Plus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { useAddToPlaylist, useCreatePlaylist, useMyPlaylists } from "./api";

/**
 * "Add to playlist" for one track: the user's playlists (ticked when the track is already in one) and a
 * quick "new playlist" field that creates it and adds the track. Confirms with a brief tick on the button.
 */
export function AddToPlaylistMenu({
  track,
  className,
  variant = "icon",
}: {
  track: { id: string; title: string };
  className?: string;
  variant?: "icon" | "button";
}) {
  const t = useTranslations("playlist");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const me = useMe();
  const [open, setOpen] = useState(false);
  const [newTitle, setNewTitle] = useState("");
  const [status, setStatus] = useState<string | null>(null);
  const playlists = useMyPlaylists(open && Boolean(me.data), track.id);
  const add = useAddToPlaylist();
  const create = useCreatePlaylist();

  useEffect(() => {
    if (!status) return;
    const timer = setTimeout(() => setStatus(null), 2500);
    return () => clearTimeout(timer);
  }, [status]);

  const addTo = (playlistId: string, title: string) =>
    add.mutate(
      { playlistId, trackId: track.id },
      {
        onSuccess: () => setStatus(t("added", { playlist: title })),
        onError: (error) => setStatus(tErrors(errorCode(error))),
      },
    );

  const createAndAdd = () => {
    const title = newTitle.trim();
    if (!title) return;
    create.mutate(
      { title, description: null, visibility: "Private" },
      {
        onSuccess: (playlist) => {
          setNewTitle("");
          setOpen(false);
          addTo(playlist.id, playlist.title);
        },
        onError: (error) => setStatus(tErrors(errorCode(error))),
      },
    );
  };

  const busy = add.isPending || create.isPending;
  const Icon = busy ? Loader2 : status ? Check : ListMusic;

  return (
    <>
      <DropdownMenu open={open} onOpenChange={setOpen}>
        <DropdownMenuTrigger asChild>
          {variant === "icon" ? (
            <button
              type="button"
              aria-label={t("addTo")}
              title={t("addTo")}
              className={cn(
                "flex size-8 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-surface-hover hover:text-foreground",
                status && "text-primary",
                className,
              )}
            >
              <Icon className={cn("size-4", busy && "animate-spin")} aria-hidden="true" />
            </button>
          ) : (
            <Button variant="outline" size="lg" className={cn("rounded-full", className)}>
              <Icon className={cn("size-4", busy && "animate-spin")} aria-hidden="true" />
              {t("addTo")}
            </Button>
          )}
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-64">
          <DropdownMenuLabel className="truncate">
            {t("addToTitle", { title: track.title })}
          </DropdownMenuLabel>
          <DropdownMenuSeparator />
          {!me.data ? (
            <DropdownMenuItem asChild>
              <Link href="/login">{tCommon("signInFirst")}</Link>
            </DropdownMenuItem>
          ) : playlists.isPending ? (
            <div className="flex justify-center py-3" aria-label={tCommon("loading")}>
              <Loader2 className="size-4 animate-spin text-muted-foreground" aria-hidden="true" />
            </div>
          ) : (
            <>
              <div className="vz-thin-scrollbar max-h-60 overflow-y-auto">
                {(playlists.data ?? []).length === 0 ? (
                  <p className="px-2 py-1.5 text-xs text-muted-foreground">{t("noPlaylists")}</p>
                ) : (
                  playlists.data?.map((playlist) => (
                    <DropdownMenuItem
                      key={playlist.id}
                      disabled={playlist.containsTrack === true}
                      onSelect={() => addTo(playlist.id, playlist.title)}
                    >
                      {playlist.containsTrack ? (
                        <Check className="size-4 text-primary" aria-hidden="true" />
                      ) : (
                        <ListMusic className="size-4" aria-hidden="true" />
                      )}
                      <span className="truncate">{playlist.title}</span>
                      {playlist.containsTrack ? (
                        <span className="sr-only">{t("alreadyIn", { playlist: playlist.title })}</span>
                      ) : null}
                    </DropdownMenuItem>
                  ))
                )}
              </div>
              <DropdownMenuSeparator />
              <form
                className="flex gap-1.5 p-1"
                onSubmit={(event) => {
                  event.preventDefault();
                  createAndAdd();
                }}
              >
                <input
                  value={newTitle}
                  onChange={(e) => setNewTitle(e.target.value)}
                  // Keep the menu's type-ahead from taking the keystrokes.
                  onKeyDown={(e) => e.stopPropagation()}
                  maxLength={100}
                  placeholder={t("quickCreatePlaceholder")}
                  aria-label={t("newPlaylist")}
                  className="h-8 min-w-0 flex-1 rounded-md border border-input bg-transparent px-2 text-sm"
                />
                <Button
                  type="submit"
                  size="icon-sm"
                  disabled={!newTitle.trim() || busy}
                  aria-label={t("create")}
                >
                  <Plus className="size-4" aria-hidden="true" />
                </Button>
              </form>
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>
      <span role="status" className="sr-only">
        {status}
      </span>
    </>
  );
}
