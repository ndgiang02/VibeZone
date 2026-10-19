"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useLocale, useTranslations } from "next-intl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import type { z } from "zod";
import { FormMessage } from "@/components/forms/form-message";
import { SubmitButton } from "@/components/forms/submit-button";
import { TextField } from "@/components/forms/text-field";
import { Label } from "@/components/ui/label";
import { usePathname, useRouter } from "@/i18n/navigation";
import { routing } from "@/i18n/routing";
import type { ErrorCode } from "@/lib/api/errors";
import { type Me, useUpdateProfile } from "@/features/auth/api";
import { profileSchema } from "@/features/auth/schemas";
import { applyServerErrors } from "@/features/auth/server-errors";
import { useValidationMessages } from "@/features/auth/use-validation-messages";

type Values = z.infer<ReturnType<typeof profileSchema>>;

export function ProfileForm({ me }: { me: Me }) {
  const t = useTranslations("settings.profile");
  const tLanguages = useTranslations("nav.languages");
  const tErrors = useTranslations("errors");
  const tCommon = useTranslations("common");
  const currentLocale = useLocale();
  const router = useRouter();
  const pathname = usePathname();
  const update = useUpdateProfile();
  const [formError, setFormError] = useState<ErrorCode | null>(null);
  const form = useForm<Values>({
    resolver: zodResolver(profileSchema(useValidationMessages())),
    defaultValues: { displayName: me.displayName, locale: me.locale === "en" ? "en" : "vi" },
  });

  const onSubmit = form.handleSubmit(async (values) => {
    setFormError(null);
    try {
      await update.mutateAsync(values);
      form.reset(values);
      if (values.locale !== currentLocale) {
        router.replace(pathname, { locale: values.locale });
      }
    } catch (error) {
      setFormError(applyServerErrors(error, ["displayName", "locale"], form.setError, (c) => tErrors(c)));
    }
  });

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError ? <FormMessage tone="error">{tErrors(formError)}</FormMessage> : null}
      {update.isSuccess && !form.formState.isDirty ? (
        <FormMessage tone="success">{t("saved")}</FormMessage>
      ) : null}
      <dl className="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt className="text-muted-foreground">{t("handle")}</dt>
          <dd className="font-medium">@{me.handle}</dd>
        </div>
        <div>
          <dt className="text-muted-foreground">{t("email")}</dt>
          <dd className="font-medium break-all">{me.email}</dd>
        </div>
      </dl>
      <TextField
        label={t("displayName")}
        autoComplete="nickname"
        error={form.formState.errors.displayName?.message}
        {...form.register("displayName")}
      />
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="profile-locale">{t("language")}</Label>
        <select
          id="profile-locale"
          aria-describedby="profile-locale-hint"
          className="h-10 rounded-lg border border-input bg-transparent px-3 text-sm dark:bg-input/30"
          {...form.register("locale")}
        >
          {routing.locales.map((locale) => (
            <option key={locale} value={locale}>
              {tLanguages(locale)}
            </option>
          ))}
        </select>
        <p id="profile-locale-hint" className="text-xs text-muted-foreground">
          {t("languageHint")}
        </p>
      </div>
      <SubmitButton pending={update.isPending} className="self-start">
        {update.isPending ? tCommon("saving") : tCommon("save")}
      </SubmitButton>
    </form>
  );
}
