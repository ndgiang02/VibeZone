"use client";

import { Music } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { useNewReleases } from "./api";
import { TrackList } from "./track-list";

/** Newest public, ready tracks — a real chronological list, not a recommendation. */
export function NewReleases() {
  const t = useTranslations("home.newReleases");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const releases = useNewReleases(12);

  return (
    <section aria-labelledby="new-releases" className="flex flex-col gap-4">
      <h2 id="new-releases" className="text-title font-semibold">
        {t("title")}
      </h2>
      {releases.isPending ? (
        <div className="flex flex-col gap-2" aria-busy="true" aria-label={tCommon("loading")}>
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="h-16" />
          ))}
        </div>
      ) : releases.isError ? (
        <ErrorState
          title={t("errorTitle")}
          description={tErrors(errorCode(releases.error))}
          onRetry={() => void releases.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : releases.data.length === 0 ? (
        <EmptyState icon={Music} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <TrackList tracks={releases.data} />
      )}
    </section>
  );
}
