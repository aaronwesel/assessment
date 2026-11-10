#!/usr/bin/env bash
# Auth + MFA policy smoke: register paths, MFA enroll material, optional vs healthcare flags.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
API_URL="${API_URL:-http://localhost:8000}"
FRONTEND_URL="${FRONTEND_URL:-http://localhost:5173}"

cd "$ROOT/backend"
python3 -m pip install -q -r requirements.txt

export ENVIRONMENT=test
export SECRET_KEY=e2e-local-secret-not-for-prod
export DATABASE_URL=sqlite:////tmp/assessment-auth-e2e.db
export REDIS_URL="${REDIS_URL:-redis://localhost:6379/15}"
export RATE_LIMIT_ENABLED=false
export GOOGLE_CLIENT_ID=test-google-client-id.apps.googleusercontent.com
export MICROSOFT_CLIENT_ID=00000000-0000-0000-0000-000000000001

rm -f /tmp/assessment-auth-e2e.db

python3 -m uvicorn app.main:app --host 127.0.0.1 --port 8765 &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true' EXIT

for _ in $(seq 1 40); do
  if curl -sf "http://127.0.0.1:8765/health" >/dev/null; then
  break
  fi
  sleep 0.25
done

BASE="http://127.0.0.1:8765"
PASS='SecurePass1!x'

echo "== providers"
curl -sf "$BASE/api/v1/auth/providers" | python3 -m json.tool >/dev/null

echo "== standard register + me (no MFA wall at API)"
STD=$(curl -sf -X POST "$BASE/api/v1/auth/register" \
  -H 'Content-Type: application/json' \
  -d '{"email":"e2e-std@example.com","password":"'"$PASS"'"}')
STD_AT=$(echo "$STD" | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
ME=$(curl -sf "$BASE/api/v1/auth/me" -H "Authorization: Bearer $STD_AT")
python3 -c "import json,sys; b=json.loads(sys.argv[1]); assert b['mfa_policy_required'] is False and b['mfa_enabled'] is False" "$ME"

echo "== healthcare register policy flag"
HC=$(curl -sf -X POST "$BASE/api/v1/auth/register" \
  -H 'Content-Type: application/json' \
  -d '{"email":"e2e-hc@example.com","password":"'"$PASS"'","healthcare_account":true}')
HC_AT=$(echo "$HC" | python3 -c "import sys,json; print(json.load(sys.stdin)['access_token'])")
HC_ME=$(curl -sf "$BASE/api/v1/auth/me" -H "Authorization: Bearer $HC_AT")
python3 -c "import json,sys; b=json.loads(sys.argv[1]); assert b['mfa_policy_required'] is True" "$HC_ME"

echo "== MFA enroll returns otpauth URI"
ENROLL=$(curl -sf -X POST "$BASE/api/v1/auth/mfa/enroll" -H "Authorization: Bearer $HC_AT")
python3 -c "import json,sys; b=json.loads(sys.argv[1]); assert b['otpauth_uri'].startswith('otpauth://totp/')" "$ENROLL"

echo "== frontend build"
cd "$ROOT/frontend"
npm run build

echo "== optional UI routes respond"
if curl -sf "$FRONTEND_URL/login" -o /dev/null 2>/dev/null; then
  curl -sf "$FRONTEND_URL/register" -o /dev/null
  echo "Frontend dev server reachable at $FRONTEND_URL"
else
  echo "Frontend dev server not running — build gate passed; start Vite for manual One Tap screenshots."
fi

echo "PASS: e2e-auth-mfa-gate"
