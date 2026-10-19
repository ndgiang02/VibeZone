"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import type { ErrorCode } from "@/lib/api/errors";
import { useChangePassword } from "@/features/auth/api";
import { changePasswordSchema } from "@/features/auth/schemas";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useValidationMessages } from "@/features/auth/use-validation-messages";

type Values = z.infer<ReturnType<typeof changePasswordSchema>>;

export function ChangePasswordForm() {
  const t = useTranslations("settings.password");
  const tAuth = useTranslations("auth.register");
  const tErrors = useTranslations("errors");
  const change = useChangePassword();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(changePasswordSchema(useValidationMessages())),
    defaultValues: { currentPassword: "", newPassword: "" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await change.mutateAsync(values);
      form.reset();
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["currentPassword", "newPassword"], form.setError, (c) => tErrors(c)),
      );
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      {change.isSuccess && !form.formState.isDirty ? (
        <FormMessage tone="success">{t("changed")}</FormMessage>
      ) : null}
      <TextField
        label={t("current")}
        type="password"
        autoComplete="current-password"
        error={form.formState.errors.currentPassword?.message}
        {...form.register("currentPassword")}
      />
      <TextField
        label={t("new")}
        hint={tAuth("passwordHint")}
        type="password"
        autoComplete="new-password"
        error={form.formState.errors.newPassword?.message}
        {...form.register("newPassword")}
      />
      <SubmitButton pending={change.isPending} className="self-start">
        {t("submit")}
      </SubmitButton>
    </form>
  );
}
