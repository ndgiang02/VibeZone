"use client";

import { Music2 } from "lucide-react";
import { useState } from "react";
import { cn } from "@/lib/utils";

/** Deterministic hue for an id — shared by placeholder artwork and the backdrops derived from it. */
export function artHue(id: string) {
  let hash = 0;
  for (const char of id) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return hash % 360;
}

export type ArtworkUrls = { small: string; large: string };

/**
 * Track cover: the uploaded artwork (API-served square JPEG) fading in over a deterministic gradient per
 * track id. The gradient is also the fallback when there is no cover or it fails to load — never a stock
 * or fake cover. `size` picks the rendition: "small" (≈320 px) for lists, "large" for hero views.
 */
export function TrackArt({
  id,
  artwork,
  size = "small",
  className,
}: {
  id: string;
  artwork?: ArtworkUrls | null;
  size?: keyof ArtworkUrls;
  className?: string;
}) {
  const hue = artHue(id);
  const src = artwork?.[size];
  // Keyed by URL so a changed cover starts hidden again and fades in.
  const [state, setState] = useState<{ src?: string; loaded: boolean; failed: boolean }>({
    loaded: false,
    failed: false,
  });
  const current = state.src === src ? state : { src, loaded: false, failed: false };

  return (
    <div
      aria-hidden="true"
      className={cn(
        "relative flex shrink-0 items-center justify-center overflow-hidden rounded-lg",
        className,
      )}
      style={{
        background: `linear-gradient(135deg, oklch(0.6 0.16 ${hue}), oklch(0.3 0.08 ${(hue + 50) % 360}))`,
      }}
    >
      <Music2 className="size-2/5 text-white/70" />
      {src && !current.failed ? (
        // A plain <img>: covers are served by our API with the viewer's cookie (private tracks), which the
        // Next image optimizer would not forward.
        // eslint-disable-next-line @next/next/no-img-element
        <img
          src={src}
          alt=""
          loading="lazy"
          decoding="async"
          draggable={false}
          // An image cached before hydration may finish before React attaches onLoad.
          ref={(img) => {
            if (img?.complete && img.naturalWidth > 0 && !current.loaded)
              setState({ src, loaded: true, failed: false });
          }}
          onLoad={() => setState({ src, loaded: true, failed: false })}
          onError={() => setState({ src, loaded: false, failed: true })}
          className={cn(
            "absolute inset-0 size-full object-cover transition-[opacity,scale] duration-500 ease-out",
            current.loaded ? "scale-100 opacity-100" : "scale-[1.04] opacity-0",
          )}
        />
      ) : null}
    </div>
  );
}
