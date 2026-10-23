"use client";

import { Heart } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { useEngagement, useLikedIds, useToggleLike } from "./api";

/**
 * Heart for one track. "icon": compact, state from the user's liked-ids set (track rows). "button": pill
 * with the like count (track page). Signed-out users are sent to sign in. Only playable tracks can be liked.
 */
export function LikeButton({
  track,
  variant = "icon",
  className,
}: {
  track: { id: string; title: string; playable: boolean };
  variant?: "icon" | "button";
  className?: string;
}) {
  const t = useTranslations("social");
  const format = useFormatter();
  const me = useMe();
  const router = useRouter();
  const signedIn = Boolean(me.data);
  const likedIds = useLikedIds(signedIn);
  const engagement = useEngagement(track.id, variant === "button");
  const toggle = useToggleLike(track.id);
  // The page variant always fetches counts; rows only need the id set.
  const liked =
    variant === "button" && engagement.data ? engagement.data.liked : (likedIds.data?.has(track.id) ?? false);

  if (!track.playable && !liked) return null;

  const onClick = () => {
    if (!signedIn) {
      router.push("/login");
      return;
    }
    toggle.mutate(!liked);
  };

  const label = liked ? t("unlike", { title: track.title }) : t("like", { title: track.title });

  if (variant === "button") {
    const count = engagement.data ? Number(engagement.data.likeCount) : null;
    return (
      <button
        type="button"
        aria-pressed={liked}
        aria-label={label}
        title={signedIn ? label : t("signInToLike")}
        onClick={onClick}
        disabled={toggle.isPending}
        className={cn(
          "inline-flex h-10 items-center gap-2 rounded-full border px-4 text-sm font-medium transition-colors",
          liked ? "border-primary-border bg-primary-subtle text-primary" : "hover:bg-surface-hover",
          className,
        )}
      >
        <Heart className={cn("size-4", liked && "fill-current")} aria-hidden="true" />
        <span className="tabular-nums">{count == null ? t("likeShort") : format.number(count)}</span>
      </button>
    );
  }

  return (
    <button
      type="button"
      aria-pressed={liked}
      aria-label={label}
      title={signedIn ? label : t("signInToLike")}
      onClick={onClick}
      disabled={toggle.isPending}
      className={cn(
        "flex size-8 items-center justify-center rounded-full transition-colors hover:bg-surface-hover",
        liked ? "text-primary" : "text-muted-foreground hover:text-foreground",
        className,
      )}
    >
      <Heart className={cn("size-4", liked && "fill-current")} aria-hidden="true" />
    </button>
  );
}
