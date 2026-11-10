# Crawl cloud sketch (NO APPLY)

Graded crawler runs as **the same .NET Crawl.Api + Crawl.Worker** on AWS. Local Compose is the demo path; this document is the production shape for **reading job results from the cloud API**.

## Mapping

| Concern | Local | Cloud |
|---------|-------|-------|
| API | `crawl-api` :8080 | ECS Fargate + ALB target group `/health/*` |
| Worker | `crawl-worker` (N replicas) | ECS Fargate; scale on MQ queue depth / CPU |
| SQL | Postgres `crawl` DB | Aurora PostgreSQL (prep `modules/rds`) |
| Broker | RabbitMQ Compose | **Amazon MQ** RabbitMQ engine (`modules/amazon-mq`) |
| Frontend | nginx SPA :3000 | CloudFront/S3 or ECS; auth via platform |
| Secrets | `.env` | Secrets Manager + KMS (prep modules) |
| Telemetry | optional OTEL collector | ADOT sidecar / OTLP |

## Modules in this sketch

| Path | Status |
|------|--------|
| `modules/amazon-mq/` | Sketch — broker, subnet groups, SG placeholders |
| `modules/crawl-ecs/` | Sketch — task defs for crawl-api / crawl-worker, env for MQ + Aurora |
| Prep `modules/ecs`, `alb`, `rds`, `ecr`, … | Existing platform scaffold; **reuse patterns**, do not dual-apply blindly |

Root `main.tf` does **not** instantiate crawl modules yet — avoids accidental apply coupling. Wire on assessment day after Compose is green and apply is authorized.

## Operator access to results

1. Deploy Crawl.Api behind ALB (HTTPS).  
2. React (CloudFront) calls the **cloud Crawl API** (`/api/jobs/...`) with cloud auth.  
3. Workers consume Amazon MQ; pages/edges land in Aurora.  
4. History / tree / DLR are served from Aurora via Crawl.Api — identical contracts to local.

## Still forbidden

```text
terraform apply
terraform destroy
```

Allowed prep commands only: `fmt`, `init -backend=false`, `validate`.
