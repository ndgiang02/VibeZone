"use client";

import { Music, Trash2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { useCancelUpload, useDeleteTrack, useMyArtist, useMyTracks, type Track } from "@/features/tracks/api";
import { PlayButton } from "@/features/tracks/play-button";
import { StatusBadge } from "@/features/tracks/status-badge";
import { TrackArt } from "@/features/tracks/track-art";
import { BecomeArtistForm } from "./become-artist-form";
import { NewTrackForm } from "./new-track-form";
import { TrackUploader } from "./track-uploader";

export function StudioView() {
  const t = useTranslations("studio");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const artist = useMyArtist(Boolean(me.data));

  if (me.isError || artist.isError) {
    const error = me.error ?? artist.error;
    return (
      <ErrorState
        title={tErrors(errorCode(error))}
        onRetry={() => void (me.isError ? me.refetch() : artist.refetch())}
        retryLabel={tCommon("retry")}
      />
    );
  }

  if (!me.data || artist.isPending) {
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-48 rounded-2xl" />
        <Skeleton className="h-24 rounded-2xl" />
      </div>
    );
  }

  if (!artist.data) {
    return <BecomeArtistForm />;
  }

  return (
    <div className="flex flex-col gap-6">
      <p className="text-sm text-muted-foreground">
        {t("signedInAs")}{" "}
        <Link href={`/artists/${artist.data.slug}`} className="font-medium text-foreground hover:underline">
          {artist.data.name}
        </Link>
      </p>
      <Workspace />
    </div>
  );
}

function Workspace() {
  const t = useTranslations("studio");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const [highlight, setHighlight] = useState<string | null>(null);
  const tracks = useMyTracks(true);

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,2fr)_minmax(0,3fr)]">
      <section aria-labelledby="new-track" className="h-fit rounded-2xl border bg-surface p-5">
        <h2 id="new-track" className="mb-4 text-lg font-semibold">
          {t("newTrack.heading")}
        </h2>
        <NewTrackForm onCreated={setHighlight} />
      </section>
      <section aria-labelledby="my-tracks" className="flex flex-col gap-3">
        <h2 id="my-tracks" className="text-lg font-semibold">
          {t("myTracks")}
        </h2>
        {tracks.isPending ? (
          <Skeleton className="h-32 rounded-2xl" />
        ) : tracks.isError ? (
          <ErrorState
            title={tErrors(errorCode(tracks.error))}
            onRetry={() => void tracks.refetch()}
            retryLabel={tCommon("retry")}
          />
        ) : tracks.data.length === 0 ? (
          <EmptyState icon={Music} title={t("emptyTitle")} description={t("emptyBody")} />
        ) : (
          <ul className="flex flex-col gap-3">
            {tracks.data.map((track) => (
              <StudioTrack key={track.id} track={track} highlighted={track.id === highlight} />
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

function StudioTrack({ track, highlighted }: { track: Track; highlighted: boolean }) {
  const t = useTranslations("studio");
  const tErrors = useTranslations("errors");
  const cancel = useCancelUpload();
  const remove = useDeleteTrack();
  const uploadable = track.status === "Draft" || track.status === "Failed" || track.status === "Cancelled";

  return (
    <li
      className={cn(
        "flex flex-col gap-3 rounded-2xl border bg-surface p-4",
        highlighted && "border-primary/60",
      )}
    >
      <div className="flex items-start gap-3">
        <TrackArt id={track.id} className="size-12" />
        <div className="min-w-0 flex-1">
          <Link href={`/tracks/${track.id}`} className="block truncate font-medium hover:underline">
            {track.title}
          </Link>
          <StatusBadge track={track} />
        </div>
        <PlayButton track={track} size="icon-sm" />
        <Button
          variant="ghost"
          size="icon-sm"
          disabled={remove.isPending || track.status === "Processing"}
          onClick={() => {
            if (window.confirm(t("deleteConfirm", { title: track.title }))) remove.mutate(track.id);
          }}
          aria-label={t("delete", { title: track.title })}
        >
          <Trash2 className="size-4" aria-hidden="true" />
        </Button>
      </div>
      {track.status === "Processing" ? (
        <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-muted-foreground">
          <span aria-live="polite">{t("processing")}</span>
          <Button
            variant="outline"
            size="sm"
            disabled={cancel.isPending}
            onClick={() => cancel.mutate(track.id)}
          >
            {t("cancelProcessing")}
          </Button>
        </div>
      ) : null}
      {uploadable ? <TrackUploader trackId={track.id} /> : null}
      {cancel.isError || remove.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(cancel.error ?? remove.error))}
        </p>
      ) : null}
    </li>
  );
}
