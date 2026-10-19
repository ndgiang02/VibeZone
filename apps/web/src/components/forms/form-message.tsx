import { CheckCircle2, AlertTriangle } from "lucide-react";
import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/** Form-level feedback. Errors use role="alert" so screen readers announce them immediately. */
export function FormMessage({
  tone,
  children,
  className,
}: {
  tone: "error" | "success";
  children: ReactNode;
  className?: string;
}) {
  const Icon = tone === "error" ? AlertTriangle : CheckCircle2;
  return (
    <div
      role={tone === "error" ? "alert" : "status"}
      className={cn(
        "flex items-start gap-2 rounded-lg border px-3 py-2.5 text-sm",
        tone === "error"
          ? "border-destructive/30 bg-destructive/10 text-foreground"
          : "border-success/30 bg-success/10 text-foreground",
        className,
      )}
    >
      <Icon
        className={cn("mt-0.5 size-4 shrink-0", tone === "error" ? "text-destructive" : "text-success")}
        aria-hidden="true"
      />
      <div>{children}</div>
    </div>
  );
}
