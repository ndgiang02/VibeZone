import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { HistoryView } from "@/features/library/history-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("history");
  return { title: t("title") };
}

export default function HistoryPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <HistoryView />
    </div>
  );
}
