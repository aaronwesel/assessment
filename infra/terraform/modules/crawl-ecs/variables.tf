variable "name_prefix" { type = string }
variable "aws_region" { type = string }
variable "cluster_arn" { type = string }
variable "private_subnet_ids" { type = list(string) }
variable "ecs_security_group_id" { type = string }
variable "execution_role_arn" { type = string }
variable "task_role_arn" { type = string }
variable "api_image" { type = string }
variable "worker_image" { type = string }
variable "api_log_group_name" { type = string }
variable "worker_log_group_name" { type = string }
variable "db_secret_arn" { type = string }
variable "mq_host_secret_arn" { type = string }
variable "mq_user_secret_arn" { type = string }
variable "mq_password_secret_arn" { type = string }

variable "api_target_group_arn" {
  type        = string
  description = "ALB TG for Crawl.Api; null to skip LB attachment in sketch."
  default     = null
}

variable "api_cpu" {
  type    = number
  default = 256
}
variable "api_memory" {
  type    = number
  default = 512
}
variable "worker_cpu" {
  type    = number
  default = 256
}
variable "worker_memory" {
  type    = number
  default = 512
}
variable "api_desired_count" {
  type    = number
  default = 1
}
variable "worker_desired_count" {
  type    = number
  default = 2
}
variable "queue_name" {
  type    = string
  default = "crawl.page"
}
variable "dlq_name" {
  type    = string
  default = "crawl.page.dlq"
}
variable "tags" {
  type    = map(string)
  default = {}
}

# --- Elastic workers (sketch; enable when apply authorized) ---

variable "enable_worker_autoscaling" {
  type        = bool
  description = "When true, sketch App Auto Scaling + CW alarms for crawl-worker desired_count."
  default     = false
}

variable "worker_min_count" {
  type    = number
  default = 1
}

variable "worker_max_count" {
  type    = number
  default = 8
}

variable "worker_scale_out_adjustment" {
  type    = number
  default = 2
}

variable "worker_scale_in_adjustment" {
  type    = number
  default = -1
}

variable "worker_scale_out_cooldown_seconds" {
  type    = number
  default = 60
}

variable "worker_scale_in_cooldown_seconds" {
  type    = number
  default = 120
}

variable "queue_depth_metric_namespace" {
  type    = string
  default = "Crawl/Assessment"
}

variable "queue_depth_metric_name" {
  type    = string
  default = "PageQueueMessagesReady"
}

variable "queue_depth_scale_out_threshold" {
  type    = number
  default = 50
}

variable "queue_depth_scale_in_threshold" {
  type    = number
  default = 0
}

variable "queue_depth_metric_dimensions" {
  type    = map(string)
  default = { Queue = "crawl.page" }
}
