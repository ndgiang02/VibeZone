import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { ChartsView } from "@/features/library/charts-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("charts");
  return { title: t("title") };
}

export default function ChartsPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <ChartsView />
    </div>
  );
}
