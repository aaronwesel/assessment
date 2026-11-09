# assessment

Production-oriented, HIPAA-ready SaaS scaffold (FastAPI · React · Terraform → ECS · Postgres · Redis). Prompt-agnostic stubs only — domain features wait for the live/spec brief.

## Monorepo layout

| Path | Purpose |
|------|---------|
| `backend/` | FastAPI API, workers, Alembic, tests |
| `frontend/` | Simple React/Vite web UI |
| `apps/android/` | Android client (same API/auth) |
| `apps/ios/` | Apple/iOS client (same API/auth) |
| `infra/terraform/` | AWS IaC (ECS, ALB, Aurora/RDS, Redis, KMS, etc.) — **never apply from CI** |
| `docs/` | HIPAA checklist, assessment-day runbook, team management / workstreams |
| `docker-compose.yml` | Local parity: api, db, redis, frontend |

## Quick start (local)

1. Copy env template (never commit real secrets):

   ```bash
   cp .env.example .env
   ```

2. Start the stack:

   ```bash
   docker compose up --build
   ```

3. Health checks:

   | Service | Default URL / check |
   |---------|---------------------|
   | API | `GET http://localhost:8000/health` → `200` |
   | Frontend | `http://localhost:3000` (stub until `frontend/Dockerfile`); use `cd frontend && npm run dev` for real auth UI |
   | Postgres | `postgres://app:changeme-local-only@localhost:5432/app` (Compose only) |
   | Redis | `redis://localhost:6379/0` |

Compose builds the real FastAPI image from `backend/`. Frontend remains an http-echo stub until `frontend/Dockerfile` lands.

Auth smoke (password + Google/Microsoft One Tap): see [docs/auth-smoke.md](docs/auth-smoke.md).

## Environment

- Use `.env.example` as the contract. Put secrets in local `.env`, CI secrets, or AWS Secrets Manager — **never** in git.
- OAuth client IDs, JWT secrets, DeepSeek keys, and DB passwords are placeholders only in `.env.example`.

## CI

GitHub Actions (`.github/workflows/`):

- **lint** — backend/frontend lint stubs (no-op until packages exist)
- **test** — unit/integration stub
- **docker** — compose config check + image build when Dockerfiles exist
- **terraform** — `fmt -check` + `validate` only (**no apply**)

## Docs

- [HIPAA engineering checklist](docs/hipaa-checklist.md) — required controls before calling anything complete (not a certification claim)
- [Assessment-day runbook](docs/assessment-day-runbook.md) — parallel streams, health gates, demo order
- [Load balancing & scaling](docs/load-balancing.md) — ALB fan-out, API vs worker autoscaling
- [Team management + Slack](docs/team-management.md) — org/team roles, JWT APIs, webhook vs Slack app
- [Workstreams](docs/workstreams.md) — assign API / UI / infra / mobile / AI to developers
- [Contributing](CONTRIBUTING.md) — PR ownership and CODEOWNERS expectations

## Explicit non-goals (prep)

- No domain product features until the live prompt
- No `terraform apply` in prep or CI
- No committed secrets or PHI sample data
