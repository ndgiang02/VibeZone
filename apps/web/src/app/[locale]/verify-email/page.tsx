import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AuthCard } from "@/features/auth/auth-card";
import { VerifyEmail } from "@/features/auth/verify-email";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.verify");
  return { title: t("title"), robots: { index: false } };
}

export default async function VerifyEmailPage({ searchParams }: PageProps<"/[locale]/verify-email">) {
  const t = await getTranslations("auth.verify");
  const { userId, token } = await searchParams;
  return (
    <AuthCard title={t("title")}>
      <VerifyEmail
        userId={typeof userId === "string" ? userId : undefined}
        token={typeof token === "string" ? token : undefined}
      />
    </AuthCard>
  );
}
