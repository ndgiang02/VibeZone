# Runbook: AWS deployment (ADR-0014)

Single EC2 host + S3 + SES, deployed by `.github/workflows/deploy.yml`. Region examples use `ap-southeast-1`
(Singapore); replace `<ACCOUNT_ID>`, `<REGION>`, domains and bucket names with yours.

Status: **prepared, not yet executed.** Files are validated locally (compose config, `caddy validate`,
shellcheck, actionlint); no AWS resource exists yet.

## 0. Account safety (first)

1. Root user: MFA on, no access keys. Daily work with an IAM Identity Center / IAM user that has MFA.
2. Billing → Budgets: monthly cost budget 100 USD with alerts at 25 / 50 / 75 / 90 %.
3. Billing → Credits: note the **expiry date** and the eligible services. If the account is on the *Free plan*,
   decide before expiry whether to upgrade (otherwise AWS closes the account) — see §9.

## 1. One-time AWS setup

| Resource | Setting |
|---|---|
| S3 bucket `vibezone-media-prod` | Block all public access, SSE-S3, versioning off |
| S3 bucket `vibezone-backups-prod` | Block all public access, lifecycle: expire objects after 14 days |
| IAM role `vibezone-ec2` (instance profile) | Policies below + `AmazonSSMManagedInstanceCore` |
| EC2 | t4g.medium, Ubuntu 24.04 arm64, 30 GB gp3, the role above, **IMDSv2 required, hop limit 2** |
| Security group | inbound 80/tcp, 443/tcp, 443/udp from anywhere; **no 22** (use SSM Session Manager) |
| Elastic IP | attach to the instance (an unattached EIP is billed) |
| EC2 auto-recovery | on (default for t4g); CloudWatch alarm CPU > 80 % for 15 min → email |
| SES | verify the domain (DKIM records), create SMTP credentials, **request production access** (sandbox only mails verified addresses) |
| DNS | `A` records `WEB_DOMAIN` and `API_DOMAIN` → Elastic IP (before the first deploy, or Let's Encrypt fails) |

Instance role inline policy:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    { "Effect": "Allow", "Action": ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"],
      "Resource": "arn:aws:s3:::vibezone-media-prod/*" },
    { "Effect": "Allow", "Action": "s3:ListBucket", "Resource": "arn:aws:s3:::vibezone-media-prod" },
    { "Effect": "Allow", "Action": "s3:PutObject", "Resource": "arn:aws:s3:::vibezone-backups-prod/db/*" },
    { "Effect": "Allow", "Action": "ssm:GetParametersByPath",
      "Resource": ["arn:aws:ssm:<REGION>:<ACCOUNT_ID>:parameter/vibezone/prod",
                   "arn:aws:ssm:<REGION>:<ACCOUNT_ID>:parameter/vibezone/prod/*"] }
  ]
}
```

(SecureStrings encrypted with the default `aws/ssm` key need no extra KMS permission.)

### GitHub → AWS (OIDC deploy role)

1. IAM → Identity providers → add OpenID Connect `https://token.actions.githubusercontent.com`, audience
   `sts.amazonaws.com`.
2. Role `vibezone-github-deploy`, trust policy limited to the `production` environment of this repository:

```json
{
  "Effect": "Allow",
  "Principal": { "Federated": "arn:aws:iam::<ACCOUNT_ID>:oidc-provider/token.actions.githubusercontent.com" },
  "Action": "sts:AssumeRoleWithWebIdentity",
  "Condition": {
    "StringEquals": {
      "token.actions.githubusercontent.com:aud": "sts.amazonaws.com",
      "token.actions.githubusercontent.com:sub": "repo:ndgiang02/VibeZone:environment:production"
    }
  }
}
```

3. Permissions: `ssm:SendCommand` on `arn:aws:ec2:<REGION>:<ACCOUNT_ID>:instance/<INSTANCE_ID>` and
   `arn:aws:ssm:<REGION>::document/AWS-RunShellScript`; `ssm:GetCommandInvocation` on `*`.

### Secrets (SSM Parameter Store)

One **SecureString** per key of `.env.production.example`, named `/vibezone/prod/<KEY>`, e.g.

```bash
aws ssm put-parameter --type SecureString --name /vibezone/prod/POSTGRES_PASSWORD --value "$(openssl rand -hex 24)"
```

Never put secret values in the repository, workflow logs or shell history on shared machines.

## 2. One-time host setup (SSM Session Manager shell)

