# Job-based web crawler (graded assessment)

.NET 8 **Crawl.Api** + **Crawl.Worker**, RabbitMQ, PostgreSQL, React Start / Details / History.  
Prep FastAPI/auth/billing stays side-by-side as a differentiator — it is **not** a C#→Python crawl shim.

Companion API cheat-sheet: [crawl.md](crawl.md). Plan: Project store `docs/technical-assessment-plan.md`.

---

## How to run (local)

```bash
cp .env.example .env
docker compose up --build
```

Optional OpenTelemetry collector (debug exporter):

```bash
docker compose --profile otel up --build
# set OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 in .env
```

| Surface | URL / check |
|---------|-------------|
| Crawl API | `http://localhost:8080` · `GET /health` · Swagger `/swagger` |
| Crawl Worker | `http://localhost:8081/health` + competing consumer on `crawl.page` |
| React UI | `http://localhost:3000` · `/crawl`, `/crawl/jobs/:id`, `/crawl/history` · `VITE_CRAWL_API_URL` |
| RabbitMQ UI | `http://localhost:15672` (default guest/guest) |
| Postgres | `app` DB = platform · `crawl` DB = crawler (`docker/postgres-init`) |
| Platform API | `http://localhost:8000/health` (prep SaaS) |

Dev without full images:

```bash
docker compose up -d db redis rabbitmq
dotnet run --project src/Crawl.Api
dotnet run --project src/Crawl.Worker
cd frontend && npm run dev   # set VITE_CRAWL_API_URL=http://localhost:5059
dotnet test Crawl.sln
```

---

## Architecture

```
Browser (React)
    │  REST poll
    ▼
Crawl.Api  ──publish──►  RabbitMQ (crawl.page → crawl.page.dlq)
    │                         │
    │                         ▼
    │                   Crawl.Worker(s)  ──HTTP GET HTML──► sites
    │                         │
    └──────── EF Core ────────┴──► PostgreSQL (jobs / pages / edges)
```

| Piece | Responsibility |
|-------|----------------|
| `Crawl.Domain` | UrlNormalizer, Domain Link Ratio, job/page/edge rules — **no I/O** |
| `Crawl.Infrastructure` | EF Core, RabbitMQ declare/publish/consume, HttpClient resilience |
| `Crawl.Api` | Create job, status, tree, history, cancel, health, OpenAPI |
| `Crawl.Worker` | At-least-once consume → fetch HTML → parse links (.NET only) → upsert → enqueue children |
| React | Start crawl, live progress, tree + DLR, history |
| Platform FastAPI | Auth / billing / ops — optional locally; required for cloud UI auth story |

**Hard rule:** AI never owns link parsing, URL normalization, or Domain Link Ratio.

---

## Message schema

One message **per page** (`CrawlPageRequested`). Queue `crawl.page`; DLQ `crawl.page.dlq` via `crawl.page.dlx`.

```json
{
  "jobId": "guid",
  "pageId": 123,
  "url": "https://example.com/",
  "depth": 0,
  "enqueuedAt": "2026-11-10T00:00:00Z"
}
```

| Field | Notes |
|-------|--------|
| `jobId` / `pageId` | Parent job + persisted page row |
| `url` | Worker normalizes; unique `(job_id, url)` enforces idempotency |
| `depth` | 0 = seed; children `depth+1` up to max (default **2**) |
| `enqueuedAt` | UTC enqueue time |
| `x-retry-count` (AMQP header) | Transient retries (default max **3**); then DLQ |

---

## Idempotency

At-least-once delivery → handlers must be safe under redelivery and multi-worker races:

1. Unique `(job_id, url)` on `crawl_pages`; unique `(source_page_id, target_url)` on edges.
2. Terminal page statuses are no-ops on redelivery.
3. Child inserts tolerate unique races between workers.
4. Cancel: workers check job status before fetch/enqueue; do not schedule children after cancel.

---

## Retry / DLQ

| Stage | Behavior |
|-------|----------|
| Transient HTTP / broker | Retry with backoff; `x-retry-count` header |
| Exhausted attempts / poison | Nack without requeue → `crawl.page.dlq` |
| Job cancel | Finish in-flight page without further fan-out |

Operator: inspect DLQ in RabbitMQ management UI; replay is deliberate (not auto).

---

## Known limits

- Max depth default **2**; max pages per job ~**200**; HTML-only (no JS render).
- No SSRF allowlist yet (known gap vs production).
- Polling UI (SignalR/SSE cut from 4h path).
- Per-job write serialization may cap huge single-job throughput (fine at 200 pages).
- Auth optional locally; cloud UI expects platform auth.
- robots.txt / politeness / redirect-loop guards / query-sort dedup are **on** (see [crawl.md](crawl.md)); still not a Scrapy port.

