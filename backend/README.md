# Backend (FastAPI)

HIPAA-minded FastAPI API: JWT access/refresh (+ Redis session/blacklist), password register/login, Google One Tap + Microsoft MSAL ID-token exchange, RBAC, Redis client, DeepSeek (off) + Rekognition stubs, Amazon SES alert client (off) with PHI-free templates, Alembic, health probes.

## Quick start

```bash
cd backend
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
# Fill GOOGLE_CLIENT_ID / MICROSOFT_CLIENT_ID (or GOOGLE_OAUTH_* / MICROSOFT_OAUTH_*).
uvicorn app.main:app --reload --port 8000
```

Local/test environments call `init_db()` on startup. Production should run `alembic upgrade head` as a deploy step (not on API boot).

## Auth surface (`/api/v1`)

| Method | Path | Notes |
|--------|------|-------|
| GET | `/auth/providers` | Which providers are configured |
| POST | `/auth/register` | Email/password → JWT pair |
| POST | `/auth/login` | Email/password → JWT pair (+ optional SES login alert) |
| POST | `/auth/refresh` | Rotate refresh token |
| POST | `/auth/logout` | Blacklist access/refresh jtis |
| POST | `/auth/google/onetap` | Google ID token → JWT (google-auth) |
| POST | `/auth/microsoft/onetap` | Microsoft ID token → JWT (JWKS) |
| GET | `/auth/me` | Bearer access token |

Tokens use `jti`; Redis tracks sessions and supports blacklist. Logs never include email, passwords, or raw tokens.

## Tests

```bash
pytest -q
```

Google/Microsoft verifiers are unit-tested with mocked google-auth / JWKS. SES alert tests live in `tests/test_ses_alerts.py`.

## Notes

- `DEEPSEEK_ENABLED`, `REKOGNITION_ENABLED`, `SES_ENABLED`, and `LOGIN_ALERT_EMAIL_ENABLED` default to `false`.
- SES alert templates forbid PHI in bodies (`app/alerts/templates.py`).
- Do not log PHI; see `app/core/logging.py`.
