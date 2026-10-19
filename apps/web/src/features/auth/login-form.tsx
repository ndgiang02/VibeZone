"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Button } from "@/components/ui/button";
import { Link, useRouter } from "@/i18n/navigation";
import type { ErrorCode } from "@/lib/api/errors";
import { useLogin, useResendVerification } from "./api";
import { loginSchema, safeNextPath } from "./schemas";
import { applyServerErrors } from "./server-errors";
import { useValidationMessages } from "./use-validation-messages";

type Values = z.infer<ReturnType<typeof loginSchema>>;

export function LoginForm({ next }: { next?: string }) {
  const t = useTranslations("auth");
  const tErrors = useTranslations("errors");
  const router = useRouter();
  const login = useLogin();
  const resend = useResendVerification();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(loginSchema(useValidationMessages())),
    defaultValues: { email: "", password: "", rememberMe: false },
  });
  const { errors } = form.formState;

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    resend.reset();
    try {
      await login.mutateAsync(values);
      router.replace(safeNextPath(next) ?? "/");
    } catch (error) {
      setFormError(applyServerErrors(error, ["email", "password"], form.setError, (c) => tErrors(c)));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? (
        <FormMessage tone="error">
          <p>{tErrors(formError)}</p>
          {formError === "email_not_confirmed" ? (
            <Button
              type="button"
              variant="link"
              className="h-auto p-0"
              disabled={resend.isPending || resend.isSuccess}
              onClick={() => resend.mutate({ email: form.getValues("email"), locale: null })}
            >
              {t("login.resend")}
            </Button>
          ) : null}
        </FormMessage>
      ) : null}
      {resend.isSuccess ? <FormMessage tone="success">{t("login.resent")}</FormMessage> : null}

      <TextField
        label={t("email")}
        type="email"
        autoComplete="email"
        error={errors.email?.message}
        {...form.register("email")}
      />
      <TextField
        label={t("password")}
        type="password"
        autoComplete="current-password"
        error={errors.password?.message}
        {...form.register("password")}
      />
      <div className="flex flex-wrap items-center justify-between gap-3 text-sm">
        <label className="flex items-center gap-2 text-muted-foreground">
          <input type="checkbox" className="size-4 accent-primary" {...form.register("rememberMe")} />
          {t("login.rememberMe")}
        </label>
        <Link href="/forgot-password" className="font-medium text-primary hover:underline">
          {t("login.forgot")}
        </Link>
      </div>
      <SubmitButton pending={login.isPending}>{t("login.submit")}</SubmitButton>
      <p className="text-center text-sm text-muted-foreground">
        {t("login.noAccount")}{" "}
        <Link href="/register" className="font-medium text-primary hover:underline">
          {t("login.signUp")}
        </Link>
      </p>
    </form>
  );
}
