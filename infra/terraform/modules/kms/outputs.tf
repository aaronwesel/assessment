output "key_arn" {
  value = aws_kms_key.primary.arn
}

output "key_id" {
  value = aws_kms_key.primary.key_id
}

output "alias_name" {
  value = aws_kms_alias.primary.name
}
