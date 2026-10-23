"use client";

import { useQueryClient } from "@tanstack/react-query";
import { ImagePlus, Loader2, Music, Pencil, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useId, useRef, useState } from "react";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import {
  trackKeys,
  useCancelUpload,
  useDeleteTrack,
  useMyArtist,
  useMyTracks,
  type Track,
} from "@/features/tracks/api";
import { PlayButton } from "@/features/tracks/play-button";
import { StatusBadge } from "@/features/tracks/status-badge";
import { TrackArt } from "@/features/tracks/track-art";
import {
  ACCEPTED_ARTWORK,
  MAX_ARTWORK_BYTES,
  uploadArtwork,
  type UploadHandle,
  type TrackArtwork,
} from "@/features/tracks/upload";
import { AlbumManager } from "./album-manager";
import { ArtistProfileCard } from "./artist-profile-card";
import { BecomeArtistForm } from "./become-artist-form";
import { TrackEditForm } from "./track-edit-form";
import { TrackUploader } from "./track-uploader";
import { UploadTrackForm } from "./upload-track-form";

export function StudioView() {
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const artist = useMyArtist(Boolean(me.data));

  if (me.isError || artist.isError) {
    const error = me.error ?? artist.error;
    return (
      <ErrorState
        title={tErrors(errorCode(error))}
        onRetry={() => void (me.isError ? me.refetch() : artist.refetch())}
        retryLabel={tCommon("retry")}
      />
    );
  }

  if (!me.data || artist.isPending) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-48 rounded-2xl" />
        <Skeleton className="h-24 rounded-2xl" />
      </div>
    );
  }

  if (!artist.data) {
    return <BecomeArtistForm me={me.data} />;
  }

  return (
    <div className="flex flex-col gap-6">
      <ArtistProfileCard artist={artist.data} />
      <Workspace />
    </div>
  );
}

function Workspace() {
  const t = useTranslations("studio");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const [highlight, setHighlight] = useState<string | null>(null);
  const tracks = useMyTracks(true);

  return (
    <div className="flex flex-col gap-8">
      <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
        <section
          aria-labelledby="new-track"
          className="h-fit animate-in rounded-2xl border bg-surface p-5 duration-500 fade-in slide-in-from-bottom-2 lg:sticky lg:top-[calc(var(--topbar-height)+1rem)]"
        >
          <h2 id="new-track" className="mb-4 text-lg font-semibold">
            {t("newTrack.heading")}
          </h2>
          <UploadTrackForm onCreated={setHighlight} />
        </section>
        <section aria-labelledby="my-tracks" className="flex flex-col gap-3">
          <h2 id="my-tracks" className="text-lg font-semibold">
            {t("myTracks")}
          </h2>
          {tracks.isPending ? (
            <Skeleton className="h-32 rounded-2xl" />
          ) : tracks.isError ? (
            <ErrorState
              title={tErrors(errorCode(tracks.error))}
              onRetry={() => void tracks.refetch()}
              retryLabel={tCommon("retry")}
            />
          ) : tracks.data.length === 0 ? (
            <EmptyState icon={Music} title={t("emptyTitle")} description={t("emptyBody")} />
          ) : (
            <ul className="flex flex-col gap-3">
              {tracks.data.map((track, index) => (
                <StudioTrack
                  key={track.id}
                  track={track}
                  index={index}
                  highlighted={track.id === highlight}
                />
              ))}
            </ul>
          )}
        </section>
      </div>
      <AlbumManager myTracks={tracks.data ?? []} />
    </div>
  );
}

