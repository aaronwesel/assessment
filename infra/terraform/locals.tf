locals {
  name_prefix = "${var.project_name}-${var.environment}"

  common_tags = merge(var.tags, {
    Name = local.name_prefix
  })

  # Encryption defaults: always on for this HIPAA-ready scaffold
  encryption_at_rest_enabled    = true
  encryption_in_transit_enabled = true
}
