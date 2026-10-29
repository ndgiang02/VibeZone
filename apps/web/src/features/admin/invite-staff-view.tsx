"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { ArrowLeft, MailCheck } from "lucide-react";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { useForm, useWatch } from "react-hook-form";
import type { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { ErrorState, Skeleton } from "@/components/states/states";
import { Button } from "@/components/ui/button";
import { Link } from "@/i18n/navigation";
import type { ErrorCode } from "@/lib/api/errors";
import { cn } from "@/lib/utils";
import { inviteStaffSchema } from "@/features/auth/schemas";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { useValidationMessages } from "@/features/auth/use-validation-messages";
import { type AdminUser, useInviteStaff } from "./api";

type Schema = ReturnType<typeof inviteStaffSchema>;
const ROLES = ["Moderator", "Admin"] as const;

/**
 * Admins create a Moderator/Admin account; the invitee gets an email to choose their own password
 * (the admin never sees it). The API enforces the Admin role.
 */
export function InviteStaffView() {
  const t = useTranslations("admin");
  const me = useRequireUser();

  if (me.data && !me.data.roles.includes("Admin")) {
    return <ErrorState title={t("forbiddenTitle")} description={t("forbiddenBody")} />;
  }

  return (
    <div className="flex flex-col gap-6">
      <Link
        href="/admin"
        className="inline-flex w-fit items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground"
      >
        <ArrowLeft className="size-4" aria-hidden="true" />
        {t("title")}
      </Link>
      <header>
        <h1 className="text-title font-semibold">{t("invite.title")}</h1>
        <p className="mt-1 text-sm text-muted-foreground">{t("invite.subtitle")}</p>
      </header>
      <div className="rounded-2xl border bg-surface-card p-6">
        {me.data ? <InviteStaffForm /> : <Skeleton className="h-80 rounded-xl" />}
      </div>
    </div>
  );
}

function InviteStaffForm() {
  const t = useTranslations("admin.invite");
  const tAuth = useTranslations("auth");
  const tRoles = useTranslations("admin.users.roles");
  const tErrors = useTranslations("errors");
  const locale = useLocale();
  const invite = useInviteStaff();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const [invited, setInvited] = useState<AdminUser | null>(null);
  const form = useForm<z.input<Schema>, unknown, z.output<Schema>>({
    resolver: zodResolver(inviteStaffSchema(useValidationMessages())),
    defaultValues: { email: "", handle: "", displayName: "", role: "Moderator" },
  });
  const { errors } = form.formState;
  const role = useWatch({ control: form.control, name: "role" });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      setInvited(await invite.mutateAsync({ ...values, displayName: values.displayName || null, locale }));
    } catch (error) {
      setFormError(
        applyServerErrors(error, ["email", "handle", "displayName", "role"], form.setError, (c) =>
          tErrors(c),
        ),
      );
    }
  });

  if (invited) {
    const invitedRole = invited.roles.includes("Admin") ? "Admin" : "Moderator";
    return (
      <div className="flex flex-col items-center gap-3 text-center" role="status">
        <MailCheck className="size-10 text-primary" aria-hidden="true" />
        <h2 className="text-lg font-semibold">{t("successTitle")}</h2>
        <p className="text-sm text-muted-foreground">
          {t("successBody", { email: invited.email, role: tRoles(invitedRole) })}
        </p>
        <div className="mt-2 flex flex-wrap justify-center gap-2">
          <Button
            variant="outline"
            onClick={() => {
              form.reset();
              setInvited(null);
            }}
          >
            {t("another")}
          </Button>
          <Button asChild>
            <Link href="/admin">{t("back")}</Link>
          </Button>
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      <fieldset className="flex flex-col gap-2">
        <legend className="mb-2 text-sm font-medium">{t("role")}</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          {ROLES.map((value) => (
            <label
              key={value}
              className={cn(
                "flex cursor-pointer flex-col gap-1 rounded-xl border p-3 transition-colors has-[:focus-visible]:ring-2 has-[:focus-visible]:ring-ring/60",
                role === value ? "border-primary bg-primary/10" : "hover:bg-surface-hover",
              )}
            >
              <span className="flex items-center gap-2 text-sm font-medium">
                <input type="radio" value={value} className="accent-primary" {...form.register("role")} />
                {tRoles(value)}
              </span>
              <span className="text-xs text-muted-foreground">{t(`roleHint.${value}`)}</span>
            </label>
          ))}
        </div>
        {errors.role?.message ? <p className="text-sm text-destructive">{errors.role.message}</p> : null}
      </fieldset>
      <TextField
        label={tAuth("email")}
        type="email"
        autoComplete="off"
        error={errors.email?.message}
        {...form.register("email")}
      />
      <TextField
        label={tAuth("register.handle")}
        hint={tAuth("register.handleHint")}
        autoComplete="off"
        autoCapitalize="none"
        spellCheck={false}
        error={errors.handle?.message}
        {...form.register("handle")}
      />
      <TextField
        label={tAuth("register.displayName")}
        hint={tAuth("register.displayNameHint")}
        autoComplete="off"
        error={errors.displayName?.message}
        {...form.register("displayName")}
      />
      <p className="text-xs text-subtle-foreground">{t("note")}</p>
      <SubmitButton pending={invite.isPending}>{t("submit")}</SubmitButton>
    </form>
  );
}
