"use client";

import {
  AlertTriangle,
  CheckCircle2,
  Flag,
  Heart,
  MessageSquare,
  ShieldAlert,
  UserPlus,
  type LucideIcon,
} from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { Link } from "@/i18n/navigation";
import { isErrorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import type { Notification } from "./api";

const ICONS: Record<Notification["type"], LucideIcon> = {
  TrackLiked: Heart,
  ArtistFollowed: UserPlus,
  CommentPosted: MessageSquare,
  CommentReplied: MessageSquare,
  TrackReady: CheckCircle2,
  TrackFailed: AlertTriangle,
  ReportResolved: Flag,
  ContentRemoved: ShieldAlert,
};

/** Where a notification leads: the track it is about, else the artist page. */
export function notificationHref(n: Notification): string | null {
  if (n.trackId) return `/tracks/${n.trackId}`;
  if (n.artistSlug) return `/artists/${n.artistSlug}`;
  return null;
}

/** One notification as a sentence (all text from messages; titles and excerpts rendered as text). */
export function NotificationItem({
  notification: n,
  onOpen,
  compact = false,
}: {
  notification: Notification;
  onOpen?: (n: Notification) => void;
  compact?: boolean;
}) {
  const t = useTranslations("notifications");
  const tErrors = useTranslations("errors");
  const format = useFormatter();
  const Icon = ICONS[n.type];
  const actor = n.actor?.displayName || n.actor?.handle || t("someone");
  const title = n.title ?? "";
  const code = n.code ?? "";

  const text = (() => {
    switch (n.type) {
      case "TrackLiked":
        return t("text.trackLiked", { actor, title });
      case "ArtistFollowed":
        return t("text.artistFollowed", { actor });
      case "CommentPosted":
        return t("text.commentPosted", { actor, title });
      case "CommentReplied":
        return t("text.commentReplied", { actor, title });
      case "TrackReady":
        return t("text.trackReady", { title });
      case "TrackFailed":
        return t("text.trackFailed", {
          title,
          reason: isErrorCode(code) ? tErrors(code) : tErrors("processing_error"),
        });
      case "ReportResolved":
        return code === "actioned"
          ? t("text.reportActioned", { title })
          : t("text.reportRejected", { title });
      case "ContentRemoved":
        return t("text.contentRemoved", {
          subject: t(
            `subjects.${n.subjectType === "comment" ? "comment" : n.subjectType === "playlist" ? "playlist" : "track"}`,
          ),
          title,
          reason: t(
            `reasons.${["copyright", "abuse", "spam"].includes(code) ? (code as "copyright" | "abuse" | "spam") : "other"}`,
          ),
        });
    }
  })();

  const href = notificationHref(n);
  const unread = !n.readAt;
  const content = (
    <>
      <span className="relative shrink-0">
        {n.actor ? (
          <ArtistAvatar
            id={n.actor.id}
            name={actor}
            src={n.actor.avatarUrl}
            className="size-9 text-[0.625rem]"
          />
        ) : (
          <span className="flex size-9 items-center justify-center rounded-full bg-surface-elevated">
            <Icon className="size-4 text-muted-foreground" aria-hidden="true" />
          </span>
        )}
        {n.actor ? (
          <span className="absolute -right-1 -bottom-1 flex size-5 items-center justify-center rounded-full bg-surface-card">
            <Icon className="size-3 text-primary" aria-hidden="true" />
          </span>
        ) : null}
      </span>
      <span className="min-w-0 flex-1">
        <span className={cn("block text-sm", compact && "line-clamp-2")}>{text}</span>
        {n.excerpt && !compact ? (
          <span className="mt-1 block text-sm break-words whitespace-pre-line text-muted-foreground">
            “{n.excerpt}”
          </span>
        ) : null}
        <time dateTime={n.createdAt} className="mt-0.5 block text-xs text-subtle-foreground">
          {format.relativeTime(new Date(n.createdAt))}
        </time>
      </span>
      {unread ? (
        <span className="mt-1.5 size-2 shrink-0 rounded-full bg-primary" aria-label={t("unread")} />
      ) : null}
    </>
  );

  const className = cn(
    "flex w-full items-start gap-3 rounded-xl p-3 text-left transition-colors hover:bg-surface-hover",
    unread && "bg-primary-subtle/40",
  );

  return href ? (
    <Link href={href} className={className} onClick={() => onOpen?.(n)}>
      {content}
    </Link>
  ) : (
    <button type="button" className={className} onClick={() => onOpen?.(n)}>
      {content}
    </button>
  );
}
