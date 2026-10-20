"use client";

import { useQueryClient } from "@tanstack/react-query";
import { Camera, ExternalLink, ImagePlus, Loader2, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useId, useRef, useState, type ReactNode } from "react";
import { ArtistAvatar, artistHue } from "@/components/media/artist-avatar";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { invalidateArtistImages, useRemoveArtistImage, type Artist } from "@/features/tracks/api";
import {
  ACCEPTED_ARTWORK,
  MAX_ARTWORK_BYTES,
  uploadArtistImage,
  type ArtistImageSlot,
  type ImageUrls,
  type UploadHandle,
} from "@/features/tracks/upload";

const ARTWORK_TYPES = new Set(["image/jpeg", "image/png", "image/webp"]);
const MB = 1024 * 1024;

/** Upload state for one image slot; the API validates, crops and re-encodes (ADR-0009). */
function useArtistImageUpload(slot: ArtistImageSlot, onError: (message: string | null) => void) {
  const t = useTranslations("studio.newTrack");
  const tErrors = useTranslations("errors");
  const queryClient = useQueryClient();
  const handle = useRef<UploadHandle<ImageUrls> | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => () => handle.current?.abort(), []);

  const start = (file: File) => {
    if (!ARTWORK_TYPES.has(file.type)) return onError(t("artworkType"));
    if (file.size > MAX_ARTWORK_BYTES) return onError(t("artworkTooLarge", { max: MAX_ARTWORK_BYTES / MB }));
    onError(null);
    setPending(true);
    const current = uploadArtistImage(slot, file);
    handle.current = current;
    current.promise
      .then(() => invalidateArtistImages(queryClient))
      .catch((error: unknown) => {
        if (!(error instanceof DOMException && error.name === "AbortError"))
          onError(tErrors(errorCode(error)));
      })
      .finally(() => {
        handle.current = null;
        setPending(false);
      });
  };

  return { pending, start };
}

/** Hidden file input behind a label; also accepts a dropped image. */
function ImagePickLabel({
  onPick,
  disabled,
  label,
  className,
  children,
}: {
  onPick: (file: File) => void;
  disabled: boolean;
  label: string;
  className?: string;
  children: ReactNode;
}) {
  const inputId = useId();
  return (
    <label
      htmlFor={inputId}
      title={label}
      onDragOver={(e) => e.preventDefault()}
      onDrop={(e) => {
        e.preventDefault();
        const file = e.dataTransfer.files[0];
        if (file && !disabled) onPick(file);
      }}
      className={cn(
        "cursor-pointer has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-ring",
        disabled && "pointer-events-none",
        className,
      )}
    >
      {children}
      <span className="sr-only">{label}</span>
      <input
        id={inputId}
        type="file"
        accept={ACCEPTED_ARTWORK}
        className="sr-only"
        disabled={disabled}
        onChange={(e) => {
          const file = e.target.files?.[0];
          e.target.value = "";
          if (file) onPick(file);
        }}
      />
    </label>
  );
}

/**
 * The artist's public face in the Studio: banner (3:1) and avatar, each replaceable by clicking or dropping
 * an image, and removable. Shows exactly what the artist page will show.
 */
