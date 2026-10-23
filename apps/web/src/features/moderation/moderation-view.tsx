"use client";

import { Flag, ShieldAlert } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { useState } from "react";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useRequireUser } from "@/features/auth/use-require-user";
import { type Report, type ReportStatus, useReports, useResolveReport, useRestoreContent } from "./api";

const TABS: (ReportStatus | "all")[] = ["Open", "Actioned", "Rejected", "all"];

/** Moderators' queue: open reports oldest first; remove the content or dismiss; restore takedowns. */
export function ModerationView() {
  const t = useTranslations("moderation");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const me = useRequireUser();
  const roles = me.data?.roles ?? [];
  const allowed = roles.includes("Moderator") || roles.includes("Admin");
  const [tab, setTab] = useState<ReportStatus | "all">("Open");
  const [page, setPage] = useState(1);
  const reports = useReports(tab, page, allowed);

  if (me.data && !allowed) {
    return <ErrorState title={t("forbiddenTitle")} description={t("forbiddenBody")} />;
  }

  const total = reports.data?.meta.total ?? 0;

  return (
    <div className="flex flex-col gap-6">
      <header>
        <h1 className="text-title font-semibold">{t("title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("subtitle")}</p>
      </header>

      <div role="tablist" aria-label={t("title")} className="flex flex-wrap gap-2">
        {TABS.map((value) => (
          <button
            key={value}
            type="button"
            role="tab"
            aria-selected={tab === value}
            onClick={() => {
              setTab(value);
              setPage(1);
            }}
            className={cn(
              "rounded-full border px-4 py-1.5 text-sm font-medium transition-colors",
              tab === value ? "border-primary bg-primary text-primary-foreground" : "hover:bg-surface-hover",
            )}
          >
            {t(`tabs.${value}`)}
          </button>
        ))}
      </div>

      {!me.data || reports.isPending ? (
        <div className="flex flex-col gap-3" aria-busy="true" aria-label={tCommon("loading")}>
          {Array.from({ length: 3 }, (_, i) => (
            <Skeleton key={i} className="h-36 rounded-2xl" />
          ))}
        </div>
      ) : reports.isError ? (
        <ErrorState
          title={tErrors(errorCode(reports.error))}
          onRetry={() => void reports.refetch()}
          retryLabel={tCommon("retry")}
        />
      ) : reports.data.items.length === 0 ? (
        <EmptyState icon={Flag} title={t("emptyTitle")} description={t("emptyBody")} />
      ) : (
        <ul className="flex flex-col gap-3">
          {reports.data.items.map((report) => (
            <li key={report.id}>
              <ReportCard report={report} />
            </li>
          ))}
        </ul>
      )}

      {total > 20 ? (
        <div className="flex items-center justify-center gap-3">
          <Button variant="outline" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
            {t("previous")}
          </Button>
          <span className="text-sm text-muted-foreground tabular-nums">{t("page", { page })}</span>
          <Button
            variant="outline"
            disabled={!reports.data?.meta.hasMore}
            onClick={() => setPage((p) => p + 1)}
          >
            {t("next")}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

function ReportCard({ report }: { report: Report }) {
  const t = useTranslations("moderation");
  const tReport = useTranslations("report");
  const tErrors = useTranslations("errors");
  const format = useFormatter();
  const [note, setNote] = useState("");
  const resolve = useResolveReport();
  const restore = useRestoreContent();
  const subject = report.subject;
  const href =
    report.subjectType === "Playlist"
      ? `/playlists/${report.subjectId}`
      : subject.trackId
        ? `/tracks/${subject.trackId}`
        : null;
  const error = resolve.error ?? restore.error;

  return (
    <article className="flex flex-col gap-3 rounded-2xl border bg-surface-card p-5">
      <header className="flex flex-wrap items-center gap-2 text-sm">
        <span className="rounded-full border px-2 py-0.5 text-xs font-semibold">
          {t(`subjects.${report.subjectType}`)}
        </span>
        <span className="rounded-full bg-destructive/15 px-2 py-0.5 text-xs font-semibold text-destructive">
          {tReport(`reasons.${report.reason}.label`)}
        </span>
        <span className="text-xs text-subtle-foreground">
          {t("reportedBy", {
            name: report.reporter?.displayName || report.reporter?.handle || "?",
            when: format.relativeTime(new Date(report.createdAt)),
          })}
        </span>
        <span className="ml-auto text-xs font-medium text-muted-foreground">
          {t(`tabs.${report.status}`)}
        </span>
      </header>

      <div className="min-w-0">
        {subject.title ? (
          href ? (
            <Link href={href} className="font-semibold break-words hover:underline">
              {subject.title}
            </Link>
          ) : (
            <p className="font-semibold break-words">{subject.title}</p>
          )
        ) : (
          <p className="text-muted-foreground italic">{t("subjectGone")}</p>
        )}
        {subject.owner ? (
          <p className="text-xs text-muted-foreground">
            {t("owner", { name: subject.owner.displayName || subject.owner.handle })}
          </p>
        ) : null}
        {subject.removed ? (
          <p className="mt-1 inline-flex items-center gap-1 text-xs font-medium text-destructive">
            <ShieldAlert className="size-3.5" aria-hidden="true" />
            {t("removed")}
          </p>
        ) : null}
      </div>

      {report.details ? (
        <p className="rounded-xl bg-surface-elevated p-3 text-sm break-words whitespace-pre-line text-muted-foreground">
          {report.details}
        </p>
      ) : null}

      {report.status === "Open" ? (
        <div className="flex flex-col gap-2">
          <label className="flex flex-col gap-1 text-sm">
            <span className="font-medium">{t("note")}</span>
            <input
              value={note}
              maxLength={1000}
              onChange={(event) => setNote(event.target.value)}
              className="h-9 rounded-lg border bg-background px-3 outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
            />
          </label>
          <div className="flex flex-wrap gap-2">
            <Button
              variant="destructive"
              disabled={resolve.isPending}
              onClick={() => {
                if (window.confirm(t("removeConfirm")))
                  resolve.mutate({ reportId: report.id, action: "remove", note });
              }}
            >
              {t("remove")}
            </Button>
            <Button
              variant="outline"
              disabled={resolve.isPending}
              onClick={() => resolve.mutate({ reportId: report.id, action: "dismiss", note })}
            >
              {t("dismiss")}
            </Button>
          </div>
        </div>
      ) : (
        <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
          {report.handledBy && report.handledAt ? (
            <span>
              {t("handledBy", {
                name: report.handledBy.displayName || report.handledBy.handle,
                when: format.relativeTime(new Date(report.handledAt)),
              })}
            </span>
          ) : null}
          {report.resolutionNote ? <span className="break-words">“{report.resolutionNote}”</span> : null}
          {subject.removed && report.subjectType !== "Comment" ? (
            <Button
              size="sm"
              variant="outline"
              disabled={restore.isPending}
              onClick={() => restore.mutate({ subjectType: report.subjectType, subjectId: report.subjectId })}
            >
              {t("restore")}
            </Button>
          ) : null}
        </div>
      )}

      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(error))}
        </p>
      ) : null}
    </article>
  );
}
