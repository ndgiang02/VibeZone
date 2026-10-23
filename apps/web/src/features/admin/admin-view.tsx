"use client";

import { Activity, Flag, ShieldAlert, Users } from "lucide-react";
import { useFormatter, useTranslations } from "next-intl";
import { useState, type ReactNode } from "react";
import { EmptyState, ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Link } from "@/i18n/navigation";
import { errorCode, isErrorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { useRequireUser } from "@/features/auth/use-require-user";
import { useReports, useRestoreContent } from "@/features/moderation/api";
import {
  type JobStatus,
  useAdminJobs,
  useAdminTracks,
  useAdminUsers,
  useAuditLog,
  useJobSummary,
  useOutboxSummary,
  useSetRole,
} from "./api";

const TABS = ["overview", "users", "tracks", "jobs", "audit"] as const;
type Tab = (typeof TABS)[number];
const JOB_STATUSES: JobStatus[] = ["Uploaded", "Processing", "Completed", "Failed", "Cancelled"];
const ASSIGNABLE = ["Moderator", "Admin"] as const;
const ROLE_FILTERS = ["Artist", ...ASSIGNABLE] as const;

/** Admin dashboard (Admin role; the API enforces it on every endpoint). */
export function AdminView() {
  const t = useTranslations("admin");
  const me = useRequireUser();
  const [tab, setTab] = useState<Tab>("overview");
  const isAdmin = me.data?.roles.includes("Admin") ?? false;

  if (me.data && !isAdmin) {
    return <ErrorState title={t("forbiddenTitle")} description={t("forbiddenBody")} />;
  }

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
            onClick={() => setTab(value)}
            className={cn(
              "rounded-full border px-4 py-1.5 text-sm font-medium transition-colors",
              tab === value ? "border-primary bg-primary text-primary-foreground" : "hover:bg-surface-hover",
            )}
          >
            {t(`tabs.${value}`)}
          </button>
        ))}
      </div>
      {!isAdmin ? (
        <Skeleton className="h-64 rounded-2xl" />
      ) : tab === "overview" ? (
        <Overview />
      ) : tab === "users" ? (
        <UsersPanel />
      ) : tab === "tracks" ? (
        <TracksPanel />
      ) : tab === "jobs" ? (
        <JobsPanel />
      ) : (
        <AuditPanel />
      )}
    </div>
  );
}

function StatCard({
  icon: Icon,
  label,
  value,
  children,
}: {
  icon: typeof Users;
  label: string;
  value?: number;
  children?: ReactNode;
}) {
  const format = useFormatter();
  return (
    <div className="flex flex-col gap-2 rounded-2xl border bg-surface-card p-5">
      <p className="flex items-center gap-2 text-sm text-muted-foreground">
        <Icon className="size-4" aria-hidden="true" />
        {label}
      </p>
      {value == null ? (
        <Skeleton className="h-8 w-16" />
      ) : (
        <p className="text-3xl font-semibold tabular-nums">{format.number(value)}</p>
      )}
      {children}
    </div>
  );
}

function Overview() {
  const t = useTranslations("admin.overview");
  const tJobs = useTranslations("admin.jobs");
  const users = useAdminUsers("", "", 1);
  const reports = useReports("Open", 1, true);
  const takenDown = useAdminTracks("", true, 1);
  const jobs = useJobSummary();
  const outbox = useOutboxSummary();
  const counts = jobs.data?.counts as Record<string, number | string> | undefined;

  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <StatCard icon={Users} label={t("users")} value={users.data?.meta.total} />
      <StatCard icon={Flag} label={t("openReports")} value={reports.data?.meta.total}>
        <Link href="/moderation" className="text-sm font-medium text-primary hover:underline">
          {t("openQueue")}
        </Link>
      </StatCard>
      <StatCard icon={ShieldAlert} label={t("takenDown")} value={takenDown.data?.meta.total} />
      <StatCard
        icon={Activity}
        label={t("failedJobs")}
        value={counts ? Number(counts.Failed ?? 0) : undefined}
      >
        {counts ? (
          <p className="text-xs text-muted-foreground">
            {JOB_STATUSES.map((s) => `${tJobs(`status.${s}`)}: ${Number(counts[s] ?? 0)}`).join(" · ")}
          </p>
        ) : null}
      </StatCard>
      <StatCard
        icon={Activity}
        label={t("outboxPending")}
        value={outbox.data ? Number(outbox.data.pending) : undefined}
      >
        {outbox.data ? (
          <p className="text-xs text-muted-foreground">
            {t("outboxDetail", {
              dead: Number(outbox.data.deadLettered),
              processed: Number(outbox.data.processedLastDay),
            })}
          </p>
        ) : null}
      </StatCard>
    </div>
  );
}

