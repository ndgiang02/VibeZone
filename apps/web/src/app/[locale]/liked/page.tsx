import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { LikedView } from "@/features/library/liked-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("liked");
  return { title: t("title") };
}

export default function LikedPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <LikedView />
    </div>
  );
}
