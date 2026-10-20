"use client";

import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { primaryNav } from "./nav-items";

/** Sidebar: icon rail on tablets (labels from lg), lime pill for the current page. Bottom: mobile tab bar. */
export function NavLinks({ variant }: { variant: "sidebar" | "bottom" }) {
  const t = useTranslations("nav");
  const pathname = usePathname(); // without the locale prefix

  return (
    <ul className={cn(variant === "sidebar" ? "flex flex-col gap-1" : "flex items-stretch justify-around")}>
      {primaryNav.map(({ href, key, icon: Icon }) => {
        const active = href === "/" ? pathname === "/" : pathname.startsWith(href);
        return (
          <li key={href} className={cn(variant === "bottom" && "flex-1")}>
            <Link
              href={href}
              aria-current={active ? "page" : undefined}
              title={variant === "sidebar" ? t(key) : undefined}
              className={cn(
                "flex items-center text-sm font-medium transition-colors duration-200",
                variant === "sidebar"
                  ? "h-10 gap-3 rounded-full px-3 max-lg:justify-center max-lg:px-0"
                  : "h-full flex-col justify-center gap-1 rounded-lg py-2 text-xs",
                active
                  ? variant === "sidebar"
                    ? "bg-primary text-primary-foreground shadow-[0_6px_20px_-8px_var(--accent-glow)]"
                    : "text-primary"
                  : "text-muted-foreground hover:bg-surface-hover hover:text-foreground",
              )}
            >
              <Icon className="size-5 shrink-0" aria-hidden="true" />
              <span className={cn(variant === "sidebar" && "max-lg:sr-only")}>{t(key)}</span>
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
