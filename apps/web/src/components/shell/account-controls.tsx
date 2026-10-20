"use client";

import { LogOut, Settings } from "lucide-react";
import { useTranslations } from "next-intl";
import { ArtistAvatar } from "@/components/media/artist-avatar";
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
    return <Skeleton className={variant === "sidebar" ? "h-10 w-full" : "size-9 rounded-full"} />;
  }

  if (!me.data) {
    return (
      <div className={cn("flex gap-2", variant === "sidebar" && "flex-col")}>
        <Button
          asChild
          variant={variant === "sidebar" ? "default" : "ghost"}
          size={variant === "sidebar" ? "sm" : "lg"}
          className={cn(variant === "topbar" && "rounded-full px-4")}
        >
          <Link href="/login">{t("signIn")}</Link>
        </Button>
        <Button
          asChild
          variant={variant === "sidebar" ? "outline" : "default"}
          size={variant === "sidebar" ? "sm" : "lg"}
          className={cn(variant === "topbar" && "rounded-full px-4 max-sm:hidden")}
        >
          <Link href="/register">{t("signUp")}</Link>
        </Button>
      </div>
    );
  }

  const user = me.data;

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
          <ArtistAvatar
            id={user.id}
            name={user.displayName || user.handle}
            src={user.avatar?.small}
            className="size-9 ring-1 ring-primary-border transition-transform duration-200 hover:scale-105"
          />
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
