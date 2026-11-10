variable "name_prefix" {
  type = string
}

variable "enable_waf" {
  type    = bool
  default = true
}

variable "alb_arn" {
  type = string
}

variable "tags" {
  type    = map(string)
  default = {}
}
