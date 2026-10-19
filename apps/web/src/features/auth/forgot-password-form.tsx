"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { MailCheck } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Link } from "@/i18n/navigation";
import type { ErrorCode } from "@/lib/api/errors";
import { useForgotPassword } from "./api";
import { emailOnlySchema } from "./schemas";
import { applyServerErrors } from "./server-errors";
import { useValidationMessages } from "./use-validation-messages";

type Values = z.infer<ReturnType<typeof emailOnlySchema>>;

export function ForgotPasswordForm() {
  const t = useTranslations("auth");
  const tErrors = useTranslations("errors");
  const locale = useLocale();
  const forgot = useForgotPassword();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const [sentTo, setSentTo] = useState<string | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(emailOnlySchema(useValidationMessages())),
    defaultValues: { email: "" },
  });

  const onSubmit = form.handleSubmit(async ({ email }) => {
    setFormError(null);
    try {
      await forgot.mutateAsync({ email, locale });
      setSentTo(email);
    } catch (error) {
      setFormError(applyServerErrors(error, ["email"], form.setError, (c) => tErrors(c)));
    }
  });

  if (sentTo) {
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="status">
        <MailCheck className="size-10 text-primary" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("forgot.successTitle")}</h2>
        <p className="text-sm text-muted-foreground">{t("forgot.successBody", { email: sentTo })}</p>
        <Link href="/login" className="mt-2 text-sm font-medium text-primary hover:underline">
          {t("forgot.back")}
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <TextField
        label={t("email")}
        type="email"
        autoComplete="email"
        error={form.formState.errors.email?.message}
        {...form.register("email")}
      />
      <SubmitButton pending={forgot.isPending}>{t("forgot.submit")}</SubmitButton>
      <Link href="/login" className="text-center text-sm font-medium text-primary hover:underline">
        {t("forgot.back")}
      </Link>
    </form>
  );
}
