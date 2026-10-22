import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { ModerationView } from "@/features/moderation/moderation-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("moderation");
  return { title: t("title") };
}

export default function ModerationPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <ModerationView />
    </div>
  );
}
