"use client";

import { Check, UserPlus } from "lucide-react";
import { useTranslations } from "next-intl";
import { useRouter } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { useFollowSummary, useToggleFollow } from "./api";

/** Follow/unfollow an artist, with the follower count. Hidden on the viewer's own artist page. */
export function FollowButton({
  artist,
  isOwner,
  className,
}: {
  artist: { id: string; name: string };
  isOwner: boolean;
  className?: string;
}) {
  const t = useTranslations("social");
  const tErrors = useTranslations("errors");
  const me = useMe();
  const router = useRouter();
  const summary = useFollowSummary(artist.id);
  const toggle = useToggleFollow(artist.id);
  const following = summary.data?.following ?? false;
  const count = summary.data ? Number(summary.data.followerCount) : null;

  return (
    <div className={cn("flex flex-wrap items-center gap-3", className)}>
      {!isOwner ? (
        <button
          type="button"
          aria-pressed={following}
          disabled={toggle.isPending || summary.isPending}
          onClick={() => (me.data ? toggle.mutate(!following) : router.push("/login"))}
          className={cn(
            "inline-flex h-10 items-center gap-2 rounded-full border px-4 text-sm font-semibold transition-colors disabled:opacity-60",
            following
              ? "border-white/30 bg-black/20 text-white hover:bg-black/30"
              : "border-transparent bg-primary text-primary-foreground hover:bg-primary-hover",
          )}
        >
          {following ? (
            <Check className="size-4" aria-hidden="true" />
          ) : (
            <UserPlus className="size-4" aria-hidden="true" />
          )}
          {following ? t("following") : t("follow")}
        </button>
      ) : null}
      {count != null ? <span className="text-sm text-white/80">{t("followers", { count })}</span> : null}
      {toggle.isError ? (
        <span role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(toggle.error))}
        </span>
      ) : null}
    </div>
  );
}
