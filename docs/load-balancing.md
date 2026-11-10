# Load balancing & scaling

How HTTP traffic fans out in the assessment SaaS scaffold, and how to scale **API** vs **worker** capacity independently. Infra is Terraform-only prep — **do not `terraform apply` until assessment day.**

## Traffic path

```
Clients
  → ALB (public subnets, ≥2 AZs, TLS on :443)
    → Target group `api` (target_type=ip, stickiness OFF)
      → ECS Fargate API tasks (private subnets, multi-AZ)
        → Aurora Postgres + ElastiCache Redis (private)
```

Workers are **not** registered with the ALB. They consume async jobs (Redis/SQS sketch) and scale on their own ECS service policies.

### Why sticky sessions are OFF

API auth is JWT (stateless access + Redis-backed refresh/blacklist). Stickiness would pin clients to one task and under-use new capacity during scale-out. Cross-task session state belongs in Redis, not on a single container.

### Multi-AZ

- ALB spans public subnets in at least two AZs (AWS requirement).
- ECS API/worker services use private subnets in the same AZs.
- Prefer `api_min_capacity ≥ 2` in prod so an AZ blip does not take the API offline.
- Cross-zone load balancing on the ALB keeps request spread even when task counts differ per AZ.

## Health checks (LB-suitable)

| Endpoint | Meaning | Who calls it |
|----------|---------|--------------|
| `GET /health` | Process liveness (always cheap) | **ALB target group** health check |
| `GET /health/ready` | Redis/DB readiness; `503` when not ready | Deploy/ops gates — **not** the ALB path |

Keep the ALB on `/health`. Pointing the TG at readiness would deregister tasks during brief Redis flaps.

Request correlation: every response includes `X-Request-ID` (echoed or generated). Safe across tasks; never put PHI in that header.

## Autoscaling levers

Defined in `infra/terraform/modules/ecs/autoscaling.tf` (Application Auto Scaling target tracking):

| Service | Signals | When to raise capacity |
|---------|---------|------------------------|
| **API** | CPU %, memory %, ALB `RequestCountPerTarget` | Latency up, TG request rate high, or CPU/mem near targets |
| **Worker** | CPU %, memory % | Job backlog growing; CPU-bound image/AI work |

Bootstrap `desired_count` is only the starting floor. Runtime DesiredCount is owned by autoscaling (`lifecycle.ignore_changes` on the ECS services).

### Scaling API vs workers

- **More interactive traffic / auth / CRUD** → raise `api_max_capacity` / lower `api_requests_per_target` (or CPU target). ALB automatically spreads across new task IPs.
- **More async processing (Rekognition, DeepSeek jobs, email fan-out)** → raise `worker_min_capacity` / `worker_max_capacity`. Does not change ALB registration.
- Do not scale workers to fix HTTP 5xx from the API, or scale API to clear a Redis queue.

Optional assessment-day follow-up: publish queue depth as a custom CloudWatch metric and add a worker step scaling policy.

## App-level complements

- **Redis rate limit** (`RATE_LIMIT_ENABLED`, off by default) — shared counters across tasks (required with stickiness off). WAF remains edge abuse control.
- **Auth / SES** — unchanged by this scaffold; login and alert paths stay on the API tasks behind the ALB.

## Terraform files to review

| Path | What it scaffolds |
|------|-------------------|
| `infra/terraform/modules/alb/main.tf` | ALB, API TG, health check, **stickiness disabled**, multi-AZ notes |
| `infra/terraform/modules/ecs/main.tf` | API + worker services, `desired_count`, TG attachment |
| `infra/terraform/modules/ecs/autoscaling.tf` | CPU / memory / request-count policies |
| `infra/terraform/environments/*.tfvars.example` | Capacity knobs for dev/prod |

Validate only:

```bash
cd infra/terraform
terraform fmt -recursive
terraform init -backend=false
terraform validate
```
