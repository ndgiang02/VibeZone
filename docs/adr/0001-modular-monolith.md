# ADR-0001: Modular monolith with 8 code modules and a shared database

- Status: Accepted (M0)
- Date: 2026-10-19

## Context

The brief lists 15 functional areas and asks for a modular monolith with clear
Domain/Application/Infrastructure/API boundaries that can later be extracted into services.
A literal mapping (15 modules × 4 projects = 60 projects) adds build time and ceremony without
improving boundaries at this stage — several areas share an aggregate (Artists/Tracks/Albums)
or a lifecycle (Uploads/Media Processing/Playback).

## Decision

- **8 code modules**: Identity, Catalog, Playlists, Media, Social, Analytics, Notifications,
  Moderation (mapping in [overview.md](../architecture/overview.md#3-backend-modular-monolith)).
- **One project per module** (`VibeZone.Modules.<Name>`) with `Domain/`, `Application/`,
  `Infrastructure/`, `Endpoints/` folders, plus a small `VibeZone.Modules.<Name>.Contracts`
  project only when another module needs to call it. Layering and cross-module rules are enforced
  by architecture tests (NetArchTest or equivalent) rather than project references alone.
- **One PostgreSQL database, one schema per module, one `AppDbContext`** composed from each
  module's `IEntityTypeConfiguration`s, one migration history.
- Two hosts: `VibeZone.Api` and `VibeZone.Worker`.

## Consequences

- + Real foreign keys and transactions across modules where they matter (likes → tracks).
- + One migration stream is simple to review and deploy.
- − Extracting a module later requires replacing cross-schema FKs with eventual consistency.
  Mitigation: cross-module FKs only target stable identity keys; no cross-module joins in
  queries outside read models; communication through Contracts/outbox events.
- − Folder-level layering is weaker than project-level; architecture tests are mandatory in CI.
- A module may be split into multiple projects later if it grows (e.g. Media → Uploads + Processing).
