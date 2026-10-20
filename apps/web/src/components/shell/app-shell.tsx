import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { PlayerBar } from "@/features/player/player-bar";
import { PlayerEngine } from "@/features/player/player-engine";
import { NavLinks } from "./nav-links";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";

/**
 * Persistent chrome around every page: sidebar (icon rail on tablets, full from lg), top bar with
 * search, bottom nav (< md) and the single global audio player. Lives in the root layout, so it — and
 * playback — survives client-side navigation.
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

      <Sidebar />

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar />

        <main
          id="main"
          tabIndex={-1}
          className="flex-1 px-4 pt-4 pb-24 outline-none md:px-6 md:pt-2 md:pb-10"
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
