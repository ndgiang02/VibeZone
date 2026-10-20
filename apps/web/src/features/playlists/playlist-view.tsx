"use client";

import {
  ArrowDown,
  ArrowUp,
  GripVertical,
  ListMusic,
  Lock,
  Pencil,
  Play,
  Shuffle,
  Trash2,
  X,
} from "lucide-react";
import { useTranslations } from "next-intl";
import { useState, type DragEvent, type KeyboardEvent } from "react";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, isNotFound, type Track } from "@/features/tracks/api";
import { PlayButton } from "@/features/tracks/play-button";
import { artHue, TrackArt } from "@/features/tracks/track-art";
import { TrackList } from "@/features/tracks/track-list";
import {
  type Playlist,
  useDeletePlaylist,
  usePlaylist,
  usePlaylistTracks,
  useRemoveFromPlaylist,
  useReorderPlaylist,
} from "./api";
import { PlaylistCover } from "./playlist-card";
import { PlaylistForm } from "./playlist-form";

/** Playlist page: cover mosaic, details, play/shuffle; the owner can edit, delete and reorder. */
export function PlaylistView({ playlistId }: { playlistId: string }) {
  const t = useTranslations("playlist");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const playlist = usePlaylist(playlistId);
  const tracks = usePlaylistTracks(playlistId, playlist.isSuccess);

  if (playlist.isPending) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-72 rounded-3xl" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  if (playlist.isError) {
    return isNotFound(playlist.error) ? (
      <ErrorState title={t("notFoundTitle")} description={t("notFoundBody")} />
    ) : (
      <ErrorState
        title={tErrors(errorCode(playlist.error))}
        onRetry={() => void playlist.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  const info = playlist.data;
  const hidden = tracks.data ? Math.max(0, Number(info.trackCount) - tracks.data.length) : 0;

  return (
    <div className="flex flex-col gap-8">
      <Hero playlist={info} tracks={tracks.data ?? []} />
      <section aria-labelledby="playlist-tracks" className="flex flex-col gap-3">
        <h2 id="playlist-tracks" className="sr-only">
          {t("label")}
        </h2>
        {tracks.isPending ? (
          <Skeleton className="h-40 rounded-2xl" />
        ) : tracks.isError ? (
          <ErrorState
            title={tErrors(errorCode(tracks.error))}
            onRetry={() => void tracks.refetch()}
            retryLabel={tCommon("retry")}
          />
        ) : tracks.data.length === 0 ? (
          <EmptyState icon={ListMusic} title={t("emptyTitle")} description={t("emptyBody")} />
        ) : info.isOwner ? (
          <OrderableTracks playlistId={info.id} tracks={tracks.data} />
        ) : (
          <TrackList tracks={tracks.data} />
        )}
        {hidden > 0 ? (
          <p className="text-xs text-subtle-foreground">{t("hiddenTracks", { count: hidden })}</p>
        ) : null}
      </section>
    </div>
  );
}

function Hero({ playlist, tracks }: { playlist: Playlist; tracks: Track[] }) {
  const t = useTranslations("playlist");
  const tErrors = useTranslations("errors");
  const router = useRouter();
  const [editing, setEditing] = useState(false);
  const remove = useDeletePlaylist();
  const playQueue = usePlayerStore((s) => s.playQueue);
  const playable = tracks.filter((track) => track.playable).map(toPlayerTrack);
  const hue = artHue(playlist.id);
  const owner = playlist.owner.displayName || playlist.owner.handle;

  function shufflePlay() {
    const store = usePlayerStore.getState();
    if (!store.shuffle) store.toggleShuffle();
    store.playQueue(playable, Math.floor(Math.random() * playable.length));
  }

  return (
    <header
      className="relative isolate overflow-hidden rounded-3xl border p-6 md:p-10"
      style={{
        background: `radial-gradient(120% 140% at 0% 0%, oklch(0.42 0.11 ${hue} / 0.95) 0%, oklch(0.2 0.04 ${hue}) 55%, #0b0b0b 100%)`,
      }}
    >
      <div className="flex flex-col gap-6 sm:flex-row sm:items-end md:gap-8">
        <PlaylistCover
          playlist={playlist}
          className="size-44 animate-in rounded-2xl shadow-elevated duration-500 zoom-in-95 fade-in md:size-56"
        />
        <div className="flex min-w-0 flex-1 flex-col gap-3">
          {editing ? (
            <div className="animate-in rounded-2xl bg-black/40 p-4 backdrop-blur duration-300 fade-in slide-in-from-bottom-1">
              <PlaylistForm playlist={playlist} onDone={() => setEditing(false)} />
            </div>
          ) : (
            <>
              <p className="flex items-center gap-2 text-xs font-semibold tracking-[0.2em] text-white/70 uppercase">
                {playlist.visibility === "Private" ? <Lock className="size-3.5" aria-hidden="true" /> : null}
                {t("label")} · {t(`visibility.${playlist.visibility}`)}
              </p>
              <h1 className="text-4xl font-bold tracking-tight break-words text-white md:text-5xl">
                {playlist.title}
              </h1>
              {playlist.description ? (
                <p className="max-w-2xl text-sm whitespace-pre-line text-white/80">{playlist.description}</p>
              ) : null}
              <p className="flex items-center gap-2 text-sm text-white/80">
                <ArtistAvatar
                  id={playlist.owner.id}
                  name={owner}
                  src={playlist.owner.avatarUrl}
                  className="size-6 text-[0.6rem]"
                />
                {t("by", { name: owner })} · {t("stats", { count: Number(playlist.trackCount) })}
              </p>
            </>
          )}
          <div className="mt-2 flex flex-wrap items-center gap-3">
            {playable.length > 0 ? (
              <>
                <button
                  type="button"
                  onClick={() => playQueue(playable)}
                  className="inline-flex h-11 items-center gap-2 rounded-full bg-primary pr-6 pl-5 text-sm font-semibold text-primary-foreground shadow-[0_10px_30px_-8px_var(--accent-glow)] transition-[background-color,transform] duration-200 hover:scale-[1.03] hover:bg-primary-hover active:scale-95"
                >
                  <Play className="size-4 fill-current" aria-hidden="true" />
                  {t("playAll")}
                </button>
                <button
                  type="button"
                  onClick={shufflePlay}
                  className="inline-flex h-11 items-center gap-2 rounded-full border border-white/20 bg-black/20 px-5 text-sm font-medium text-white backdrop-blur transition-colors hover:border-white/40 hover:bg-black/30"
                >
                  <Shuffle className="size-4" aria-hidden="true" />
                  {t("shufflePlay")}
                </button>
              </>
            ) : null}
            {playlist.isOwner && !editing ? (
              <>
                <button
                  type="button"
                  onClick={() => setEditing(true)}
                  className="inline-flex h-11 items-center gap-2 rounded-full border border-white/20 px-5 text-sm font-medium text-white transition-colors hover:bg-white/10"
                >
                  <Pencil className="size-4" aria-hidden="true" />
                  {t("edit")}
                </button>
                <button
                  type="button"
                  disabled={remove.isPending}
                  onClick={() => {
                    if (window.confirm(t("deleteConfirm", { title: playlist.title })))
                      remove.mutate(playlist.id, { onSuccess: () => router.replace("/library") });
                  }}
                  aria-label={t("delete")}
                  title={t("delete")}
                  className="inline-flex size-11 items-center justify-center rounded-full border border-white/20 text-white transition-colors hover:bg-destructive/30"
                >
                  <Trash2 className="size-4" aria-hidden="true" />
                </button>
              </>
            ) : null}
          </div>
          {remove.isError ? (
            <p role="alert" className="text-sm text-destructive">
              {tErrors(errorCode(remove.error))}
            </p>
          ) : null}
        </div>
      </div>
    </header>
  );
}

/**
 * The owner's track list: drag a row by its handle, use the arrow buttons, or press Alt+↑/↓ on a focused
 * row. Each move is saved right away (optimistically); the result is announced to screen readers.
 */
function OrderableTracks({ playlistId, tracks }: { playlistId: string; tracks: Track[] }) {
  const t = useTranslations("playlist");
  const tErrors = useTranslations("errors");
  const reorder = useReorderPlaylist(playlistId);
  const remove = useRemoveFromPlaylist(playlistId);
  const currentId = usePlayerStore((s) => s.queue[s.index]?.id);
  const [dragIndex, setDragIndex] = useState<number | null>(null);
  const [overIndex, setOverIndex] = useState<number | null>(null);
  const [announcement, setAnnouncement] = useState("");

  const move = (from: number, to: number) => {
    if (to < 0 || to >= tracks.length || from === to) return;
    const next = [...tracks];
    const [item] = next.splice(from, 1);
    next.splice(to, 0, item!);
    reorder.mutate(next);
    setAnnouncement(t("moved", { title: item!.title, position: to + 1 }));
  };

  const onKeyDown = (event: KeyboardEvent, index: number) => {
    if (!event.altKey || (event.key !== "ArrowUp" && event.key !== "ArrowDown")) return;
    event.preventDefault();
    const to = event.key === "ArrowUp" ? index - 1 : index + 1;
    move(index, to);
    // Keep focus on the moved row.
    requestAnimationFrame(() => document.getElementById(`pl-row-${tracks[index]!.id}`)?.focus());
  };

  const onDrop = (event: DragEvent, index: number) => {
    event.preventDefault();
    if (dragIndex !== null) move(dragIndex, index);
    setDragIndex(null);
    setOverIndex(null);
  };

  return (
    <div className="flex flex-col gap-2">
      <p className="text-xs text-subtle-foreground">{t("dragHint")}</p>
      <ol className="flex flex-col gap-1">
        {tracks.map((track, i) => {
          const duration = durationMsOf(track);
          const isCurrent = currentId === track.id;
          return (
            <li
              key={track.id}
              id={`pl-row-${track.id}`}
              tabIndex={0}
              draggable
              onDragStart={(e) => {
                setDragIndex(i);
                e.dataTransfer.effectAllowed = "move";
              }}
              onDragOver={(e) => {
                e.preventDefault();
                setOverIndex(i);
              }}
              onDragEnd={() => {
                setDragIndex(null);
                setOverIndex(null);
              }}
              onDrop={(e) => onDrop(e, i)}
              onKeyDown={(e) => onKeyDown(e, i)}
              className={cn(
                "group grid grid-cols-[1.5rem_2.25rem_3rem_minmax(0,1fr)_auto] items-center gap-x-3 rounded-xl p-2 transition-[background-color,opacity,box-shadow] duration-200 hover:bg-surface-hover/70 sm:gap-x-4",
                isCurrent && "bg-primary-subtle",
                dragIndex === i && "opacity-40",
                overIndex === i &&
                  dragIndex !== null &&
                  dragIndex !== i &&
                  "shadow-[inset_0_2px_0_var(--primary)]",
              )}
            >
              <GripVertical
                className="size-4 cursor-grab text-subtle-foreground active:cursor-grabbing"
                aria-hidden="true"
              />
              <div className="relative flex size-9 items-center justify-center">
                <span className="text-sm text-subtle-foreground tabular-nums transition-opacity group-hover:opacity-0">
                  {i + 1}
                </span>
                <span className="absolute inset-0 flex items-center justify-center opacity-0 transition-opacity group-focus-within:opacity-100 group-hover:opacity-100">
                  <PlayButton track={track} context={tracks} size="icon-sm" className="size-9" />
                </span>
              </div>
              <TrackArt id={track.id} artwork={track.artwork} className="size-12 rounded-md" />
              <div className="min-w-0">
                <Link
                  href={`/tracks/${track.id}`}
                  className={cn("block truncate font-medium hover:underline", isCurrent && "text-primary")}
                >
                  {track.title}
                </Link>
                <Link
                  href={`/artists/${track.artist.slug}`}
                  className="block truncate text-sm text-muted-foreground hover:text-foreground"
                >
                  {track.artist.name}
                </Link>
              </div>
              <div className="flex items-center gap-0.5">
                <span className="mr-2 hidden w-12 text-right text-sm text-subtle-foreground tabular-nums sm:block">
                  {duration ? formatTime(duration / 1000) : ""}
                </span>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  disabled={i === 0 || reorder.isPending}
                  onClick={() => move(i, i - 1)}
                  aria-label={t("moveUp", { title: track.title })}
                >
                  <ArrowUp className="size-4" aria-hidden="true" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  disabled={i === tracks.length - 1 || reorder.isPending}
                  onClick={() => move(i, i + 1)}
                  aria-label={t("moveDown", { title: track.title })}
                >
                  <ArrowDown className="size-4" aria-hidden="true" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  disabled={remove.isPending}
                  onClick={() => remove.mutate(track.id)}
                  aria-label={t("remove", { title: track.title })}
                >
                  <X className="size-4" aria-hidden="true" />
                </Button>
              </div>
            </li>
          );
        })}
      </ol>
      <p role="status" className="sr-only">
        {announcement}
      </p>
      {reorder.isError || remove.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(reorder.error ?? remove.error))}
        </p>
      ) : null}
    </div>
  );
}
