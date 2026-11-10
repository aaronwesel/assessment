# Amazon SES identity + configuration set placeholders for ops/login alerts.
# DO NOT terraform apply until assessment day.
#
# App wiring: backend/app/clients/ses.py + alerts (LOGIN_ALERT_*). Bodies must
# stay free of PHI; TLS is required on the configuration set.
#
# Assessment-day fill-ins:
# - Verify domain (or email) identity in the account/region
# - Exit SES sandbox (production access) before volume sends
# - Align FROM address with verified identity
# - Optional: SNS topics for bounce/complaint → CloudWatch alarms

resource "aws_ses_domain_identity" "alerts" {
  count = var.enable_ses && var.domain_name != "" ? 1 : 0

  domain = var.domain_name
}

resource "aws_ses_email_identity" "from_address" {
  count = var.enable_ses && var.from_email != "" ? 1 : 0

  email = var.from_email
}

resource "aws_ses_configuration_set" "alerts" {
  count = var.enable_ses ? 1 : 0

  name = "${var.name_prefix}-alerts"

  delivery_options {
    tls_policy = "Require"
  }

  # Reputation metrics feed CloudWatch (see README CloudWatch notes).
  reputation_metrics_enabled = true
  sending_enabled            = true
}

# Event destination placeholder — wire SNS/CloudWatch on assessment day.
# Kept as a comment sketch so prep does not create unused SNS topics.
#
# resource "aws_ses_event_destination" "cloudwatch" {
#   name                   = "${var.name_prefix}-ses-cw"
#   configuration_set_name = aws_ses_configuration_set.alerts[0].name
#   enabled                = true
#   matching_types         = ["send", "reject", "bounce", "complaint", "delivery"]
#   cloud_watch_destination {
#     dimension_name  = "ses:configuration-set"
#     dimension_value  = aws_ses_configuration_set.alerts[0].name
#     default_value    = "default"
#     value_source     = "messageTag"
#   }
# }
