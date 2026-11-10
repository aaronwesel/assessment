# =============================================================================
# Application Load Balancer — public edge for the API tier
# =============================================================================
#
# Traffic fan-out (production SaaS sketch):
#   Internet → ALB (public subnets, ≥2 AZs) → target group (ip mode)
#            → ECS Fargate API tasks (private subnets, multi-AZ)
#
# Workers are NOT behind this ALB. They scale independently via the ECS worker
# service autoscaling policies (queue/CPU/memory). See docs/load-balancing.md.
#
# Multi-AZ notes:
#   - aws_lb requires at least two subnets in different AZs (enforced by AWS).
#   - Pass module.vpc.public_subnet_ids (defaults: eu-west-1a + eu-west-1b).
#   - ECS tasks register with target_type=ip across private subnets in those AZs
#     so AZ failure only drops the unhealthy fraction of tasks.
#   - Sticky sessions are OFF for the API TG: JWT auth is stateless; stickiness
#     would pin clients to a single task and defeat horizontal scale-out.
#
# DO NOT terraform apply until assessment day.

resource "aws_lb" "this" {
  name               = substr("${var.name_prefix}-alb", 0, 32)
  internal           = false
  load_balancer_type = "application"
  security_groups    = [var.security_group_id]
  # Public subnets across ≥2 AZs — required for ALB high availability.
  subnets = var.public_subnet_ids

  drop_invalid_header_fields = true
  enable_deletion_protection = false # enable on assessment-day prod
  # Cross-zone load balancing is on by default for ALBs (even request spread).
  enable_cross_zone_load_balancing = true

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-alb"
  })
}

# -----------------------------------------------------------------------------
# API target group
# -----------------------------------------------------------------------------
# target_type = "ip" matches awsvpc networking on Fargate (each task ENI IP).
# Health checks hit the process-local /health liveness probe (no Redis/DB).
# Keep this path cheap: unhealthy tasks are drained from the LB automatically.
resource "aws_lb_target_group" "api" {
  name        = substr("${var.name_prefix}-api", 0, 32)
  port        = 8000
  protocol    = "HTTP"
  vpc_id      = var.vpc_id
  target_type = "ip"

  # Graceful drain window when a task is deregistered (deploy / scale-in).
  # Long enough for in-flight requests; short enough for fast rollouts.
  deregistration_delay = var.deregistration_delay_seconds

  # API is JWT-stateless — stickiness MUST stay disabled so requests fan out
  # evenly and scale-out capacity is used immediately.
  stickiness {
    enabled         = false
    type            = "lb_cookie"
    cookie_duration = 86400
  }

  health_check {
    enabled = true
    # Liveness only — see backend /health vs /health/ready.
    path                = var.health_check_path
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 30
    matcher             = "200"
    protocol            = "HTTP"
    port                = "traffic-port"
  }

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-api-tg"
  })
}

resource "aws_lb_listener" "http" {
  load_balancer_arn = aws_lb.this.arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = "redirect"

    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

# HTTPS listener requires ACM ARN. When empty, listener is omitted so validate/plan
# can still work for skeleton review; set acm_certificate_arn before apply.
resource "aws_lb_listener" "https" {
  count = var.acm_certificate_arn != "" ? 1 : 0

  load_balancer_arn = aws_lb.this.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.acm_certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.api.arn
  }
}
