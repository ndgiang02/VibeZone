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
import { useAcceptInvite } from "./api";
import { acceptInviteSchema } from "./schemas";
import { applyServerErrors } from "./server-errors";
import { useValidationMessages } from "./use-validation-messages";

type Values = z.infer<ReturnType<typeof acceptInviteSchema>>;

/** Staff invitation link: the invitee chooses their first password (which also confirms the email). */
export function AcceptInviteForm({ userId, token }: { userId?: string; token?: string }) {
  const t = useTranslations("auth");
  const tErrors = useTranslations("errors");
  const accept = useAcceptInvite();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(acceptInviteSchema(useValidationMessages())),
    defaultValues: { password: "", confirmPassword: "" },
  });

  if (!userId || !token) {
    return (
      <FormMessage tone="error">
        <p>{t("invite.missing")}</p>
      </FormMessage>
    );
  }

  if (accept.isSuccess) {
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="status">
        <CheckCircle2 className="size-10 text-success" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("invite.successTitle")}</h2>
        <p className="text-sm text-muted-foreground">{t("invite.successBody")}</p>
        <Button asChild className="mt-2">
          <Link href="/login">{t("invite.successCta")}</Link>
        </Button>
      </div>
    );
  }

  const onSubmit = form.handleSubmit(async ({ password }) => {
    setFormError(null);
    try {
      await accept.mutateAsync({ userId, token, password });
    } catch (error) {
      setFormError(applyServerErrors(error, ["password"], form.setError, (c) => tErrors(c)));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? (
        <FormMessage tone="error">
          <p>{tErrors(formError)}</p>
          {formError === "token_invalid" ? <p>{t("invite.expiredHint")}</p> : null}
        </FormMessage>
      ) : null}
      <TextField
        label={t("password")}
        hint={t("register.passwordHint")}
        type="password"
        autoComplete="new-password"
        error={form.formState.errors.password?.message}
        {...form.register("password")}
      />
      <TextField
        label={t("reset.confirmPassword")}
        type="password"
        autoComplete="new-password"
        error={form.formState.errors.confirmPassword?.message}
        {...form.register("confirmPassword")}
      />
      <SubmitButton pending={accept.isPending}>{t("invite.submit")}</SubmitButton>
    </form>
  );
}
