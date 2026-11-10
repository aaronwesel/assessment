variable "name_prefix" {
  type = string
}

variable "aws_region" {
  type = string
}

variable "ecr_api_repository_arn" {
  type = string
}

variable "ecr_worker_repository_arn" {
  type = string
}

variable "secrets_arns" {
  type = list(string)
}

variable "kms_key_arn" {
  type = string
}

variable "cloudwatch_log_group_arns" {
  type = list(string)
}

variable "tags" {
  type    = map(string)
  default = {}
}
