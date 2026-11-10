# =============================================================================
# Worker elastic scale (sketch) — queue-backed Crawl.Worker consumers.
# DO NOT terraform apply until authorized. Operator-runnable shape for "next".
# =============================================================================
# Primary path: ECS Service desired_count for crawl-worker tracks Amazon MQ /
# RabbitMQ depth (or custom metric from /api/ops/queue). Scale-in after drain.
# Per-job ephemeral EC2 provision/handoff/delete remains stretch-next, not core.

resource "aws_appautoscaling_target" "crawl_worker" {
  count              = var.enable_worker_autoscaling ? 1 : 0
  max_capacity       = var.worker_max_count
  min_capacity       = var.worker_min_count
  resource_id        = "service/${split("/", var.cluster_arn)[length(split("/", var.cluster_arn)) - 1]}/${aws_ecs_service.crawl_worker.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

# Scale OUT when custom queue-depth metric is high (wire CW metric from MQ or sidecar).
resource "aws_appautoscaling_policy" "crawl_worker_scale_out" {
  count              = var.enable_worker_autoscaling ? 1 : 0
  name               = "${var.name_prefix}-crawl-worker-scale-out"
  policy_type        = "StepScaling"
  resource_id        = aws_appautoscaling_target.crawl_worker[0].resource_id
  scalable_dimension = aws_appautoscaling_target.crawl_worker[0].scalable_dimension
  service_namespace  = aws_appautoscaling_target.crawl_worker[0].service_namespace

  step_scaling_policy_configuration {
    adjustment_type         = "ChangeInCapacity"
    cooldown                = var.worker_scale_out_cooldown_seconds
    metric_aggregation_type = "Average"

    step_adjustment {
      metric_interval_lower_bound = 0
      scaling_adjustment          = var.worker_scale_out_adjustment
    }
  }
}

# Scale IN when queue drained (ephemeral capacity returns after drain).
resource "aws_appautoscaling_policy" "crawl_worker_scale_in" {
  count              = var.enable_worker_autoscaling ? 1 : 0
  name               = "${var.name_prefix}-crawl-worker-scale-in"
  policy_type        = "StepScaling"
  resource_id        = aws_appautoscaling_target.crawl_worker[0].resource_id
  scalable_dimension = aws_appautoscaling_target.crawl_worker[0].scalable_dimension
  service_namespace  = aws_appautoscaling_target.crawl_worker[0].service_namespace

  step_scaling_policy_configuration {
    adjustment_type         = "ChangeInCapacity"
    cooldown                = var.worker_scale_in_cooldown_seconds
    metric_aggregation_type = "Average"

    step_adjustment {
      metric_interval_upper_bound = 0
      scaling_adjustment          = var.worker_scale_in_adjustment
    }
  }
}

# Optional CloudWatch alarm placeholders — replace MetricName with Amazon MQ
# MessageReadyCount or a custom metric published from Crawl.Api /api/ops/queue.
resource "aws_cloudwatch_metric_alarm" "crawl_queue_depth_high" {
  count               = var.enable_worker_autoscaling ? 1 : 0
  alarm_name          = "${var.name_prefix}-crawl-queue-depth-high"
  comparison_operator = "GreaterThanOrEqualToThreshold"
  evaluation_periods  = 2
  metric_name         = var.queue_depth_metric_name
  namespace           = var.queue_depth_metric_namespace
  period              = 60
  statistic           = "Average"
  threshold           = var.queue_depth_scale_out_threshold
  alarm_description   = "SKETCH: crawl.page depth high → scale out Crawl.Worker"
  alarm_actions       = [aws_appautoscaling_policy.crawl_worker_scale_out[0].arn]

  dimensions = var.queue_depth_metric_dimensions
}

resource "aws_cloudwatch_metric_alarm" "crawl_queue_depth_low" {
  count               = var.enable_worker_autoscaling ? 1 : 0
  alarm_name          = "${var.name_prefix}-crawl-queue-depth-low"
  comparison_operator = "LessThanOrEqualToThreshold"
  evaluation_periods  = 5
  metric_name         = var.queue_depth_metric_name
  namespace           = var.queue_depth_metric_namespace
  period              = 60
  statistic           = "Average"
  threshold           = var.queue_depth_scale_in_threshold
  alarm_description   = "SKETCH: crawl.page drained → scale in Crawl.Worker"
  alarm_actions       = [aws_appautoscaling_policy.crawl_worker_scale_in[0].arn]

  dimensions = var.queue_depth_metric_dimensions
}
