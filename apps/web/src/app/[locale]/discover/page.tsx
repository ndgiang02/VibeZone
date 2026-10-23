import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { DiscoverView } from "@/features/discover/discover-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("discover");
  return { title: t("title") };
}

export default function DiscoverPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <DiscoverView />
    </div>
  );
}
