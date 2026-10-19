"use client";

import { useEffect } from "react";
import { usePathname, useRouter } from "@/i18n/navigation";
import { useMe } from "./api";

/**
 * Client-side gate for pages that need a signed-in user: anonymous visitors are sent to the login page
 * and come back afterwards. This is UX only — the API authorizes every request itself.
 */
export function useRequireUser() {
  const me = useMe();
  const router = useRouter();
  const pathname = usePathname();

  useEffect(() => {
    if (me.data === null) {
      router.replace({ pathname: "/login", query: { next: pathname } });
    }
  }, [me.data, pathname, router]);

  return me;
}
