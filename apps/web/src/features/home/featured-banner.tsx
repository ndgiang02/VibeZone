"use client";

import { Play } from "lucide-react";
import { useTranslations } from "next-intl";
import { Artwork } from "@/components/media/artwork";
import { formatTime, toPlayerTrack } from "@/features/player/format";
import { usePlayerStore } from "@/features/player/store";
import { durationMsOf, useNewReleases } from "@/features/tracks/api";
import { HOME_RELEASES } from "@/features/tracks/new-releases";

/** Decorative collage (illustrations, not covers of any track). */
const COLLAGE = ["/artwork/night-drive.svg", "/artwork/chill-sunset.svg", "/artwork/edm-stage.svg"];

/** Home hero: brand message plus a real "play all" over the newest playable releases. */
export function FeaturedBanner() {
  const t = useTranslations("home");
  const releases = useNewReleases(HOME_RELEASES);
  const playQueue = usePlayerStore((s) => s.playQueue);

  const playable = (releases.data ?? []).filter((track) => track.playable);
  const totalSeconds = playable.reduce((sum, track) => sum + (durationMsOf(track) ?? 0), 0) / 1000;

  return (
    <section className="relative isolate overflow-hidden rounded-3xl border border-primary-border/60 bg-[radial-gradient(120%_140%_at_0%_0%,#2f3d14_0%,#161c0a_45%,#0b0d07_100%)] px-6 py-8 md:min-h-72 md:px-10 md:py-10">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -top-24 right-10 -z-10 size-96 rounded-full bg-primary/25 blur-3xl"
      />
      <div
        aria-hidden="true"
        className="pointer-events-none absolute -bottom-32 left-1/3 -z-10 size-72 rounded-full bg-primary/10 blur-3xl"
      />

      <div className="flex items-center gap-8">
        <div className="flex max-w-xl min-w-0 flex-1 flex-col">
          <p className="text-xs font-semibold tracking-[0.2em] text-primary uppercase">{t("eyebrow")}</p>
          <h1 className="mt-3 text-3xl leading-tight font-bold tracking-tight text-balance md:text-4xl 2xl:text-5xl 2xl:leading-[1.05]">
            {t("title")}
          </h1>
          <p className="mt-3 max-w-md text-sm text-muted-foreground md:text-base">{t("subtitle")}</p>
          <div className="mt-6 flex flex-wrap items-center gap-4">
            {playable.length > 0 ? (
              <button
                type="button"
                onClick={() => playQueue(playable.map(toPlayerTrack))}
                className="inline-flex h-11 items-center gap-2 rounded-full bg-primary pr-6 pl-5 text-sm font-semibold text-primary-foreground shadow-[0_10px_30px_-8px_var(--accent-glow)] transition-[background-color,transform] duration-200 hover:scale-[1.03] hover:bg-primary-hover active:scale-95"
              >
                <Play className="size-4 fill-current" aria-hidden="true" />
                {t("banner.playAll")}
              </button>
            ) : null}
            <p className="text-sm text-muted-foreground tabular-nums">
              {releases.isSuccess && playable.length > 0
                ? t("banner.stats", { count: playable.length, duration: formatTime(totalSeconds) })
                : releases.isSuccess
                  ? t("banner.statsEmpty")
                  : null}
            </p>
          </div>
        </div>

        <div
          aria-hidden="true"
          className="relative hidden h-56 w-64 shrink-0 sm:block lg:w-72 xl:max-2xl:w-44"
        >
          <div className="absolute inset-6 rounded-full bg-primary/30 blur-2xl" />
          <Artwork
            src={COLLAGE[1]!}
            sizes="160px"
            className="absolute top-2 left-0 size-36 -rotate-8 rounded-2xl shadow-elevated lg:size-40 xl:max-2xl:hidden"
            priority
          />
          <Artwork
            src={COLLAGE[2]!}
            sizes="160px"
            className="absolute right-0 bottom-0 size-36 rotate-6 rounded-2xl shadow-elevated lg:size-40 xl:max-2xl:hidden"
            priority
          />
          <Artwork
            src={COLLAGE[0]!}
            sizes="176px"
            className="absolute top-8 left-14 size-40 rounded-2xl shadow-elevated ring-1 ring-white/10 lg:left-16 lg:size-44 xl:max-2xl:top-6 xl:max-2xl:left-0 xl:max-2xl:rotate-3"
            priority
          />
        </div>
      </div>
    </section>
  );
}
