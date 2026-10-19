"use client";

import { LogOut, Settings } from "lucide-react";
import { useTranslations } from "next-intl";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/states/states";
import { useLogout, useMe } from "@/features/auth/api";
import { Link, useRouter } from "@/i18n/navigation";
import { cn } from "@/lib/utils";

/** Sign-in/up buttons for visitors; avatar menu for signed-in users. */
export function AccountControls({ variant }: { variant: "sidebar" | "topbar" }) {
  const t = useTranslations("nav");
  const me = useMe();
  const logout = useLogout();
  const router = useRouter();

  if (me.isPending) {
    return <Skeleton className={variant === "sidebar" ? "h-10 w-full" : "size-8 rounded-full"} />;
  }

  if (!me.data) {
    return (
      <div className={cn("flex gap-2", variant === "sidebar" && "flex-col")}>
        <Button asChild variant={variant === "sidebar" ? "default" : "ghost"} size="sm">
          <Link href="/login">{t("signIn")}</Link>
        </Button>
        {variant === "sidebar" ? (
          <Button asChild variant="outline" size="sm">
            <Link href="/register">{t("signUp")}</Link>
          </Button>
        ) : null}
      </div>
    );
  }

  const user = me.data;
  const initial = (user.displayName || user.handle).charAt(0).toUpperCase();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={t("account")}
          className={cn(
            "flex items-center gap-2.5 rounded-lg text-left transition-colors hover:bg-surface-hover",
            variant === "sidebar" ? "w-full p-2" : "p-1",
          )}
        >
          <span
            aria-hidden="true"
            className="flex size-8 shrink-0 items-center justify-center rounded-full bg-primary/20 text-sm font-semibold text-primary"
          >
            {initial}
          </span>
          {variant === "sidebar" ? (
            <span className="min-w-0">
              <span className="block truncate text-sm font-medium">{user.displayName}</span>
              <span className="block truncate text-xs text-muted-foreground">@{user.handle}</span>
            </span>
          ) : null}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align={variant === "sidebar" ? "start" : "end"} className="w-56">
        <DropdownMenuLabel className="truncate">@{user.handle}</DropdownMenuLabel>
        <DropdownMenuSeparator />
        <DropdownMenuItem asChild>
          <Link href="/settings">
            <Settings className="size-4" aria-hidden="true" />
            {t("settings")}
          </Link>
        </DropdownMenuItem>
        <DropdownMenuItem
          disabled={logout.isPending}
          onSelect={() => logout.mutate(undefined, { onSettled: () => router.replace("/") })}
        >
          <LogOut className="size-4" aria-hidden="true" />
          {t("signOut")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
