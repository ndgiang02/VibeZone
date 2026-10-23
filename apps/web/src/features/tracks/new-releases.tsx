"use client";

import { Music } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { CardGridSkeleton, MediaSection } from "@/features/home/media-section";
import { demoHotSongs } from "@/features/showcase/data";
import { DemoBadge } from "@/features/showcase/demo-badge";
import { DemoCard } from "@/features/showcase/demo-card";
import { showcaseEnabled } from "@/features/showcase/showcase";
import { useNewReleases } from "./api";
import { TrackCard } from "./track-card";

/** How many new releases the home page loads (shared with the banner and the artist panel: one request). */
export const HOME_RELEASES = 12;

/**
 * Newest public, ready tracks — a real chronological list, not a recommendation. Only when the catalog is
 * empty (never on error) does the showcase fill the slot with badged sample songs.
 */
export function NewReleases() {
  const t = useTranslations("home");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const releases = useNewReleases(HOME_RELEASES);

  if (releases.isSuccess && releases.data.length === 0 && showcaseEnabled) {
    return (
      <MediaSection
        title={t("hotSongs")}
        badge={<DemoBadge />}
        note={t("demo.note")}
        items={demoHotSongs}
        getKey={(item) => item.id}
        renderItem={(item) => <DemoCard item={item} />}
      />
    );
  }

  return (
    <MediaSection
      title={t("newReleases.title")}
      busy={releases.isPending}
      items={releases.data ?? []}
      getKey={(track) => track.id}
      renderItem={(track) => <TrackCard track={track} context={releases.data ?? []} />}
      fallback={
        releases.isPending ? (
          <CardGridSkeleton label={tCommon("loading")} />
        ) : releases.isError ? (
          <ErrorState
            title={t("newReleases.errorTitle")}
            description={tErrors(errorCode(releases.error))}
            onRetry={() => void releases.refetch()}
            retryLabel={tCommon("retry")}
          />
        ) : releases.data.length === 0 ? (
          <EmptyState
            icon={Music}
            title={t("newReleases.emptyTitle")}
            description={t("newReleases.emptyBody")}
          />
        ) : undefined
      }
    />
  );
}
