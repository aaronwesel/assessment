# Crawl assessment (.NET 8)

Graded job-based web crawler: **Crawl.Api** + **Crawl.Worker** + PostgreSQL + RabbitMQ.

Rubric narrative (architecture, idempotency, retries, DLQ, DLR, assessment PDF priorities, cloud sketch): [crawl-readme.md](crawl-readme.md).

## Run (Compose)

```bash
cp .env.example .env
docker compose up --build
# crawl-only subset:
docker compose up --build db rabbitmq crawl-api crawl-worker
```

| Service | URL |
|---------|-----|
| Crawl API + Swagger | http://localhost:8080/swagger |
| Crawl Worker health | http://localhost:8081/health |
| Frontend | http://localhost:3000 (`/crawl`) |
| RabbitMQ management | http://localhost:15672 (`guest`/`guest`) |
| Postgres | same Compose `db` (`crawl_*` tables; optional `crawl` DB via init) |
| OTEL (optional) | `docker compose --profile otel up` |

## Run (from source)

```bash
# infra
docker compose up -d db rabbitmq

# API (applies EF migrations) — launchSettings binds http://localhost:5059
dotnet run --project src/Crawl.Api

# Worker (separate terminal) — health on http://localhost:8081 unless overridden
dotnet run --project src/Crawl.Worker --urls http://localhost:8081
```

Vite/UI defaults to `VITE_CRAWL_API_URL=http://localhost:5059` for source runs; Compose maps the API on **8080**.

