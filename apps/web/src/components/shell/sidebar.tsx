import type { LucideIcon } from "lucide-react";
import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { Logo, LogoMark } from "@/components/brand/logo";
import { Artwork } from "@/components/media/artwork";
import { Link } from "@/i18n/navigation";
import { demoPlaylists } from "@/features/showcase/data";
import { DemoBadge } from "@/features/showcase/demo-badge";
import { showcaseEnabled } from "@/features/showcase/showcase";
import { showUpcoming, upcomingLibrary, upcomingMenu, type UpcomingKey } from "./nav-items";
import { NavLinks } from "./nav-links";
import { SidebarPlaylists } from "./sidebar-playlists";
import { StaffLinks } from "./staff-links";

/** Left navigation (≥ md). Tablets get an icon rail with the real destinations only; lg+ the full sidebar. */
export function Sidebar() {
  const t = useTranslations();

  return (
    <aside className="sticky top-0 hidden h-dvh w-(--sidebar-width) shrink-0 flex-col border-r bg-surface md:flex">
      <Link
        href="/"
        aria-label={t("common.homeLink")}
        className="mx-3 mt-5 mb-6 flex h-10 items-center rounded-lg max-lg:justify-center lg:px-2"
      >
        <Logo className="max-lg:hidden" />
        <LogoMark className="lg:hidden" />
      </Link>

      <div className="vz-thin-scrollbar flex min-h-0 flex-1 flex-col gap-7 overflow-y-auto px-3 pb-6">
        <SidebarSection title={t("nav.menu")}>
          <nav aria-label={t("nav.primary")} className="flex flex-col gap-1">
            <NavLinks variant="sidebar" />
            <NavLinks variant="sidebar" list="menu" />
            {showUpcoming ? <UpcomingList items={upcomingMenu} /> : null}
          </nav>
        </SidebarSection>

        <SidebarSection title={t("nav.library")}>
          <nav aria-label={t("nav.library")} className="flex flex-col gap-1">
            <NavLinks variant="sidebar" list="library" />
            {showUpcoming ? <UpcomingList items={upcomingLibrary} /> : null}
          </nav>
        </SidebarSection>

        <StaffLinks />

        <SidebarSection title={t("nav.playlists")} className="max-lg:hidden">
          <SidebarPlaylists />
        </SidebarSection>

        {showcaseEnabled ? (
          <SidebarSection title={t("nav.playlists")} badge={<DemoBadge />} className="max-lg:hidden">
            <ul className="flex flex-col gap-0.5">
              {demoPlaylists.map((playlist) => (
                <li
                  key={playlist.id}
                  className="flex h-10 items-center gap-3 rounded-lg px-2 text-sm text-muted-foreground transition-colors duration-200 hover:bg-surface-hover hover:text-foreground"
                >
                  <Artwork src={playlist.artwork} sizes="28px" className="size-7 rounded-md" />
                  <span className="truncate">{playlist.title}</span>
                </li>
              ))}
            </ul>
          </SidebarSection>
        ) : null}
      </div>
    </aside>
  );
}

function SidebarSection({
  title,
  badge,
  className,
  children,
}: {
  title: string;
  badge?: ReactNode;
  className?: string;
  children: ReactNode;
}) {
  return (
    <section className={className}>
      <h2 className="mb-2 flex items-center gap-2 px-3 text-[0.7rem] font-semibold tracking-[0.12em] text-subtle-foreground uppercase max-lg:sr-only">
        {title}
        {badge}
      </h2>
      {children}
    </section>
  );
}

/** Planned features: visibly disabled, not focusable, announced as "coming soon". */
function UpcomingList({ items }: { items: readonly { key: UpcomingKey; icon: LucideIcon }[] }) {
  const t = useTranslations("nav");
  return (
    <ul className="flex flex-col gap-1 max-lg:hidden">
      {items.map(({ key, icon: Icon }) => (
        <li
          key={key}
          title={t("comingSoon", { label: t(key) })}
          className="flex h-10 cursor-default items-center gap-3 rounded-full px-3 text-sm font-medium text-subtle-foreground"
        >
          <Icon className="size-5 shrink-0" aria-hidden="true" />
          <span className="truncate">{t(key)}</span>
          <span className="ml-auto rounded-full border px-1.5 py-px text-[0.625rem] font-semibold tracking-wide uppercase">
            {t("soon")}
          </span>
        </li>
      ))}
    </ul>
  );
}
