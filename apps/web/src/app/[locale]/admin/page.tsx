import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { AdminView } from "@/features/admin/admin-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("admin");
  return { title: t("title") };
}

export default function AdminPage() {
  return (
    <div className="mx-auto max-w-7xl">
      <AdminView />
    </div>
  );
}
