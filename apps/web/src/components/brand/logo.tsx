import { cn } from "@/lib/utils";

/** VibeZone mark: three rising equaliser bars in a rounded tile. */
export function LogoMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 32 32" aria-hidden="true" className={cn("size-8", className)}>
      <rect width="32" height="32" rx="9" className="fill-primary" />
      <rect x="8" y="15" width="4" height="9" rx="2" className="fill-primary-foreground" />
      <rect x="14" y="10" width="4" height="14" rx="2" className="fill-primary-foreground" />
      <rect x="20" y="6" width="4" height="18" rx="2" className="fill-primary-foreground" />
    </svg>
  );
}

export function Logo({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2.5", className)}>
      <LogoMark />
      <span className="text-lg font-semibold tracking-tight">
        Vibe<span className="text-primary">Zone</span>
      </span>
    </span>
  );
}
