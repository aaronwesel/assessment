provider "aws" {
  region = var.aws_region

  default_tags {
    tags = merge(var.tags, {
      Project            = var.project_name
      Environment        = var.environment
      ManagedBy          = "terraform"
      HipaaReadyScaffold = "true"
      TerraformApplyGate = "forbidden-until-assessment-day"
    })
  }
}
