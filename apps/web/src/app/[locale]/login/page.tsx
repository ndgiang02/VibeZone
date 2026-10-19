import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AuthCard } from "@/features/auth/auth-card";
import { LoginForm } from "@/features/auth/login-form";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("auth.login");
  return { title: t("title"), robots: { index: false } };
}

export default async function LoginPage({ searchParams }: PageProps<"/[locale]/login">) {
  const t = await getTranslations("auth.login");
  const { next } = await searchParams;
  return (
    <AuthCard title={t("title")} subtitle={t("subtitle")}>
      <LoginForm next={typeof next === "string" ? next : undefined} />
    </AuthCard>
  );
}
