# =============================================================================
# Crawl ECS Fargate (API + Worker) — SKETCH ONLY. DO NOT apply.
# =============================================================================
# Mirrors compose services crawl-api / crawl-worker. Reuses cluster or dedicated.
# Prefer extending prep modules/ecs on apply day; this module documents the delta.

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = ">= 5.0"
    }
  }
}

resource "aws_ecs_task_definition" "crawl_api" {
  family                   = "${var.name_prefix}-crawl-api"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.api_cpu
  memory                   = var.api_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name      = "crawl-api"
      image     = var.api_image
      essential = true
      portMappings = [{
        containerPort = 8080
        protocol      = "tcp"
      }]
      environment = [
        { name = "ASPNETCORE_URLS", value = "http://+:8080" },
        { name = "OTEL_SERVICE_NAME", value = "crawl-api" },
        { name = "RabbitMq__PageQueue", value = var.queue_name },
        { name = "RabbitMq__PageDlq", value = var.dlq_name }
      ]
      secrets = [
        { name = "ConnectionStrings__Crawl", valueFrom = var.db_secret_arn },
        { name = "RabbitMq__HostName", valueFrom = var.mq_host_secret_arn },
        { name = "RabbitMq__UserName", valueFrom = var.mq_user_secret_arn },
        { name = "RabbitMq__Password", valueFrom = var.mq_password_secret_arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.api_log_group_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "crawl-api"
        }
      }
      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://127.0.0.1:8080/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
      # SignalR hub: /hubs/crawl-jobs (ALB sticky sessions recommended for multi-API).
    }
  ])

  tags = merge(var.tags, { Service = "crawl-api" })
}

resource "aws_ecs_task_definition" "crawl_worker" {
  family                   = "${var.name_prefix}-crawl-worker"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = var.worker_cpu
  memory                   = var.worker_memory
  execution_role_arn       = var.execution_role_arn
  task_role_arn            = var.task_role_arn

  container_definitions = jsonencode([
    {
      name      = "crawl-worker"
      image     = var.worker_image
      essential = true
      environment = [
        { name = "OTEL_SERVICE_NAME", value = "crawl-worker" },
        { name = "RabbitMq__PageQueue", value = var.queue_name },
        { name = "RabbitMq__PageDlq", value = var.dlq_name },
        { name = "ASPNETCORE_URLS", value = "http://+:8081" }
      ]
      secrets = [
        { name = "ConnectionStrings__Crawl", valueFrom = var.db_secret_arn },
        { name = "RabbitMq__HostName", valueFrom = var.mq_host_secret_arn },
        { name = "RabbitMq__UserName", valueFrom = var.mq_user_secret_arn },
        { name = "RabbitMq__Password", valueFrom = var.mq_password_secret_arn }
      ]
      logConfiguration = {
        logDriver = "awslogs"
        options = {
          awslogs-group         = var.worker_log_group_name
          awslogs-region        = var.aws_region
          awslogs-stream-prefix = "crawl-worker"
        }
      }
      # Competing consumers on crawl.page; health + /ops/queue for scale signals.
      healthCheck = {
        command     = ["CMD-SHELL", "curl -fsS http://127.0.0.1:8081/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
      portMappings = [{
        containerPort = 8081
        protocol      = "tcp"
      }]
    }
  ])

  tags = merge(var.tags, { Service = "crawl-worker" })
}

resource "aws_ecs_service" "crawl_api" {
  name            = "${var.name_prefix}-crawl-api"
  cluster         = var.cluster_arn
  task_definition = aws_ecs_task_definition.crawl_api.arn
  desired_count   = var.api_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.ecs_security_group_id]
    assign_public_ip = false
  }

  dynamic "load_balancer" {
    for_each = var.api_target_group_arn == null ? [] : [1]
    content {
      target_group_arn = var.api_target_group_arn
      container_name   = "crawl-api"
      container_port   = 8080
    }
  }

  lifecycle {
    ignore_changes = [desired_count]
  }

  tags = merge(var.tags, { Service = "crawl-api" })
}

resource "aws_ecs_service" "crawl_worker" {
  name            = "${var.name_prefix}-crawl-worker"
  cluster         = var.cluster_arn
  task_definition = aws_ecs_task_definition.crawl_worker.arn
  desired_count   = var.worker_desired_count
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = var.private_subnet_ids
    security_groups  = [var.ecs_security_group_id]
    assign_public_ip = false
  }

  lifecycle {
    ignore_changes = [desired_count]
  }

  tags = merge(var.tags, { Service = "crawl-worker" })
}
