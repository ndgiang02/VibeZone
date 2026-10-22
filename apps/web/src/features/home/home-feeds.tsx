"use client";

import { ChartNoAxesColumn, History } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { useMe } from "@/features/auth/api";
import { useHistory } from "@/features/social/api";
import { useTrending } from "@/features/tracks/api";
import { TrackCard } from "@/features/tracks/track-card";
import { CardGridSkeleton, MediaSection } from "./media-section";

const HOME_SIZE = 12;

/** Trending this week (valid listens and likes) — a chart, not a personal recommendation. */
export function TrendingSection() {
  const t = useTranslations("home.trending");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const trending = useTrending(HOME_SIZE);
  const items = trending.data ?? [];

  return (
    <MediaSection
      title={t("title")}
      note={t("note")}
      busy={trending.isPending}
      items={items}
      getKey={(track) => track.id}
      renderItem={(track) => <TrackCard track={track} context={items} />}
      fallback={
        trending.isPending ? (
          <CardGridSkeleton label={tCommon("loading")} />
        ) : trending.isError ? (
          <ErrorState
            title={t("errorTitle")}
            description={tErrors(errorCode(trending.error))}
            onRetry={() => void trending.refetch()}
            retryLabel={tCommon("retry")}
          />
        ) : items.length === 0 ? (
          <EmptyState icon={ChartNoAxesColumn} title={t("emptyTitle")} description={t("emptyBody")} />
        ) : undefined
      }
    />
  );
}

/** The signed-in user's recently played tracks; nothing for visitors. */
export function RecentlyPlayedSection() {
  const t = useTranslations("home.recent");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useMe();
  const history = useHistory(Boolean(me.data), HOME_SIZE);
  if (!me.data) return null;
  const items = history.data?.pages[0]?.items.map((entry) => entry.track) ?? [];

  return (
    <MediaSection
      title={t("title")}
      busy={history.isPending}
      items={items}
      getKey={(track) => track.id}
      renderItem={(track) => <TrackCard track={track} context={items} />}
      fallback={
        history.isPending ? (
          <CardGridSkeleton label={tCommon("loading")} />
        ) : history.isError ? (
          <ErrorState
            title={tErrors(errorCode(history.error))}
            onRetry={() => void history.refetch()}
            retryLabel={tCommon("retry")}
          />
        ) : items.length === 0 ? (
          <EmptyState icon={History} title={t("emptyTitle")} description={t("emptyBody")} />
        ) : undefined
      }
    />
  );
}
