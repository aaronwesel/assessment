variable "name_prefix" {
  type        = string
  description = "Resource name prefix (e.g. assessment-dev)."
}

variable "subnet_ids" {
  type        = list(string)
  description = "Private subnet IDs for the broker (multi-AZ when deployment_mode requires)."
}

variable "security_group_id" {
  type        = string
  description = "SG allowing ECS crawl tasks to AMQP 5671 and management as needed."
}

variable "kms_key_arn" {
  type        = string
  description = "CMK for broker encryption at rest."
}

variable "engine_version" {
  type        = string
  description = "Amazon MQ RabbitMQ engine version."
  default     = "3.13"
}

variable "host_instance_type" {
  type        = string
  description = "Broker instance class."
  default     = "mq.t3.micro"
}

variable "deployment_mode" {
  type        = string
  description = "SINGLE_INSTANCE or CLUSTER_MULTI_AZ."
  default     = "SINGLE_INSTANCE"
}

variable "admin_username" {
  type        = string
  description = "Broker admin username (password via secret — not git)."
  default     = "crawl"
}

variable "admin_password" {
  type        = string
  description = "Broker admin password — inject from Secrets Manager at apply time."
  sensitive   = true
}

variable "tags" {
  type        = map(string)
  description = "Common tags."
  default     = {}
}
