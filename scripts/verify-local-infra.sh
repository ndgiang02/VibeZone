#!/usr/bin/env bash
# Verifies the local docker compose dependencies actually work — not just that
# containers exist. Checks only the services that are currently running.
#
#   bash scripts/verify-local-infra.sh
#
# Exit code is non-zero if any check fails.
set -uo pipefail

cd "$(dirname "$0")/.." || exit 1

failures=0
pass() { printf '  \033[32mPASS\033[0m %s\n' "$1"; }
fail() { printf '  \033[31mFAIL\033[0m %s\n' "$1"; failures=$((failures + 1)); }
check() { local desc="$1"; shift; if "$@" >/dev/null 2>&1; then pass "$desc"; else fail "$desc"; fi; }

if [[ ! -f .env ]]; then
  echo "Missing .env — run: cp .env.example .env (and fill in the secrets)" >&2
  exit 1
fi
set -a
# shellcheck disable=SC1091
source .env
set +a

compose() { docker compose "$@"; }
running() { [[ -n "$(compose ps --status running -q "$1" 2>/dev/null)" ]]; }
health() { docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}' "$(compose ps -q "$1")" 2>/dev/null; }
host_bindings() { docker inspect -f '{{range $p, $b := .NetworkSettings.Ports}}{{range $b}}{{.HostIp}} {{end}}{{end}}' "$(compose ps -q "$1")"; }
loopback_only() {
  local ips; ips="$(host_bindings "$1")"
  [[ -n "$ips" ]] || return 1
  for ip in $ips; do [[ "$ip" == "127.0.0.1" ]] || return 1; done
}

echo "== Repository hygiene"
check ".env is git-ignored" git check-ignore -q .env
if git ls-files --error-unmatch .env >/dev/null 2>&1; then fail ".env is not tracked by git"; else pass ".env is not tracked by git"; fi
check "docker compose config is valid" compose config -q

echo "== PostgreSQL"
if running postgres; then
  [[ "$(health postgres)" == healthy ]] && pass "container healthy" || fail "container healthy (status: $(health postgres))"
  check "ports bound to 127.0.0.1 only" loopback_only postgres
  pg() { compose exec -T postgres psql -v ON_ERROR_STOP=1 -U "$POSTGRES_USER" -d "$POSTGRES_DB" -tAc "$1"; }
  check "query executes" pg "select 1"
  [[ "$(pg 'show server_version_num' 2>/dev/null)" -ge 180000 ]] && pass "server version >= 18" || fail "server version >= 18"
  [[ "$(pg 'show password_encryption' 2>/dev/null)" == scram-sha-256 ]] && pass "password_encryption = scram-sha-256" || fail "password_encryption = scram-sha-256"
  [[ "$(pg 'show data_checksums' 2>/dev/null)" == on ]] && pass "data checksums enabled" || fail "data checksums enabled"
  check "gen_random_uuid() available" pg "select gen_random_uuid()"
else
  fail "postgres is not running (docker compose up -d)"
fi

echo "== Redis (profile: cache)"
if running redis; then
  [[ "$(health redis)" == healthy ]] && pass "container healthy" || fail "container healthy (status: $(health redis))"
  check "ports bound to 127.0.0.1 only" loopback_only redis
  [[ "$(compose exec -T redis redis-cli ping 2>/dev/null)" == PONG ]] && pass "authenticated PING" || fail "authenticated PING"
  [[ "$(compose exec -T -e REDISCLI_AUTH= redis redis-cli --no-auth-warning ping 2>&1)" == *NOAUTH* ]] \
    && pass "unauthenticated access rejected" || fail "unauthenticated access rejected"
else
  echo "  skip (not running)"
fi

echo "== MinIO (profile: media)"
if running minio; then
  [[ "$(health minio)" == healthy ]] && pass "container healthy" || fail "container healthy (status: $(health minio))"
  check "ports bound to 127.0.0.1 only" loopback_only minio
  init_id="$(compose ps -a -q minio-init)"
  if [[ -n "$init_id" && "$(docker inspect -f '{{.State.ExitCode}}' "$init_id")" == 0 ]]; then pass "minio-init completed"; else fail "minio-init completed"; fi

  api="http://127.0.0.1:${MINIO_API_PORT:-9000}"
  code="$(curl -s -o /dev/null -w '%{http_code}' "$api/${S3_BUCKET_MEDIA}/")"
  [[ "$code" == 403 ]] && pass "anonymous bucket listing denied (HTTP $code)" || fail "anonymous bucket listing denied (got HTTP $code)"

  # Exercise the least-privilege app credentials from inside the compose network.
  probe="$(compose run --rm --no-deps -T --entrypoint bash \
    -e APP_KEY="$S3_ACCESS_KEY" -e APP_SECRET="$S3_SECRET_KEY" -e BUCKET="$S3_BUCKET_MEDIA" \
    minio-init -c '
      mc alias set app http://minio:9000 "$APP_KEY" "$APP_SECRET" >/dev/null || { echo alias=fail; exit 0; }
      obj="app/$BUCKET/_verify/probe-$RANDOM.txt"
      echo vibezone | mc pipe "$obj" >/dev/null 2>&1 && echo put=ok || echo put=fail
      [[ "$(mc cat "$obj" 2>/dev/null)" == vibezone ]] && echo get=ok || echo get=fail
      mc rm "$obj" >/dev/null 2>&1 && echo del=ok || echo del=fail
      mc mb "app/vibezone-forbidden-$RANDOM" >/dev/null 2>&1 && echo mb=allowed || echo mb=denied
      mc admin info app >/dev/null 2>&1 && echo admin=allowed || echo admin=denied
    ' 2>/dev/null)"
  for expect in put=ok get=ok del=ok mb=denied admin=denied; do
    [[ "$probe" == *"$expect"* ]] && pass "app credentials: $expect" || fail "app credentials: expected $expect"
  done
