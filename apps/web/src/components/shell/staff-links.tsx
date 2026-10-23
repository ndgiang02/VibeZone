"use client";

import { Shield, ShieldCheck } from "lucide-react";
import { useTranslations } from "next-intl";
import { useMe } from "@/features/auth/api";
import type { NavItem } from "./nav-items";
import { NavLinks } from "./nav-links";

/** Staff destinations, shown to moderators/admins only (UX only — the API enforces the roles). */
export function StaffLinks() {
  const t = useTranslations("nav");
  const roles = useMe().data?.roles ?? [];
  const isAdmin = roles.includes("Admin");
  const isModerator = isAdmin || roles.includes("Moderator");
  if (!isModerator) return null;

  const items: NavItem[] = [{ href: "/moderation", key: "moderation", icon: Shield }];
  if (isAdmin) items.push({ href: "/admin", key: "admin", icon: ShieldCheck });

  return (
    <section>
      <h2 className="mb-2 px-3 text-[0.7rem] font-semibold tracking-[0.12em] text-subtle-foreground uppercase max-lg:sr-only">
        {t("staff")}
      </h2>
      <nav aria-label={t("staff")}>
        <NavLinks variant="sidebar" items={items} />
      </nav>
    </section>
  );
}
