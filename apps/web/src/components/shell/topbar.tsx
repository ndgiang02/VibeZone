"use client";

import { ChevronLeft, ChevronRight } from "lucide-react";
import { useTranslations } from "next-intl";
import { Suspense, type ReactNode } from "react";
import { Logo } from "@/components/brand/logo";
import { Link, useRouter } from "@/i18n/navigation";
import { AccountControls } from "./account-controls";
import { LanguageSwitcher } from "./language-switcher";
import { SearchBox } from "./search-box";

/** Sticky top bar: history buttons (≥ md), search, language and account. On phones search wraps below. */
export function Topbar() {
  const t = useTranslations();
  const router = useRouter();

  return (
    <header className="sticky top-0 z-30 border-b bg-background/85 backdrop-blur-md md:border-b-0">
      <div className="flex h-(--topbar-height) items-center gap-2 px-4 md:gap-3 md:px-6">
        <Link href="/" aria-label={t("common.homeLink")} className="rounded-lg md:hidden">
          <Logo />
        </Link>

        <div className="hidden items-center gap-1.5 md:flex">
          <HistoryButton label={t("nav.back")} onClick={() => router.back()}>
            <ChevronLeft className="size-5" aria-hidden="true" />
          </HistoryButton>
          <HistoryButton label={t("nav.forward")} onClick={() => router.forward()}>
            <ChevronRight className="size-5" aria-hidden="true" />
          </HistoryButton>
        </div>

        <Suspense fallback={<div className="hidden max-w-xl flex-1 md:block" />}>
          <SearchBox className="hidden max-w-xl flex-1 md:block" />
        </Suspense>

        <div className="ml-auto flex items-center gap-1 md:gap-2">
          <LanguageSwitcher compact />
          <AccountControls variant="topbar" />
        </div>
      </div>
      <div className="px-4 pb-3 md:hidden">
        <Suspense>
          <SearchBox />
        </Suspense>
      </div>
    </header>
  );
}

function HistoryButton({
  label,
  onClick,
  children,
}: {
  label: string;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      title={label}
      onClick={onClick}
      className="flex size-9 items-center justify-center rounded-full bg-surface-elevated text-muted-foreground transition-colors duration-200 hover:bg-surface-hover hover:text-foreground"
    >
      {children}
    </button>
  );
}
