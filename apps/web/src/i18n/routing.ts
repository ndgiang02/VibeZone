import { defineRouting } from "next-intl/routing";

/** Keep in sync with VibeZone.SharedKernel.Locales (API emails and profile language). */
export const routing = defineRouting({
  locales: ["vi", "en"],
  defaultLocale: "vi",
  localePrefix: "always",
});

export type AppLocale = (typeof routing.locales)[number];
