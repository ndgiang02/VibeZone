import {
  ChartNoAxesColumn,
  Compass,
  Download,
  Heart,
  LibraryBig,
  History,
  House,
  Mic2,
  Radio,
  Sparkles,
  Trophy,
  type LucideIcon,
} from "lucide-react";

/** Only routes that exist. Add an item in the same change that ships its page. */
export const primaryNav = [
  { href: "/", key: "home", icon: House },
  { href: "/discover", key: "discover", icon: Compass },
  { href: "/library", key: "library", icon: LibraryBig },
  { href: "/studio", key: "studio", icon: Mic2 },
] as const satisfies readonly {
  href: string;
  key: "home" | "discover" | "library" | "studio";
  icon: LucideIcon;
}[];

export type UpcomingKey = "yourFm" | "forYou" | "charts" | "favourites" | "ranking" | "history" | "downloads";

/**
 * Planned destinations without a page yet. Rendered as disabled "Soon" entries (never links), so the
 * sidebar shows the roadmap without pretending the feature works. Move an item to `primaryNav` (or a
 * real section) in the same change that ships its page.
 */
export const upcomingMenu = [
  { key: "yourFm", icon: Radio },
  { key: "forYou", icon: Sparkles },
  { key: "charts", icon: ChartNoAxesColumn },
] as const satisfies readonly { key: UpcomingKey; icon: LucideIcon }[];

export const upcomingLibrary = [
  { key: "favourites", icon: Heart },
  { key: "ranking", icon: Trophy },
  { key: "history", icon: History },
  { key: "downloads", icon: Download },
] as const satisfies readonly { key: UpcomingKey; icon: LucideIcon }[];
