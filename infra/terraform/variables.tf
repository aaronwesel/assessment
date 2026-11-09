variable "project_name" {
  description = "Short project name used in resource naming"
  type        = string
  default     = "assessment"
}

variable "environment" {
  description = "Environment name (dev|prod)"
  type        = string
}

variable "aws_region" {
  description = "Primary AWS region"
  type        = string
  default     = "eu-west-1"
}

variable "vpc_cidr" {
  description = "VPC CIDR block"
  type        = string
  default     = "10.20.0.0/16"
}

variable "availability_zones" {
  description = "AZs for subnets (min 2 for ALB/RDS)"
  type        = list(string)
  default     = ["eu-west-1a", "eu-west-1b"]
}

variable "acm_certificate_arn" {
  description = "ACM certificate ARN for ALB HTTPS (placeholder until assessment day)"
  type        = string
  default     = ""
}

variable "api_image_tag" {
  description = "ECR image tag for API task"
  type        = string
  default     = "latest"
}

variable "worker_image_tag" {
  description = "ECR image tag for worker task"
  type        = string
  default     = "latest"
}

variable "api_cpu" {
  description = "Fargate CPU units for API"
  type        = number
  default     = 512
}

variable "api_memory" {
  description = "Fargate memory (MiB) for API"
  type        = number
  default     = 1024
}

variable "worker_cpu" {
  description = "Fargate CPU units for worker"
  type        = number
  default     = 512
}

variable "worker_memory" {
  description = "Fargate memory (MiB) for worker"
  type        = number
  default     = 1024
}

variable "api_desired_count" {
  description = "Bootstrap DesiredCount for API ECS service (autoscaling owns runtime)"
  type        = number
  default     = 2
}

variable "worker_desired_count" {
  description = "Bootstrap DesiredCount for worker ECS service (autoscaling owns runtime)"
  type        = number
  default     = 1
}

variable "api_min_capacity" {
  description = "API autoscaling minimum (≥2 for multi-AZ prod)"
  type        = number
  default     = 2
}

variable "api_max_capacity" {
  description = "API autoscaling maximum"
  type        = number
  default     = 8
}

variable "worker_min_capacity" {
  description = "Worker autoscaling minimum"
  type        = number
  default     = 1
}

variable "worker_max_capacity" {
  description = "Worker autoscaling maximum"
  type        = number
  default     = 6
}

variable "api_cpu_target_percent" {
  description = "API target tracking CPU utilization %"
  type        = number
  default     = 60
}

variable "api_memory_target_percent" {
  description = "API target tracking memory utilization %"
  type        = number
  default     = 70
}

variable "api_requests_per_target" {
  description = "ALB RequestCountPerTarget target for API scale-out"
  type        = number
  default     = 1000
}

variable "worker_cpu_target_percent" {
  description = "Worker target tracking CPU utilization %"
  type        = number
  default     = 65
}

variable "worker_memory_target_percent" {
  description = "Worker target tracking memory utilization %"
  type        = number
  default     = 75
}

variable "db_engine_version" {
  description = "Aurora PostgreSQL engine version"
  type        = string
  default     = "15.4"
}

variable "db_instance_class" {
  description = "Aurora instance class"
  type        = string
  default     = "db.t4g.medium"
}

variable "db_name" {
  description = "Initial database name"
  type        = string
  default     = "assessment"
}

variable "redis_node_type" {
  description = "ElastiCache node type"
  type        = string
  default     = "cache.t4g.micro"
}

variable "log_retention_days" {
  description = "CloudWatch log retention (days)"
  type        = number
  default     = 90
}

variable "enable_waf" {
  description = "Whether to create/associate WAF (placeholder module always defined; gate association)"
  type        = bool
  default     = true
}

variable "enable_ses" {
  description = "Create SES identity/config-set placeholders (DO NOT apply until assessment day)"
  type        = bool
  default     = true
}

variable "ses_domain_name" {
  description = "Domain for SES domain identity (placeholder until assessment day)"
  type        = string
  default     = ""
}

variable "ses_from_email" {
  description = "Verified FROM email for SES (placeholder; must match identity)"
  type        = string
  default     = ""
}

variable "enable_nat_gateway" {
  description = "Create NAT gateway for private subnet egress (costly; on for prod-like)"
  type        = bool
  default     = true
}

variable "tags" {
  description = "Additional resource tags"
  type        = map(string)
  default     = {}
}
