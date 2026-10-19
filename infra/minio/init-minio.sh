#!/usr/bin/env bash
# Idempotent MinIO bootstrap for local development:
#   1. private media bucket (no anonymous access)
#   2. bucket-scoped policy
#   3. application user bound to that policy
# The image ships only coreutils (no grep/sed), so this sticks to bash builtins.
set -euo pipefail

: "${MINIO_ROOT_USER:?}" "${MINIO_ROOT_PASSWORD:?}" "${S3_BUCKET_MEDIA:?}" "${S3_ACCESS_KEY:?}" "${S3_SECRET_KEY:?}"

POLICY_NAME="vibezone-media-rw"
POLICY_FILE="$(mktemp)"
trap 'rm -f "$POLICY_FILE"' EXIT

mc alias set local http://minio:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" >/dev/null

mc mb --ignore-existing "local/${S3_BUCKET_MEDIA}"
mc anonymous set none "local/${S3_BUCKET_MEDIA}"

cat >"$POLICY_FILE" <<JSON
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": ["s3:GetBucketLocation", "s3:ListBucket", "s3:ListBucketMultipartUploads"],
      "Resource": ["arn:aws:s3:::${S3_BUCKET_MEDIA}"]
    },
    {
      "Effect": "Allow",
      "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject", "s3:AbortMultipartUpload", "s3:ListMultipartUploadParts"],
      "Resource": ["arn:aws:s3:::${S3_BUCKET_MEDIA}/*"]
    }
  ]
}
JSON

mc admin policy create local "$POLICY_NAME" "$POLICY_FILE" >/dev/null
# 'user add' on an existing user updates its secret key.
mc admin user add local "$S3_ACCESS_KEY" "$S3_SECRET_KEY" >/dev/null

user_info="$(mc admin user info local "$S3_ACCESS_KEY" --json)"
attached=""
if [[ "$user_info" =~ \"policyName\":\"([^\"]*)\" ]]; then attached=",${BASH_REMATCH[1]},"; fi
if [[ "$attached" != *",$POLICY_NAME,"* ]]; then
  mc admin policy attach local "$POLICY_NAME" --user "$S3_ACCESS_KEY" >/dev/null
fi

echo "minio-init: bucket '${S3_BUCKET_MEDIA}' is private; user '${S3_ACCESS_KEY}' has policy '${POLICY_NAME}'."
