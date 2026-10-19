import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { StudioView } from "@/features/studio/studio-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("studio");
  return { title: t("title"), robots: { index: false } };
}

export default async function StudioPage() {
  const t = await getTranslations("studio");
  return (
    <div className="mx-auto flex max-w-6xl flex-col gap-6">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>
      <StudioView />
    </div>
  );
}
