"use client";

import { CheckCircle2, Loader2, XCircle } from "lucide-react";
import { useTranslations } from "next-intl";
import { useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import { useVerifyEmail } from "./api";

export function VerifyEmail({ userId, token }: { userId?: string; token?: string }) {
  const t = useTranslations("auth.verify");
  const { mutate, isError, isSuccess } = useVerifyEmail();
  const started = useRef(false);
  const complete = Boolean(userId && token);

  useEffect(() => {
    // Confirm once per page load (StrictMode runs effects twice in development).
    if (userId && token && !started.current) {
      started.current = true;
      mutate({ userId, token });
    }
  }, [mutate, token, userId]);

  if (!complete || isError) {
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="alert">
        <XCircle className="size-10 text-destructive" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("failedTitle")}</h2>
        <p className="text-sm text-muted-foreground">{complete ? t("failedBody") : t("missing")}</p>
        <Button asChild variant="outline" className="mt-2">
          <Link href="/login">{t("successCta")}</Link>
        </Button>
      </div>
    );
  }

  if (isSuccess) {
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="status">
        <CheckCircle2 className="size-10 text-success" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("successTitle")}</h2>
        <p className="text-sm text-muted-foreground">{t("successBody")}</p>
        <Button asChild className="mt-2">
          <Link href="/login">{t("successCta")}</Link>
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center gap-3 py-4 text-center" role="status" aria-busy="true">
      <Loader2 className="size-8 animate-spin text-primary" aria-hidden="true" />
      <p className="text-sm text-muted-foreground">{t("verifying")}</p>
    </div>
  );
}
