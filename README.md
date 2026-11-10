# assessment

Job-based **web crawler** (.NET 8 API + Worker · RabbitMQ · Postgres · React) plus HIPAA-minded SaaS prep (FastAPI auth/billing · Terraform ECS sketch).

**Graded path:** [docs/crawl-readme.md](docs/crawl-readme.md) (rubric narrative) · [docs/crawl.md](docs/crawl.md) (API + message details).

## Monorepo layout

| Path | Purpose |
|------|---------|
| `src/` | Graded .NET 8 crawl stack (`Crawl.Api`, `Crawl.Worker`, Domain, Infrastructure) |
| `tests/` | Crawl unit + HTML fixture integration tests |
| `frontend/` | React/Vite — crawl console + prep auth UI |
| `backend/` | FastAPI platform (auth, billing, flags) — side-by-side |
| `apps/android/` · `apps/ios/` | Mobile clients (platform auth) |
| `infra/terraform/` | AWS IaC sketch (ECS, Aurora, Amazon MQ crawl modules) — **never apply from CI** |
| `docs/` | Crawl rubric docs, HIPAA, runbooks |
| `docker-compose.yml` | db, redis, rabbitmq, crawl-api, crawl-worker, frontend, platform api |

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
   | Crawl API | `GET http://localhost:8080/health` · Swagger `/swagger` |
   | Crawl Worker | `GET http://localhost:8081/health` |
   | Frontend | `http://localhost:3000` (`/crawl`, `/crawl/history`) |
   | RabbitMQ | `http://localhost:15672` (guest/guest by default) |
   | Platform API | `GET http://localhost:8000/health` |
   | Postgres | `app` + `crawl` databases on `localhost:5432` |
   | Redis | `redis://localhost:6379/0` (platform sessions) |

Optional OTEL collector: `docker compose --profile otel up`. Details: [docs/crawl-readme.md](docs/crawl-readme.md).

Auth smoke (password + Google/Microsoft One Tap): see [docs/auth-smoke.md](docs/auth-smoke.md).

Platform ops smoke (audit, break-glass, consent, flags, signed media, webhooks, health/SLO): see [docs/security-ops.md](docs/security-ops.md).

## Environment

- Use `.env.example` as the contract. Put secrets in local `.env`, CI secrets, or AWS Secrets Manager — **never** in git.
- OAuth client IDs, JWT secrets, DeepSeek keys, and DB passwords are placeholders only in `.env.example`.

## CI

GitHub Actions (`.github/workflows/ci.yml`):

- **lint** — frontend oxlint (+ backend stub)
- **test-dotnet** — `dotnet restore/build/test` on `Crawl.sln`
- **test-frontend** — `npm ci` + production build
- **test-backend** — pytest (platform)
- **docker** — compose config + build crawl-api, crawl-worker, frontend, platform api
- **terraform** — `fmt -check` + `validate` only (**no apply**)

## Docs

- [Crawl assessment README](docs/crawl-readme.md) — run, architecture, schema, idempotency, DLQ, 4h narrative, cloud sketch
- [Crawl API details](docs/crawl.md) — endpoints, queues, domain notes from the .NET stream
- [Crawl cloud Terraform notes](infra/terraform/CRAWL_CLOUD.md) — ECS + Amazon MQ + Aurora (**no apply**)
- [HIPAA engineering checklist](docs/hipaa-checklist.md) — required controls before calling anything complete (not a certification claim)
- [Assessment-day runbook](docs/assessment-day-runbook.md) — parallel streams, health gates, demo order
- [Load balancing & scaling](docs/load-balancing.md) — ALB fan-out, API vs worker autoscaling
- [Team management + Slack](docs/team-management.md) — org/team roles, JWT APIs, webhook vs Slack app
- [Workstreams](docs/workstreams.md) — assign API / UI / infra / mobile / AI to developers
- [Landing + superadmin](docs/superadmin-landing.md) — public landing, self-serve register, admin console smoke
- [Stripe billing (test mode)](docs/billing-stripe.md) — Checkout, Customer Portal, webhook smoke
- [Contributing](CONTRIBUTING.md) — PR ownership and CODEOWNERS expectations

## Explicit non-goals

- No `terraform apply` in prep or CI (cloud sketch only)
- No committed secrets or PHI sample data
- AI must not own HTML link parsing or Domain Link Ratio
