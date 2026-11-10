# Auth smoke test (local)

Manual steps to verify password + One Tap exchange paths. No PHI in logs; do not paste real tokens into tickets.

## Prerequisites

1. Copy root `.env.example` → `.env` (and optionally `frontend/.env` from `frontend/.env.example`).
2. Set `JWT_SECRET` to a local secret.
3. For social login, set:
   - `GOOGLE_CLIENT_ID` / `VITE_GOOGLE_CLIENT_ID` (GIS authorized JS origin: `http://localhost:5173`)
   - `MICROSOFT_CLIENT_ID` / `VITE_MICROSOFT_CLIENT_ID` + tenant (`VITE_MICROSOFT_TENANT_ID`)
   - Entra redirect URI: `http://localhost:5173` (SPA)
4. Leave `SES_ENABLED=false` unless deliberately testing alerts.

## Backend (Compose or uvicorn)

```bash
# Option A — Compose API + Postgres + Redis
docker compose up --build api db redis

# Option B — local venv (SQLite/Postgres + Redis optional)
cd backend && python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
ENVIRONMENT=local uvicorn app.main:app --reload --port 8000
```

Health: `GET http://localhost:8000/health` → 200  
Providers: `GET http://localhost:8000/api/v1/auth/providers`

### Password path

```bash
curl -s -X POST http://localhost:8000/api/v1/auth/register \
  -H 'Content-Type: application/json' \
  -d '{"email":"smoke@example.com","password":"SecurePass1!x","full_name":"Smoke"}'

curl -s -X POST http://localhost:8000/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"smoke@example.com","password":"SecurePass1!x"}'

# Use access_token from the response (do not commit it):
curl -s http://localhost:8000/api/v1/auth/me -H "Authorization: Bearer $ACCESS"
```

### Automated suite

```bash
cd backend && pytest -q
```

Covers register/login/refresh/logout blacklist, mocked Google/Microsoft exchange, JWKS unit path, HIPAA redaction, SES stubs.

## Frontend

```bash
cd frontend
cp .env.example .env   # set VITE_* client IDs + VITE_API_BASE_URL=http://localhost:8000
npm install
npm run dev            # http://localhost:5173
```

1. Register / sign in with email+password → land on Home with email + roles.
2. With Google client ID: One Tap / button → backend `/auth/google/onetap` → Home.
3. With Microsoft client ID: popup → `/auth/microsoft/onetap` → Home.
4. Sign out → access token rejected on `/auth/me`.

## Playwright (optional)

Not wired in CI yet. Local sketch:

```bash
# with API + Vite running
npx --yes playwright install chromium
# then a one-off script hitting /login, filling password, asserting Home
```

Prefer pytest for CI gate; use Playwright when a stable frontend Dockerfile exists.

## HIPAA checks while smoking

- API logs show event names (`password_login`, `google_onetap_exchange`) without email/token values.
- SES login alerts (if enabled) use PHI-free templates and ops mailboxes only.
