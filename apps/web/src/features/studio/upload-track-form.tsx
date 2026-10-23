"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useQueryClient } from "@tanstack/react-query";
import { CheckCircle2, FileAudio, ImagePlus, RefreshCw, Upload, X } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useEffect, useId, useRef, useState, type DragEvent, type ReactNode } from "react";
import { useForm, useWatch } from "react-hook-form";
import { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ApiError } from "@/lib/api/client";
import { isErrorCode, type ErrorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useGenres } from "@/features/catalog/use-genres";
import { trackKeys } from "@/features/tracks/api";
import {
  ACCEPTED_ARTWORK,
  ACCEPTED_AUDIO,
  MAX_ARTWORK_BYTES,
  MAX_UPLOAD_BYTES,
  type UploadHandle,
  type UploadProgress,
  type UploadStatus,
  uploadNewTrack,
} from "@/features/tracks/upload";

const selectClass = "h-10 rounded-lg border border-input bg-transparent px-3 text-sm dark:bg-input/30";
const ARTWORK_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MB = 1024 * 1024;

type Phase =
  { kind: "edit" } | { kind: "uploading"; progress: UploadProgress | null } | { kind: "done"; title: string };

/** "My Song (Final Mix).mp3" → "My Song (Final Mix)". */
export function titleFromFileName(name: string) {
  return name
    .replace(/\.[^.]+$/, "")
    .replace(/[_]+/g, " ")
    .replace(/\s+/g, " ")
    .trim()
    .slice(0, 120);
}

/**
 * One step: pick the audio (and optionally a cover), fill in the details, press Upload. Everything goes in a
 * single request, so the server creates the track only if all of it is valid. Progress and cancel while
 * uploading; the new track then appears in the list as Processing.
 */
