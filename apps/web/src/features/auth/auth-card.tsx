import type { ReactNode } from "react";
import { LogoMark } from "@/components/brand/logo";

export function AuthCard({
  title,
  subtitle,
  children,
}: {
  title: string;
  subtitle?: string;
  children: ReactNode;
}) {
  return (
    <div className="mx-auto flex w-full max-w-md flex-col gap-6 py-6 md:py-12">
      <div className="flex flex-col items-center gap-3 text-center">
        <LogoMark className="size-10" />
        <h1 className="text-title font-semibold">{title}</h1>
        {subtitle ? <p className="text-sm text-muted-foreground">{subtitle}</p> : null}
      </div>
      <div className="rounded-2xl border bg-surface p-6 shadow-card">{children}</div>
    </div>
  );
}
