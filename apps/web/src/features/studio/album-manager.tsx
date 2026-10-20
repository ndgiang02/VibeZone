"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowDown, ArrowUp, ChevronDown, Disc3, ExternalLink, Plus, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Link } from "@/i18n/navigation";
import { errorCode, type ErrorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { applyServerErrors } from "@/features/auth/server-errors";
import {
  type Album,
  useAlbum,
  useCreateAlbum,
  useDeleteAlbum,
  useMyAlbums,
  useSetAlbumTracks,
} from "@/features/catalog/api";
import { type Track } from "@/features/tracks/api";
import { TrackArt } from "@/features/tracks/track-art";

const selectClass = "h-10 rounded-lg border border-input bg-transparent px-3 text-sm dark:bg-input/30";

/** Studio section: create albums/EPs/singles and choose and order their tracks from the artist's own. */
export function AlbumManager({ myTracks }: { myTracks: Track[] }) {
  const t = useTranslations("studio.albums");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const albums = useMyAlbums(true);
  const [creating, setCreating] = useState(false);

  return (
    <section aria-labelledby="studio-albums" className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-3">
        <h2 id="studio-albums" className="text-lg font-semibold">
          {t("heading")}
        </h2>
        <Button variant={creating ? "ghost" : "outline"} size="sm" onClick={() => setCreating((c) => !c)}>
          <Plus
            className={cn("size-3.5 transition-transform duration-200", creating && "rotate-45")}
            aria-hidden="true"
          />
          {creating ? tCommon("cancel") : t("create")}
        </Button>
      </div>
      {creating ? <CreateAlbumForm onDone={() => setCreating(false)} /> : null}
      {albums.isPending ? (
        <Skeleton className="h-20 rounded-2xl" />
      ) : albums.isError ? (
        <ErrorState
          title={tErrors(errorCode(albums.error))}
          onRetry={() => void albums.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : albums.data.length === 0 ? (
        creating ? null : (
          <EmptyState icon={Disc3} title={t("empty")} />
        )
      ) : (
        <ul className="flex flex-col gap-3">
          {albums.data.map((album, i) => (
            <AlbumRow key={album.id} album={album} myTracks={myTracks} index={i} />
          ))}
        </ul>
      )}
    </section>
  );
}

function CreateAlbumForm({ onDone }: { onDone: () => void }) {
  const t = useTranslations("studio.albums");
  const tAlbum = useTranslations("album");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const create = useCreateAlbum();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const schema = z.object({
    title: z.string().trim().min(1, t("titleInvalid")).max(120, t("titleInvalid")),
    kind: z.enum(["Album", "Ep", "Single"]),
    releaseDate: z.string(),
    visibility: z.enum(["Public", "Unlisted", "Private"]),
    description: z.string().max(2000),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { title: "", kind: "Album", releaseDate: "", visibility: "Public", description: "" },
  });

  const submit = async (values: z.infer<typeof schema>) => {
    setFormError(null);
    try {
      await create.mutateAsync({
        title: values.title,
        kind: values.kind,
        releaseDate: values.releaseDate || null,
        visibility: values.visibility,
        description: values.description || null,
      });
      onDone();
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["title", "kind", "visibility", "description"], form.setError, (c) =>
          tErrors(c),
        ),
      );
    }
  };

  return (
    <form
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
      noValidate
      className="flex animate-in flex-col gap-4 rounded-2xl border bg-surface p-5 duration-300 fade-in slide-in-from-top-2"
    >
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <TextField
        label={t("title")}
        error={form.formState.errors.title?.message}
        {...form.register("title")}
      />
      <div className="grid gap-4 sm:grid-cols-3">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="album-kind">{t("kind")}</Label>
          <select id="album-kind" className={selectClass} {...form.register("kind")}>
            <option value="Album">{tAlbum("kind.Album")}</option>
            <option value="Ep">{tAlbum("kind.Ep")}</option>
            <option value="Single">{tAlbum("kind.Single")}</option>
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="album-date">{t("releaseDate")}</Label>
          <input id="album-date" type="date" className={selectClass} {...form.register("releaseDate")} />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="album-vis">{t("visibility")}</Label>
          <select id="album-vis" className={selectClass} {...form.register("visibility")}>
            <option value="Public">{tTrack("visibility.Public")}</option>
            <option value="Unlisted">{tTrack("visibility.Unlisted")}</option>
            <option value="Private">{tTrack("visibility.Private")}</option>
          </select>
        </div>
      </div>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="album-desc">{t("description")}</Label>
        <textarea
          id="album-desc"
          rows={2}
          className="rounded-lg border border-input bg-transparent px-3 py-2 text-sm dark:bg-input/30"
          {...form.register("description")}
        />
      </div>
      <SubmitButton pending={create.isPending} className="self-start">
        {t("create")}
      </SubmitButton>
    </form>
  );
}

function AlbumRow({ album, myTracks, index }: { album: Album; myTracks: Track[]; index: number }) {
  const t = useTranslations("studio.albums");
  const tAlbum = useTranslations("album");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const [open, setOpen] = useState(false);
  const remove = useDeleteAlbum();

  return (
    <li
      className="animate-in overflow-hidden rounded-2xl border bg-surface duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
      style={{ animationDelay: `${Math.min(index, 8) * 50}ms` }}
    >
      <div className="flex items-center gap-3 p-4">
        <TrackArt id={album.id} artwork={album.cover} className="size-14 rounded-lg" />
        <div className="min-w-0 flex-1">
          <p className="truncate font-medium">{album.title}</p>
          <p className="truncate text-xs text-muted-foreground">
            {tAlbum(`kind.${album.kind}`)} · {tTrack(`visibility.${album.visibility}`)} ·{" "}
            {t("selected", { count: Number(album.trackCount) })}
          </p>
        </div>
        <Button asChild variant="ghost" size="icon-sm" aria-label={t("view")} title={t("view")}>
          <Link href={`/albums/${album.id}`}>
            <ExternalLink className="size-4" aria-hidden="true" />
          </Link>
        </Button>
        <Button
          variant="ghost"
          size="icon-sm"
          disabled={remove.isPending}
          aria-label={t("delete")}
          title={t("delete")}
          onClick={() => {
            if (window.confirm(t("deleteConfirm", { title: album.title }))) remove.mutate(album.id);
          }}
        >
          <Trash2 className="size-4" aria-hidden="true" />
        </Button>
        <Button variant="outline" size="sm" aria-expanded={open} onClick={() => setOpen((o) => !o)}>
          {t("editTracks")}
          <ChevronDown
            className={cn("size-3.5 transition-transform duration-200", open && "rotate-180")}
            aria-hidden="true"
          />
        </Button>
      </div>
      {open ? <TracklistEditor albumId={album.id} myTracks={myTracks} /> : null}
      {remove.isError ? (
        <p role="alert" className="px-4 pb-3 text-sm text-destructive">
          {tErrors(errorCode(remove.error))}
        </p>
      ) : null}
    </li>
  );
}

/** Tick tracks to include; order the chosen ones with the arrows; save replaces the album's tracklist. */
function TracklistEditor({ albumId, myTracks }: { albumId: string; myTracks: Track[] }) {
  const detail = useAlbum(albumId);
  if (!detail.data) return <Skeleton className="m-4 mt-0 h-24 rounded-xl" />;
  return (
    <TracklistEditorLoaded
      albumId={albumId}
      myTracks={myTracks}
      initial={detail.data.tracks.map((track) => track.id)}
    />
  );
}

function TracklistEditorLoaded({
  albumId,
  myTracks,
  initial,
}: {
  albumId: string;
  myTracks: Track[];
  initial: string[];
}) {
  const t = useTranslations("studio.albums");
  const tPlaylist = useTranslations("playlist");
  const tErrors = useTranslations("errors");
  const save = useSetAlbumTracks(albumId);
  const [chosen, setChosen] = useState<string[]>(initial);
  const [saved, setSaved] = useState(false);
  const byId = new Map(myTracks.map((track) => [track.id, track]));
  const others = myTracks.filter((track) => !chosen.includes(track.id));

  const move = (from: number, to: number) =>
    setChosen((list) => {
      const next = [...list];
      const [item] = next.splice(from, 1);
      next.splice(to, 0, item!);
      return next;
    });

  if (myTracks.length === 0) {
    return <p className="px-4 pb-4 text-sm text-muted-foreground">{t("noTracks")}</p>;
  }

  return (
    <div className="flex animate-in flex-col gap-3 border-t px-4 py-4 duration-300 fade-in slide-in-from-top-1">
      <p className="text-xs text-subtle-foreground">{t("pickerHint")}</p>
      <ol className="flex flex-col gap-1">
        {chosen.map((id, i) => {
          const track = byId.get(id);
          if (!track) return null;
          return (
            <li
              key={id}
              className="flex animate-in items-center gap-3 rounded-lg bg-primary-subtle/40 px-2 py-1.5 duration-200 fade-in"
            >
              <input
                type="checkbox"
                checked
                onChange={() => setChosen((list) => list.filter((x) => x !== id))}
                aria-label={track.title}
                className="size-4 accent-[var(--primary)]"
              />
              <span className="w-5 text-right text-xs text-subtle-foreground tabular-nums">{i + 1}</span>
              <TrackArt id={track.id} artwork={track.artwork} className="size-8 rounded" />
              <span className="min-w-0 flex-1 truncate text-sm">{track.title}</span>
              <Button
                variant="ghost"
                size="icon-xs"
                disabled={i === 0}
                onClick={() => move(i, i - 1)}
                aria-label={tPlaylist("moveUp", { title: track.title })}
              >
                <ArrowUp className="size-3.5" aria-hidden="true" />
              </Button>
              <Button
                variant="ghost"
                size="icon-xs"
                disabled={i === chosen.length - 1}
                onClick={() => move(i, i + 1)}
                aria-label={tPlaylist("moveDown", { title: track.title })}
              >
                <ArrowDown className="size-3.5" aria-hidden="true" />
              </Button>
            </li>
          );
        })}
      </ol>
      {others.length > 0 ? (
        <ul className="vz-thin-scrollbar flex max-h-56 flex-col gap-1 overflow-y-auto">
          {others.map((track) => (
            <li
              key={track.id}
              className="flex items-center gap-3 rounded-lg px-2 py-1.5 hover:bg-surface-hover/60"
            >
              <input
                type="checkbox"
                checked={false}
                onChange={() => setChosen((list) => [...list, track.id])}
                aria-label={track.title}
                className="size-4 accent-[var(--primary)]"
              />
              <span className="w-5" />
              <TrackArt id={track.id} artwork={track.artwork} className="size-8 rounded" />
              <span className="min-w-0 flex-1 truncate text-sm text-muted-foreground">
                {track.title}
                {track.album && track.album.id !== albumId ? ` · ${track.album.title}` : ""}
              </span>
            </li>
          ))}
        </ul>
      ) : null}
      <div className="flex items-center gap-3">
        <Button
          size="sm"
          disabled={save.isPending}
          onClick={() =>
            save.mutate(chosen, {
              onSuccess: () => {
                setSaved(true);
                setTimeout(() => setSaved(false), 2000);
              },
            })
          }
        >
          {t("saveTracks")}
        </Button>
        <span className="text-xs text-muted-foreground">{t("selected", { count: chosen.length })}</span>
        {saved ? (
          <span role="status" className="animate-in text-xs text-success fade-in">
            {t("saved")}
          </span>
        ) : null}
      </div>
      {save.isError ? <FormMessage tone="error">{tErrors(errorCode(save.error))}</FormMessage> : null}
    </div>
  );
}