## API

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/jobs` | `{ "url", "maxDepth?"(default 2), "maxPages?"(default 200) }` → `{ jobId }` |
| `GET` | `/api/jobs/{jobId}` | Status + progress summary (`pageCounts` + `progressPercent`) |
| `GET` | `/api/jobs/{jobId}/tree` | `{ jobId, status, root }` tree + Domain Link Ratio |
| `GET` | `/api/jobs/{jobId}/pages/{pageId}/links` | Outgoing links for a page |
| `GET` | `/api/jobs?page=&pageSize=` | History (newest first) |
| `POST` | `/api/jobs/{jobId}/cancel` | Cancel in-flight job (`204`) |
| `GET` | `/health` | Postgres + RabbitMQ checks |
| `GET` | `/api/ops/queue` | Queue depth / consumer count / scale hints |
| `GET` | `/api/ops/inflight` | Pages queued+processing + active jobs |
| Hub | `/hubs/crawl-jobs` | SignalR: `SubscribeJob(jobId)` → `jobProgress` pushes |

Worker also exposes `GET /ops/queue` (same depth snapshot) and `GET /health` so extra ECS tasks can join safely.

## Message schema

Queue `crawl.page` (DLQ `crawl.page.dlq` via `crawl.page.dlx`):

```json
{
  "jobId": "guid",
  "pageId": 123,
  "url": "https://example.com/",
  "depth": 0,
  "enqueuedAt": "2026-11-10T00:00:00Z"
}
```

| Header / property | Purpose |
|-------------------|---------|
| `x-retry-count` | Transient retries (default max 3); exhausted → DLQ |
| `x-crawl-depth` | Depth mirror for ops |
| AMQP `priority` | BFS-aware: shallower pages drain first (`x-max-priority=10`) |

Progress fanout `crawl.job.events`: workers publish `CrawlJobProgressEvent`; each API instance binds an exclusive queue and relays to SignalR group `job:{jobId}`.

If an older `crawl.page` queue was declared without `x-max-priority`, delete that queue once (or set `RabbitMq__EnableDepthPriority=false`) before restarting.

## Stretch — incident monitor (ops)

Optional AI-assisted fault monitor (`CrawlAi:IncidentMonitorEnabled`): worker emits structured faults → classifier/backoff → DeepSeek remediation advice → politeness/`IEgressProxyProvider` (fail closed) → `GET /api/ai/jobs/{id}/incidents`. See [crawl-readme.md](crawl-readme.md#stretch-ai-crawl-incident-monitor-ops--observability). Does **not** replace Domain Link Ratio or RabbitMQ parsing.

## Engine (Scrapy-inspired job crawler)

Not a Scrapy port — a .NET job crawler with the pieces that matter for correct HTML crawl graphs:

| Capability | Behavior |
|------------|----------|
| URL canonicalization | Scheme/host lowercased (IDN→punycode), default ports stripped, fragment + user-info removed, empty path → `/`, **query params sorted** (configurable) |
| Scheduling | Message-per-page; depth priority + stable URL order within depth (BFS-aware) |
| Politeness | Per-host delay (`CrawlHttp:PolitenessDelayMilliseconds`, default 250ms) + optional Postgres advisory lock across workers (`DistributedHostPoliteness`) |
| Concurrency | `MaxConcurrentFetches` per worker + RabbitMQ prefetch; multi-worker competing consumers |
| Page claim | Atomic Queued→Processing; redelivered Processing reclaims after crash; duplicates skip (no double-count) |
| robots.txt | Configurable (`RespectRobotsTxt`, default on); honors Disallow/Allow longest-prefix + `Crawl-delay`; fail-open on fetch errors |
| HTML-only | Content-Type check + byte sniff when type missing/generic; size cap |
| Charset | `Content-Type` charset → `<meta charset>` → UTF-8 |
| Redirects | Manual follow, max hops, loop guard; robots re-checked on target |
| Retry taxonomy | `TransientCrawlException` (408/429/5xx/network) → retry; poison/other → DLQ; 4xx/non-HTML → page Failed/Skipped |
| Metrics | `Crawl.Worker` meter: processed/failed/skipped/robots_blocked/retries/poison + fetch duration; job `progressPercent` |

**Hard rule:** Domain Link Ratio, URL normalization, and edge/page upserts are deterministic .NET — never AI.

### `CrawlHttp` knobs

```json
{
  "TimeoutSeconds": 15,
  "MaxRetryAttempts": 3,
  "MaxResponseBytes": 2000000,
  "MaxConcurrentFetches": 4,
  "PolitenessDelayMilliseconds": 250,
  "RespectRobotsTxt": true,
  "MaxRedirects": 5,
  "SortQueryParameters": true,
  "DistributedHostPoliteness": true
}
```

## Elastic workers (queue-backed)

Primary scale path for long/large jobs: **add `Crawl.Worker` consumers** on RabbitMQ (Compose replicas or ECS `desired_count` / ASG). Idempotent upserts + atomic page claims make joining safe.

| Signal | Where |
|--------|-------|
| `messagesReady` / `consumerCount` / DLQ depth | `GET /api/ops/queue`, worker `GET /ops/queue` |
| In-flight pages | `GET /api/ops/inflight` |
| Cloud sketch | `infra/terraform/modules/crawl-ecs/autoscaling.tf` (App Auto Scaling + CW alarms; **no apply**) |

Per-job ephemeral EC2 provision/handoff/delete remains stretch **next**, not the graded primary path.

## SignalR realtime

Job Details subscribes to `/hubs/crawl-jobs` for `jobProgress` (status, `%`, page counts). REST create/history/tree stay mandatory so the PDF flow works if WebSockets drop; UI falls back to polling automatically.

## Idempotency

- Unique `(job_id, url)` on `crawl_pages`
- Unique `(source_page_id, target_url)` on `crawl_edges`
- Terminal page statuses are no-ops on redelivery
- Atomic claim prevents multi-worker double-count / double Done
- Child inserts tolerate unique races between workers
- Canonical URLs (including sorted query) shrink duplicate fan-out before the DB unique indexes fire
- Duplicate queue messages (at-least-once): MessageId `{jobId}:{pageId}` + claim/terminal short-circuit

## Domain Link Ratio

`internal_http(s)_links_on_start_host / total_http(s)_outgoing_links` (0 when no links). mailto/tel/non-http ignored. **Not** computed by AI.

## vs [WMalka/UrlCrawler](https://github.com/WMalka/UrlCrawler)

UrlCrawler is a strong timed take-home (and an explicit cut list). This engine closes the crawl-quality gaps they deferred, while staying HTML-only / depth / max-pages / DLR / RabbitMQ rubric-correct:

| Gap in UrlCrawler | This assessment crawler |
|-------------------|-------------------------|
| robots.txt / politeness explicitly **cut** | robots.txt + Crawl-delay + per-host delay |
| No concurrent-fetch / DOWNLOAD_DELAY story beyond MassTransit prefetch | `MaxConcurrentFetches` + `HostPolitenessGate` |
| URL normalize keeps query order as-is | Optional **sorted query** for stronger dedup |
| Auto/handler redirects; limited loop story | Manual redirect policy, max hops, loop set, robots on target |
| Content-Type allow-list only | Allow-list + HTML sniff + charset decode |
| Retries via resilience; less explicit poison taxonomy in-app | `TransientCrawlException` vs poison → DLQ; metrics counters |
| No OTEL metrics (logs/health only) | OTEL hooks + `Crawl.Worker` instruments + `progressPercent` |
| SQL Server + MassTransit outbox | PostgreSQL / Aurora path + Amazon MQ sketch; Compose demo |
| No assistive AI (by design) | AI overlays **assist** operators; never own links/DLR |

Still not a full Scrapy port (no JS render, no broad middleware ecosystem, no spider contracts). Intentional: graded **job crawler** with Scrapy-class fetch hygiene.

## Tests

```bash
dotnet test Crawl.sln
```

Domain: normalizer edge URLs, robots rules, depth priority, DLR, page-claim matrix, scale-signal heuristics.  
Integration: HTML fixtures (incl. query/host/fragment edges), content sniff/charset, dedup stress + unique index shape, distributed edge cases (redirect loop, Crawl-delay, transient vs poison, progress payload hygiene).

## Observability

Structured Serilog logs include `JobId` (no PHI/passwords). OpenTelemetry tracing hooks are registered; set `OTEL_EXPORTER_OTLP_ENDPOINT` to export. Worker meters: `crawl.pages.*`, `crawl.retries.transient`, `crawl.messages.poison`, `crawl.messages.duplicate_skipped`, `crawl.pages.claimed` / `reclaimed`, `crawl.fetch.duration_ms`.
