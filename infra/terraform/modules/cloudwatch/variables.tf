variable "name_prefix" {
  type = string
}

variable "retention_in_days" {
  type    = number
  default = 90
}

variable "kms_key_arn" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
