import { House, Mic2, type LucideIcon } from "lucide-react";

/** Only routes that exist. Add an item in the same change that ships its page. */
export const primaryNav = [
  { href: "/", key: "home", icon: House },
  { href: "/studio", key: "studio", icon: Mic2 },
] as const satisfies readonly {
  href: string;
  key: "home" | "studio";
  icon: LucideIcon;
}[];
