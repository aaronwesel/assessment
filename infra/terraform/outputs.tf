output "vpc_id" {
  description = "VPC ID"
  value       = module.vpc.vpc_id
}

output "private_subnet_ids" {
  description = "Private subnet IDs (ECS, RDS, Redis)"
  value       = module.vpc.private_subnet_ids
}

output "public_subnet_ids" {
  description = "Public subnet IDs (ALB)"
  value       = module.vpc.public_subnet_ids
}

output "alb_dns_name" {
  description = "Application Load Balancer DNS name"
  value       = module.alb.alb_dns_name
}

output "ecr_api_repository_url" {
  description = "ECR repository URL for API"
  value       = module.ecr.api_repository_url
}

output "ecr_worker_repository_url" {
  description = "ECR repository URL for worker"
  value       = module.ecr.worker_repository_url
}

output "ecs_cluster_name" {
  description = "ECS cluster name"
  value       = module.ecs.cluster_name
}

output "api_autoscaling_range" {
  description = "API ECS autoscaling min/max capacity"
  value = {
    min = module.ecs.api_autoscaling_min
    max = module.ecs.api_autoscaling_max
  }
}

output "worker_autoscaling_range" {
  description = "Worker ECS autoscaling min/max capacity"
  value = {
    min = module.ecs.worker_autoscaling_min
    max = module.ecs.worker_autoscaling_max
  }
}

output "rds_cluster_endpoint" {
  description = "Aurora/RDS writer endpoint"
  value       = module.rds.cluster_endpoint
  sensitive   = true
}

output "redis_primary_endpoint" {
  description = "ElastiCache Redis primary endpoint"
  value       = module.redis.primary_endpoint
  sensitive   = true
}

output "kms_key_arn" {
  description = "Primary CMK ARN"
  value       = module.kms.key_arn
}

output "secrets_app_arn" {
  description = "Application secrets ARN (Secrets Manager)"
  value       = module.secrets.app_secret_arn
}

output "cloudwatch_api_log_group" {
  description = "API CloudWatch log group name"
  value       = module.cloudwatch.api_log_group_name
}

output "waf_web_acl_arn" {
  description = "WAF Web ACL ARN (placeholder)"
  value       = module.waf.web_acl_arn
}

output "ses_configuration_set_name" {
  description = "SES alerts configuration set name (placeholder)"
  value       = module.ses.configuration_set_name
}

output "ses_from_email" {
  description = "SES FROM email placeholder"
  value       = module.ses.from_email
}

output "apply_gate" {
  description = "Reminder: apply is forbidden until assessment day"
  value       = "FORBIDDEN_UNTIL_ASSESSMENT_DAY"
}
