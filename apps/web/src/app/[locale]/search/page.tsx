import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { Suspense } from "react";
import { SearchView } from "@/features/search/search-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("search");
  return { title: t("pageTitle") };
}

export default function SearchPage() {
  return (
    <div className="mx-auto max-w-7xl">
      {/* useSearchParams: keep the rest of the page renderable without the query. */}
      <Suspense>
        <SearchView />
      </Suspense>
    </div>
  );
}
