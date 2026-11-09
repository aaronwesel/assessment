resource "aws_db_subnet_group" "this" {
  name       = "${var.name_prefix}-aurora"
  subnet_ids = var.subnet_ids

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-aurora-subnets"
  })
}

resource "random_password" "master" {
  length           = 32
  special          = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

# Skeleton uses managed master password generation locally; on assessment day
# prefer Secrets Manager integration / manage_master_user_password where available.
resource "aws_rds_cluster" "this" {
  cluster_identifier = "${var.name_prefix}-aurora"
  engine             = "aurora-postgresql"
  engine_version     = var.engine_version
  database_name      = var.database_name
  master_username    = "assessment_admin"
  master_password    = random_password.master.result

  db_subnet_group_name   = aws_db_subnet_group.this.name
  vpc_security_group_ids = [var.security_group_id]

  # Encryption at rest (HIPAA-ready): AES via customer CMK — do not disable.
  # In-transit: require SSL on assessment-day DATABASE_URL (sslmode=require).
  storage_encrypted = true
  kms_key_id        = var.kms_key_arn

  backup_retention_period      = 7
  preferred_backup_window      = "03:00-04:00"
  preferred_maintenance_window = "sun:04:00-sun:05:00"
  copy_tags_to_snapshot        = true
  deletion_protection          = false # enable for prod on assessment day
  skip_final_snapshot          = true  # flip false + final_snapshot_identifier for prod

  enabled_cloudwatch_logs_exports = ["postgresql"]

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-aurora"
  })
}

resource "aws_rds_cluster_instance" "this" {
  count = 1

  identifier         = "${var.name_prefix}-aurora-${count.index}"
  cluster_identifier = aws_rds_cluster.this.id
  instance_class     = var.instance_class
  engine             = aws_rds_cluster.this.engine
  engine_version     = aws_rds_cluster.this.engine_version

  publicly_accessible = false

  tags = merge(var.tags, {
    Name = "${var.name_prefix}-aurora-${count.index}"
  })
}
