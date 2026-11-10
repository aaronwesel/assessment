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

On Cloud Agent VMs where Docker **bridge container↔container TCP is broken**, Compose uses `network_mode: host` for `api` / `crawl-api` / `crawl-worker` / `frontend`, talking to published `127.0.0.1:5432` / `:5672`. Data-plane images (`db`, `redis`, `rabbitmq`) stay bridged with published ports.

Optional OpenTelemetry collector (debug exporter):

```bash
docker compose --profile otel up --build
# set OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317 in .env
```

| Surface | URL / check |
|---------|-------------|
| Crawl API | `http://localhost:8080` · `GET /health` · Swagger `/swagger` · SignalR `/hubs/crawl-jobs` · `GET /api/ops/queue` |
| Crawl Worker | `http://localhost:8081/health` · `GET /ops/queue` + competing consumer on `crawl.page` |
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

Worker flow: persist page row → publish `CrawlPageRequested` only after save; on consume, skip terminal pages, then fetch → edges → optional child rows → publish children **after** DB commit (`CrawlPageProcessor`).

---

## Domain Link Ratio & crawl rules

**Domain Link Ratio (DLR)** on each completed page:

\[
\text{DLR} = \frac{\text{http(s) links whose host equals the job start host}}{\text{total http(s) outgoing links on that page}}
\]

