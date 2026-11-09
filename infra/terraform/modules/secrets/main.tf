# Secret shells only — never commit real values. Populate on assessment day.

resource "aws_secretsmanager_secret" "app" {
  name       = "${var.name_prefix}/app"
  kms_key_id = var.kms_key_arn

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-app-secret"
  })
}

resource "aws_secretsmanager_secret" "db" {
  name       = "${var.name_prefix}/db"
  kms_key_id = var.kms_key_arn

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-db-secret"
  })
}

resource "aws_secretsmanager_secret" "redis" {
  name       = "${var.name_prefix}/redis"
  kms_key_id = var.kms_key_arn

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-redis-secret"
  })
}

# Placeholder version so secret ARNs resolve; replace before apply with real JSON.
#
# Slack Incoming Webhook (optional team handoffs / deploy alerts):
# - Store the webhook URL here as SLACK_WEBHOOK_URL (never commit the real value).
# - Keep SLACK_ENABLED="false" until the URL is populated and message templates
#   are confirmed PHI-free (see docs/team-management.md).
# - This is a secret-shell note only — do NOT terraform apply during prep.
resource "aws_secretsmanager_secret_version" "app" {
  secret_id = aws_secretsmanager_secret.app.id
  secret_string = jsonencode({
    JWT_SECRET                  = "CHANGE_ME"
    GOOGLE_CLIENT_ID            = "CHANGE_ME"
    MICROSOFT_CLIENT_ID         = "CHANGE_ME"
    DEEPSEEK_API_KEY            = "CHANGE_ME"
    FEATURE_DEEPSEEK_ENABLED    = "false"
    FEATURE_REKOGNITION_ENABLED = "false"
    SLACK_WEBHOOK_URL           = "CHANGE_ME_INCOMING_WEBHOOK"
    SLACK_ENABLED               = "false"
  })
}

resource "aws_secretsmanager_secret_version" "db" {
  secret_id = aws_secretsmanager_secret.db.id
  secret_string = jsonencode({
    username = "assessment_admin"
    password = "CHANGE_ME_BEFORE_APPLY"
    engine   = "postgres"
    port     = 5432
    dbname   = "assessment"
  })
}

resource "aws_secretsmanager_secret_version" "redis" {
  secret_id = aws_secretsmanager_secret.redis.id
  secret_string = jsonencode({
    auth_token = "CHANGE_ME_BEFORE_APPLY"
  })
}
