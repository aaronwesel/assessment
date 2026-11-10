# =============================================================================
# ECS Fargate — API (ALB-backed) + worker (async) services
# =============================================================================
#
# desired_count on each service is the *initial* capacity. Application Auto
# Scaling (see autoscaling.tf) owns runtime DesiredCount; lifecycle below
# ignores drift so terraform plan does not fight the scaler.
#
# Network: private_subnet_ids across ≥2 AZs. API tasks register their ENI IPs
# into the ALB target group (stickiness OFF → even fan-out).
#
# DO NOT terraform apply until assessment day.

resource "aws_ecs_cluster" "this" {
  name = "${var.name_prefix}-cluster"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-cluster"
  })
}

resource "aws_ecs_cluster_capacity_providers" "this" {
  cluster_name = aws_ecs_cluster.this.name

  capacity_providers = ["FARGATE", "FARGATE_SPOT"]

  default_capacity_provider_strategy {
    capacity_provider = "FARGATE"
    weight            = 1
    base              = 1
  }
}

resource "aws_ecs_task_definition" "api" {
  family                   = "${var.name_prefix}-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name      = "api"
      image     = var.api_image
      essential = true
      portMappings = [{
        containerPort = 8000
        hostPort      = 8000
        protocol      = "tcp"
      }]
      environment = [
        { name = "APP_ENV", value = "aws" },
        { name = "NO_PHI_IN_LOGS", value = "true" }
      ]
      secrets = [
        { name = "APP_SECRET_JSON", valueFrom = var.app_secret_arn },
        { name = "DB_SECRET_JSON", valueFrom = var.db_secret_arn },
        { name = "REDIS_SECRET_JSON", valueFrom = var.redis_secret_arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.api_log_group_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "api"
        }
      }
    }
  ])

  tags = var.tags
}

resource "aws_ecs_task_definition" "worker" {
  family                   = "${var.name_prefix}-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.worker_cpu
  memory                   = var.worker_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name      = "worker"
      image     = var.worker_image
      essential = true
      environment = [
        { name = "APP_ENV", value = "aws" },
        { name = "WORKER_ROLE", value = "queue-consumer" },
        { name = "NO_PHI_IN_LOGS", value = "true" }
      ]
      secrets = [
        { name = "APP_SECRET_JSON", valueFrom = var.app_secret_arn },
        { name = "DB_SECRET_JSON", valueFrom = var.db_secret_arn },
        { name = "REDIS_SECRET_JSON", valueFrom = var.redis_secret_arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.worker_log_group_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "worker"
        }
      }
    }
  ])

  tags = var.tags
}

# API service — registered with ALB TG; health via container /health.
# Bootstrap desired_count should be ≥2 in prod for multi-AZ redundancy.
resource "aws_ecs_service" "api" {
  name            = "${var.name_prefix}-api"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.api.arn
  desired_count   = var.api_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    # Spread across private subnets in multiple AZs (from VPC module).
    subnets          = var.private_subnet_ids
    security_groups  = [var.ecs_security_group_id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = var.api_target_group_arn
    container_name   = "api"
    container_port   = 8000
  }

  # Rolling deploy: keep ≥50% healthy while doubling capacity during rollout.
  deployment_minimum_healthy_percent = 50
  deployment_maximum_percent         = 200
  health_check_grace_period_seconds  = var.api_health_check_grace_period_seconds

  # Autoscaling owns DesiredCount after bootstrap — do not thrash on plan.
  lifecycle {
    ignore_changes = [desired_count]
  }

  tags = var.tags
}

# Worker service — NOT load-balanced. Scale via autoscaling.tf (CPU/memory).
# Raise desired_count / max_capacity for backlog; API scaling is independent.
resource "aws_ecs_service" "worker" {
  name            = "${var.name_prefix}-worker"
  cluster         = aws_ecs_cluster.this.id
  task_definition = aws_ecs_task_definition.worker.arn
  desired_count   = var.worker_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.ecs_security_group_id]
    assign_public_ip = false
  }

  deployment_minimum_healthy_percent = 0
  deployment_maximum_percent         = 200

  lifecycle {
    ignore_changes = [desired_count]
  }

  tags = var.tags
}
