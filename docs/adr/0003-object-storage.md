# ADR-0003: S3 API abstraction; MinIO community build for local development

- Status: Accepted (M0)
- Date: 2026-10-19

## Context

The brief specifies MinIO locally and S3-compatible storage in deployment. During M0 we found
that upstream MinIO no longer publishes public images: `minio/minio` is gone from Docker Hub and
`quay.io/minio/*` requires authentication (HTTP 401). Options evaluated: `pgsty/minio`
(community build of the MinIO source, AGPLv3), SeaweedFS, RustFS, `bitnamilegacy/minio` (frozen).

## Decision

- The application talks to storage only through an `IObjectStorage` port implemented with the
  AWS S3 SDK (path-style addressing configurable). No MinIO-specific APIs in application code.
- Local development uses **`pgsty/minio:RELEASE.2026-08-04T00-00-00Z`** (pinned), chosen by the
  project owner. Its image includes `mc`, used by the `minio-init` bootstrap service.
- Bucket `S3_BUCKET_MEDIA` is private; the app uses a bucket-scoped user (`vibezone-media-rw`
  policy), never root credentials.
- Production uses a managed S3-compatible service (AWS S3, Cloudflare R2, etc.), decided in M5.

## Consequences

- + Storage provider can change without touching domain/application code.
- − Local image depends on a third-party maintainer; pinned tag, and integration tests must also
  pass against a second S3 implementation before production (M5).
- AGPLv3 applies to the MinIO server we run locally, not to VibeZone code (we do not modify or
  distribute it).
