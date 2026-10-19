"use client";

import { Laptop } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { FormMessage } from "@/components/forms/form-message";
import { ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { errorCode } from "@/lib/api/errors";
import { useRevokeOtherSessions, useRevokeSession, useSessions } from "@/features/auth/api";

export function SessionsList() {
  const t = useTranslations("settings.sessions");
  const tErrors = useTranslations("errors");
  const format = useFormatter();
  const sessions = useSessions(true);
  const revoke = useRevokeSession();
  const revokeOthers = useRevokeOtherSessions();

  if (sessions.isPending) {
    return (
      <div className="flex flex-col gap-2" aria-busy="true">
        <Skeleton className="h-16" />
        <Skeleton className="h-16" />
      </div>
    );
  }

  if (sessions.isError) {
    return (
      <ErrorState
        title={t("errorTitle")}
        description={tErrors(errorCode(sessions.error))}
        onRetry={() => void sessions.refetch()}
      />
    );
  }

  const hasOthers = sessions.data.some((s) => !s.isCurrent);

  return (
    <div className="flex flex-col gap-4">
      {revokeOthers.isSuccess ? (
        <FormMessage tone="success">{t("revokedOthers", { count: revokeOthers.data })}</FormMessage>
      ) : null}
      {revoke.isError || revokeOthers.isError ? (
        <FormMessage tone="error">{tErrors(errorCode(revoke.error ?? revokeOthers.error))}</FormMessage>
      ) : null}
      <ul className="flex flex-col divide-y rounded-xl border">
        {sessions.data.map((session) => (
          <li key={session.id} className="flex flex-wrap items-center gap-3 p-3">
            <Laptop className="size-5 shrink-0 text-muted-foreground" aria-hidden="true" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-medium" title={session.userAgent ?? undefined}>
                {session.userAgent ?? t("unknownDevice")}
              </p>
              <p className="text-xs text-muted-foreground">
                {t("lastActive", { time: format.relativeTime(new Date(session.lastSeenAt)) })} ·{" "}
                {t("signedIn", {
                  date: format.dateTime(new Date(session.createdAt), { dateStyle: "medium" }),
                })}
              </p>
            </div>
            {session.isCurrent ? (
              <span className="rounded-full bg-primary/15 px-2 py-0.5 text-xs font-medium text-primary">
                {t("current")}
              </span>
            ) : (
              <Button
                variant="outline"
                size="sm"
                disabled={revoke.isPending}
                onClick={() => revoke.mutate(session.id)}
                aria-label={`${t("revoke")}: ${session.userAgent ?? t("unknownDevice")}`}
              >
                {t("revoke")}
              </Button>
            )}
          </li>
        ))}
      </ul>
      {hasOthers ? (
        <Button
          variant="destructive"
          className="self-start"
          disabled={revokeOthers.isPending}
          onClick={() => revokeOthers.mutate()}
        >
          {t("revokeOthers")}
        </Button>
      ) : null}
    </div>
  );
}
