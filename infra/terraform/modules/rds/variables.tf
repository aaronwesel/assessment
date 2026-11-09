variable "name_prefix" {
  type = string
}

variable "subnet_ids" {
  type = list(string)
}

variable "security_group_id" {
  type = string
}

variable "kms_key_arn" {
  type = string
}

variable "engine_version" {
  type = string
}

variable "instance_class" {
  type = string
}

variable "database_name" {
  type = string
}

variable "master_secret_arn" {
  type        = string
  description = "Secrets Manager ARN holding master username/password (assessment-day wiring)"
}

variable "tags" {
  type    = map(string)
  default = {}
}
