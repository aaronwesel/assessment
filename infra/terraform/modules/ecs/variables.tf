variable "name_prefix" {
  type = string
}

variable "private_subnet_ids" {
  type = list(string)
}

variable "ecs_security_group_id" {
  type = string
}

variable "api_target_group_arn" {
  type = string
}

variable "api_target_group_arn_suffix" {
  description = "TG ARN suffix for ALBRequestCountPerTarget autoscaling"
  type        = string
}

variable "alb_arn_suffix" {
  description = "ALB ARN suffix for ALBRequestCountPerTarget autoscaling"
  type        = string
}

variable "api_image" {
  type = string
}

variable "worker_image" {
  type = string
}

variable "api_cpu" {
  type = number
}

variable "api_memory" {
  type = number
}

variable "worker_cpu" {
  type = number
}

variable "worker_memory" {
  type = number
}

variable "api_desired_count" {
  description = "Bootstrap DesiredCount for API (autoscaling owns runtime)"
  type        = number
}

variable "worker_desired_count" {
  description = "Bootstrap DesiredCount for workers (autoscaling owns runtime)"
  type        = number
}

variable "api_min_capacity" {
  description = "Autoscaling min tasks for API (≥2 recommended for multi-AZ)"
  type        = number
  default     = 2
}

variable "api_max_capacity" {
  description = "Autoscaling max tasks for API"
  type        = number
  default     = 8
}

variable "worker_min_capacity" {
  description = "Autoscaling min tasks for workers"
  type        = number
  default     = 1
}

variable "worker_max_capacity" {
  description = "Autoscaling max tasks for workers"
  type        = number
  default     = 6
}

variable "api_cpu_target_percent" {
  description = "Target average CPU % for API service"
  type        = number
  default     = 60
}

variable "api_memory_target_percent" {
  description = "Target average memory % for API service"
  type        = number
  default     = 70
}

variable "api_requests_per_target" {
  description = "Target ALB RequestCountPerTarget for API scale-out"
  type        = number
  default     = 1000
}

variable "worker_cpu_target_percent" {
  description = "Target average CPU % for worker service"
  type        = number
  default     = 65
}

variable "worker_memory_target_percent" {
  description = "Target average memory % for worker service"
  type        = number
  default     = 75
}

variable "scale_in_cooldown_seconds" {
  type    = number
  default = 120
}

variable "scale_out_cooldown_seconds" {
  type    = number
  default = 60
}

variable "api_health_check_grace_period_seconds" {
  description = "Seconds to ignore ALB health failures after task start"
  type        = number
  default     = 60
}

variable "execution_role_arn" {
  type = string
}

variable "task_role_arn" {
  type = string
}

variable "api_log_group_name" {
  type = string
}

variable "worker_log_group_name" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "app_secret_arn" {
  type = string
}

variable "db_secret_arn" {
  type = string
}

variable "redis_secret_arn" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
