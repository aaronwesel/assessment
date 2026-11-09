output "cluster_name" {
  value = aws_ecs_cluster.this.name
}

output "cluster_arn" {
  value = aws_ecs_cluster.this.arn
}

output "api_service_name" {
  value = aws_ecs_service.api.name
}

output "worker_service_name" {
  value = aws_ecs_service.worker.name
}

output "api_task_definition_arn" {
  value = aws_ecs_task_definition.api.arn
}

output "worker_task_definition_arn" {
  value = aws_ecs_task_definition.worker.arn
}

output "api_autoscaling_min" {
  value = aws_appautoscaling_target.api.min_capacity
}

output "api_autoscaling_max" {
  value = aws_appautoscaling_target.api.max_capacity
}

output "worker_autoscaling_min" {
  value = aws_appautoscaling_target.worker.min_capacity
}

output "worker_autoscaling_max" {
  value = aws_appautoscaling_target.worker.max_capacity
}
