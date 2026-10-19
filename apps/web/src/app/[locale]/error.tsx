"use client";

import { useTranslations } from "next-intl";
import { useEffect } from "react";
import { ErrorState } from "@/components/states/states";

export default function RouteError({ error, retry }: { error: unknown; retry: () => void }) {
  const t = useTranslations();

  useEffect(() => {
    // Client-side error reporting hooks in with observability (M5).
    console.error(error);
  }, [error]);

  return (
    <div className="mx-auto max-w-xl py-16">
      <ErrorState
        title={t("routeError.title")}
        description={t("routeError.body")}
        onRetry={retry}
        retryLabel={t("common.retry")}
      />
    </div>
  );
}
