import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AlbumView } from "@/features/albums/album-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("album");
  return { title: t("label") };
}

export default async function AlbumPage({ params }: PageProps<"/[locale]/albums/[albumId]">) {
  const { albumId } = await params;
  return (
    <div className="mx-auto max-w-7xl">
      <AlbumView albumId={albumId} />
    </div>
  );
}
