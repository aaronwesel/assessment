# --- Execution role (pull images, write logs, read secrets for injection) ---

resource "aws_iam_role" "ecs_execution" {
  name = "${var.name_prefix}-ecs-execution"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy" "ecs_execution" {
  name = "${var.name_prefix}-ecs-execution"
  role = aws_iam_role.ecs_execution.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "EcrAuth"
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken"
        ]
        Resource = "*"
      },
      {
        Sid    = "EcrPull"
        Effect = "Allow"
        Action = [
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage"
        ]
        Resource = [
          var.ecr_api_repository_arn,
          var.ecr_worker_repository_arn,
        ]
      },
      {
        Sid    = "Logs"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = [for arn in var.cloudwatch_log_group_arns : "${arn}:*"]
      },
      {
        Sid    = "SecretsRead"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = var.secrets_arns
      },
      {
        Sid    = "KmsDecryptSecrets"
        Effect = "Allow"
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey"
        ]
        Resource = [var.kms_key_arn]
      }
    ]
  })
}

# --- Task role (runtime app permissions; includes Rekognition) ---

resource "aws_iam_role" "ecs_task" {
  name = "${var.name_prefix}-ecs-task"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action    = "sts:AssumeRole"
      Effect    = "Allow"
      Principal = { Service = "ecs-tasks.amazonaws.com" }
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy" "ecs_task" {
  name = "${var.name_prefix}-ecs-task"
  role = aws_iam_role.ecs_task.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "SecretsRead"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = var.secrets_arns
      },
      {
        Sid    = "KmsDecrypt"
        Effect = "Allow"
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey"
        ]
        Resource = [var.kms_key_arn]
      },
      {
        Sid    = "CloudWatchPut"
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = [for arn in var.cloudwatch_log_group_arns : "${arn}:*"]
      },
      {
        # Amazon Rekognition via task role — no long-lived keys in the app.
        Sid    = "Rekognition"
        Effect = "Allow"
        Action = [
          "rekognition:DetectLabels",
          "rekognition:DetectModerationLabels",
          "rekognition:DetectText",
          "rekognition:DetectFaces"
        ]
        Resource = ["*"] # Rekognition Detect* APIs do not support resource-level ARNs
      }
    ]
  })
}
