# =============================================================================
# Application Auto Scaling — API (behind ALB) + workers (queue consumers)
# =============================================================================
#
# Scaling model:
#   API     — horizontal scale on CPU, memory, AND ALB RequestCountPerTarget.
#             desired_count is the bootstrap floor; autoscaling owns runtime count
#             (see lifecycle.ignore_changes on the ECS services).
#   Worker  — scale on CPU/memory only (no ALB). For queue-depth scaling on
#             assessment day, add a custom metric (e.g. Redis list length /
#             SQS ApproximateNumberOfMessages) — not wired here.
#
# Why separate services:
#   - API capacity follows HTTP traffic; workers follow async job load.
#   - Scaling workers does not change ALB target registration.
#   - Scaling API changes how traffic fans out across healthy task IPs.
#
# Multi-AZ: tasks land in private_subnet_ids (multi-AZ). Autoscaling adds tasks;
# the ALB TG spreads new IPs automatically (stickiness OFF).
#
# DO NOT terraform apply until assessment day.

# -----------------------------------------------------------------------------
# API service autoscaling
# -----------------------------------------------------------------------------

resource "aws_appautoscaling_target" "api" {
  max_capacity       = var.api_max_capacity
  min_capacity       = var.api_min_capacity
  resource_id        = "service/${aws_ecs_cluster.this.name}/${aws_ecs_service.api.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

# CPU target tracking — primary signal for compute-bound request handlers.
resource "aws_appautoscaling_policy" "api_cpu" {
  name               = "${var.name_prefix}-api-cpu"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.api.resource_id
  scalable_dimension = aws_appautoscaling_target.api.scalable_dimension
  service_namespace  = aws_appautoscaling_target.api.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value       = var.api_cpu_target_percent
    scale_in_cooldown  = var.scale_in_cooldown_seconds
    scale_out_cooldown = var.scale_out_cooldown_seconds
  }
}

# Memory target tracking — catches cache-heavy / large-payload pressure.
resource "aws_appautoscaling_policy" "api_memory" {
  name               = "${var.name_prefix}-api-memory"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.api.resource_id
  scalable_dimension = aws_appautoscaling_target.api.scalable_dimension
  service_namespace  = aws_appautoscaling_target.api.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageMemoryUtilization"
    }
    target_value       = var.api_memory_target_percent
    scale_in_cooldown  = var.scale_in_cooldown_seconds
    scale_out_cooldown = var.scale_out_cooldown_seconds
  }
}

# Request-count-per-target — ALB-aware signal so busy TGs scale before CPU saturates.
# resource_label format: <alb-arn-suffix>/<tg-arn-suffix>
resource "aws_appautoscaling_policy" "api_request_count" {
  name               = "${var.name_prefix}-api-requests"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.api.resource_id
  scalable_dimension = aws_appautoscaling_target.api.scalable_dimension
  service_namespace  = aws_appautoscaling_target.api.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ALBRequestCountPerTarget"
      resource_label         = "${var.alb_arn_suffix}/${var.api_target_group_arn_suffix}"
    }
    target_value       = var.api_requests_per_target
    scale_in_cooldown  = var.scale_in_cooldown_seconds
    scale_out_cooldown = var.scale_out_cooldown_seconds
  }
}

# -----------------------------------------------------------------------------
# Worker service autoscaling (no ALB)
# -----------------------------------------------------------------------------

resource "aws_appautoscaling_target" "worker" {
  max_capacity       = var.worker_max_capacity
  min_capacity       = var.worker_min_capacity
  resource_id        = "service/${aws_ecs_cluster.this.name}/${aws_ecs_service.worker.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "worker_cpu" {
  name               = "${var.name_prefix}-worker-cpu"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.worker.resource_id
  scalable_dimension = aws_appautoscaling_target.worker.scalable_dimension
  service_namespace  = aws_appautoscaling_target.worker.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value       = var.worker_cpu_target_percent
    scale_in_cooldown  = var.scale_in_cooldown_seconds
    scale_out_cooldown = var.scale_out_cooldown_seconds
  }
}

resource "aws_appautoscaling_policy" "worker_memory" {
  name               = "${var.name_prefix}-worker-memory"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.worker.resource_id
  scalable_dimension = aws_appautoscaling_target.worker.scalable_dimension
  service_namespace  = aws_appautoscaling_target.worker.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageMemoryUtilization"
    }
    target_value       = var.worker_memory_target_percent
    scale_in_cooldown  = var.scale_in_cooldown_seconds
    scale_out_cooldown = var.scale_out_cooldown_seconds
  }
}
