#!/usr/bin/env bash
# Nightly PostgreSQL backup on the production host (ADR-0014): pg_dump (custom format) streamed to S3.
# Runs as root from /etc/cron.d/vibezone-backup (docs/runbooks/deploy-aws.md). Retention is an S3 lifecycle
# rule on the backup bucket, not this script. A backup counts only after a restore drill (operations.md).
set -euo pipefail

app_dir="${VIBEZONE_DIR:-/opt/vibezone}"
cd "$app_dir"

# Read single keys from the deploy-written .env; never `source` it (values such as EMAIL_FROM aren't shell-safe).
env_get() { grep -m1 "^$1=" .env | cut -d= -f2- || true; }
bucket="$(env_get BACKUP_BUCKET)"
region="$(env_get AWS_REGION)"
image_tag="$(env_get IMAGE_TAG)"
db_user="$(env_get POSTGRES_USER)"
db_name="$(env_get POSTGRES_DB)"
: "${bucket:?BACKUP_BUCKET must be set in SSM /vibezone/prod}"
: "${region:?AWS_REGION must be set in SSM /vibezone/prod}"

key="db/$(date -u +%Y-%m-%dT%H%M%SZ)-${image_tag:0:12}.dump"
docker compose -f docker-compose.prod.yml --env-file .env exec -T postgres \
  pg_dump --format=custom --no-owner -U "${db_user:-vibezone}" -d "${db_name:-vibezone}" |
  aws s3 cp - "s3://$bucket/$key" --region "$region" --only-show-errors

echo "backup written: s3://$bucket/$key"
