#!/usr/bin/env bash
# Deploys one commit on the production host (ADR-0014). Runs ON the server, as root, from the repository
# checkout at /opt/vibezone — normally via SSM Run Command from .github/workflows/deploy.yml, which first
# checks out the commit (so this script is never modified while it runs):
#
#   git -C /opt/vibezone fetch --quiet origin && git -C /opt/vibezone checkout --quiet --detach <sha> \
#     && /opt/vibezone/scripts/deploy/deploy.sh <sha>
#
# Steps: write .env from SSM Parameter Store (/vibezone/prod/*) → pull the signed images for <sha> →
# migrate + start (`up --wait` fails the deploy if any container is unhealthy) → prune old images.
# Rollback = run the same thing with an older sha (migrations must be backward compatible; operations.md).
set -euo pipefail

sha="${1:?usage: deploy.sh <full git sha>}"
[[ "$sha" =~ ^[0-9a-f]{40}$ ]] || { echo "expected a full 40-character commit sha" >&2; exit 2; }

app_dir="${VIBEZONE_DIR:-/opt/vibezone}"
param_path="${VIBEZONE_PARAM_PATH:-/vibezone/prod}"
cd "$app_dir"

head="$(git rev-parse HEAD)"
[[ "$head" == "$sha" ]] || { echo "checkout is at $head, expected $sha" >&2; exit 2; }

if [[ -z "${AWS_REGION:-}" ]]; then
  token="$(curl -fsS -X PUT http://169.254.169.254/latest/api/token -H 'X-aws-ec2-metadata-token-ttl-seconds: 60')"
  AWS_REGION="$(curl -fsS -H "X-aws-ec2-metadata-token: $token" http://169.254.169.254/latest/meta-data/placement/region)"
  export AWS_REGION
fi

echo "==> Writing .env from SSM $param_path"
umask 077
tmp_env="$(mktemp .env.XXXXXX)"
trap 'rm -f "$tmp_env"' EXIT
aws ssm get-parameters-by-path --region "$AWS_REGION" --path "$param_path" --recursive --with-decryption \
  --query 'Parameters[].[Name,Value]' --output text |
  while IFS=$'\t' read -r name value; do
    printf '%s=%s\n' "${name##*/}" "$value"
  done >"$tmp_env"
printf 'IMAGE_TAG=%s\n' "$sha" >>"$tmp_env"
mv "$tmp_env" .env
trap - EXIT

compose=(docker compose -f docker-compose.prod.yml --env-file .env)

echo "==> Pulling images for $sha"
"${compose[@]}" pull --quiet

echo "==> Migrating and starting"
"${compose[@]}" up -d --wait --wait-timeout 300 --remove-orphans

previous="$(cat .deployed 2>/dev/null || true)"
echo "$sha" >.deployed
echo "==> Deployed $sha (previous: ${previous:-none})"

docker image prune -af --filter "until=168h" >/dev/null
