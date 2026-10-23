"use client";

import { History } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { useHistory } from "@/features/social/api";
import { TrackList } from "@/features/tracks/track-list";
import { ListSkeleton } from "./liked-view";

/** Recently played tracks (one row per track, most recent first). */
export function HistoryView() {
  const t = useTranslations("history");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const history = useHistory(Boolean(me.data));
  const tracks = history.data?.pages.flatMap((p) => p.items.map((i) => i.track)) ?? [];

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>
      {!me.data || history.isPending ? (
        <ListSkeleton label={tCommon("loading")} />
      ) : history.isError ? (
        <ErrorState
          title={tErrors(errorCode(history.error))}
          onRetry={() => void history.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : tracks.length === 0 ? (
        <EmptyState icon={History} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <TrackList tracks={tracks} />
      )}
      {history.hasNextPage ? (
        <Button
          variant="outline"
          className="self-center"
          disabled={history.isFetchingNextPage}
          onClick={() => void history.fetchNextPage()}
        >
          {history.isFetchingNextPage ? tCommon("loading") : tCommon("showMore")}
        </Button>
      ) : null}
    </div>
  );
}
