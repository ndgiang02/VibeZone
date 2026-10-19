#!/usr/bin/env bash
# Copies local credentials from .env into the API's .NET user-secrets store
# (%APPDATA%\Microsoft\UserSecrets\vibezone-api-dev — outside the repository).
# Shared by the API and worker (same UserSecretsId); used by `dotnet run` (Development) and `dotnet ef`.
#
#   bash scripts/setup-dev-secrets.sh
set -euo pipefail
cd "$(dirname "$0")/.."

[[ -f .env ]] || { echo "Missing .env — run: cp .env.example .env" >&2; exit 1; }
set -a
# shellcheck disable=SC1091
source .env
set +a

project=src/VibeZone.Api
conn="Host=localhost;Port=${POSTGRES_PORT:-5432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

dotnet user-secrets set "ConnectionStrings:Database" "$conn" --project "$project" >/dev/null
# Least-privilege bucket credentials created by minio-init (docker compose --profile media).
dotnet user-secrets set "Storage:AccessKey" "${S3_ACCESS_KEY}" --project "$project" >/dev/null
dotnet user-secrets set "Storage:SecretKey" "${S3_SECRET_KEY}" --project "$project" >/dev/null
dotnet user-secrets set "Storage:Bucket" "${S3_BUCKET_MEDIA:-vibezone-media}" --project "$project" >/dev/null
echo "user-secrets: database and storage settings set for $project and the worker (values not printed)."
