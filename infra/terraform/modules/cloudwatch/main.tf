resource "aws_cloudwatch_log_group" "api" {
  name              = "/ecs/${var.name_prefix}/api"
  retention_in_days = var.retention_in_days
  kms_key_id        = var.kms_key_arn

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-api-logs"
  })
}

resource "aws_cloudwatch_log_group" "worker" {
  name              = "/ecs/${var.name_prefix}/worker"
  retention_in_days = var.retention_in_days
  kms_key_id        = var.kms_key_arn

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-worker-logs"
  })
}

resource "aws_cloudwatch_log_group" "audit" {
  name              = "/ecs/${var.name_prefix}/audit"
  retention_in_days = var.retention_in_days
  kms_key_id        = var.kms_key_arn

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-audit-logs"
  })
}
