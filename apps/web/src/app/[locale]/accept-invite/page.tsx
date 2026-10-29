import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AcceptInviteForm } from "@/features/auth/accept-invite-form";
import { AuthCard } from "@/features/auth/auth-card";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.invite");
  return { title: t("title"), robots: { index: false } };
}

export default async function AcceptInvitePage({ searchParams }: PageProps<"/[locale]/accept-invite">) {
  const t = await getTranslations("auth.invite");
  const { userId, token } = await searchParams;
  return (
    <AuthCard title={t("title")} subtitle={t("subtitle")}>
      <AcceptInviteForm
        userId={typeof userId === "string" ? userId : undefined}
        token={typeof token === "string" ? token : undefined}
      />
    </AuthCard>
  );
}
