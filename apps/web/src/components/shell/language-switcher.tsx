"use client";

import { Check, Languages } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useTransition } from "react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useMe, useUpdateProfile } from "@/features/auth/api";
import { usePathname, useRouter } from "@/i18n/navigation";
import { routing, type AppLocale } from "@/i18n/routing";

export function LanguageSwitcher({ compact = false }: { compact?: boolean }) {
  const t = useTranslations("nav");
  const locale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const me = useMe();
  const updateProfile = useUpdateProfile();
  const [pending, startTransition] = useTransition();

  function choose(next: AppLocale) {
    if (next === locale) return;
    // Signed-in users keep the choice for emails too; failure here must not block switching the UI.
    if (me.data) {
      updateProfile.mutate({ displayName: me.data.displayName, locale: next });
    }
    startTransition(() => router.replace(pathname, { locale: next }));
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size={compact ? "icon" : "default"}
          aria-label={t("language")}
          disabled={pending}
          className={compact ? undefined : "justify-start"}
        >
          <Languages className="size-4" aria-hidden="true" />
          {compact ? null : <span>{t(`languages.${locale}`)}</span>}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuLabel>{t("language")}</DropdownMenuLabel>
        {routing.locales.map((option) => (
          <DropdownMenuItem key={option} lang={option} onSelect={() => choose(option)}>
            <Check className={option === locale ? "size-4" : "size-4 opacity-0"} aria-hidden="true" />
            {t(`languages.${option}`)}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
