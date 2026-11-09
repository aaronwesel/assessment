output "execution_role_arn" {
  value = aws_iam_role.ecs_execution.arn
}

output "task_role_arn" {
  value = aws_iam_role.ecs_task.arn
}

output "execution_role_name" {
  value = aws_iam_role.ecs_execution.name
}

output "task_role_name" {
  value = aws_iam_role.ecs_task.name
}
