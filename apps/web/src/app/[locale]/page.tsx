import { useTranslations } from "next-intl";
import { GenreBrowser } from "@/features/catalog/genre-browser";
import { NewReleases } from "@/features/tracks/new-releases";

export default function HomePage() {
  const t = useTranslations("home");

  return (
    <div className="mx-auto flex max-w-7xl flex-col gap-12">
      <section className="relative overflow-hidden rounded-2xl border bg-surface px-6 py-10 md:px-10 md:py-14">
        <div
          aria-hidden="true"
          className="pointer-events-none absolute -top-24 -right-24 size-80 rounded-full bg-primary/25 blur-3xl"
        />
        <p className="text-sm font-medium tracking-wide text-primary uppercase">{t("eyebrow")}</p>
        <h1 className="mt-3 max-w-2xl text-4xl font-semibold tracking-tight md:text-display">{t("title")}</h1>
        <p className="mt-4 max-w-xl text-muted-foreground">{t("subtitle")}</p>
      </section>

      <NewReleases />

      <GenreBrowser />
    </div>
  );
}
