# Root wiring for the assessment AWS scaffold (HIPAA-ready defaults).
# DO NOT terraform apply until assessment day (see README.md).
#
# Dependency sketch:
#   KMS → encrypts RDS / Redis / Secrets / CloudWatch
#   VPC + security groups → private data plane
#   ALB (+ optional WAF) → public HTTPS → ECS Fargate API (stickiness OFF)
#   RDS + ElastiCache Redis → auth sessions / JWT blacklist / rate limits
#   SES → ops alerts (no PHI in bodies; see app.alerts)
#   IAM task role → least-privilege AWS API access (prefer over static keys)
#   ECS autoscaling → API on CPU/memory/ALB RequestCountPerTarget

module "kms" {
  source = "./modules/kms"

  name_prefix = local.name_prefix
  tags        = local.common_tags
}

module "vpc" {
  source = "./modules/vpc"

  name_prefix        = local.name_prefix
  cidr_block         = var.vpc_cidr
  availability_zones = var.availability_zones
  enable_nat_gateway = var.enable_nat_gateway
  tags               = local.common_tags
}

module "security" {
  source = "./modules/security"

  name_prefix = local.name_prefix
  vpc_id      = module.vpc.vpc_id
  tags        = local.common_tags
}

module "ecr" {
  source = "./modules/ecr"

  name_prefix = local.name_prefix
  kms_key_arn = module.kms.key_arn
  tags        = local.common_tags
}

module "secrets" {
  source = "./modules/secrets"

  name_prefix = local.name_prefix
  kms_key_arn = module.kms.key_arn
  tags        = local.common_tags
}

module "cloudwatch" {
  source = "./modules/cloudwatch"

  name_prefix       = local.name_prefix
  retention_in_days = var.log_retention_days
  kms_key_arn       = module.kms.key_arn
  tags              = local.common_tags
}

module "ses" {
  source = "./modules/ses"

  name_prefix = local.name_prefix
  enable_ses  = var.enable_ses
  domain_name = var.ses_domain_name
  from_email  = var.ses_from_email
  tags        = local.common_tags
}

module "iam" {
  source = "./modules/iam"

  name_prefix               = local.name_prefix
  aws_region                = var.aws_region
  ecr_api_repository_arn    = module.ecr.api_repository_arn
  ecr_worker_repository_arn = module.ecr.worker_repository_arn
  secrets_arns = [
    module.secrets.app_secret_arn,
    module.secrets.db_secret_arn,
    module.secrets.redis_secret_arn,
  ]
  kms_key_arn = module.kms.key_arn
  cloudwatch_log_group_arns = [
    module.cloudwatch.api_log_group_arn,
    module.cloudwatch.worker_log_group_arn,
  ]
  # Scope SES send to identities when placeholders are filled; else wildcard sketch.
  ses_identity_arns = compact([
    module.ses.domain_identity_arn,
    module.ses.email_identity_arn,
  ])
  tags = local.common_tags
}

module "alb" {
  source = "./modules/alb"

  name_prefix         = local.name_prefix
  vpc_id              = module.vpc.vpc_id
  public_subnet_ids   = module.vpc.public_subnet_ids
  security_group_id   = module.security.alb_security_group_id
  acm_certificate_arn = var.acm_certificate_arn
  tags                = local.common_tags
}

module "rds" {
  source = "./modules/rds"

  name_prefix       = local.name_prefix
  subnet_ids        = module.vpc.private_subnet_ids
  security_group_id = module.security.rds_security_group_id
  kms_key_arn       = module.kms.key_arn
  engine_version    = var.db_engine_version
  instance_class    = var.db_instance_class
  database_name     = var.db_name
  master_secret_arn = module.secrets.db_secret_arn
  tags              = local.common_tags
}

module "redis" {
  source = "./modules/redis"

  name_prefix       = local.name_prefix
  subnet_ids        = module.vpc.private_subnet_ids
  security_group_id = module.security.redis_security_group_id
  kms_key_arn       = module.kms.key_arn
  node_type         = var.redis_node_type
  tags              = local.common_tags
}

module "ecs" {
  source = "./modules/ecs"

  name_prefix                  = local.name_prefix
  private_subnet_ids           = module.vpc.private_subnet_ids
  ecs_security_group_id        = module.security.ecs_security_group_id
  api_target_group_arn         = module.alb.api_target_group_arn
  api_target_group_arn_suffix  = module.alb.api_target_group_arn_suffix
  alb_arn_suffix               = module.alb.alb_arn_suffix
  api_image                    = "${module.ecr.api_repository_url}:${var.api_image_tag}"
  worker_image                 = "${module.ecr.worker_repository_url}:${var.worker_image_tag}"
  api_cpu                      = var.api_cpu
  api_memory                   = var.api_memory
  worker_cpu                   = var.worker_cpu
  worker_memory                = var.worker_memory
  api_desired_count            = var.api_desired_count
  worker_desired_count         = var.worker_desired_count
  api_min_capacity             = var.api_min_capacity
  api_max_capacity             = var.api_max_capacity
  worker_min_capacity          = var.worker_min_capacity
  worker_max_capacity          = var.worker_max_capacity
  api_cpu_target_percent       = var.api_cpu_target_percent
  api_memory_target_percent    = var.api_memory_target_percent
  api_requests_per_target      = var.api_requests_per_target
  worker_cpu_target_percent    = var.worker_cpu_target_percent
  worker_memory_target_percent = var.worker_memory_target_percent
  execution_role_arn           = module.iam.execution_role_arn
  task_role_arn                = module.iam.task_role_arn
  api_log_group_name           = module.cloudwatch.api_log_group_name
  worker_log_group_name        = module.cloudwatch.worker_log_group_name
  aws_region                   = var.aws_region
  app_secret_arn               = module.secrets.app_secret_arn
  db_secret_arn                = module.secrets.db_secret_arn
  redis_secret_arn             = module.secrets.redis_secret_arn
  tags                         = local.common_tags
}

module "waf" {
  source = "./modules/waf"

  name_prefix = local.name_prefix
  enable_waf  = var.enable_waf
  alb_arn     = module.alb.alb_arn
  tags        = local.common_tags
}
