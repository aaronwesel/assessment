# =============================================================================
# Amazon MQ (RabbitMQ) — SKETCH ONLY. DO NOT terraform apply until authorized.
# =============================================================================
# Local parity: docker compose service `rabbitmq`.
# Cloud: managed RabbitMQ engine for Crawl.Worker competing consumers + DLQ.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

resource "aws_mq_broker" "crawl" {
  broker_name         = "${var.name_prefix}-crawl-mq"
  engine_type         = "RabbitMQ"
  engine_version      = var.engine_version
  host_instance_type  = var.host_instance_type
  deployment_mode     = var.deployment_mode
  publicly_accessible = false
  subnet_ids          = var.subnet_ids
  security_groups     = [var.security_group_id]

  user {
    username = var.admin_username
    # Password from Secrets Manager ARN at apply time — never commit values.
    password = var.admin_password
  }

  encryption_options {
    use_aws_owned_key = false
    kms_key_id        = var.kms_key_arn
  }

  logs {
    general = true
  }

  tags = merge(var.tags, {
    Name    = "${var.name_prefix}-crawl-mq"
    Service = "crawl"
  })
}
