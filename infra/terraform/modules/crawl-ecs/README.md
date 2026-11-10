# crawl-ecs (sketch)

ECS Fargate task definitions + services for **Crawl.Api** and **Crawl.Worker**.

- Env/secrets mirror Compose: `ConnectionStrings__CrawlDb`, `RabbitMq__*`, OTEL service names.
- Worker `desired_count` ignored after apply so queue-depth autoscaling can own capacity.
- **Not** referenced from root `main.tf` yet — validate module in isolation if desired; never apply from CI.
