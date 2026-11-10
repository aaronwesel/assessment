# Infrastructure

Terraform-managed AWS scaffold for the assessment SaaS (ECS Fargate, ALB, Aurora/RDS Postgres, ElastiCache Redis, Secrets Manager, KMS, CloudWatch, WAF placeholder).

## Ownership

- This directory (`infra/`) is the Terraform workstream surface.
- Live application code lives outside this tree.

## Apply policy (mandatory)

**`terraform apply` is forbidden until assessment day.**

Until then, only:

- Edit / review this skeleton
- `terraform fmt`
- `terraform init -backend=false` (or local backend)
- `terraform validate`

Do **not** create real AWS resources from this prep branch.

Crawl cloud sketch (ECS + Amazon MQ, not wired into root `main.tf`): see [`terraform/CRAWL_CLOUD.md`](terraform/CRAWL_CLOUD.md).

## Layout

```
infra/
  README.md                 # this file
  otel/                     # local OTEL collector config (Compose --profile otel)
  terraform/
    README.md               # usage, HIPAA defaults, apply gate
    CRAWL_CLOUD.md          # crawl ECS + MQ + Aurora mapping (no apply)
    versions.tf
    providers.tf
    variables.tf
    locals.tf
    outputs.tf
    main.tf                 # wires platform modules (not crawl sketches yet)
    environments/
      dev.tfvars.example
      prod.tfvars.example
    modules/
      vpc/
      security/
      alb/          # TG + stickiness OFF + multi-AZ notes
      ecr/
      ecs/          # services + autoscaling.tf (CPU/mem/requests)
      rds/
      redis/
      secrets/
      kms/
      cloudwatch/
      waf/
      iam/
      amazon-mq/    # SKETCH — Crawl RabbitMQ broker
      crawl-ecs/    # SKETCH — Crawl.Api + Crawl.Worker tasks
```

## Quick validate (no apply)

```bash
cd infra/terraform
terraform fmt -recursive
terraform init -backend=false
terraform validate
```

See `terraform/README.md` for module inventory and HIPAA engineering defaults.
