import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { TrackView } from "@/features/tracks/track-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("track");
  return { title: t("pageTitle") };
}

export default async function TrackPage({ params }: PageProps<"/[locale]/tracks/[trackId]">) {
  const { trackId } = await params;
  return (
    <div className="mx-auto max-w-5xl">
      <TrackView trackId={trackId} />
    </div>
  );
}
