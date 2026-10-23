/**
 * UI showcase content (sample playlists, artists and songs) fills sections the API can't serve yet so the
 * design can be previewed. It is on in `next dev` and off in production builds unless
 * NEXT_PUBLIC_SHOWCASE=true. Showcase items are always badged "Demo", never playable, never written
 * anywhere and never shown in place of an API error.
 */
export const showcaseEnabled =
  process.env.NODE_ENV === "development" || process.env.NEXT_PUBLIC_SHOWCASE === "true";
