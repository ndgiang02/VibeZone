"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Upload, X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useId, useRef, useState } from "react";
import { FormMessage } from "@/components/forms/form-message";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { errorCode, type ErrorCode } from "@/lib/api/errors";
import { trackKeys } from "@/features/tracks/api";
import {
  ACCEPTED_AUDIO,
  MAX_UPLOAD_BYTES,
  type UploadHandle,
  uploadTrackAudio,
} from "@/features/tracks/upload";

type State =
  | { kind: "idle" }
  | { kind: "uploading"; fileName: string; progress: number }
  | { kind: "error"; code: ErrorCode };

/** Pick or drop an audio file and upload it with progress; cancellable while uploading. */
export function TrackUploader({ trackId }: { trackId: string }) {
  const t = useTranslations("studio.upload");
  const tErrors = useTranslations("errors");
  const queryClient = useQueryClient();
  const inputId = useId();
  const input = useRef<HTMLInputElement>(null);
  const handle = useRef<UploadHandle | null>(null);
  const [state, setState] = useState<State>({ kind: "idle" });
  const [dragging, setDragging] = useState(false);

  useEffect(() => () => handle.current?.abort(), []);

  const start = (file: File) => {
    if (file.size === 0) {
      setState({ kind: "error", code: "unsupported_file_type" });
      return;
    }
    if (file.size > MAX_UPLOAD_BYTES) {
      setState({ kind: "error", code: "file_too_large" });
      return;
    }
    setState({ kind: "uploading", fileName: file.name, progress: 0 });
    const current = uploadTrackAudio(trackId, file, (progress) =>
      setState((s) => (s.kind === "uploading" ? { ...s, progress } : s)),
    );
    handle.current = current;
    current.promise
      .then(() => {
        setState({ kind: "idle" });
        void queryClient.invalidateQueries({ queryKey: trackKeys.all });
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") {
          setState({ kind: "idle" });
          return;
        }
        setState({ kind: "error", code: errorCode(error) });
      })
      .finally(() => {
        handle.current = null;
      });
  };

  if (state.kind === "uploading") {
    const percent = Math.round(state.progress * 100);
    return (
      <div className="flex flex-col gap-2" aria-live="polite">
        <div className="flex items-center justify-between gap-3 text-sm">
          <span className="truncate">{t("uploading", { name: state.fileName })}</span>
          <Button variant="ghost" size="sm" onClick={() => handle.current?.abort()}>
            <X className="size-4" aria-hidden="true" />
            {t("cancel")}
          </Button>
        </div>
        <div
          role="progressbar"
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={percent}
          aria-label={t("progress")}
          className="h-2 overflow-hidden rounded-full bg-surface-hover"
        >
          <div className="h-full bg-primary transition-[width]" style={{ width: `${percent}%` }} />
        </div>
        <p className="text-xs text-muted-foreground tabular-nums">{percent}%</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      {state.kind === "error" ? <FormMessage tone="error">{tErrors(state.code)}</FormMessage> : null}
      <label
        htmlFor={inputId}
        onDragOver={(e) => {
          e.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(e) => {
          e.preventDefault();
          setDragging(false);
          const file = e.dataTransfer.files[0];
          if (file) start(file);
        }}
        className={cn(
          "flex cursor-pointer flex-col items-center gap-2 rounded-xl border border-dashed px-4 py-6 text-center text-sm transition-colors hover:bg-surface-hover/50",
          dragging && "border-primary bg-primary/10",
        )}
      >
        <Upload className="size-6 text-primary" aria-hidden="true" />
        <span className="font-medium">{t("choose")}</span>
        <span className="text-xs text-muted-foreground">
          {t("hint", { max: MAX_UPLOAD_BYTES / (1024 * 1024) })}
        </span>
        <input
          ref={input}
          id={inputId}
          type="file"
          accept={ACCEPTED_AUDIO}
          className="sr-only"
          onChange={(e) => {
            const file = e.target.files?.[0];
            e.target.value = "";
            if (file) start(file);
          }}
        />
      </label>
    </div>
  );
}
