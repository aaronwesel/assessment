output "crawl_api_task_definition_arn" {
  value = aws_ecs_task_definition.crawl_api.arn
}

output "crawl_worker_task_definition_arn" {
  value = aws_ecs_task_definition.crawl_worker.arn
}

output "crawl_api_service_name" {
  value = aws_ecs_service.crawl_api.name
}

output "crawl_worker_service_name" {
  value = aws_ecs_service.crawl_worker.name
}
