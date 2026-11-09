output "alb_arn" {
  value = aws_lb.this.arn
}

output "alb_dns_name" {
  value = aws_lb.this.dns_name
}

output "alb_arn_suffix" {
  description = "ALB ARN suffix for CloudWatch/App Autoscaling ALB metrics"
  value       = aws_lb.this.arn_suffix
}

output "api_target_group_arn" {
  value = aws_lb_target_group.api.arn
}

output "api_target_group_arn_suffix" {
  description = "Target group ARN suffix for RequestCountPerTarget scaling"
  value       = aws_lb_target_group.api.arn_suffix
}

output "https_listener_arn" {
  value = try(aws_lb_listener.https[0].arn, null)
}
