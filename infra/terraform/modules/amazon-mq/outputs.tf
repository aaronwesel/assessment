output "broker_id" {
  value       = aws_mq_broker.crawl.id
  description = "Amazon MQ broker id."
}

output "primary_amqp_endpoint" {
  value       = try(aws_mq_broker.crawl.instances[0].endpoints[0], null)
  description = "Primary AMQP endpoint for Crawl.Api / Worker RabbitMq__Host."
}

output "broker_arn" {
  value       = aws_mq_broker.crawl.arn
  description = "Broker ARN for IAM / alarms."
}
