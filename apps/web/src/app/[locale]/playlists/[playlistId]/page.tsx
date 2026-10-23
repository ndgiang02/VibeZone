import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { PlaylistView } from "@/features/playlists/playlist-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("playlist");
  return { title: t("label") };
}

export default async function PlaylistPage({ params }: PageProps<"/[locale]/playlists/[playlistId]">) {
  const { playlistId } = await params;
  return (
    <div className="mx-auto max-w-7xl">
      <PlaylistView playlistId={playlistId} />
    </div>
  );
}
