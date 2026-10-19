import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Logo } from "@/components/brand/logo";
import { Link } from "@/i18n/navigation";
import { PlayerBar } from "@/features/player/player-bar";
import { PlayerEngine } from "@/features/player/player-engine";
import { AccountControls } from "./account-controls";
import { LanguageSwitcher } from "./language-switcher";
import { NavLinks } from "./nav-links";

/**
 * Persistent chrome around every page: sidebar (≥ md), top bar (< md), bottom nav (< md)
 * and the single global audio player. Lives in the root layout, so it — and playback — survives
 * client-side navigation.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const t = useTranslations();

  return (
    <div className="flex min-h-dvh">
      <a
        href="#main"
        className="sr-only z-50 rounded-md bg-primary px-3 py-2 text-primary-foreground focus:not-sr-only focus:fixed focus:top-3 focus:left-3"
      >
        {t("common.skipToContent")}
      </a>

      <aside className="sticky top-0 hidden h-dvh w-(--sidebar-width) shrink-0 flex-col gap-8 border-r bg-surface px-4 py-5 md:flex">
        <Link href="/" aria-label={t("common.homeLink")} className="rounded-lg px-2">
          <Logo />
        </Link>
        <nav aria-label={t("nav.primary")}>
          <NavLinks variant="sidebar" />
        </nav>
        <div className="mt-auto flex flex-col gap-3">
          <LanguageSwitcher />
          <AccountControls variant="sidebar" />
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-30 flex h-(--topbar-height) items-center justify-between gap-2 border-b bg-background/80 px-4 backdrop-blur md:hidden">
          <Link href="/" aria-label={t("common.homeLink")}>
            <Logo />
          </Link>
          <div className="flex items-center gap-1">
            <LanguageSwitcher compact />
            <AccountControls variant="topbar" />
          </div>
        </header>

        <main
          id="main"
          tabIndex={-1}
          className="flex-1 px-4 pt-6 pb-24 outline-none md:px-8 md:pt-10 md:pb-10"
        >
          {children}
        </main>

        {/* The one playback engine for the whole app + its UI; both survive client-side navigation. */}
        <PlayerEngine />
        <PlayerBar />

        <nav
          aria-label={t("nav.primary")}
          className="fixed inset-x-0 bottom-0 z-30 h-16 border-t bg-surface/95 backdrop-blur md:hidden"
        >
          <NavLinks variant="bottom" />
        </nav>
      </div>
    </div>
  );
}