export function ArtistProfileCard({ artist }: { artist: Artist }) {
  const t = useTranslations("studio.profile");
  const tErrors = useTranslations("errors");
  const [error, setError] = useState<string | null>(null);
  const banner = useArtistImageUpload("banner", setError);
  const avatar = useArtistImageUpload("avatar", setError);
  const remove = useRemoveArtistImage();
  const hue = artistHue(artist.id);
  const [bannerLoaded, setBannerLoaded] = useState<string | null>(null);
  const bannerSrc = artist.banner?.large ?? null;

  return (
    <section
      aria-label={t("title")}
      className="animate-in overflow-hidden rounded-2xl border bg-surface duration-500 fade-in slide-in-from-bottom-2"
    >
      <div
        className="group/banner relative aspect-[3/1] max-h-56 w-full overflow-hidden"
        style={{
          background: `radial-gradient(120% 140% at 0% 0%, oklch(0.45 0.12 ${hue}) 0%, oklch(0.2 0.04 ${hue}) 60%, #0b0b0b 100%)`,
        }}
      >
        {bannerSrc ? (
          // eslint-disable-next-line @next/next/no-img-element -- served by our API
          <img
            key={bannerSrc}
            src={bannerSrc}
            alt=""
            ref={(img) => {
              if (img?.complete && img.naturalWidth > 0 && bannerLoaded !== bannerSrc)
                setBannerLoaded(bannerSrc);
            }}
            onLoad={() => setBannerLoaded(bannerSrc)}
            className={cn(
              "absolute inset-0 size-full object-cover transition-[opacity,scale] duration-700 ease-out group-hover/banner:scale-[1.02]",
              bannerLoaded === bannerSrc ? "opacity-100" : "opacity-0",
            )}
          />
        ) : null}
        <div className="absolute inset-0 bg-linear-to-t from-black/60 to-transparent" aria-hidden="true" />
        {banner.pending ? (
          <span className="absolute inset-0 flex animate-in items-center justify-center bg-black/50 fade-in">
            <Loader2 className="size-7 animate-spin text-white" aria-hidden="true" />
          </span>
        ) : null}
        <div className="absolute top-3 right-3 flex gap-2">
          <ImagePickLabel
            onPick={banner.start}
            disabled={banner.pending}
            label={bannerSrc ? t("changeBanner") : t("addBanner")}
            className="inline-flex h-8 items-center gap-1.5 rounded-full bg-black/55 px-3 text-xs font-medium text-white backdrop-blur transition-colors hover:bg-black/75"
          >
            <ImagePlus className="size-3.5" aria-hidden="true" />
            <span aria-hidden="true">{bannerSrc ? t("changeBanner") : t("addBanner")}</span>
          </ImagePickLabel>
          {bannerSrc ? (
            <button
              type="button"
              onClick={() => remove.mutate("banner")}
              disabled={remove.isPending}
              aria-label={t("removeBanner")}
              title={t("removeBanner")}
              className="inline-flex size-8 items-center justify-center rounded-full bg-black/55 text-white backdrop-blur transition-colors hover:bg-black/75"
            >
              <Trash2 className="size-3.5" aria-hidden="true" />
            </button>
          ) : null}
        </div>
      </div>

      <div className="flex flex-col gap-4 px-5 pb-5 sm:flex-row sm:items-end">
        <div className="relative -mt-12 w-fit shrink-0">
          <ImagePickLabel
            onPick={avatar.start}
            disabled={avatar.pending}
            label={t("changeAvatar")}
            className="group/avatar relative block rounded-full"
          >
            <ArtistAvatar
              id={artist.id}
              name={artist.name}
              src={artist.avatar?.large}
              className="size-24 text-2xl shadow-elevated ring-4 ring-surface transition-transform duration-300 group-hover/avatar:scale-[1.03]"
            />
            <span
              aria-hidden="true"
              className={cn(
                "absolute inset-0 flex items-center justify-center rounded-full bg-black/55 text-white transition-opacity duration-200",
                avatar.pending ? "opacity-100" : "opacity-0 group-hover/avatar:opacity-100",
              )}
            >
              {avatar.pending ? <Loader2 className="size-6 animate-spin" /> : <Camera className="size-6" />}
            </span>
          </ImagePickLabel>
          {artist.avatar ? (
            <button
              type="button"
              onClick={() => remove.mutate("avatar")}
              disabled={remove.isPending}
              aria-label={t("removeAvatar")}
              title={t("removeAvatar")}
              className="absolute -right-1 bottom-0 flex size-7 animate-in items-center justify-center rounded-full border bg-surface-elevated text-muted-foreground shadow-card transition-colors zoom-in-50 hover:text-foreground"
            >
              <Trash2 className="size-3.5" aria-hidden="true" />
            </button>
          ) : null}
        </div>
        <div className="min-w-0 flex-1">
          <p className="text-xs font-medium text-muted-foreground">{t("publishingAs")}</p>
          <p className="truncate text-xl font-bold tracking-tight">{artist.name}</p>
          <p className="mt-1 text-xs text-subtle-foreground">{t("hint", { max: MAX_ARTWORK_BYTES / MB })}</p>
        </div>
        <Link
          href={`/artists/${artist.slug}`}
          className="inline-flex h-8 shrink-0 items-center gap-1.5 self-start rounded-full border px-3 text-xs font-medium transition-colors hover:bg-surface-hover sm:self-end"
        >
          {t("viewProfile")}
          <ExternalLink className="size-3.5" aria-hidden="true" />
        </Link>
      </div>
      {error || remove.isError ? (
        <p role="alert" className="animate-in px-5 pb-4 text-sm text-destructive fade-in">
          {error ?? tErrors(errorCode(remove.error))}
        </p>
      ) : null}
    </section>
  );
}
