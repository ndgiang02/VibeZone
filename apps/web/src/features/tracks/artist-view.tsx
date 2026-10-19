"use client";

import { Music } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { isNotFound, useArtistProfile } from "./api";
import { TrackArt } from "./track-art";
import { TrackList } from "./track-list";

export function ArtistView({ slug }: { slug: string }) {
  const t = useTranslations("artist");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const profile = useArtistProfile(slug);

  if (profile.isPending) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-40 rounded-2xl" />
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  if (profile.isError) {
    return isNotFound(profile.error) ? (
      <ErrorState title={t("notFoundTitle")} description={t("notFoundBody")} />
    ) : (
      <ErrorState
        title={tErrors(errorCode(profile.error))}
        onRetry={() => void profile.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  const { artist, tracks } = profile.data;
  return (
    <div className="flex flex-col gap-8">
      <header className="flex flex-col gap-5 rounded-2xl border bg-surface p-6 sm:flex-row sm:items-center md:p-8">
        <TrackArt id={artist.id} className="size-28 rounded-full md:size-36" />
        <div className="min-w-0">
          <p className="text-sm font-medium tracking-wide text-primary uppercase">{t("label")}</p>
          <h1 className="text-3xl font-semibold tracking-tight break-words md:text-4xl">{artist.name}</h1>
          {artist.bio ? (
            <p className="mt-2 max-w-2xl whitespace-pre-line text-muted-foreground">{artist.bio}</p>
          ) : null}
        </div>
      </header>
      <section aria-labelledby="artist-tracks" className="flex flex-col gap-3">
        <h2 id="artist-tracks" className="text-title font-semibold">
          {t("tracks")}
        </h2>
        {tracks.length === 0 ? (
          <EmptyState icon={Music} title={t("noTracksTitle")} description={t("noTracksBody")} />
        ) : (
          <TrackList tracks={tracks} showArtist={false} />
        )}
      </section>
    </div>
  );
}
