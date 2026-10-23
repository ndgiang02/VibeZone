"use client";

import { Music2 } from "lucide-react";
import Image from "next/image";
import { useState } from "react";
import { cn } from "@/lib/utils";

/**
 * Decorative artwork from /public (fixed aspect, cover-fit, lazy by default). Falls back to a gradient tile
 * if the image fails to load, so a card never shows a broken image.
 */
export function Artwork({
  src,
  sizes,
  priority = false,
  className,
}: {
  src: string;
  /** Rendered width hint, e.g. "(min-width: 1024px) 180px, 45vw". */
  sizes: string;
  priority?: boolean;
  className?: string;
}) {
  const [failed, setFailed] = useState(false);

  return (
    <div
      aria-hidden="true"
      className={cn("relative shrink-0 overflow-hidden rounded-lg bg-surface-hover", className)}
    >
      {failed ? (
        <div className="flex size-full items-center justify-center bg-linear-135 from-primary/40 to-surface-elevated">
          <Music2 className="size-2/5 text-white/70" />
        </div>
      ) : (
        <Image
          src={src}
          alt=""
          fill
          sizes={sizes}
          priority={priority}
          onError={() => setFailed(true)}
          className="object-cover"
        />
      )}
    </div>
  );
}
