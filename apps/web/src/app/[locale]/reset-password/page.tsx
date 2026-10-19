import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AuthCard } from "@/features/auth/auth-card";
import { ResetPasswordForm } from "@/features/auth/reset-password-form";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.reset");
  return { title: t("title"), robots: { index: false } };
}

export default async function ResetPasswordPage({ searchParams }: PageProps<"/[locale]/reset-password">) {
  const t = await getTranslations("auth.reset");
  const { email, token } = await searchParams;
  return (
    <AuthCard title={t("title")}>
      <ResetPasswordForm
        email={typeof email === "string" ? email : undefined}
        token={typeof token === "string" ? token : undefined}
      />
    </AuthCard>
  );
}
