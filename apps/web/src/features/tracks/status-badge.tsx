"use client";

import { AlertTriangle, CheckCircle2, CircleDashed, EyeOff, Link2, Loader2, XCircle } from "lucide-react";
import { useTranslations } from "next-intl";
import { cn } from "@/lib/utils";
import { isErrorCode } from "@/lib/api/errors";
import type { Track } from "./api";

/** Owner-only: processing status and visibility. */
export function StatusBadge({ track }: { track: Track }) {
  const t = useTranslations("track");
  const tErrors = useTranslations("errors");

  const status = {
    Draft: { icon: CircleDashed, tone: "text-muted-foreground", spin: false },
    Processing: { icon: Loader2, tone: "text-warning", spin: true },
    Ready: { icon: CheckCircle2, tone: "text-success", spin: false },
    Failed: { icon: AlertTriangle, tone: "text-destructive", spin: false },
    Cancelled: { icon: XCircle, tone: "text-muted-foreground", spin: false },
  }[track.status];
  const Icon = status.icon;
  const VisibilityIcon =
    track.visibility === "Private" ? EyeOff : track.visibility === "Unlisted" ? Link2 : null;

  return (
    <div className="flex flex-col gap-1 text-sm">
      <div className="flex flex-wrap items-center gap-3">
        <span className={cn("inline-flex items-center gap-1.5 font-medium", status.tone)}>
          <Icon className={cn("size-4", status.spin && "animate-spin")} aria-hidden="true" />
          {t(`status.${track.status}`)}
        </span>
        <span className="inline-flex items-center gap-1.5 text-muted-foreground">
          {VisibilityIcon ? <VisibilityIcon className="size-4" aria-hidden="true" /> : null}
          {t(`visibility.${track.visibility}`)}
        </span>
      </div>
      {track.status === "Failed" && track.processingError ? (
        <p className="text-destructive">
          {isErrorCode(track.processingError) ? tErrors(track.processingError) : tErrors("unknown")}
        </p>
      ) : null}
    </div>
  );
}
