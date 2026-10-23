"use client";

import { Users } from "lucide-react";
import { useTranslations } from "next-intl";
import { ArtistAvatar } from "@/components/media/artist-avatar";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { useFollowing } from "@/features/social/api";

/** Artists the user follows (library page). */
export function FollowingArtists({ enabled }: { enabled: boolean }) {
  const t = useTranslations("library.following");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const following = useFollowing(enabled);
  const artists = following.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <section aria-labelledby="library-following" className="flex flex-col gap-4">
      <h2 id="library-following" className="text-xl font-semibold tracking-tight">
        {t("title")}
      </h2>
      {!enabled || following.isPending ? (
        <div className="flex gap-4" aria-busy="true" aria-label={tCommon("loading")}>
          {Array.from({ length: 4 }, (_, i) => (
            <Skeleton key={i} className="size-24 rounded-full" />
          ))}
        </div>
      ) : following.isError ? (
        <ErrorState
          title={tErrors(errorCode(following.error))}
          onRetry={() => void following.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : artists.length === 0 ? (
        <EmptyState icon={Users} title={t("emptyTitle")} description={t("emptyBody")} className="py-8" />
      ) : (
        <ul className="grid grid-cols-3 gap-4 sm:grid-cols-4 lg:grid-cols-6">
          {artists.map((artist) => (
            <li key={artist.id}>
              <Link
                href={`/artists/${artist.slug}`}
                className="group flex flex-col items-center gap-2 rounded-xl p-2 text-center transition-colors hover:bg-surface-hover"
              >
                <ArtistAvatar
                  id={artist.id}
                  name={artist.name}
                  src={artist.avatarUrl}
                  className="size-20 text-xl transition-transform group-hover:scale-105"
                />
                <span className="w-full truncate text-sm font-medium">{artist.name}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
      {following.hasNextPage ? (
        <Button
          variant="outline"
          className="self-center"
          disabled={following.isFetchingNextPage}
          onClick={() => void following.fetchNextPage()}
        >
          {tCommon("showMore")}
        </Button>
      ) : null}
    </section>
  );
}
