# Shared naming/tags and HIPAA encryption defaults for every module below.
locals {
  name_prefix = "${var.project_name}-${var.environment}"

  common_tags = merge(var.tags, {
    Name = local.name_prefix
  })

  # Encryption defaults: always on for this HIPAA-ready scaffold.
  # RDS/Redis/CloudWatch/ECR/Secrets use the primary CMK (modules/kms).
  # S3: enable SSE-KMS when buckets are added; tf remote state uses encrypt=true.
  # App-level Fernet (FIELD_ENCRYPTION_KEY) covers small secrets (MFA seeds) only —
  # bulk PHI stays in KMS-backed data stores, not ad-hoc app crypto.
  encryption_at_rest_enabled    = true
  encryption_in_transit_enabled = true
}
