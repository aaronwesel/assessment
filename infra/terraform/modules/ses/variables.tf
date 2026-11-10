variable "name_prefix" {
  type = string
}

variable "enable_ses" {
  description = "Create SES identity/config-set placeholders (still DO NOT apply until assessment day)"
  type        = bool
  default     = true
}

variable "domain_name" {
  description = "Domain to verify for SES sending (placeholder until assessment day)"
  type        = string
  default     = ""
}

variable "from_email" {
  description = "Verified FROM email placeholder (must match SES identity)"
  type        = string
  default     = ""
}

variable "tags" {
  type    = map(string)
  default = {}
}
