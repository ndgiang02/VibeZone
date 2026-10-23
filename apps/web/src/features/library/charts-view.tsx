"use client";

import { ChartNoAxesColumn } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { useTrending } from "@/features/tracks/api";
import { TrackList } from "@/features/tracks/track-list";
import { ListSkeleton } from "./liked-view";

const CHART_SIZE = 50;

/** Trending chart: valid listens and likes over the last 7 days (recent days weigh more). */
export function ChartsView() {
  const t = useTranslations("charts");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const trending = useTrending(CHART_SIZE);

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>
      {trending.isPending ? (
        <ListSkeleton label={tCommon("loading")} />
      ) : trending.isError ? (
        <ErrorState
          title={tErrors(errorCode(trending.error))}
          onRetry={() => void trending.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : trending.data.length === 0 ? (
        <EmptyState icon={ChartNoAxesColumn} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <TrackList tracks={trending.data} />
      )}
    </div>
  );
}
