/**
 * Sample content for the UI showcase (see ./showcase.ts). Fictional names, decorative artwork from
 * /public/artwork, no audio, no play counts or follower numbers.
 */

export type DemoKind = "playlist" | "radio" | "album" | "mix";

export type DemoItem = {
  id: string;
  title: string;
  /** Artist line for songs; omitted for playlists/radios (the kind label is shown instead). */
  artist?: string;
  kind: DemoKind;
  artwork: string;
};

export type DemoArtist = { id: string; name: string };

const art = (name: string) => `/artwork/${name}.svg`;

export const demoMostPlayed: DemoItem[] = [
  { id: "mp-lofi-beats", title: "Lofi Beats", kind: "playlist", artwork: art("rain-window") },
  { id: "mp-chill-radio", title: "Chill Radio", kind: "radio", artwork: art("palm-sunset") },
  {
    id: "mp-night-shift",
    title: "Night Shift",
    artist: "Neon Harbor",
    kind: "album",
    artwork: art("equalizer"),
  },
  { id: "mp-my-time", title: "My Time", kind: "playlist", artwork: art("forest-mist") },
  { id: "mp-summer-mix", title: "Summer Mix", kind: "mix", artwork: art("pop-blobs") },
  { id: "mp-vinyl-hours", title: "Vinyl Hours", kind: "radio", artwork: art("vinyl") },
];

export const demoHotSongs: DemoItem[] = [
  {
    id: "hs-midnight-signal",
    title: "Midnight Signal",
    artist: "Neon Harbor",
    kind: "album",
    artwork: art("night-drive"),
  },
  {
    id: "hs-bass-theory",
    title: "Bass Theory",
    artist: "Kilo Pulse",
    kind: "album",
    artwork: art("bass-rings"),
  },
  {
    id: "hs-nonstop-2026",
    title: "Nonstop 2026",
    artist: "Various Artists",
    kind: "mix",
    artwork: art("disco-ball"),
  },
  {
    id: "hs-paper-moon",
    title: "Paper Moon",
    artist: "Lumi & The Tides",
    kind: "album",
    artwork: art("lofi-room"),
  },
  {
    id: "hs-afterglow",
    title: "Afterglow",
    artist: "Saigon Static",
    kind: "album",
    artwork: art("chill-sunset"),
  },
  {
    id: "hs-gridrunner",
    title: "Gridrunner",
    artist: "Vector Bloom",
    kind: "album",
    artwork: art("neon-grid"),
  },
  { id: "hs-low-tide", title: "Low Tide", artist: "Mara Vale", kind: "album", artwork: art("ocean-waves") },
  {
    id: "hs-strobe-garden",
    title: "Strobe Garden",
    artist: "Kilo Pulse",
    kind: "album",
    artwork: art("edm-stage"),
  },
];

export const demoRecommended: DemoItem[] = [
  { id: "rc-chill-vibes", title: "Chill Vibes", kind: "playlist", artwork: art("aurora") },
  { id: "rc-night-drive", title: "Night Drive", kind: "playlist", artwork: art("desert-road") },
  { id: "rc-edm-festival", title: "EDM Festival", kind: "mix", artwork: art("edm-stage") },
  { id: "rc-bass-boost", title: "Bass Boost", kind: "mix", artwork: art("bass-rings") },
  { id: "rc-lofi-focus", title: "Lo-Fi Focus", kind: "playlist", artwork: art("lofi-room") },
];

export const demoPlaylists: DemoItem[] = [
  { id: "pl-chill", title: "Chill 🔥", kind: "playlist", artwork: art("chill-sunset") },
  { id: "pl-mood-booster", title: "Mood Booster 🔥", kind: "playlist", artwork: art("pop-blobs") },
  { id: "pl-lofi", title: "Lo-Fi 🥰", kind: "playlist", artwork: art("lofi-room") },
  { id: "pl-drive", title: "Drive 🚗", kind: "playlist", artwork: art("night-drive") },
  { id: "pl-edm-mix", title: "EDM Mix", kind: "mix", artwork: art("edm-stage") },
  { id: "pl-nonstop-mix", title: "Nonstop Mix", kind: "mix", artwork: art("disco-ball") },
];

export const demoArtists: DemoArtist[] = [
  { id: "ar-neon-harbor", name: "Neon Harbor" },
  { id: "ar-mara-vale", name: "Mara Vale" },
  { id: "ar-kilo-pulse", name: "Kilo Pulse" },
  { id: "ar-saigon-static", name: "Saigon Static" },
  { id: "ar-lumi-tides", name: "Lumi & The Tides" },
];

/** Case- and accent-insensitive match used by the local search. */
export function matchesQuery(query: string, ...fields: Array<string | null | undefined>) {
  const normalise = (value: string) =>
    value
      .normalize("NFD")
      .replace(/\p{Diacritic}/gu, "")
      .toLowerCase();
  const needle = normalise(query.trim());
  return needle === "" || fields.some((field) => field != null && normalise(field).includes(needle));
}
