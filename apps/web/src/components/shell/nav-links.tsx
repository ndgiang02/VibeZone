"use client";

import { useTranslations } from "next-intl";
import { Link, usePathname } from "@/i18n/navigation";
import { cn } from "@/lib/utils";
import { primaryNav } from "./nav-items";

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
              className={cn(
                "flex items-center rounded-lg text-sm font-medium transition-colors",
                variant === "sidebar"
                  ? "gap-3 px-3 py-2"
                  : "h-full flex-col justify-center gap-1 py-2 text-xs",
                active
                  ? "bg-surface-hover text-foreground"
                  : "text-muted-foreground hover:bg-surface-hover/60 hover:text-foreground",
              )}
            >
              <Icon className={cn("size-5", active && "text-primary")} aria-hidden="true" />
              {t(key)}
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
