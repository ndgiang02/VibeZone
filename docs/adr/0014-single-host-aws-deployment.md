# ADR-0014: First production deployment — one EC2 host, S3, SES; CI-built signed images; manual deploy via SSM

- Status: Accepted
- Date: 2026-10-29
- Related: ADR-0002 (cookies behind a TLS proxy), ADR-0003 (object storage), ADR-0011 (API domain), ADR-0001 (M6)

## Context

The owner has 100 USD of AWS credit that expires in about two months, and wants a first public deployment.
The stack already runs fully in containers (Compose `app` profile). Budget is ~45–50 USD/month. A load balancer
(~20 USD/month), RDS (~15) and Redis (~12) would consume the credit in ~5 weeks, and the code is single-instance
today (in-memory rate limits and email queue, SignalR without a backplane), so auto scaling is out of scope (M6).

## Decision

- **Topology**: one EC2 **t4g.medium** (Graviton, 4 GB, Ubuntu 24.04) with Elastic IP, running
  `docker-compose.prod.yml`: Postgres (container + EBS volume), migrator, API, worker, web, Caddy.
  Only Caddy publishes ports (80/443); it terminates TLS with Let's Encrypt for `WEB_DOMAIN` and `API_DOMAIN`.
- **Media**: AWS S3 private bucket, accessed through the **instance role** (no static keys; `StorageOptions` falls back
  to the SDK credential chain when no keys are configured). IMDSv2 hop limit 2 so containers can reach it.
- **Email**: Amazon SES SMTP (StartTls 587), sending identity = the domain.
- **Secrets**: SSM Parameter Store SecureStrings under `/vibezone/prod/`; the deploy writes `.env` (mode 600).
- **Images**: CI publishes arm64 images to GHCR on every green `main` (after e2e), tagged with the commit sha,
  with SBOM + provenance attestations, keyless cosign signatures and a Trivy gate (fixable critical CVEs).
- **Deploy**: `.github/workflows/deploy.yml`, manual (`workflow_dispatch`) with the `production` environment as the
  approval step. It verifies the signatures, assumes an AWS role via **GitHub OIDC** (no stored AWS keys) and runs
  `scripts/deploy/deploy.sh <sha>` on the host through **SSM Run Command** (no inbound SSH). `up --wait` fails the
  deploy if a container is unhealthy. **Rollback** = deploy an older sha.
- **Backups**: nightly `pg_dump` to a separate S3 bucket (14-day lifecycle); restore drill per runbook.

## Consequences

- + ~40 USD/month; whole stack moves to any Docker host later (e.g. Lightsail/VPS when the credit ends).
- + No long-lived AWS credentials anywhere (OIDC for CI, instance role on the host); no SSH port open.
- − Single point of failure: an instance or AZ outage is downtime (EC2 auto-recovery only); deploys restart
  containers (seconds of unavailability; no blue/green).
- − Postgres on the host: backups and restore drills are our job; no point-in-time recovery.
- − Rollback across a schema change requires backward-compatible (expand/contract) migrations.
- − Native arm64 runners need a public repository (or `vars.ARM_RUNNER` pointing at another runner).
- Revisit (new ADR) when traffic is measured to need more than vertical scaling: RDS, ALB + ASG, Redis backplane.
