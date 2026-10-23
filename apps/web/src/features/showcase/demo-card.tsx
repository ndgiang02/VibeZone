import { useTranslations } from "next-intl";
import { Artwork } from "@/components/media/artwork";
import { cn } from "@/lib/utils";
import type { DemoItem } from "./data";

/** Showcase card: artwork + title + artist or kind. Deliberately not playable and not a link. */
export function DemoCard({ item, round = false }: { item: DemoItem; round?: boolean }) {
  const t = useTranslations("home.kinds");

  return (
    <article className="group flex flex-col gap-3 rounded-xl bg-surface-card p-3 transition-[background-color,transform] duration-200 hover:-translate-y-0.5 hover:bg-surface-elevated motion-reduce:hover:translate-y-0">
      <Artwork
        src={item.artwork}
        sizes="(min-width: 1536px) 200px, (min-width: 1024px) 180px, (min-width: 640px) 30vw, 45vw"
        className={cn("aspect-square w-full", round ? "rounded-full" : "rounded-lg")}
      />
      <div className="min-w-0">
        <h3 className="truncate text-sm font-semibold">{item.title}</h3>
        <p className="truncate text-xs text-muted-foreground">{item.artist ?? t(item.kind)}</p>
      </div>
    </article>
  );
}
