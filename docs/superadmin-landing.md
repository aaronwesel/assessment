# Landing, registration, and superadmin console

Smoke notes for workstream C (growth + admin UI). React stays on Vite + TypeScript under `frontend/` (plan path `web/` maps here).

## Public landing

- Route: `/`
- Brand-first hero with register / sign-in CTAs (or **Open app** when already authenticated).
- No PHI; marketing copy only.

Smoke: `cd frontend && npm run dev` → open `/` → brand “Assessment” is the hero signal → CTAs navigate to `/register` and `/login`.

## Self-serve registration

- Route: `/register`
- Calls existing `POST /api/v1/auth/register` via `registerPassword` in `frontend/src/api/auth.ts`.
- On success: hydrates session (`/auth/me`) and redirects to `/app`.
- Password rules match login form UX (min 12 chars); server remains authoritative.

Smoke:

1. Start API (`docker compose up` or local uvicorn) with password auth enabled.
2. Open `/register`, submit a unique email + strong password.
3. Expect redirect to `/app` and nav shows the email.
4. Duplicate email should surface the API error string (no token/PII logging).

Login page links to register; register links back to login.

## Superadmin console

- Base route: `/superadmin` (nested: `health`, `flags`, `audit`, `break-glass`, `webhooks`, `billing`)
- Gate: `RequireRole` allows `admin` or `superadmin` JWT roles. Others redirect to `/app`; anonymous → `/login`.
- Nav shows **Superadmin** only when the hydrated user has those roles.
- Thin clients in `frontend/src/api/platform.ts` (wired to Platform A/B):

| Widget | API |
|--------|-----|
| Health / SLO | `/health`, `/health/ready` (alias `/ready`), `/health/slo`, `/health/metrics` |
| Flags | `GET/PUT/DELETE /api/v1/flags` (toggle + reset override) |
| Audit skim | `GET /api/v1/audit/events`, export `GET /api/v1/audit/export?format=csv\|json` |
| Break-glass | `GET /api/v1/break-glass/grants?active_only=true` (skim) |
| Webhooks | `GET /api/v1/webhooks/outbox` (host-only rows) |
| Billing skim | `GET /api/v1/billing/summary` (fallback `/billing/me`) |

Missing endpoints (404/501/network) still render **placeholder** tiles — no hard failure.

Smoke (role gate + live APIs):

1. Sign in as a normal `user` → `/superadmin` redirects to `/app`; no Superadmin nav link.
2. Sign in as `admin` → open `/superadmin` → overview tiles should show **live** for health when the API is up; flags/audit/break-glass/webhooks/billing once those routers are reachable with an admin JWT.
3. Flags: Force on/off then Reset (clears Redis override).
4. Audit: Export CSV/JSON downloads (opaque ids only).

Pair with [security-ops.md](security-ops.md) for curl-level Platform A smoke.

## Mobile push stubs

| Platform | Config placeholders | Alert path |
|----------|---------------------|------------|
| Android | `apps/android/.../push/PushConfig.kt` (FCM project/sender) | `AlertReceivedHandler.onAlertReceived` |
| iOS | `apps/ios/AssessmentApp/Push/PushConfig.swift` (APNs team/key/topic) | `AlertReceivedHandler.onAlertReceived` |

No real FCM/APNs credentials in git. Payloads must use opaque alert IDs only (no PHI).

## Routes map

| Path | Auth | Purpose |
|------|------|---------|
| `/` | public | Landing |
| `/login` | public | Sign in |
| `/register` | public | Self-serve signup |
| `/app` | JWT | Authenticated home / MFA |
| `/billing` | JWT | Stripe Checkout / Portal settings (workstream B) |
| `/superadmin/*` | JWT + admin\|superadmin | Platform console |
