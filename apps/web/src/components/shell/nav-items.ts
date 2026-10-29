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

export type NavKey =
  "home" | "discover" | "library" | "studio" | "charts" | "favourites" | "history" | "moderation" | "admin";

export type NavItem = { href: string; key: NavKey; icon: LucideIcon };

/** Only routes that exist. Add an item in the same change that ships its page. Also the mobile tab bar. */
export const primaryNav = [
  { href: "/", key: "home", icon: House },
  { href: "/discover", key: "discover", icon: Compass },
  { href: "/library", key: "library", icon: LibraryBig },
  { href: "/studio", key: "studio", icon: Mic2 },
] as const satisfies readonly NavItem[];

/** Sidebar-only destinations under "Menu". */
export const secondaryMenu = [
  { href: "/charts", key: "charts", icon: ChartNoAxesColumn },
] as const satisfies readonly NavItem[];

/** Sidebar "Library" section. */
export const libraryNav = [
  { href: "/liked", key: "favourites", icon: Heart },
  { href: "/history", key: "history", icon: History },
] as const satisfies readonly NavItem[];

export type UpcomingKey = "yourFm" | "forYou" | "ranking" | "downloads";

/** Hidden for now: the "Soon" entries cluttered the sidebar. Flip to show the roadmap again. */
export const showUpcoming = false;

/**
 * Planned destinations without a page yet. Rendered as disabled "Soon" entries (never links), so the
 * sidebar shows the roadmap without pretending the feature works. Move an item to a real list in the
 * same change that ships its page.
 */
export const upcomingMenu = [
  { key: "yourFm", icon: Radio },
  { key: "forYou", icon: Sparkles },
] as const satisfies readonly { key: UpcomingKey; icon: LucideIcon }[];

export const upcomingLibrary = [
  { key: "ranking", icon: Trophy },
  { key: "downloads", icon: Download },
] as const satisfies readonly { key: UpcomingKey; icon: LucideIcon }[];
