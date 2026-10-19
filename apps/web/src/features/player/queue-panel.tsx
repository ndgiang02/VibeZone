"use client";

import { X } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { TrackArt } from "@/features/tracks/track-art";
import { formatTime } from "./format";
import { usePlayerStore } from "./store";

export function QueuePanel({ onClose }: { onClose: () => void }) {
  const t = useTranslations("player");
  const queue = usePlayerStore((s) => s.queue);
  const index = usePlayerStore((s) => s.index);
  const jumpTo = usePlayerStore((s) => s.jumpTo);
  const removeAt = usePlayerStore((s) => s.removeAt);
  const clearQueue = usePlayerStore((s) => s.clearQueue);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    ref.current?.focus();
    const onKey = (event: KeyboardEvent) => event.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div
      ref={ref}
      role="dialog"
      aria-label={t("queue")}
      tabIndex={-1}
      className="fixed right-2 bottom-(--player-height) z-50 mb-2 flex max-h-[60dvh] w-[min(24rem,calc(100vw-1rem))] flex-col rounded-xl border bg-surface-elevated shadow-elevated outline-none max-md:bottom-[calc(var(--player-height)+4rem)]"
    >
      <div className="flex items-center justify-between border-b px-4 py-3">
        <h2 className="font-semibold">{t("queue")}</h2>
        <div className="flex items-center gap-1">
          {queue.length > 0 ? (
            <Button variant="ghost" size="sm" onClick={clearQueue}>
              {t("clearQueue")}
            </Button>
          ) : null}
          <Button variant="ghost" size="icon-sm" onClick={onClose} aria-label={t("closeQueue")}>
            <X className="size-4" aria-hidden="true" />
          </Button>
        </div>
      </div>
      {queue.length === 0 ? (
        <p className="p-6 text-center text-sm text-muted-foreground">{t("queueEmpty")}</p>
      ) : (
        <ol className="overflow-y-auto p-2">
          {queue.map((track, i) => (
            <li
              key={track.id}
              className={cn(
                "group flex items-center gap-3 rounded-lg p-2",
                i === index && "bg-surface-hover",
              )}
            >
              <button
                type="button"
                onClick={() => jumpTo(i)}
                className="flex min-w-0 flex-1 items-center gap-3 text-left"
                aria-current={i === index ? "true" : undefined}
              >
                <TrackArt id={track.id} className="size-9" />
                <span className="min-w-0">
                  <span className={cn("block truncate text-sm font-medium", i === index && "text-primary")}>
                    {track.title}
                  </span>
                  <span className="block truncate text-xs text-muted-foreground">{track.artistName}</span>
                </span>
              </button>
              <span className="text-xs text-subtle-foreground tabular-nums">
                {track.durationMs ? formatTime(track.durationMs / 1000) : ""}
              </span>
              <Button
                variant="ghost"
                size="icon-xs"
                onClick={() => removeAt(i)}
                aria-label={t("removeFromQueue", { title: track.title })}
              >
                <X className="size-3" aria-hidden="true" />
              </Button>
            </li>
          ))}
        </ol>
      )}
    </div>
  );
}
