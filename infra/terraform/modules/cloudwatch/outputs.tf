output "api_log_group_name" {
  value = aws_cloudwatch_log_group.api.name
}

output "api_log_group_arn" {
  value = aws_cloudwatch_log_group.api.arn
}

output "worker_log_group_name" {
  value = aws_cloudwatch_log_group.worker.name
}

output "worker_log_group_arn" {
  value = aws_cloudwatch_log_group.worker.arn
}

output "audit_log_group_name" {
  value = aws_cloudwatch_log_group.audit.name
}

output "audit_log_group_arn" {
  value = aws_cloudwatch_log_group.audit.arn
}
