"use client";

import { ListPlus } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, isNotFound, useTrack } from "./api";
import { PlayButton } from "./play-button";
import { StatusBadge } from "./status-badge";
import { TrackArt } from "./track-art";
import { Waveform } from "./waveform";

export function TrackView({ trackId }: { trackId: string }) {
  const t = useTranslations("track");
  const tPlayer = useTranslations("player");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const format = useFormatter();
  const track = useTrack(trackId);
  const enqueue = usePlayerStore((s) => s.enqueue);

  if (track.isPending) {
    return (
      <div className="flex flex-col gap-6" aria-busy="true" aria-label={tCommon("loading")}>
        <div className="flex gap-6">
          <Skeleton className="size-40 rounded-2xl md:size-56" />
          <div className="flex flex-1 flex-col gap-3">
            <Skeleton className="h-8 w-2/3" />
            <Skeleton className="h-5 w-1/3" />
          </div>
        </div>
        <Skeleton className="h-24" />
      </div>
    );
  }

  if (track.isError) {
    return isNotFound(track.error) ? (
      <ErrorState title={t("notFoundTitle")} description={t("notFoundBody")} />
    ) : (
      <ErrorState
        title={tErrors(errorCode(track.error))}
        onRetry={() => void track.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  const data = track.data;
  const duration = durationMsOf(data);
  return (
    <article className="flex flex-col gap-8">
      <header className="flex flex-col gap-6 sm:flex-row sm:items-end">
        <TrackArt id={data.id} className="size-40 rounded-2xl shadow-elevated md:size-56" />
        <div className="flex min-w-0 flex-col gap-2">
          <h1 className="text-3xl font-semibold tracking-tight break-words md:text-4xl">{data.title}</h1>
          <Link
            href={`/artists/${data.artist.slug}`}
            className="text-lg text-muted-foreground hover:text-foreground"
          >
            {data.artist.name}
          </Link>
          <dl className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
            {data.genre ? (
              <div>
                <dt className="sr-only">{t("genre")}</dt>
                <dd>{data.genre.name}</dd>
              </div>
            ) : null}
            {duration ? (
              <div>
                <dt className="sr-only">{t("duration")}</dt>
                <dd className="tabular-nums">{formatTime(duration / 1000)}</dd>
              </div>
            ) : null}
            {data.publishedAt ? (
              <div>
                <dt className="sr-only">{t("published")}</dt>
                <dd>{format.dateTime(new Date(data.publishedAt), { dateStyle: "medium" })}</dd>
              </div>
            ) : null}
          </dl>
          {data.isOwner ? <StatusBadge track={data} /> : null}
          <div className="mt-2 flex items-center gap-2">
            <PlayButton track={data} size="icon-lg" className="size-12" />
            {data.playable ? (
              <Button variant="outline" onClick={() => enqueue(toPlayerTrack(data))}>
                <ListPlus className="size-4" aria-hidden="true" />
                {tPlayer("addToQueueShort")}
              </Button>
            ) : null}
          </div>
        </div>
      </header>

      {data.playable ? (
        <section aria-label={t("waveformSection")} className="rounded-2xl border bg-surface p-4">
          <Waveform track={data} />
        </section>
      ) : (
        <p className="rounded-2xl border border-dashed p-6 text-sm text-muted-foreground">
          {t("notPlayable")}
        </p>
      )}

      {data.description ? (
        <section aria-label={t("about")}>
          <h2 className="mb-2 font-semibold">{t("about")}</h2>
          {/* Plain text: rendered as text, never as HTML. */}
          <p className="whitespace-pre-line text-muted-foreground">{data.description}</p>
        </section>
      ) : null}
    </article>
  );
}
