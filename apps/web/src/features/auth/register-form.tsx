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
import { useRegister } from "./api";
import { registerSchema } from "./schemas";
import { applyServerErrors } from "./server-errors";
import { useValidationMessages } from "./use-validation-messages";

type Schema = ReturnType<typeof registerSchema>;

export function RegisterForm() {
  const t = useTranslations("auth");
  const tErrors = useTranslations("errors");
  const locale = useLocale();
  const register = useRegister();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const [submittedEmail, setSubmittedEmail] = useState<string | null>(null);
  const form = useForm<z.input<Schema>, unknown, z.output<Schema>>({
    resolver: zodResolver(registerSchema(useValidationMessages())),
    defaultValues: { email: "", handle: "", displayName: "", password: "" },
  });
  const { errors } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await register.mutateAsync({ ...values, displayName: values.displayName || null, locale });
      setSubmittedEmail(values.email);
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["email", "handle", "displayName", "password"], form.setError, (c) =>
          tErrors(c),
        ),
      );
    }
  });

  if (submittedEmail) {
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="status">
        <MailCheck className="size-10 text-primary" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("register.successTitle")}</h2>
        <p className="text-sm text-muted-foreground">
          {t("register.successBody", { email: submittedEmail })}
        </p>
        <p className="text-xs text-subtle-foreground">{t("register.successHint")}</p>
        <Link href="/login" className="mt-2 text-sm font-medium text-primary hover:underline">
          {t("register.signIn")}
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
        error={errors.email?.message}
        {...form.register("email")}
      />
      <TextField
        label={t("register.handle")}
        hint={t("register.handleHint")}
        autoComplete="username"
        autoCapitalize="none"
        spellCheck={false}
        error={errors.handle?.message}
        {...form.register("handle")}
      />
      <TextField
        label={t("register.displayName")}
        hint={t("register.displayNameHint")}
        autoComplete="nickname"
        error={errors.displayName?.message}
        {...form.register("displayName")}
      />
      <TextField
        label={t("password")}
        hint={t("register.passwordHint")}
        type="password"
        autoComplete="new-password"
        error={errors.password?.message}
        {...form.register("password")}
      />
      <SubmitButton pending={register.isPending}>{t("register.submit")}</SubmitButton>
      <p className="text-center text-sm text-muted-foreground">
        {t("register.haveAccount")}{" "}
        <Link href="/login" className="font-medium text-primary hover:underline">
          {t("register.signIn")}
        </Link>
      </p>
    </form>
  );
}
