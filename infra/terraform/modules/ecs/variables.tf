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
  type = number
}

variable "worker_desired_count" {
  type = number
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
