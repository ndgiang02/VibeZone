"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { CheckCircle2 } from "lucide-react";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import type { ErrorCode } from "@/lib/api/errors";
import { useResetPassword } from "./api";
import { resetPasswordSchema } from "./schemas";
import { applyServerErrors } from "./server-errors";
import { useValidationMessages } from "./use-validation-messages";

type Values = z.infer<ReturnType<typeof resetPasswordSchema>>;

export function ResetPasswordForm({ email, token }: { email?: string; token?: string }) {
  const t = useTranslations("auth");
  const tErrors = useTranslations("errors");
  const reset = useResetPassword();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(resetPasswordSchema(useValidationMessages())),
    defaultValues: { newPassword: "", confirmPassword: "" },
  });

  if (!email || !token) {
    return (
      <FormMessage tone="error">
        <p>{t("reset.missing")}</p>
        <Link href="/forgot-password" className="font-medium text-primary hover:underline">
          {t("forgot.submit")}
        </Link>
      </FormMessage>
    );
  }

  if (reset.isSuccess) {
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="status">
        <CheckCircle2 className="size-10 text-success" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("reset.successTitle")}</h2>
        <p className="text-sm text-muted-foreground">{t("reset.successBody")}</p>
        <Button asChild className="mt-2">
          <Link href="/login">{t("reset.successCta")}</Link>
        </Button>
      </div>
    );
  }

  const onSubmit = form.handleSubmit(async ({ newPassword }) => {
    setFormError(null);
    try {
      await reset.mutateAsync({ email, token, newPassword });
    } catch (error) {
      setFormError(applyServerErrors(error, ["newPassword"], form.setError, (c) => tErrors(c)));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? (
        <FormMessage tone="error">
          <p>{tErrors(formError)}</p>
          {formError === "token_invalid" ? (
            <Link href="/forgot-password" className="font-medium text-primary hover:underline">
              {t("forgot.submit")}
            </Link>
          ) : null}
        </FormMessage>
      ) : null}
      <TextField
        label={t("reset.newPassword")}
        hint={t("register.passwordHint")}
        type="password"
        autoComplete="new-password"
        error={form.formState.errors.newPassword?.message}
        {...form.register("newPassword")}
      />
      <TextField
        label={t("reset.confirmPassword")}
        type="password"
        autoComplete="new-password"
        error={form.formState.errors.confirmPassword?.message}
        {...form.register("confirmPassword")}
      />
      <SubmitButton pending={reset.isPending}>{t("reset.submit")}</SubmitButton>
    </form>
  );
}
