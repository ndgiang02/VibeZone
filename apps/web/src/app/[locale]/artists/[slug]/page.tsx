import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { ArtistView } from "@/features/tracks/artist-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("artist");
  return { title: t("label") };
}

export default async function ArtistPage({ params }: PageProps<"/[locale]/artists/[slug]">) {
  const { slug } = await params;
  return (
    <div className="mx-auto max-w-7xl">
      <ArtistView slug={slug} />
    </div>
  );
}