---

## Four-hour narrative

**Implemented first**

1. Domain tests — UrlNormalizer + Domain Link Ratio  
2. API + SQL — jobs/pages/edges, create/status/tree/history/cancel/health  
3. Worker + RabbitMQ — consume, fetch, parse, idempotent upsert, retries/DLQ  
4. React poll UI — Start / Details / History  
5. Compose + this README + CI  

**Cut if needed**

- SignalR / SSE live push  
- Full Terraform **apply** / real AWS cutover  
- Salesforce live webhook, Rekognition screenshots  
- Strong SSRF allowlist (robots / politeness shipped in worker) 

**Next**

- Payer portal / clearinghouse adapter stubs wired to field-finder  
- X12/FHIR as documented integration path (not weekend EDI engine)  
- Worker autoscaling on Amazon MQ depth + alarms  
- Vault-backed assisted login with full audit  

---

## Cloud access of results (sketch only — **do not apply**)

Same .NET contracts in AWS; operators read **job results from the cloud Crawl API** (not a FastAPI proxy).

| Local Compose | AWS shape |
|---------------|-----------|
| `crawl-api` / `crawl-worker` | ECS Fargate services (scale workers on queue depth) |
| `db` (`crawl`) | Aurora PostgreSQL |
| `rabbitmq` | Amazon MQ (RabbitMQ engine) |
| `frontend` | CloudFront + S3 (or container behind ALB) |
| secrets in `.env` | Secrets Manager / KMS (prep modules) |
| optional `otel-collector` | ADOT / OTLP → CloudWatch / X-Ray |

Terraform module **sketches** (not wired into root apply):

- [`infra/terraform/modules/amazon-mq/`](../infra/terraform/modules/amazon-mq/) — broker + SG notes  
- [`infra/terraform/modules/crawl-ecs/`](../infra/terraform/modules/crawl-ecs/) — Crawl API/Worker task defs + MQ env  
- Notes: [`infra/terraform/CRAWL_CLOUD.md`](../infra/terraform/CRAWL_CLOUD.md)

**Forbidden until explicitly authorized:** `terraform apply` / `destroy`. Validate only: `fmt`, `init -backend=false`, `validate`.

Release/rollback (cloud day): bake immutable image tags → ECS new task definition → health gate on `/health` → rollback = previous task def. Still no apply from CI.

---

## Stretch: AI crawl incident monitor (ops / observability)

**Label: stretch** — not required for the graded crawl path. Deterministic .NET still owns HTML link parsing, Domain Link Ratio, retries/DLQ, and the RabbitMQ job pipeline. This overlay strengthens the **RCM portal-ops** story: when payer-like sites rate-limit, time out, or hard-block the crawler, an operator gets structured incidents plus safe, configurable remediations (the same class of knobs you’d tune for eligibility portal scraping).

| Piece | Behavior |
|-------|----------|
| Fault emission | Worker emits structured faults (429/503, timeout, DNS, connection reset, captcha/block heuristics) with `jobId` + `correlationId` — **no secrets/PHI/HTML bodies** in prompts or emails |
| Classifier + backoff | Deterministic severity + Retry-After / exponential backoff + per-host politeness bump (Scrapy `DOWNLOAD_DELAY`-inspired knobs on `HostPolitenessGate`) |
| AI remediation agent | DeepSeek (optional) advises remediations; falls back to policy when the key is missing |
| Egress hop | `IEgressProxyProvider` with config-list HTTP(S) proxies / rotating endpoints. Real VPN/residential requires **operator-supplied** endpoints — **fail closed** if none configured (no fake paid VPN keys in repo) |
| Email alerts | Feature flag `Crawl:Alerts:EmailEnabled` (env `CRAWL_ALERTS_EMAIL_ENABLED`); SES stub or SMTP; alerts on Failed job, repeated DLQ, block remediation. **Never email passwords** |
| UI | Job Details → Assistive AI → **Incidents / remediations** (last AI action status) |

Config: `CrawlAi:IncidentMonitorEnabled`, `CrawlEgress:Proxies`, `Crawl:Alerts:*`. API: `GET /api/ai/jobs/{jobId}/incidents`.

---

## OpenTelemetry

OTLP hooks are registered on Api/Worker; export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

```bash
docker compose --profile otel up otel-collector
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

Config: `infra/otel/otel-collector-config.yaml`.
