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

- Base route: `/superadmin` (nested: `health`, `flags`, `audit`, `billing`)
- Gate: `RequireRole` allows `admin` or `superadmin` JWT roles. Others redirect to `/app`; anonymous → `/login`.
- Nav shows **Superadmin** only when the hydrated user has those roles.
- Dashboards call thin clients in `frontend/src/api/platform.ts`:
  - Health: `/health`, `/health/ready` (already on API)
  - Flags: `/api/v1/flags` (fallback `/api/v1/feature-flags`)
  - Audit skim: `/api/v1/audit?limit=10` (fallback `/api/v1/audits/recent`)
  - Billing skim: `/api/v1/billing/summary` (fallback `/api/v1/billing/me`)
- Missing sibling APIs (404/501/network) render **placeholder** tiles — no hard failure.

Smoke (role gate):

1. Sign in as a normal `user` → `/superadmin` redirects to `/app`; no Superadmin nav link.
2. Sign in (or promote) an `admin` user → open `/superadmin` → overview tiles load; Health should be **live** against a running API.
3. Flags / audit / billing stay placeholders until workstreams A/B expose those routes.

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
| `/superadmin/*` | JWT + admin\|superadmin | Platform console |
