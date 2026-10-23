"use client";

import { useTranslations } from "next-intl";
import type { ReactNode } from "react";
import { ErrorState, Skeleton } from "@/components/states/states";
import { errorCode } from "@/lib/api/errors";
import { useRequireUser } from "@/features/auth/use-require-user";
import { AvatarEditor } from "./avatar-editor";
import { ChangePasswordForm } from "./change-password-form";
import { ProfileForm } from "./profile-form";
import { SessionsList } from "./sessions-list";

export function SettingsView() {
  const t = useTranslations("settings");
  const tCommon = useTranslations("common");
  const tErrors = useTranslations("errors");
  const me = useRequireUser();

  if (me.isError) {
    return (
      <ErrorState
        title={tErrors(errorCode(me.error))}
        onRetry={() => void me.refetch()}
        retryLabel={tCommon("retry")}
      />
    );
  }

  if (!me.data) {
    // Loading, or anonymous and about to be redirected to the login page.
    return (
      <div className="flex flex-col gap-4" aria-busy="true" aria-label={tCommon("loading")}>
        <Skeleton className="h-64 rounded-2xl" />
        <Skeleton className="h-48 rounded-2xl" />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      <Section title={t("profile.title")}>
        <div className="flex flex-col gap-6">
          <AvatarEditor me={me.data} />
          <ProfileForm me={me.data} />
        </div>
      </Section>
      <Section title={t("password.title")}>
        <ChangePasswordForm />
      </Section>
      <Section title={t("sessions.title")} description={t("sessions.subtitle")}>
        <SessionsList />
      </Section>
    </div>
  );
}

function Section({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <section aria-label={title} className="rounded-2xl border bg-surface p-5 md:p-6">
      <h2 className="text-lg font-semibold">{title}</h2>
      {description ? <p className="mt-1 text-sm text-muted-foreground">{description}</p> : null}
      <div className="mt-5">{children}</div>
    </section>
  );
}
