terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # PREP: local state only. Remote backend (S3 + DynamoDB) is assessment-day only.
  # backend "s3" {
  #   bucket         = "CHANGE_ME-tf-state"
  #   key            = "assessment/terraform.tfstate"
  #   region         = "eu-west-1"
  #   dynamodb_table = "CHANGE_ME-tf-locks"
  #   encrypt        = true
  # }
}