else
  echo "  skip (not running)"
fi

echo "== Mailpit (profile: mail)"
if running mailpit; then
  [[ "$(health mailpit)" == healthy ]] && pass "container healthy" || fail "container healthy (status: $(health mailpit))"
  check "ports bound to 127.0.0.1 only" loopback_only mailpit
  check "web API reachable" curl -fsS "http://127.0.0.1:${MAILPIT_UI_PORT:-8025}/api/v1/info"
else
  echo "  skip (not running)"
fi

echo "== Application (profile: app)"
if running api; then
  migrate_id="$(compose ps -a -q migrate)"
  if [[ -n "$migrate_id" && "$(docker inspect -f '{{.State.ExitCode}}' "$migrate_id")" == 0 ]]; then pass "migrations applied (migrate exited 0)"; else fail "migrations applied (migrate exited 0)"; fi
  [[ "$(health api)" == healthy ]] && pass "api container healthy" || fail "api container healthy (status: $(health api))"
  check "api ports bound to 127.0.0.1 only" loopback_only api
  [[ "$(docker exec "$(compose ps -q api)" id -u)" != 0 ]] && pass "api runs as non-root" || fail "api runs as non-root"
  api_url="http://127.0.0.1:${API_PORT:-5080}"
  [[ "$(curl -s "$api_url/health/ready")" == *'"database":"Healthy"'* ]] && pass "api /health/ready reports database Healthy" || fail "api /health/ready reports database Healthy"
  genres="$(curl -s "$api_url/api/v1/genres" | grep -o '"slug"' | wc -l)"
  (( genres > 0 )) && pass "api serves genres from the database ($genres)" || fail "api serves genres from the database"
  if running web; then
    [[ "$(health web)" == healthy ]] && pass "web container healthy" || fail "web container healthy (status: $(health web))"
    [[ -z "$(host_bindings web | tr -d ' ')" ]] && pass "web has no host port (reached via proxy)" || fail "web has no host port (reached via proxy)"
    if running proxy; then
      [[ "$(health proxy)" == healthy ]] && pass "proxy container healthy" || fail "proxy container healthy (status: $(health proxy))"
      check "proxy ports bound to 127.0.0.1 only" loopback_only proxy
      [[ "$(docker exec "$(compose ps -q proxy)" id -u)" != 0 ]] && pass "proxy runs as non-root" || fail "proxy runs as non-root"
      [[ -z "$(curl -s -D - -o /dev/null "http://127.0.0.1:${WEB_PORT:-3000}/healthz" | tr -d '\r' | grep -i '^server:')" ]] \
        && pass "proxy hides its Server header" || fail "proxy hides its Server header"
    else
      fail "proxy is not running"
    fi
    [[ "$(docker exec "$(compose ps -q web)" id -u)" != 0 ]] && pass "web runs as non-root" || fail "web runs as non-root"
    web_url="http://127.0.0.1:${WEB_PORT:-3000}"
    check "web home page responds" curl -fsS -o /dev/null "$web_url/"
    via_web="$(curl -s "$web_url/api/v1/genres" | grep -o '"slug"' | wc -l)"
    (( via_web == genres && via_web > 0 )) && pass "web proxies /api/* to the api ($via_web genres)" || fail "web proxies /api/* to the api"
    csrf="$(curl -s "$web_url/api/v1/auth/csrf")"
    [[ "$csrf" == *'"token":"'* ]] && pass "auth: CSRF token issued through the web origin" || fail "auth: CSRF token issued through the web origin"
    code="$(curl -s -o /dev/null -w "%{http_code}" "$web_url/api/v1/me")"
    [[ "$code" == 401 ]] && pass "auth: anonymous /api/v1/me is 401" || fail "auth: anonymous /api/v1/me is 401 (got $code)"
    csp="$(curl -s -D - -o /dev/null "$web_url/vi" | tr -d "" | grep -i "^content-security-policy:")"
    [[ "$csp" == *"nonce-"* ]] && pass "web: per-request CSP nonce header" || fail "web: per-request CSP nonce header"
  else
    fail "web is not running"
  fi
else
  echo "  skip (not running)"
fi

echo
if (( failures > 0 )); then
  echo "$failures check(s) failed."
  exit 1
fi
echo "All checks passed."
