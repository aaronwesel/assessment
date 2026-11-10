# crawl-ecs (sketch)

ECS Fargate task definitions + services for **Crawl.Api** and **Crawl.Worker**.

- Env/secrets mirror Compose: `ConnectionStrings__Crawl`, `RabbitMq__*`, OTEL service names.
- Worker `desired_count` ignored after apply so queue-depth autoscaling can own capacity.
- **Elastic path (primary):** add `Crawl.Worker` competing consumers on RabbitMQ / Amazon MQ — see `autoscaling.tf` (App Auto Scaling + CW alarms on queue depth). Scale-in after drain.
- **Not** a custom per-job EC2 provisioner (that remains stretch-next / operator sketch).
- Worker health on `:8081/health`; local scale probe `/ops/queue` (API: `/api/ops/queue`).
- API hosts SignalR `/hubs/crawl-jobs`; prefer ALB stickiness if multiple API tasks.
- **Not** referenced from root `main.tf` yet — validate module in isolation if desired; never apply from CI.
