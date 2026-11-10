# Terraform skeleton (NOT APPLIED)

HIPAA-ready engineering controls are defaulted **on** in this scaffold (encryption at rest/in transit, private data plane, least-privilege IAM sketches, audit log groups). This is **not** a certification claim and **not** a BAA.

## DO NOT APPLY

| Allowed now | Forbidden until assessment day |
|-------------|-------------------------------|
| `terraform fmt -recursive` | `terraform apply` |
| `terraform init -backend=false` | `terraform destroy` |
| `terraform validate` | Creating real AWS resources from this tree |
| Review / fill placeholders | Committing secrets or real `.tfvars` with credentials |

Production apply happens only after the live/spec prompt, account confirmation, and deploy smoke checklist.

## Validate without applying

```bash
cd infra/terraform
terraform fmt -recursive
terraform init -backend=false
terraform validate
```

Optional plan dry-run (still no apply; requires AWS creds and filled tfvars — skip during prep if unsure):

```bash
# terraform plan -var-file=environments/dev.tfvars   # only when intentionally reviewing
```

## What this scaffold includes

| Component | Module / wiring | Notes |
|-----------|-----------------|-------|
| KMS CMKs | `modules/kms` | RDS, Redis, Secrets, CloudWatch logs |
| VPC | `modules/vpc` | Public + private subnets; NAT sketch |
| Security groups | `modules/security` | ALB → ECS → DB/Redis least privilege |
| ALB + TLS | `modules/alb` | HTTPS; API TG; stickiness OFF; multi-AZ notes |
| ECR | `modules/ecr` | API + worker repos; scan on push |
| ECS Fargate | `modules/ecs` | API + worker; desired_count + App Autoscaling (CPU/mem/requests) |
| Aurora/RDS Postgres | `modules/rds` | Private; storage encrypted; SSL |
| ElastiCache Redis | `modules/redis` | Private; at-rest + in-transit encryption |
| Secrets Manager | `modules/secrets` | App/DB/Redis secret shells (no values committed); app JSON includes `SLACK_WEBHOOK_URL` / `SLACK_ENABLED=false` placeholders for optional Incoming Webhooks |
| CloudWatch | `modules/cloudwatch` | Log groups + retention |
| WAF | `modules/waf` | Placeholder association to ALB |
| IAM | `modules/iam` | Execution + task roles; Rekognition + SES send on task role |
| SES | `modules/ses` | Domain/email identity + config set placeholders; CloudWatch metrics notes |

## Environments

Copy examples; never commit real secrets:

```bash
cp environments/dev.tfvars.example environments/dev.tfvars
cp environments/prod.tfvars.example environments/prod.tfvars
```

## Rekognition

The ECS **task role** includes an IAM permission sketch for Amazon Rekognition APIs (labels / moderation / detect text as needed). Prefer task-role credentials over long-lived access keys in the app.

## Load balancing & autoscaling

See [`docs/load-balancing.md`](../../docs/load-balancing.md). Summary:

- ALB fans out to API tasks only (`target_type=ip`, **sticky sessions disabled**).
- API autoscales on CPU, memory, and `ALBRequestCountPerTarget`.
- Workers autoscale on CPU/memory only (no ALB) — raise worker capacity for async backlog, not HTTP traffic.
- ALB health check path is `/health` (liveness); `/health/ready` is for deploy gates.

## SES (alerts)

`modules/ses` holds domain/email identity and configuration-set placeholders for login/system alerts. The ECS **task role** may `ses:SendEmail` / `ses:SendRawEmail` (scoped to identity ARNs when set). See `modules/ses/README.md` for CloudWatch bounce/complaint metric notes. Keep `SES_ENABLED=false` in the app until identities are verified. **Still do not apply.**

## DeepSeek / egress

DeepSeek is an external API. Keep it feature-flagged and non-PHI until vendor BAA posture is clear. Egress allowlisting is an assessment-day fill-in (security group / NAT / proxy policy), not applied here.

## Backend state

Default is local (prep). For assessment day, switch to a remote state backend (S3 + DynamoDB lock) in `versions.tf` / `backend` block — still only after apply is authorized.
