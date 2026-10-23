"use client";

import { Flag } from "lucide-react";
import { useTranslations } from "next-intl";
import { useId, useRef, useState, type FormEvent } from "react";
import { Button } from "@/components/ui/button";
import { useRouter } from "@/i18n/navigation";
import { errorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { useMe } from "@/features/auth/api";
import { REPORT_REASONS, type ReportReason, type ReportSubjectType, useCreateReport } from "./api";

const MAX_DETAILS = 1000;

/** "Report" opens a modal (native <dialog>: focus trap, Esc to close) with a reason and optional details. */
export function ReportButton({
  subjectType,
  subjectId,
  compact = false,
  className,
}: {
  subjectType: ReportSubjectType;
  subjectId: string;
  compact?: boolean;
  className?: string;
}) {
  const t = useTranslations("report");
  const tErrors = useTranslations("errors");
  const me = useMe();
  const router = useRouter();
  const dialog = useRef<HTMLDialogElement>(null);
  const titleId = useId();
  const [reason, setReason] = useState<ReportReason>("Copyright");
  const [details, setDetails] = useState("");
  const [sent, setSent] = useState(false);
  const report = useCreateReport();

  const open = () => {
    if (!me.data) {
      router.push("/login");
      return;
    }
    report.reset();
    setSent(false);
    dialog.current?.showModal();
  };

  const submit = (event: FormEvent) => {
    event.preventDefault();
    report.mutate(
      { subjectType, subjectId, reason, details: details.trim() || null },
      { onSuccess: () => setSent(true) },
    );
  };

  const label = t(`open.${subjectType}`);

  return (
    <>
      <button
        type="button"
        onClick={open}
        aria-label={label}
        title={label}
        className={cn(
          compact
            ? "flex size-7 items-center justify-center rounded-full text-muted-foreground hover:bg-surface-hover hover:text-foreground"
            : "inline-flex h-10 items-center gap-2 rounded-full border px-4 text-sm font-medium text-muted-foreground transition-colors hover:bg-surface-hover hover:text-foreground",
          className,
        )}
      >
        <Flag className={compact ? "size-3.5" : "size-4"} aria-hidden="true" />
        {compact ? null : t("short")}
      </button>

      <dialog
        ref={dialog}
        aria-labelledby={titleId}
        className="m-auto w-[min(32rem,calc(100vw-2rem))] rounded-2xl border bg-surface-card p-0 text-foreground backdrop:bg-black/60"
      >
        {sent ? (
          <div className="flex flex-col gap-4 p-6">
            <h2 id={titleId} className="text-lg font-semibold">
              {t("sentTitle")}
            </h2>
            <p className="text-sm text-muted-foreground">{t("sentBody")}</p>
            <Button className="self-end" onClick={() => dialog.current?.close()}>
              {t("close")}
            </Button>
          </div>
        ) : (
          <form onSubmit={submit} className="flex flex-col gap-4 p-6">
            <h2 id={titleId} className="text-lg font-semibold">
              {label}
            </h2>
            <fieldset className="flex flex-col gap-2">
              <legend className="mb-1 text-sm font-medium">{t("reason")}</legend>
              {REPORT_REASONS.map((value) => (
                <label key={value} className="flex items-start gap-2 text-sm">
                  <input
                    type="radio"
                    name="reason"
                    value={value}
                    checked={reason === value}
                    onChange={() => setReason(value)}
                    className="mt-1"
                  />
                  <span>
                    <span className="font-medium">{t(`reasons.${value}.label`)}</span>
                    <span className="block text-xs text-muted-foreground">{t(`reasons.${value}.hint`)}</span>
                  </span>
                </label>
              ))}
            </fieldset>
            <label className="flex flex-col gap-1 text-sm">
              <span className="font-medium">{t("details")}</span>
              <textarea
                value={details}
                maxLength={MAX_DETAILS}
                onChange={(event) => setDetails(event.target.value)}
                rows={3}
                placeholder={t("detailsPlaceholder")}
                className="w-full resize-y rounded-xl border bg-background px-3 py-2 outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              />
            </label>
            {report.isError ? (
              <p role="alert" className="text-sm text-destructive">
                {tErrors(errorCode(report.error))}
              </p>
            ) : null}
            <div className="flex justify-end gap-2">
              <Button type="button" variant="ghost" onClick={() => dialog.current?.close()}>
                {t("cancel")}
              </Button>
              <Button type="submit" disabled={report.isPending}>
                {t("submit")}
              </Button>
            </div>
          </form>
        )}
      </dialog>
    </>
  );
}
