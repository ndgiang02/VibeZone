"use client";

import { Bell } from "lucide-react";
import { useTranslations } from "next-intl";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { useMarkAllRead, useMarkRead, useNotifications, useUnreadCount } from "./api";
import { NotificationItem } from "./notification-item";

export function NotificationsView() {
  const t = useTranslations("notifications");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const signedIn = Boolean(me.data);
  const list = useNotifications(signedIn);
  const unread = useUnreadCount(signedIn);
  const markRead = useMarkRead();
  const markAll = useMarkAllRead();
  const items = list.data?.pages.flatMap((p) => p.items) ?? [];

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-title font-semibold">{t("title")}</h1>
          <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
        </div>
        {(unread.data ?? 0) > 0 ? (
          <Button variant="outline" disabled={markAll.isPending} onClick={() => markAll.mutate()}>
            {t("markAllRead")}
          </Button>
        ) : null}
      </header>

      {!signedIn || list.isPending ? (
        <div className="flex flex-col gap-2" aria-busy="true" aria-label={tCommon("loading")}>
          {Array.from({ length: 5 }, (_, i) => (
            <Skeleton key={i} className="h-16" />
          ))}
        </div>
      ) : list.isError ? (
        <ErrorState
          title={tErrors(errorCode(list.error))}
          onRetry={() => void list.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : items.length === 0 ? (
        <EmptyState icon={Bell} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <ul className="flex flex-col gap-1 rounded-2xl border bg-surface-card p-2">
          {items.map((n) => (
            <li key={n.id}>
              <NotificationItem
                notification={n}
                onOpen={(item) => !item.readAt && markRead.mutate(item.id)}
              />
            </li>
          ))}
        </ul>
      )}

      {list.hasNextPage ? (
        <Button
          variant="outline"
          className="self-center"
          disabled={list.isFetchingNextPage}
          onClick={() => void list.fetchNextPage()}
        >
          {list.isFetchingNextPage ? tCommon("loading") : tCommon("showMore")}
        </Button>
      ) : null}
    </div>
  );
}
