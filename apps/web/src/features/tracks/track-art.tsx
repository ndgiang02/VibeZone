import { Music2 } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * Placeholder artwork until artwork upload exists: a deterministic gradient per track id (decorative,
 * clearly not a cover image), never a stock or fake cover.
 */
export function TrackArt({ id, className }: { id: string; className?: string }) {
  let hash = 0;
  for (const char of id) hash = (hash * 31 + char.charCodeAt(0)) >>> 0;
  const hue = hash % 360;
  return (
    <div
      aria-hidden="true"
      className={cn("flex shrink-0 items-center justify-center overflow-hidden rounded-lg", className)}
      style={{
        background: `linear-gradient(135deg, oklch(0.6 0.16 ${hue}), oklch(0.3 0.08 ${(hue + 50) % 360}))`,
      }}
    >
      <Music2 className="size-2/5 text-white/70" />
    </div>
  );
}
