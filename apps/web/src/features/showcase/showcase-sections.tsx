"use client";

import { useTranslations } from "next-intl";
import { MediaSection } from "@/features/home/media-section";
import { demoMostPlayed, demoRecommended } from "./data";
import { DemoBadge } from "./demo-badge";
import { DemoCard } from "./demo-card";
import { showcaseEnabled } from "./showcase";

/**
 * "Most played" and "Recommended for you" need listening history (Analytics), which doesn't exist yet.
 * They render only in showcase mode, badged as sample content.
 */
export function ShowcaseSections() {
  const t = useTranslations("home");
  if (!showcaseEnabled) return null;

  return (
    <>
      <MediaSection
        title={t("mostPlayed")}
        badge={<DemoBadge />}
        items={demoMostPlayed}
        getKey={(item) => item.id}
        renderItem={(item) => <DemoCard item={item} />}
      />
      <MediaSection
        title={t("recommended")}
        badge={<DemoBadge />}
        items={demoRecommended}
        getKey={(item) => item.id}
        renderItem={(item) => <DemoCard item={item} />}
      />
    </>
  );
}
