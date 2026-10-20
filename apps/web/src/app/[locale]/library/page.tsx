import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { LibraryView } from "@/features/playlists/library-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("library");
  return { title: t("title") };
}

export default function LibraryPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <LibraryView />
    </div>
  );
}