function Pager({
  page,
  hasMore,
  onPage,
}: {
  page: number;
  hasMore: boolean;
  onPage: (page: number) => void;
}) {
  const t = useTranslations("moderation");
  if (page <= 1 && !hasMore) return null;
  return (
    <div className="flex items-center justify-center gap-3">
      <Button variant="outline" disabled={page <= 1} onClick={() => onPage(page - 1)}>
        {t("previous")}
      </Button>
      <span className="text-sm text-muted-foreground tabular-nums">{t("page", { page })}</span>
      <Button variant="outline" disabled={!hasMore} onClick={() => onPage(page + 1)}>
        {t("next")}
      </Button>
    </div>
  );
}

function PanelState({
  query,
  empty,
  children,
}: {
  query: { isPending: boolean; isError: boolean; error: unknown; refetch: () => unknown };
  empty: boolean;
  children: ReactNode;
}) {
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const t = useTranslations("admin");
  if (query.isPending) return <Skeleton className="h-64 rounded-2xl" />;
  if (query.isError)
    return (
      <ErrorState
        title={tErrors(errorCode(query.error))}
        onRetry={() => void query.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  if (empty) return <EmptyState title={t("empty")} />;
  return <>{children}</>;
}

const TABLE =
  "w-full min-w-[40rem] text-left text-sm [&_td]:px-3 [&_td]:py-2 [&_th]:px-3 [&_th]:py-2 [&_th]:font-medium [&_th]:text-muted-foreground [&_tr]:border-b";

function UsersPanel() {
  const t = useTranslations("admin.users");
  const tErrors = useTranslations("errors");
  const format = useFormatter();
  const me = useMe();
  const [q, setQ] = useState("");
  const [role, setRole] = useState("");
  const [page, setPage] = useState(1);
  const users = useAdminUsers(q, role, page);
  const setRoleMutation = useSetRole();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-3">
        <Input
          aria-label={t("search")}
          placeholder={t("search")}
          value={q}
          onChange={(event) => {
            setQ(event.target.value);
            setPage(1);
          }}
          className="max-w-sm"
        />
        <select
          aria-label={t("roleFilter")}
          value={role}
          onChange={(event) => {
            setRole(event.target.value);
            setPage(1);
          }}
          className="h-8 rounded-lg border bg-background px-2 text-sm"
        >
          <option value="">{t("allRoles")}</option>
          {ROLE_FILTERS.map((r) => (
            <option key={r} value={r}>
              {t(`roles.${r}`)}
            </option>
          ))}
        </select>
      </div>
      {setRoleMutation.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(setRoleMutation.error))}
        </p>
      ) : null}
      <PanelState query={users} empty={(users.data?.items.length ?? 0) === 0}>
        <div className="overflow-x-auto rounded-2xl border bg-surface-card">
          <table className={TABLE}>
            <thead>
              <tr>
                <th>{t("user")}</th>
                <th>{t("email")}</th>
                <th>{t("joined")}</th>
                <th>{t("rolesHeader")}</th>
              </tr>
            </thead>
            <tbody>
              {users.data?.items.map((user) => (
                <tr key={user.id}>
                  <td>
                    <span className="block font-medium">{user.displayName}</span>
                    <span className="text-xs text-muted-foreground">@{user.handle}</span>
                  </td>
                  <td className="break-all">
                    {user.email}
                    {!user.emailConfirmed ? (
                      <span className="ml-1 text-xs text-warning">({t("unverified")})</span>
                    ) : null}
                  </td>
                  <td className="text-muted-foreground">
                    {format.dateTime(new Date(user.createdAt), { dateStyle: "medium" })}
                  </td>
                  <td>
                    <div className="flex flex-wrap gap-1.5">
                      {user.roles.includes("Artist") ? (
                        <span className="rounded-full border px-2 py-0.5 text-xs">{t("roles.Artist")}</span>
                      ) : null}
                      {ASSIGNABLE.map((r) => {
                        const has = user.roles.includes(r);
                        const self = r === "Admin" && has && user.id === me.data?.id;
                        return (
                          <button
                            key={r}
                            type="button"
                            aria-pressed={has}
                            disabled={setRoleMutation.isPending || self}
                            title={self ? t("selfAdmin") : undefined}
                            onClick={() => setRoleMutation.mutate({ userId: user.id, role: r, grant: !has })}
                            className={cn(
                              "rounded-full border px-2 py-0.5 text-xs font-medium transition-colors disabled:opacity-50",
                              has
                                ? "border-primary bg-primary-subtle text-primary"
                                : "text-muted-foreground hover:bg-surface-hover",
                            )}
                          >
                            {has
                              ? t("revoke", { role: t(`roles.${r}`) })
                              : t("grant", { role: t(`roles.${r}`) })}
                          </button>
                        );
                      })}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </PanelState>
      <Pager page={page} hasMore={users.data?.meta.hasMore ?? false} onPage={setPage} />
    </div>
  );
}

function TracksPanel() {
  const t = useTranslations("admin.tracks");
  const tTrack = useTranslations("track");
  const tErrors = useTranslations("errors");
  const [q, setQ] = useState("");
  const [takenDown, setTakenDown] = useState<boolean | null>(null);
  const [page, setPage] = useState(1);
  const tracks = useAdminTracks(q, takenDown, page);
  const restore = useRestoreContent();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-3">
        <Input
          aria-label={t("search")}
          placeholder={t("search")}
          value={q}
          onChange={(event) => {
            setQ(event.target.value);
            setPage(1);
          }}
          className="max-w-sm"
        />
        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={takenDown === true}
            onChange={(event) => {
              setTakenDown(event.target.checked ? true : null);
              setPage(1);
            }}
          />
          {t("onlyTakenDown")}
        </label>
      </div>
      {restore.isError ? (
        <p role="alert" className="text-sm text-destructive">
          {tErrors(errorCode(restore.error))}
        </p>
      ) : null}
      <PanelState query={tracks} empty={(tracks.data?.items.length ?? 0) === 0}>
        <div className="overflow-x-auto rounded-2xl border bg-surface-card">
          <table className={TABLE}>
            <thead>
              <tr>
                <th>{t("track")}</th>
                <th>{t("status")}</th>
                <th>{t("visibility")}</th>
                <th>{t("moderation")}</th>
              </tr>
            </thead>
            <tbody>
              {tracks.data?.items.map((track) => (
                <tr key={track.id}>
                  <td>
                    <Link href={`/tracks/${track.id}`} className="block font-medium hover:underline">
                      {track.title}
                    </Link>
                    <span className="text-xs text-muted-foreground">{track.artist.name}</span>
                  </td>
                  <td>{tTrack(`status.${track.status}`)}</td>
                  <td>{tTrack(`visibility.${track.visibility}`)}</td>
                  <td>
                    {track.takenDown ? (
                      <div className="flex items-center gap-2">
                        <span className="text-xs font-medium text-destructive">
                          {tTrack("takenDownBadge")}
                        </span>
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={restore.isPending}
                          onClick={() => restore.mutate({ subjectType: "Track", subjectId: track.id })}
                        >
                          {t("restore")}
                        </Button>
                      </div>
                    ) : (
                      <span className="text-xs text-muted-foreground">—</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </PanelState>
      <Pager page={page} hasMore={tracks.data?.meta.hasMore ?? false} onPage={setPage} />
    </div>
  );
}

function JobsPanel() {
  const t = useTranslations("admin.jobs");
  const tErrors = useTranslations("errors");
  const format = useFormatter();
  const [status, setStatus] = useState<JobStatus | "">("");
  const [page, setPage] = useState(1);
  const jobs = useAdminJobs(status, page);

  return (
    <div className="flex flex-col gap-4">
      <select
        aria-label={t("statusFilter")}
        value={status}
        onChange={(event) => {
          setStatus(event.target.value as JobStatus | "");
          setPage(1);
        }}
        className="h-8 w-fit rounded-lg border bg-background px-2 text-sm"
      >
        <option value="">{t("allStatuses")}</option>
        {JOB_STATUSES.map((s) => (
          <option key={s} value={s}>
            {t(`status.${s}`)}
          </option>
        ))}
      </select>
      <PanelState query={jobs} empty={(jobs.data?.items.length ?? 0) === 0}>
        <div className="overflow-x-auto rounded-2xl border bg-surface-card">
          <table className={TABLE}>
            <thead>
              <tr>
                <th>{t("file")}</th>
                <th>{t("statusHeader")}</th>
                <th>{t("attempts")}</th>
                <th>{t("created")}</th>
                <th>{t("error")}</th>
              </tr>
            </thead>
            <tbody>
              {jobs.data?.items.map((job) => (
                <tr key={job.id}>
                  <td>
                    <Link
                      href={`/tracks/${job.trackId}`}
                      className="block max-w-56 truncate font-medium hover:underline"
                    >
                      {job.originalFileName ?? job.trackId}
                    </Link>
                    <span className="text-xs text-muted-foreground">
                      {format.number(Number(job.sizeBytes) / 1_048_576, { maximumFractionDigits: 1 })} MB
                    </span>
                  </td>
                  <td>{t(`status.${job.status as JobStatus}`)}</td>
                  <td className="tabular-nums">
                    {Number(job.attemptCount)}/{Number(job.maxAttempts)}
                  </td>
                  <td className="text-muted-foreground">
                    {format.dateTime(new Date(job.createdAt), { dateStyle: "short", timeStyle: "short" })}
                  </td>
                  <td className="text-xs text-destructive">
                    {job.lastError ? tErrors(isErrorCode(job.lastError) ? job.lastError : "unknown") : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </PanelState>
      <Pager page={page} hasMore={jobs.data?.meta.hasMore ?? false} onPage={setPage} />
    </div>
  );
}

function AuditPanel() {
  const t = useTranslations("admin.audit");
  const format = useFormatter();
  const [page, setPage] = useState(1);
  const audit = useAuditLog(page);

  return (
    <div className="flex flex-col gap-4">
      <PanelState query={audit} empty={(audit.data?.items.length ?? 0) === 0}>
        <div className="overflow-x-auto rounded-2xl border bg-surface-card">
          <table className={TABLE}>
            <thead>
              <tr>
                <th>{t("when")}</th>
                <th>{t("actor")}</th>
                <th>{t("action")}</th>
                <th>{t("subject")}</th>
                <th>{t("details")}</th>
              </tr>
            </thead>
            <tbody>
              {audit.data?.items.map((entry) => (
                <tr key={entry.id}>
                  <td className="text-muted-foreground">
                    {format.dateTime(new Date(entry.occurredAt), { dateStyle: "short", timeStyle: "short" })}
                  </td>
                  <td>{entry.actor?.displayName || entry.actor?.handle || "—"}</td>
                  <td className="font-medium">{entry.action}</td>
                  <td className="text-xs break-all">
                    {entry.subjectType} {entry.subjectId}
                  </td>
                  <td className="text-xs break-words text-muted-foreground">
                    {Object.entries(entry.metadata)
                      .map(([key, value]) => `${key}: ${value}`)
                      .join(" · ")}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </PanelState>
      <Pager page={page} hasMore={audit.data?.meta.hasMore ?? false} onPage={setPage} />
    </div>
  );
}
