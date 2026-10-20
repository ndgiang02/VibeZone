"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Camera, Loader2, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useId, useRef, useState } from "react";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { Button } from "@/components/ui/button";
import { api, ensureOk } from "@/lib/api/client";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { authKeys, type Me } from "@/features/auth/api";
import {
  ACCEPTED_ARTWORK,
  MAX_ARTWORK_BYTES,
  uploadUserAvatar,
  type ImageUrls,
  type UploadHandle,
} from "@/features/tracks/upload";

const ARTWORK_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MB = 1024 * 1024;

/** Account picture: click or drop an image to replace it; the API crops it square and re-encodes it. */
export function AvatarEditor({ me }: { me: Me }) {
  const t = useTranslations("settings.avatar");
  const tNew = useTranslations("studio.newTrack");
  const tErrors = useTranslations("errors");
  const queryClient = useQueryClient();
  const inputId = useId();
  const handle = useRef<UploadHandle<ImageUrls> | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const remove = useMutation({
    mutationFn: async () => ensureOk(await api.DELETE("/api/v1/me/avatar")),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: authKeys.me }),
  });

  useEffect(() => () => handle.current?.abort(), []);

  const start = (file: File) => {
    if (!ARTWORK_TYPES.has(file.type)) return setError(tNew("artworkType"));
    if (file.size > MAX_ARTWORK_BYTES)
      return setError(tNew("artworkTooLarge", { max: MAX_ARTWORK_BYTES / MB }));
    setError(null);
    setPending(true);
    const current = uploadUserAvatar(file);
    handle.current = current;
    current.promise
      .then(() => queryClient.invalidateQueries({ queryKey: authKeys.me }))
      .catch((e: unknown) => {
        if (!(e instanceof DOMException && e.name === "AbortError")) setError(tErrors(errorCode(e)));
      })
      .finally(() => {
        handle.current = null;
        setPending(false);
      });
  };

  return (
    <div className="flex flex-wrap items-center gap-5">
      <label
        htmlFor={inputId}
        title={t("change")}
        onDragOver={(e) => e.preventDefault()}
        onDrop={(e) => {
          e.preventDefault();
          const file = e.dataTransfer.files[0];
          if (file && !pending) start(file);
        }}
        className="group/avatar relative block cursor-pointer rounded-full has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ring"
      >
        <ArtistAvatar
          id={me.id}
          name={me.displayName || me.handle}
          src={me.avatar?.large}
          className="size-20 text-2xl ring-2 ring-primary-border transition-transform duration-300 group-hover/avatar:scale-[1.03]"
        />
        <span
          aria-hidden="true"
          className={cn(
            "absolute inset-0 flex items-center justify-center rounded-full bg-black/55 text-white transition-opacity duration-200",
            pending ? "opacity-100" : "opacity-0 group-hover/avatar:opacity-100",
          )}
        >
          {pending ? <Loader2 className="size-5 animate-spin" /> : <Camera className="size-5" />}
        </span>
        <span className="sr-only">{t("change")}</span>
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
      <div className="flex min-w-0 flex-1 flex-col gap-2">
        <p className="text-xs text-muted-foreground">{t("hint", { max: MAX_ARTWORK_BYTES / MB })}</p>
        <div className="flex gap-2">
          <Button asChild variant="outline" size="sm" disabled={pending}>
            <label htmlFor={inputId} className="cursor-pointer">
              <Camera className="size-3.5" aria-hidden="true" />
              {t("change")}
            </label>
          </Button>
          {me.avatar ? (
            <Button
              variant="ghost"
              size="sm"
              disabled={remove.isPending || pending}
              onClick={() => remove.mutate()}
            >
              <Trash2 className="size-3.5" aria-hidden="true" />
              {t("remove")}
            </Button>
          ) : null}
        </div>
        {error || remove.isError ? (
          <p role="alert" className="animate-in text-xs text-destructive fade-in">
            {error ?? tErrors(errorCode(remove.error))}
          </p>
        ) : null}
      </div>
    </div>
  );
}