export function UploadTrackForm({ onCreated }: { onCreated: (trackId: string) => void }) {
  const t = useTranslations("studio.newTrack");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const genres = useGenres();
  const queryClient = useQueryClient();
  const handle = useRef<UploadHandle<UploadStatus> | null>(null);
  const [phase, setPhase] = useState<Phase>({ kind: "edit" });
  const [audio, setAudio] = useState<File | null>(null);
  // The chosen cover and its local preview URL (created on pick, revoked when replaced or on unmount).
  const [artwork, setArtwork] = useState<{ file: File; url: string } | null>(null);
  const [audioError, setAudioError] = useState<string | null>(null);
  const [artworkError, setArtworkError] = useState<string | null>(null);
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const schema = z.object({
    title: z.string().trim().min(1, t("titleInvalid")).max(120, t("titleInvalid")),
    genreSlug: z.string(),
    visibility: z.enum(["Public", "Unlisted", "Private"]),
    description: z.string().max(2000, t("descriptionTooLong")),
  });
  const form = useForm<z.infer<typeof schema>>({
    resolver: zodResolver(schema),
    defaultValues: { title: "", genreSlug: "", visibility: "Public", description: "" },
  });
  const visibility = useWatch({ control: form.control, name: "visibility" });

  useEffect(() => () => handle.current?.abort(), []);
  useEffect(() => {
    if (!artwork) return;
    const url = artwork.url;
    return () => URL.revokeObjectURL(url);
  }, [artwork]);

  const pickAudio = (file: File) => {
    setFormError(null);
    if (file.size === 0) return setAudioError(tErrors("unsupported_file_type"));
    if (file.size > MAX_UPLOAD_BYTES) return setAudioError(tErrors("file_too_large"));
    setAudioError(null);
    setAudio(file);
    // Suggest a title from the file name unless the user already typed one.
    if (!form.getFieldState("title").isDirty || !form.getValues("title").trim()) {
      form.setValue("title", titleFromFileName(file.name), { shouldDirty: false, shouldValidate: true });
    }
  };

  const pickArtwork = (file: File) => {
    setFormError(null);
    if (!ARTWORK_TYPES.has(file.type)) return setArtworkError(t("artworkType"));
    if (file.size > MAX_ARTWORK_BYTES)
      return setArtworkError(t("artworkTooLarge", { max: MAX_ARTWORK_BYTES / MB }));
    setArtworkError(null);
    setArtwork({ file, url: URL.createObjectURL(file) });
  };

  const reset = () => {
    form.reset();
    setAudio(null);
    setArtwork(null);
    setAudioError(null);
    setArtworkError(null);
    setFormError(null);
    setPhase({ kind: "edit" });
  };

  const submit = async (values: z.infer<typeof schema>) => {
    setFormError(null);
    if (!audio) {
      setAudioError(t("audioRequired"));
      return;
    }
    const title = values.title.trim();
    setPhase({ kind: "uploading", progress: null });
    const current = uploadNewTrack(
      {
        title,
        visibility: values.visibility,
        genreSlug: values.genreSlug || null,
        description: values.description || null,
      },
      audio,
      artwork?.file ?? null,
      (progress) => setPhase((p) => (p.kind === "uploading" ? { kind: "uploading", progress } : p)),
    );
    handle.current = current;
    try {
      const status = await current.promise;
      setPhase({ kind: "done", title });
      onCreated(status.trackId);
      void queryClient.invalidateQueries({ queryKey: trackKeys.all });
    } catch (error) {
      setPhase({ kind: "edit" });
      if (error instanceof DOMException && error.name === "AbortError") return;
      const fileErrors = error instanceof ApiError ? error.fieldErrors : {};
      const fileCode = (field: "audio" | "artwork") => {
        const code = fileErrors[field];
        return code && isErrorCode(code) ? tErrors(code) : null;
      };
      setAudioError(fileCode("audio"));
      setArtworkError(fileCode("artwork"));
      if (fileCode("audio") || fileCode("artwork")) return;
      setFormError(
        applyServerErrors(error, ["title", "genreSlug", "visibility", "description"], form.setError, (c) =>
          tErrors(c),
        ),
      );
    } finally {
      handle.current = null;
    }
  };

  if (phase.kind === "done") {
    return (
      <div
        role="status"
        className="flex animate-in flex-col items-center gap-3 py-6 text-center duration-500 zoom-in-95 fade-in"
      >
        <span
          className="flex size-14 animate-in items-center justify-center rounded-full bg-primary-subtle text-primary duration-500 fill-mode-both zoom-in-50 spin-in-12"
          style={{ animationDelay: "150ms" }}
        >
          <CheckCircle2 className="size-8" aria-hidden="true" />
        </span>
        <p className="text-lg font-semibold">{t("successTitle")}</p>
        <p className="max-w-xs text-sm text-muted-foreground">{t("successBody", { title: phase.title })}</p>
        <Button variant="outline" onClick={reset} className="mt-1">
          <Upload className="size-4" aria-hidden="true" />
          {t("another")}
        </Button>
      </div>
    );
  }

  if (phase.kind === "uploading") {
    return (
      <UploadProgressPanel
        title={form.getValues("title").trim()}
        preview={artwork?.url ?? null}
        progress={phase.progress}
        onCancel={() => handle.current?.abort()}
      />
    );
  }

  return (
    <form
      onSubmit={(event) => void form.handleSubmit(submit)(event)}
      noValidate
      className="flex animate-in flex-col gap-5 duration-300 fade-in"
    >
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <div className="grid gap-4 sm:grid-cols-[9rem_minmax(0,1fr)]">
        <ArtworkPicker
          preview={artwork?.url ?? null}
          error={artworkError}
          onPick={pickArtwork}
          onClear={() => setArtwork(null)}
        />
        <AudioPicker file={audio} error={audioError} onPick={pickAudio} onClear={() => setAudio(null)} />
      </div>
      <fieldset className="flex flex-col gap-4">
        <legend className="mb-3 text-sm font-medium text-muted-foreground">{t("details")}</legend>
        <TextField
          label={t("title")}
          error={form.formState.errors.title?.message}
          {...form.register("title")}
        />
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="track-genre">{t("genre")}</Label>
            <select
              id="track-genre"
              className={selectClass}
              disabled={genres.isPending}
              {...form.register("genreSlug")}
            >
              <option value="">{t("noGenre")}</option>
              {genres.data?.map((genre) => (
                <option key={genre.slug} value={genre.slug}>
                  {genre.name}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <Label htmlFor="track-visibility">{t("visibility")}</Label>
            <select
              id="track-visibility"
              className={selectClass}
              aria-describedby="track-visibility-hint"
              {...form.register("visibility")}
            >
              <option value="Public">{tTrack("visibility.Public")}</option>
              <option value="Unlisted">{tTrack("visibility.Unlisted")}</option>
              <option value="Private">{tTrack("visibility.Private")}</option>
            </select>
          </div>
        </div>
        <p
          key={visibility}
          id="track-visibility-hint"
          className="-mt-2 animate-in text-xs text-muted-foreground duration-300 fade-in slide-in-from-top-1"
        >
          {t(`visibilityHint.${visibility}`)}
        </p>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="track-description">{t("description")}</Label>
          <textarea
            id="track-description"
            rows={3}
            className="rounded-lg border border-input bg-transparent px-3 py-2 text-sm transition-colors dark:bg-input/30"
            aria-invalid={form.formState.errors.description ? true : undefined}
            {...form.register("description")}
          />
          {form.formState.errors.description ? (
            <p className="text-xs text-destructive">{form.formState.errors.description.message}</p>
          ) : null}
        </div>
      </fieldset>
      <SubmitButton pending={false} className="w-full sm:w-auto sm:self-start">
        <Upload className="size-4" aria-hidden="true" />
        {t("submit")}
      </SubmitButton>
    </form>
  );
}

/** Drop target shared by both pickers: highlights while a file is dragged over it. */
function DropZone({
  inputId,
  accept,
  onPick,
  className,
  children,
}: {
  inputId: string;
  accept: string;
  onPick: (file: File) => void;
  className?: string;
  children: (dragging: boolean) => ReactNode;
}) {
  const [dragging, setDragging] = useState(false);
  const onDrop = (event: DragEvent) => {
    event.preventDefault();
    setDragging(false);
    const file = event.dataTransfer.files[0];
    if (file) onPick(file);
  };
  return (
    <label
      htmlFor={inputId}
      onDragOver={(e) => {
        e.preventDefault();
        setDragging(true);
      }}
      onDragLeave={() => setDragging(false)}
      onDrop={onDrop}
      className={cn(
        "group/drop relative flex cursor-pointer overflow-hidden rounded-xl border border-dashed transition-[border-color,background-color,transform,box-shadow] duration-200 hover:border-primary-border hover:bg-surface-hover/40 has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ring",
        dragging && "scale-[1.01] border-primary bg-primary-subtle shadow-glow",
        className,
      )}
    >
      {children(dragging)}
      <input
        id={inputId}
        type="file"
        accept={accept}
        className="sr-only"
        onChange={(e) => {
          const file = e.target.files?.[0];
          e.target.value = "";
          if (file) onPick(file);
        }}
      />
    </label>
  );
}

function ArtworkPicker({
  preview,
  error,
  onPick,
  onClear,
}: {
  preview: string | null;
  error: string | null;
  onPick: (file: File) => void;
  onClear: () => void;
}) {
  const t = useTranslations("studio.newTrack");
  const inputId = useId();
  const hintId = `${inputId}-hint`;

  return (
    <div className="flex flex-col gap-1.5">
      <span className="flex items-baseline justify-between gap-2 text-sm font-medium">
        {t("artwork")}
        <span className="text-xs font-normal text-subtle-foreground">{t("artworkOptional")}</span>
      </span>
      <div className="relative w-full max-w-36">
        <DropZone
          inputId={inputId}
          accept={ACCEPTED_ARTWORK}
          onPick={onPick}
          className="aspect-square w-full"
        >
          {(dragging) =>
            preview ? (
              <>
                {/* eslint-disable-next-line @next/next/no-img-element -- local blob preview */}
                <img
                  key={preview}
                  src={preview}
                  alt=""
                  className="absolute inset-0 size-full animate-in object-cover duration-500 fade-in zoom-in-110"
                />
                <span className="absolute inset-0 flex items-center justify-center bg-black/55 text-xs font-medium text-white opacity-0 transition-opacity duration-200 group-hover/drop:opacity-100">
                  <RefreshCw className="mr-1.5 size-3.5" aria-hidden="true" />
                  {t("artworkChange")}
                </span>
                <span className="sr-only">{t("artworkChange")}</span>
              </>
            ) : (
              <span className="m-auto flex flex-col items-center gap-1.5 px-2 text-center text-xs text-muted-foreground">
                <ImagePlus
                  className={cn(
                    "size-7 text-primary transition-transform duration-300 group-hover/drop:-translate-y-0.5 group-hover/drop:scale-110",
                    dragging && "scale-125",
                  )}
                  aria-hidden="true"
                />
                {t("artworkChoose")}
              </span>
            )
          }
        </DropZone>
        {preview ? (
          <button
            type="button"
            onClick={onClear}
            aria-label={t("artworkRemove")}
            className="absolute -top-2 -right-2 flex size-6 animate-in items-center justify-center rounded-full border bg-surface-elevated text-muted-foreground shadow-card transition-colors duration-200 zoom-in-50 hover:text-foreground"
          >
            <X className="size-3.5" aria-hidden="true" />
          </button>
        ) : null}
      </div>
      {error ? (
        <p role="alert" className="animate-in text-xs text-destructive fade-in slide-in-from-top-1">
          {error}
        </p>
      ) : (
        <p id={hintId} className="text-xs text-subtle-foreground">
          {t("artworkHint", { max: MAX_ARTWORK_BYTES / MB })}
        </p>
      )}
    </div>
  );
}

function AudioPicker({
  file,
  error,
  onPick,
  onClear,
}: {
  file: File | null;
  error: string | null;
  onPick: (file: File) => void;
  onClear: () => void;
}) {
  const t = useTranslations("studio.newTrack");
  const tUpload = useTranslations("studio.upload");
  const inputId = useId();
  const formatSize = useFormatSize();

  return (
    <div className="flex min-w-0 flex-col gap-1.5">
      <span className="text-sm font-medium">{t("audio")}</span>
      {file ? (
        <div
          key={file.name + file.size}
          className="flex animate-in items-center gap-3 rounded-xl border border-primary-border bg-primary-subtle/50 p-3 duration-300 fade-in slide-in-from-bottom-1"
        >
          <span className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground">
            <FileAudio className="size-5" aria-hidden="true" />
          </span>
          <span className="min-w-0 flex-1">
            <span className="block truncate text-sm font-medium">{file.name}</span>
            <span className="text-xs text-muted-foreground tabular-nums">{formatSize(file.size)}</span>
          </span>
          <label
            htmlFor={inputId}
            className="cursor-pointer rounded-md px-2 py-1 text-xs font-medium text-muted-foreground transition-colors hover:bg-surface-hover hover:text-foreground has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-ring"
          >
            {t("audioReplace")}
            <input
              id={inputId}
              type="file"
              accept={ACCEPTED_AUDIO}
              className="sr-only"
              onChange={(e) => {
                const next = e.target.files?.[0];
                e.target.value = "";
                if (next) onPick(next);
              }}
            />
          </label>
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            onClick={onClear}
            aria-label={t("audioRemove", { name: file.name })}
          >
            <X className="size-4" aria-hidden="true" />
          </Button>
        </div>
      ) : (
        <DropZone
          inputId={inputId}
          accept={ACCEPTED_AUDIO}
          onPick={onPick}
          className={cn(
            "min-h-36 flex-1 flex-col items-center justify-center gap-2 px-4 py-6 text-center",
            error && "border-destructive/60",
          )}
        >
          {(dragging) => (
            <>
              <span
                className={cn(
                  "flex size-11 items-center justify-center rounded-full bg-primary-subtle text-primary transition-transform duration-300 group-hover/drop:-translate-y-0.5",
                  dragging && "scale-110 animate-bounce",
                )}
              >
                <Upload className="size-5" aria-hidden="true" />
              </span>
              <span className="text-sm font-medium">{t("audioChoose")}</span>
              <span className="text-xs text-muted-foreground">
                {tUpload("hint", { max: MAX_UPLOAD_BYTES / MB })}
              </span>
            </>
          )}
        </DropZone>
      )}
      {error ? (
        <p role="alert" className="animate-in text-xs text-destructive fade-in slide-in-from-top-1">
          {error}
        </p>
      ) : null}
    </div>
  );
}

function UploadProgressPanel({
  title,
  preview,
  progress,
  onCancel,
}: {
  title: string;
  preview: string | null;
  progress: UploadProgress | null;
  onCancel: () => void;
}) {
  const t = useTranslations("studio.newTrack");
  const tUpload = useTranslations("studio.upload");
  const formatSize = useFormatSize();
  const percent = progress ? Math.round((progress.loaded / progress.total) * 100) : 0;
  const finishing = percent >= 100;

  return (
    <div
      className="flex animate-in flex-col gap-4 py-2 duration-300 fade-in slide-in-from-bottom-2"
      aria-live="polite"
    >
      <div className="flex items-center gap-4">
        <div className="relative size-16 shrink-0 overflow-hidden rounded-lg bg-surface-hover">
          {preview ? (
            // eslint-disable-next-line @next/next/no-img-element -- local blob preview
            <img src={preview} alt="" className="size-full object-cover" />
          ) : (
            <FileAudio className="absolute inset-0 m-auto size-6 text-muted-foreground" aria-hidden="true" />
          )}
          <span className="absolute inset-0 animate-pulse bg-primary/10" aria-hidden="true" />
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-medium tracking-wide text-primary uppercase">
            {finishing ? t("finishing") : t("uploading")}
          </p>
          <p className="truncate font-semibold">{title}</p>
          {progress ? (
            <p className="text-xs text-muted-foreground tabular-nums">
              {t("uploadingDetail", {
                done: formatSize(progress.loaded),
                total: formatSize(progress.total),
                percent,
              })}
            </p>
          ) : null}
        </div>
      </div>
      <div
        role="progressbar"
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={percent}
        aria-label={tUpload("progress")}
        className="h-2 overflow-hidden rounded-full bg-surface-hover"
      >
        <div
          className={cn(
            "vz-progress-fill h-full rounded-full bg-primary transition-[width] duration-300 ease-out",
            finishing && "animate-pulse",
          )}
          style={{ width: `${Math.max(percent, 2)}%` }}
        />
      </div>
      <Button variant="ghost" size="sm" onClick={onCancel} disabled={finishing} className="self-start">
        <X className="size-4" aria-hidden="true" />
        {t("cancel")}
      </Button>
    </div>
  );
}

function useFormatSize() {
  const locale = useLocale();
  return (bytes: number) =>
    new Intl.NumberFormat(locale, {
      style: "unit",
      unit: "megabyte",
      maximumFractionDigits: bytes < 10 * MB ? 1 : 0,
    }).format(bytes / MB);
}
