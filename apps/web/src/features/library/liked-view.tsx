"use client";

import { Heart } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { useLikedTracks } from "@/features/social/api";
import { TrackList } from "@/features/tracks/track-list";

/** Tracks the user liked, newest like first. */
export function LikedView() {
  const t = useTranslations("liked");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const liked = useLikedTracks(Boolean(me.data));
  const tracks = liked.data?.pages.flatMap((p) => p.items.map((i) => i.track)) ?? [];

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>
      {!me.data || liked.isPending ? (
        <ListSkeleton label={tCommon("loading")} />
      ) : liked.isError ? (
        <ErrorState
          title={tErrors(errorCode(liked.error))}
          onRetry={() => void liked.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : tracks.length === 0 ? (
        <EmptyState icon={Heart} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <TrackList tracks={tracks} />
      )}
      {liked.hasNextPage ? (
        <Button
          variant="outline"
          className="self-center"
          disabled={liked.isFetchingNextPage}
          onClick={() => void liked.fetchNextPage()}
        >
          {liked.isFetchingNextPage ? tCommon("loading") : tCommon("showMore")}
        </Button>
      ) : null}
    </div>
  );
}

export function ListSkeleton({ label, rows = 6 }: { label: string; rows?: number }) {
  return (
    <div className="flex flex-col gap-2" aria-busy="true" aria-label={label}>
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} className="h-16" />
      ))}
    </div>
  );
}
