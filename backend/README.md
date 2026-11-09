# Backend (FastAPI)

HIPAA-minded FastAPI scaffold: JWT + RBAC stubs, Google/Microsoft One Tap token-exchange stubs, Redis client stub, DeepSeek (off) + Rekognition stubs, Amazon SES alert client (off) with PHI-free templates, Alembic, health probes.

## Quick start

```bash
cd backend
python -m venv .venv && source .venv/bin/activate
pip install -r requirements.txt
cp .env.example .env
uvicorn app.main:app --reload --port 8000
```

## Tests

```bash
pytest
```

## Notes

- Migrations via Alembic (`alembic upgrade head`) — not on API boot.
- `DEEPSEEK_ENABLED`, `REKOGNITION_ENABLED`, `SES_ENABLED`, and `LOGIN_ALERT_EMAIL_ENABLED` default to `false`.
- SES alert templates forbid PHI in bodies by default (`app/alerts/templates.py`).
- Do not log PHI; see `app/core/logging.py`.
