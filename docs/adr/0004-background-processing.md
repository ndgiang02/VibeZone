# ADR-0004: PostgreSQL-backed job queue and transactional outbox

- Status: Accepted (M0)
- Date: 2026-10-19

## Context

Media processing (probe, transcode, waveform) is slow, CPU-heavy and must be retried and
cancellable. Domain events (likes, follows, comments, transcoded) must reliably trigger
notifications and analytics. Kafka is explicitly out of scope for the MVP.

## Decision

- **Jobs**: `media.upload_jobs` doubles as the processing queue. The worker claims work with
  `SELECT … FOR UPDATE SKIP LOCKED`, sets a lease (`locked_by`, `locked_until`), heartbeats while
  running, and releases on completion. Expired leases are re-claimable. Retries use exponential
  backoff up to `max_attempts`, then `failed`.
- **Concurrency limits**: per-worker FFmpeg concurrency (default 2), per-job timeout, CPU/memory
  limits on the worker container.
- **FFmpeg execution**: `ProcessStartInfo` with `ArgumentList`, no shell, server-generated temp
  paths, fixed argument templates; stdout/stderr bounded.
- **Outbox**: integration events written to `infra.outbox_messages` in the same transaction as the
  state change; the worker dispatches them to in-process handlers. Events carry `type` + `version`;
  handlers are idempotent (dedupe by message id).

## Consequences

- + No extra infrastructure; exactly-once *effects* via idempotent handlers.
- − Throughput bounded by Postgres; acceptable until measured otherwise. The outbox contract is
  the seam for moving to Kafka in M6 (versioned events, consumer idempotency, DLQ).
