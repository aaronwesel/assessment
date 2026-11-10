resource "aws_elasticache_subnet_group" "this" {
  name       = "${var.name_prefix}-redis"
  subnet_ids = var.subnet_ids

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-redis-subnets"
  })
}

resource "random_password" "auth_token" {
  length  = 32
  special = false
}

resource "aws_elasticache_replication_group" "this" {
  replication_group_id = substr("${var.name_prefix}-redis", 0, 40)
  description          = "${var.name_prefix} Redis (sessions, rate limits, queues)"

  engine               = "redis"
  engine_version       = "7.1"
  node_type            = var.node_type
  num_cache_clusters   = 2
  port                 = 6379
  parameter_group_name = "default.redis7"

  subnet_group_name  = aws_elasticache_subnet_group.this.name
  security_group_ids = [var.security_group_id]

  at_rest_encryption_enabled = true
  transit_encryption_enabled = true
  auth_token                 = random_password.auth_token.result
  kms_key_id                 = var.kms_key_arn

  automatic_failover_enabled = true
  multi_az_enabled           = true

  snapshot_retention_limit = 3
  apply_immediately        = true

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-redis"
  })
}
