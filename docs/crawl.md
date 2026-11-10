# Crawl assessment (.NET 8)

Graded job-based web crawler: **Crawl.Api** + **Crawl.Worker** + PostgreSQL + RabbitMQ.

## Run (Compose)

```bash
cp .env.example .env
docker compose up --build db rabbitmq crawl-api crawl-worker
```

| Service | URL |
|---------|-----|
| Crawl API + Swagger | http://localhost:8080/swagger |
| Crawl Worker health | http://localhost:8081/health |
| RabbitMQ management | http://localhost:15672 (`guest`/`guest`) |
| Postgres | same Compose `db` (`crawl_*` tables) |

## Run (from source)

```bash
# infra
docker compose up -d db rabbitmq

# API (applies EF migrations)
dotnet run --project src/Crawl.Api

# Worker (separate terminal)
dotnet run --project src/Crawl.Worker
```

## API

| Method | Path | Purpose |
|--------|------|---------|
| `POST` | `/api/jobs` | `{ "url", "maxDepth?"(default 2), "maxPages?"(default 200) }` → `{ jobId }` |
| `GET` | `/api/jobs/{jobId}` | Status + progress summary |
| `GET` | `/api/jobs/{jobId}/tree` | Hierarchical page tree + Domain Link Ratio |
| `GET` | `/api/jobs?page=&pageSize=` | History (newest first) |
| `POST` | `/api/jobs/{jobId}/cancel` | Cancel in-flight job |
| `GET` | `/health` | Postgres + RabbitMQ checks |

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

Header `x-retry-count` tracks transient retries (default max 3). Exhausted / poison → nack without requeue → DLQ.

## Idempotency

- Unique `(job_id, url)` on `crawl_pages`
- Unique `(source_page_id, target_url)` on `crawl_edges`
- Terminal page statuses are no-ops on redelivery
- Child inserts tolerate unique races between workers

## Domain Link Ratio

`internal_http(s)_links_on_start_host / total_http(s)_outgoing_links` (0 when no links). mailto/tel/non-http ignored. **Not** computed by AI.

## Tests

```bash
dotnet test Crawl.sln
```

## Observability

Structured Serilog logs include `JobId`. OpenTelemetry tracing hooks are registered; set `OTEL_EXPORTER_OTLP_ENDPOINT` to export.
