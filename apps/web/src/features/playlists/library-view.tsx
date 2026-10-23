"use client";

import { ListMusic } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { useRouter } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { GRID } from "@/features/home/media-section";
import { useMyPlaylists } from "./api";
import { PlaylistCard } from "./playlist-card";
import { FollowingArtists } from "@/features/library/following-artists";
import { PlaylistForm } from "./playlist-form";

/** The signed-in user's playlists, with a form to create one (which then opens). */
export function LibraryView() {
  const t = useTranslations("library");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const playlists = useMyPlaylists(Boolean(me.data));
  const router = useRouter();

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <div className="flex min-w-0 flex-col gap-10">
          <section aria-label={t("title")} className="min-w-0">
            {!me.data || playlists.isPending ? (
              <div className={GRID} aria-busy="true" aria-label={tCommon("loading")}>
                {Array.from({ length: 4 }, (_, i) => (
                  <Skeleton key={i} className="aspect-[4/5] rounded-xl" />
                ))}
              </div>
            ) : playlists.isError ? (
              <ErrorState
                title={tErrors(errorCode(playlists.error))}
                onRetry={() => void playlists.refetch()}
                retryLabel={tCommon("retry")}
              />
            ) : playlists.data.length === 0 ? (
              <EmptyState icon={ListMusic} title={t("emptyTitle")} description={t("emptyBody")} />
            ) : (
              <ul className={GRID}>
                {playlists.data.map((playlist, i) => (
                  <li
                    key={playlist.id}
                    className="animate-in duration-500 fill-mode-both fade-in slide-in-from-bottom-2"
                    style={{ animationDelay: `${Math.min(i, 10) * 40}ms` }}
                  >
                    <PlaylistCard playlist={playlist} />
                  </li>
                ))}
              </ul>
            )}
          </section>
          <FollowingArtists enabled={Boolean(me.data)} />
        </div>
        <aside className="h-fit animate-in rounded-2xl border bg-surface p-5 duration-500 fade-in slide-in-from-bottom-2 lg:sticky lg:top-[calc(var(--topbar-height)+1rem)]">
          <h2 className="mb-4 text-lg font-semibold">{t("newTitle")}</h2>
          {me.data ? <PlaylistForm onDone={(created) => router.push(`/playlists/${created.id}`)} /> : null}
        </aside>
      </div>
    </div>
  );
}