```bash
sudo -i
apt-get update && apt-get install -y ca-certificates curl git unzip
curl -fsSL https://get.docker.com | sh                       # Docker Engine + Compose plugin
curl -fsSL "https://awscli.amazonaws.com/awscli-exe-linux-aarch64.zip" -o /tmp/awscli.zip \
  && unzip -q /tmp/awscli.zip -d /tmp && /tmp/aws/install
fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile \
  && echo '/swapfile none swap sw 0 0' >> /etc/fstab
git clone https://github.com/ndgiang02/VibeZone.git /opt/vibezone
echo '30 2 * * * root /opt/vibezone/scripts/deploy/backup-db.sh >> /var/log/vibezone-backup.log 2>&1' \
  > /etc/cron.d/vibezone-backup
```

The repository must be public (or the clone needs a read-only deploy key). GHCR packages
`vibezone-{api,migrator,worker,web}` must be **public** (Package settings → visibility) so the host pulls without a
token; they contain no secrets.

## 3. GitHub settings

- Settings → Environments → `production`: required reviewer = owner (public repos); deployment branches = `main`.
  Environment variables: `AWS_REGION`, `AWS_DEPLOY_ROLE_ARN`, `EC2_INSTANCE_ID`, `WEB_DOMAIN`, `API_DOMAIN`.
- Settings → Code security: enable CodeQL default setup, Dependabot alerts, secret scanning + push protection.
- Branch protection on `main`: require the CI checks.

## 4. Deploy

1. Push to `main` → CI green → `publish` pushes `ghcr.io/ndgiang02/vibezone-*:<sha>` (arm64, signed; runner
   options in [github-arm64-runner.md](github-arm64-runner.md)).
2. Actions → **Deploy** → Run workflow (empty sha = head of `main`) → approve.
3. The job verifies signatures, runs `deploy.sh` via SSM (writes `.env`, pulls, migrates, `up --wait`) and smoke-tests
   `https://WEB_DOMAIN/` and `https://API_DOMAIN/api/v1/genres`.
4. First deploy only: create the first admin (Session Manager shell):
   `cd /opt/vibezone && docker compose -f docker-compose.prod.yml --env-file .env exec api dotnet VibeZone.Api.dll grant-role <email> Admin`
   (the account must have registered and confirmed its email first).

## 5. Rollback

Run **Deploy** with the previous sha (`cat /opt/vibezone/.deployed` before deploying shows the current one; the
deploy log prints the previous one). Images are kept in GHCR. A rollback does **not** undo migrations — migrations
must stay backward compatible with the previous release (expand/contract, operations.md).

## 6. Backup & restore drill

- Nightly: `scripts/deploy/backup-db.sh` → `s3://vibezone-backups-prod/db/<timestamp>-<sha>.dump`.
- Before any destructive migration: run the script by hand.
- **Restore drill** (on a scratch database, never the live one):

```bash
aws s3 cp s3://vibezone-backups-prod/db/<file>.dump /tmp/restore.dump
cd /opt/vibezone && c="docker compose -f docker-compose.prod.yml --env-file .env"
$c exec -T postgres createdb -U vibezone vibezone_restore
$c exec -T postgres pg_restore -U vibezone -d vibezone_restore --no-owner < /tmp/restore.dump
$c exec -T postgres psql -U vibezone -d vibezone_restore -c 'select count(*) from identity.users;'
$c exec -T postgres dropdb -U vibezone vibezone_restore
```

Record the drill (date, file, row counts) in `docs/progress.md`; a backup is valid only after a drill.

## 7. Operations

- Logs: `docker compose -f docker-compose.prod.yml logs -f api worker` (JSON; no secrets by policy).
- Health: `docker compose -f docker-compose.prod.yml ps` (all `healthy`); API readiness covers DB + migrations.
- Disk: `docker system df`; `deploy.sh` prunes images older than 7 days.
- Bigger instance: stop → change instance type → start (~2 min, Elastic IP keeps the address).

## 8. Cost (approximate, check the AWS Pricing Calculator)

t4g.medium ~30–35 · EBS 30 GB ~3 · public IPv4 ~3.6 · S3/SES/transfer ~1–3 → **~40 USD/month**.

## 9. Before the credit expires

About a week before: either keep running (upgrade the plan; cost goes to the card) or move off AWS:
`backup-db.sh`, `aws s3 sync s3://vibezone-media-prod ./media`, then **delete** the instance, its EBS volume and
snapshots, the Elastic IP, both buckets, SES identity and the SSM parameters. The Compose stack runs unchanged on
another Docker host (with MinIO instead of S3).
