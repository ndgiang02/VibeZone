"use client";

import { GenreBrowser } from "@/features/catalog/genre-browser";
import { NowPlayingCard } from "@/features/player/now-playing-card";
import { ShowcaseSections } from "@/features/showcase/showcase-sections";
import { NewReleases } from "@/features/tracks/new-releases";
import { ArtistPanel } from "./artist-panel";
import { RecentlyPlayedSection, TrendingSection } from "./home-feeds";
import { FeaturedBanner } from "./featured-banner";

/**
 * Home: banner + sections in the centre; on desktop (xl) a side panel with artists and the now-playing
 * card. Search has its own page (/search).
 */
export function HomeView() {
  return (
    <div className="mx-auto flex max-w-[110rem] gap-6">
      <div className="flex min-w-0 flex-1 flex-col gap-10">
        <FeaturedBanner />
        <ArtistPanel variant="rail" className="xl:hidden" />
        <RecentlyPlayedSection />
        <TrendingSection />
        <NewReleases />
        <ShowcaseSections />
        <GenreBrowser />
      </div>

      <aside className="vz-thin-scrollbar sticky top-[calc(var(--topbar-height)+0.5rem)] hidden max-h-[calc(100dvh-var(--topbar-height)-var(--player-height)-1.5rem)] w-(--right-panel-width) shrink-0 flex-col gap-6 self-start overflow-y-auto xl:flex">
        <ArtistPanel variant="list" className="rounded-2xl border bg-surface-card p-5" />
        <NowPlayingCard />
      </aside>
    </div>
  );
}
