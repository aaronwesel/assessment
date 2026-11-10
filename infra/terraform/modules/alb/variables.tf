variable "name_prefix" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "public_subnet_ids" {
  description = "Public subnet IDs across ≥2 AZs (ALB multi-AZ requirement)"
  type        = list(string)
}

variable "security_group_id" {
  type = string
}

variable "acm_certificate_arn" {
  type    = string
  default = ""
}

variable "health_check_path" {
  description = "ALB target-group health check path (API liveness; keep cheap)"
  type        = string
  default     = "/health"
}

variable "deregistration_delay_seconds" {
  description = "Seconds to drain connections when deregistering a target"
  type        = number
  default     = 30
}

variable "tags" {
  type    = map(string)
  default = {}
}
