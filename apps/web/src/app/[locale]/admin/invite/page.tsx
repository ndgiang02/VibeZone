import type { Metadata } from "next";
import { getTranslations } from "next-intl/server";
import { InviteStaffView } from "@/features/admin/invite-staff-view";

export async function generateMetadata(): Promise<Metadata> {
  const t = await getTranslations("admin.invite");
  return { title: t("title") };
}

export default function AdminInvitePage() {
  return (
    <div className="mx-auto max-w-xl">
      <InviteStaffView />
    </div>
  );
}
