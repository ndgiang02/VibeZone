#!/usr/bin/env bash
# Applies EF Core migrations to the database configured via ConnectionStrings__Database
# (environment) or the API user-secrets (scripts/setup-dev-secrets.sh).
# Explicit by design: the API never migrates on startup.
#
#   bash scripts/db-migrate.sh            # apply all pending migrations
#   bash scripts/db-migrate.sh <Name>     # migrate to (or roll back to) a named migration
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet tool restore >/dev/null
dotnet ef database update ${1:+"$1"} \
  --project src/VibeZone.Migrations \
  --startup-project src/VibeZone.Migrations
