output "primary_endpoint" {
  value     = aws_elasticache_replication_group.this.primary_endpoint_address
  sensitive = true
}

output "reader_endpoint" {
  value     = aws_elasticache_replication_group.this.reader_endpoint_address
  sensitive = true
}

output "port" {
  value = aws_elasticache_replication_group.this.port
}

output "replication_group_id" {
  value = aws_elasticache_replication_group.this.id
}
