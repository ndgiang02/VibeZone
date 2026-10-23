# ADR-0013: The artist profile is the account's public face

- Status: Accepted
- Date: 2026-10-23
- Related: ADR-0001 (modules), ADR-0009 (artist images, addendum superseded in part), ADR-0012 (follows target artists)

## Context

Until now an account and its artist profile were two identities. The account (Identity) had a handle, a display
name and a picture; the artist profile (Catalog) had its own name, its own URL slug and its own avatar, all
entered again in "Become an artist". One person could show up under two names and two pictures: the account
name and picture on comments, playlists and notifications, the artist name and avatar on tracks, albums,
search and the artist page. The owner found this confusing and hard to keep under control, and asked for one
identity.

## Decision

- **The account is the only source** of the public name, URL and picture. The artist profile's `name` is the
  account's display name, its `slug` is the account handle (immutable), and its picture is the account picture
  (`/users/{id}/avatar/...`). The profile only adds what an artist has on top of that: bio and banner.
- **Becoming an artist asks for nothing the account already has**: `POST /me/artist` takes `{ bio }` only,
  creates the profile from the account and grants the Artist role. There are still no public pages for
  accounts that aren't artists.
- **Catalog keeps copies** (`artists.slug`, `name`, `avatar_id`) so search (generated `search_vector`),
  the unique slug index and every track/album query keep working without a cross-module join. Identity keeps
  them current: it defines `IPublicProfileListener` in `Identity.Contracts`, Catalog implements it, and Identity
  calls it when the display name or picture changes. The listener only stages the change, so the copy is
  written **in the same transaction** as the account change. It can't drift, and there's no outbox delay.
- **Artist avatars are removed.** `PUT/DELETE /me/artist/avatar` and `GET /artists/{id}/avatar/...` no longer
  exist; the banner endpoints become `/me/artist/banner` and `/artists/{id}/banner/{size}`.
- Migration `UnifyArtistWithAccount` rewrites existing profiles from their accounts (slug → handle, name →
  display name, avatar → account picture) and deletes the artist-avatar rows. The previous names, slugs and
  avatars are not kept; old `/artists/{old-slug}` links stop working.

## Consequences

- One name and one picture everywhere. Changing them in Settings changes the artist page, tracks, albums and
  search at once.
- An artist can't have a stage name that differs from the account's display name. If that's ever needed, it
  becomes a deliberate "public name" field on the account, not a second identity.
- The Identity → Catalog notification goes through an interface Identity owns (dependency inversion): Identity
  does not reference Catalog, and the architecture tests still hold.
- Artist-avatar objects that existed before the migration are left as orphans in object storage (no row
  references them). They should be swept when storage cleanup exists; locally `docker compose down -v` clears
  them.
