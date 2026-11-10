output "configuration_set_name" {
  description = "SES configuration set name (empty when disabled)"
  value       = try(aws_ses_configuration_set.alerts[0].name, "")
}

output "domain_identity_arn" {
  description = "SES domain identity ARN (empty when not created)"
  value       = try(aws_ses_domain_identity.alerts[0].arn, "")
}

output "email_identity_arn" {
  description = "SES email identity ARN (empty when not created)"
  value       = try(aws_ses_email_identity.from_address[0].arn, "")
}

output "from_email" {
  description = "Configured FROM email placeholder"
  value       = var.from_email
}