function StudioTrack({ track, index, highlighted }: { track: Track; index: number; highlighted: boolean }) {
  const t = useTranslations("studio");
  const tEdit = useTranslations("studio.edit");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const cancel = useCancelUpload();
  const remove = useDeleteTrack();
  const uploadable = track.status === "Draft" || track.status === "Failed" || track.status === "Cancelled";
  const [editing, setEditing] = useState(false);

  return (
    <li
      className={cn(
        "flex animate-in flex-col gap-3 rounded-2xl border bg-surface p-4 transition-[border-color,box-shadow] duration-500 fill-mode-both fade-in slide-in-from-bottom-2",
        highlighted && "border-primary/60 shadow-glow",
      )}
      // Stagger the first rows; later ones appear together.
      style={{ animationDelay: `${Math.min(index, 8) * 50}ms` }}
    >
      <div className="flex items-start gap-3">
        <CoverButton track={track} />
        <div className="min-w-0 flex-1">
          <Link href={`/tracks/${track.id}`} className="block truncate font-medium hover:underline">
            {track.title}
          </Link>
          <StatusBadge track={track} />
        </div>
        <PlayButton track={track} size="icon-sm" />
        <Button
          variant="ghost"
          size="icon-sm"
          aria-expanded={editing}
          onClick={() => setEditing((e) => !e)}
          aria-label={tEdit("edit", { title: track.title })}
          title={tEdit("edit", { title: track.title })}
        >
          <Pencil className="size-4" aria-hidden="true" />
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          disabled={remove.isPending || track.status === "Processing"}
          onClick={() => {
            if (window.confirm(t("deleteConfirm", { title: track.title }))) remove.mutate(track.id);
          }}
          aria-label={t("delete", { title: track.title })}
        >
          <Trash2 className="size-4" aria-hidden="true" />
        </Button>
      </div>
      {editing ? <TrackEditForm track={track} onDone={() => setEditing(false)} /> : null}
      {track.album ? (
        <Link
          href={`/albums/${track.album.id}`}
          className="-mt-1 truncate text-xs text-muted-foreground hover:text-foreground"
        >
          {tTrack("onAlbum")}: {track.album.title}
        </Link>
      ) : null}
      {track.status === "Processing" ? (
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
          <span aria-live="polite">{t("processing")}</span>
          <Button
            variant="outline"
            size="sm"
            disabled={cancel.isPending}
            onClick={() => cancel.mutate(track.id)}
          >
            {t("cancelProcessing")}
          </Button>
        </div>
      ) : null}
      {uploadable ? <TrackUploader trackId={track.id} /> : null}
      {cancel.isError || remove.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(cancel.error ?? remove.error))}
        </p>
      ) : null}
    </li>
  );
}

/** The row's cover; click (or drop an image on it) to replace it. */
function CoverButton({ track }: { track: Track }) {
  const t = useTranslations("studio");
  const tNew = useTranslations("studio.newTrack");
  const tErrors = useTranslations("errors");
  const queryClient = useQueryClient();
  const inputId = useId();
  const handle = useRef<UploadHandle<TrackArtwork> | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => () => handle.current?.abort(), []);

  const start = (file: File) => {
    if (!["image/jpeg", "image/png", "image/webp"].includes(file.type)) return setError(tNew("artworkType"));
    if (file.size > MAX_ARTWORK_BYTES)
      return setError(tNew("artworkTooLarge", { max: MAX_ARTWORK_BYTES / (1024 * 1024) }));
    setError(null);
    setPending(true);
    const current = uploadArtwork(track.id, file);
    handle.current = current;
    current.promise
      .then(() => queryClient.invalidateQueries({ queryKey: trackKeys.all }))
      .catch((e: unknown) => {
        if (!(e instanceof DOMException && e.name === "AbortError")) setError(tErrors(errorCode(e)));
      })
      .finally(() => {
        handle.current = null;
        setPending(false);
      });
  };

  return (
    <div className="flex flex-col items-start gap-1">
      <label
        htmlFor={inputId}
        title={t("changeCover", { title: track.title })}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          const file = e.dataTransfer.files[0];
          if (file) start(file);
        }}
        className="group/cover relative size-14 shrink-0 cursor-pointer overflow-hidden rounded-lg has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ring"
      >
        <TrackArt
          id={track.id}
          artwork={track.artwork}
          className="size-full transition-transform duration-300 group-hover/cover:scale-105"
        />
        <span
          className={cn(
            "absolute inset-0 flex items-center justify-center bg-black/55 text-white transition-opacity duration-200",
            pending ? "opacity-100" : "opacity-0 group-hover/cover:opacity-100",
          )}
          aria-hidden="true"
        >
          {pending ? <Loader2 className="size-5 animate-spin" /> : <ImagePlus className="size-5" />}
        </span>
        <span className="sr-only">{t("changeCover", { title: track.title })}</span>
        <input
          id={inputId}
          type="file"
          accept={ACCEPTED_ARTWORK}
          className="sr-only"
          disabled={pending}
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = "";
            if (file) start(file);
          }}
        />
      </label>
      {error ? (
        <p role="alert" className="max-w-40 animate-in text-xs text-destructive fade-in">
          {error}
        </p>
      ) : null}
    </div>
  );
}
