import { useTranslations } from "next-intl";
import { cn } from "@/lib/utils";

/** Marks showcase (sample) content so it is never mistaken for real data. */
export function DemoBadge({ className }: { className?: string }) {
  const t = useTranslations("home.demo");
  return (
    <span
      title={t("note")}
      className={cn(
        "inline-flex items-center rounded-full border border-primary-border bg-primary-subtle px-2 py-px text-[0.625rem] font-semibold tracking-wide text-primary uppercase normal-nums",
        className,
      )}
    >
      {t("badge")}
    </span>
  );
}
