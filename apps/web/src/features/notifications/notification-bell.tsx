"use client";

import { Bell } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { Skeleton } from "@/components/states/states";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { useMe } from "@/features/auth/api";
import { useMarkAllRead, useMarkRead, useNotifications, useUnreadCount } from "./api";
import { NotificationItem } from "./notification-item";

const PREVIEW = 6;

/** Top-bar bell: unread badge (live via SignalR) and the latest notifications. Signed-in users only. */
export function NotificationBell() {
  const t = useTranslations("notifications");
  const tErrors = useTranslations("errors");
  const me = useMe();
  const signedIn = Boolean(me.data);
  const [open, setOpen] = useState(false);
  const unread = useUnreadCount(signedIn);
  const list = useNotifications(open && signedIn, PREVIEW);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();

  if (!signedIn) return null;
  const count = unread.data ?? 0;
  const items = list.data?.pages[0]?.items ?? [];

  return (
    <DropdownMenu open={open} onOpenChange={setOpen}>
      <DropdownMenuTrigger asChild>
        <button
          type="button"
          aria-label={count > 0 ? t("bellUnread", { count }) : t("bell")}
          className="relative flex size-9 items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-surface-hover hover:text-foreground"
        >
          <Bell className="size-5" aria-hidden="true" />
          {count > 0 ? (
            <span
              aria-hidden="true"
              className="absolute -top-0.5 -right-0.5 flex h-4 min-w-4 items-center justify-center rounded-full bg-primary px-1 text-[0.625rem] font-bold text-primary-foreground tabular-nums"
            >
              {count > 99 ? "99+" : count}
            </span>
          ) : null}
        </button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-[min(24rem,calc(100vw-2rem))] p-2">
        <div className="flex items-center justify-between gap-2">
          <DropdownMenuLabel className="text-base">{t("title")}</DropdownMenuLabel>
          {count > 0 ? (
            <button
              type="button"
              disabled={markAll.isPending}
              onClick={() => markAll.mutate()}
              className="rounded-full px-2 py-1 text-xs font-medium text-primary hover:bg-primary-subtle"
            >
              {t("markAllRead")}
            </button>
          ) : null}
        </div>
        <DropdownMenuSeparator />
        {list.isPending ? (
          <div className="flex flex-col gap-2 p-1" aria-busy="true">
            <Skeleton className="h-12" />
            <Skeleton className="h-12" />
          </div>
        ) : list.isError ? (
          <p role="alert" className="p-3 text-sm text-destructive">
            {tErrors(errorCode(list.error))}
          </p>
        ) : items.length === 0 ? (
          <p className="p-4 text-center text-sm text-muted-foreground">{t("empty")}</p>
        ) : (
          <ul className="flex max-h-[60vh] flex-col overflow-y-auto">
            {items.map((n) => (
              <li key={n.id}>
                <NotificationItem
                  notification={n}
                  compact
                  onOpen={(item) => {
                    if (!item.readAt) markRead.mutate(item.id);
                    setOpen(false);
                  }}
                />
              </li>
            ))}
          </ul>
        )}
        <DropdownMenuSeparator />
        <Link
          href="/notifications"
          onClick={() => setOpen(false)}
          className="block rounded-lg p-2 text-center text-sm font-medium text-primary hover:bg-primary-subtle"
        >
          {t("seeAll")}
        </Link>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
