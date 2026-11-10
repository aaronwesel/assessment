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

## Layout

```
infra/
  README.md                 # this file
  terraform/
    README.md               # usage, HIPAA defaults, apply gate
    versions.tf
    providers.tf
    variables.tf
    locals.tf
    outputs.tf
    main.tf                 # wires modules
    environments/
      dev.tfvars.example
      prod.tfvars.example
    modules/
      vpc/
      security/
      alb/
      ecr/
      ecs/
      rds/
      redis/
      secrets/
      kms/
      cloudwatch/
      waf/
      iam/
```

## Quick validate (no apply)

```bash
cd infra/terraform
terraform fmt -recursive
terraform init -backend=false
terraform validate
```

See `terraform/README.md` for module inventory and HIPAA engineering defaults.
