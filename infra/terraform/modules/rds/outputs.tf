output "cluster_endpoint" {
  value     = aws_rds_cluster.this.endpoint
  sensitive = true
}

output "cluster_reader_endpoint" {
  value     = aws_rds_cluster.this.reader_endpoint
  sensitive = true
}

output "cluster_id" {
  value = aws_rds_cluster.this.id
}

output "port" {
  value = aws_rds_cluster.this.port
}
