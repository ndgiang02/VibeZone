import type { ReactNode } from "react";

/**
 * Re-mounted on every navigation (unlike the layout), so each page eases in. Opacity/translate only and
 * no fill-mode: nothing stays transformed after the animation (fixed/sticky children behave normally).
 * Reduced-motion users get it instantly via the global rule in globals.css.
 */
export default function Template({ children }: { children: ReactNode }) {
  return <div className="animate-in duration-300 ease-out fade-in slide-in-from-bottom-1">{children}</div>;
}
