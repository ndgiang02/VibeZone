"use client";

import { useState } from "react";
import { cn } from "@/lib/utils";

/** Deterministic hue for an artist id (avatar gradient and the artist page backdrop). */
export function artistHue(id: string) {
  let hash = 0;
  for (const char of id) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  return hash % 360;
}

/**
 * Round artist avatar: the uploaded picture (API-served JPEG) fading in over initials on a deterministic
 * gradient. The initials are also the fallback when there is no picture or it fails to load — never a
 * stock photo.
 */
export function ArtistAvatar({
  id,
  name,
  src,
  className,
}: {
  id: string;
  name: string;
  src?: string | null;
  className?: string;
}) {
  const hue = artistHue(id);
  const initials = name
    .split(/\s+/)
    .filter((part) => /\p{L}|\p{N}/u.test(part))
    .slice(0, 2)
    .map((part) => part.charAt(0).toUpperCase())
    .join("");
  // Keyed by URL so a changed picture starts hidden again and fades in.
  const [state, setState] = useState<{ src?: string | null; loaded: boolean; failed: boolean }>({
    loaded: false,
    failed: false,
  });
  const current = state.src === src ? state : { src, loaded: false, failed: false };

  return (
    <span
      aria-hidden="true"
      className={cn(
        "relative flex shrink-0 items-center justify-center overflow-hidden rounded-full text-sm font-semibold text-white",
        className,
      )}
      style={{
        background: `linear-gradient(135deg, oklch(0.62 0.15 ${hue}), oklch(0.28 0.07 ${(hue + 60) % 360}))`,
      }}
    >
      {initials}
      {src && !current.failed ? (
        // A plain <img>: served by our API, not worth the Next image optimizer round trip.
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
            "absolute inset-0 size-full object-cover transition-opacity duration-500 ease-out",
            current.loaded ? "opacity-100" : "opacity-0",
          )}
        />
      ) : null}
    </span>
  );
}