| Rule | Behavior |
|------|----------|
| Start host | Fixed at job create from normalized seed URL (`CrawlJob.StartHost`, IDN punycode, lowercased). |
| Internal vs external | Compare link `IdnHost` to `StartHost` (case-insensitive). |
| Ignored schemes | `mailto:`, `tel:`, relative non-http, etc. — not counted in numerator or denominator. |
| Depth | Children enqueued only when `parentDepth < maxDepth` (default **2**). |
| Page budget | `maxPages` (default **200**, cap **5000**) — stop inserting new child rows when budget is exhausted. |
| Fan-out order | Internal links only become crawl tasks; stable lexicographic URL order within a depth (BFS-aware with RabbitMQ priority). |
| URL graph | `UrlNormalizer` canonical form + optional sorted query before unique indexes; see [crawl.md](crawl.md#engine-scrapy-inspired-job-crawler). |
| robots.txt | Default on — Disallow/Allow longest-prefix, optional `Crawl-delay`; fail-open if robots fetch fails. |
| HTML-only | Non-HTML responses (incl. **PDF** bodies detected by sniff) are **not** parsed for links — marked Skipped/Failed per fetch policy, not expanded. |

**Hard rule:** DLR, normalization, and edge upserts are deterministic .NET — never AI (`Crawl.Domain.Links.DomainLinkRatio`).

Tree API exposes per-node DLR for Done pages; React shows `DLR` on the job tree.

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
- SignalR live push is **on** for Job Details; REST poll remains the fallback if the hub drops.
- Per-job write serialization may cap huge single-job throughput (fine at 200 pages); scale **workers** on queue depth instead.
- Auth optional locally; cloud UI expects platform auth.
- robots.txt / politeness (incl. cross-worker advisory lock) / redirect-loop guards / query-sort dedup / atomic page claim are **on** (see [crawl.md](crawl.md)); still not a Scrapy port.

---

## Priorities (assessment PDF rubric)

Maps the home-assignment **implemented first / cut / next** trade-offs from the Technical Assessment PDF (rubric). Same headings reviewers expect in README.

### Implemented first — and why

| Priority | Why first |
|----------|-----------|
| Domain tests — `UrlNormalizer` + **Domain Link Ratio** | Wrong graph math invalidates the whole demo; cheap to test; blocks API/UI lies about ratios. |
| API + Postgres — jobs/pages/edges, create/status/tree/history/cancel/health | Contract the UI and worker share; persistence proves idempotent schema before broker load. |
| Worker + RabbitMQ — consume, fetch, parse, upsert, retries/DLQ | Rubric’s async boundary; without at-least-once + DLQ the job model is incomplete. |
| React Start / Details / History (PDF flow) | End-to-end proof; Job Details uses SignalR with poll fallback. |
| Docker Compose + this README + CI | One-command repro + gates on `dotnet test` / docker builds — submission hygiene. |
| Queue-backed elastic workers + claim/DLQ edge hardening | Long jobs scale by adding consumers; multi-worker safe; ops queue depth signals. |

### Cut if needed — and why

| Cut | Why it was deferrable |
|-----|------------------------|
| Per-job ephemeral EC2 provisioner | Stretch-next; ECS/ASG consumers on RabbitMQ fit the assessment architecture better. |
| `terraform apply` / live AWS cutover | **Forbidden** in prep/CI anyway; sketches (`CRAWL_CLOUD.md`, modules + `autoscaling.tf`) document scale without account risk. |
| Salesforce live webhook, Rekognition screenshots | Role “much more” — interfaces/outbox stubs OK; live CRM/OCR needs creds and ops time. |
| Strong SSRF allowlist | Known production gap; robots + politeness + redirect limits shipped first for crawl correctness. |
| PDF / document link extraction | HTML-only rubric; sniff rejects `%PDF` bodies (see tests) — parsing PDFs is a different product surface. |

### Next with more time

| Next | Why later |
|------|-----------|
| Payer portal / clearinghouse adapters + field-finder | Shows RCM fit once crawl graph is trustworthy. |
| X12/FHIR integration path (not a weekend EDI engine) | Honest senior scope — document contracts, don’t fake claims adjudication. |
| Wire CW metric publisher from `/api/ops/queue` → live Amazon MQ ASG | Sketch exists; needs authorized apply + broker metrics IAM. |
| Ephemeral scale-out host pool that drains then terminates | Operator-runnable next after ECS desired_count elasticity. |
| Vault-backed assisted login + full audit | Security-sensitive; must not block graded HTML crawl. |
| SSRF allowlist + optional JS render / PDF text extract | Production hardening beyond timed HTML depth crawl. |

---

## Cloud access of results (sketch only — **do not apply**)

Same .NET contracts in AWS; operators read **job results from the cloud Crawl API** (not a FastAPI proxy).

| Local Compose | AWS shape |
|---------------|-----------|
| `crawl-api` / `crawl-worker` | ECS Fargate services (scale workers on queue depth; SignalR on API) |
| `db` (`crawl`) | Aurora PostgreSQL |
| `rabbitmq` | Amazon MQ (RabbitMQ engine) |
| `frontend` | CloudFront + S3 (or container behind ALB) |
| secrets in `.env` | Secrets Manager / KMS (prep modules) |
| optional `otel-collector` | ADOT / OTLP → CloudWatch / X-Ray |

Terraform module **sketches** (not wired into root apply):

- [`infra/terraform/modules/amazon-mq/`](../infra/terraform/modules/amazon-mq/) — broker + SG notes  
- [`infra/terraform/modules/crawl-ecs/`](../infra/terraform/modules/crawl-ecs/) — Crawl API/Worker task defs + MQ env + **`autoscaling.tf`** (queue-depth scale-out/in)  
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

## Product UX (Trailmap)

React routes (unchanged contract, redesigned surfaces):

| Path | Experience |
|------|------------|
| `/` | **Landing** — Trailmap wordmark, 3-step “how it works”, per-page Stripe test pricing + example cost, single **Start guided crawl** CTA |
| `/crawl/welcome` | **Guided flow** — steps 1–4 (URL → probe → scope → confirm); persistent **Crawl guide** (`POST /api/ai/setup/preview`); **Advanced options** drawer |
| `/crawl` | Expert quick start (still Start → Job Details → History) |
| `/crawl/jobs/:id` | Steps 5–6: live progress + readable DLR summary, exports (CSV/JSON/NDJSON/Markdown/HTML), tree, element pick |
| `/crawl/history` | Paginated history with empty/error states |

**Rules:** Assistive AI (DeepSeek when `DEEPSEEK_API_KEY` is set) coaches operators only — Domain Link Ratio, URL normalization, and link graph edges remain in `Crawl.Domain` / Worker.

---

## OpenTelemetry

OTLP hooks are registered on Api/Worker; export when `OTEL_EXPORTER_OTLP_ENDPOINT` is set.

```bash
docker compose --profile otel up otel-collector
export OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4317
```

Config: `infra/otel/otel-collector-config.yaml`.
