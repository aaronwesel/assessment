"""Crawl job usage ledger for free-tier + Stripe meter.

Revision ID: 0006_crawl_job_usage
Revises: 0005_platform_ops
Create Date: 2026-11-10
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0006_crawl_job_usage"
down_revision: Union[str, None] = "0005_platform_ops"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "crawl_job_usage",
        sa.Column("id", sa.String(length=36), primary_key=True),
        sa.Column("user_id", sa.String(length=36), nullable=False),
        sa.Column("crawl_job_id", sa.String(length=64), nullable=False),
        sa.Column("pages_with_results", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("billable_pages", sa.Integer(), nullable=False, server_default="0"),
        sa.Column("stripe_meter_reported", sa.Boolean(), nullable=False, server_default=sa.false()),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.text("now()"),
            nullable=False,
        ),
        sa.UniqueConstraint("user_id", "crawl_job_id", name="uq_crawl_job_usage_user_job"),
    )
    op.create_index("ix_crawl_job_usage_user_id", "crawl_job_usage", ["user_id"])
    op.create_index("ix_crawl_job_usage_crawl_job_id", "crawl_job_usage", ["crawl_job_id"])


def downgrade() -> None:
    op.drop_index("ix_crawl_job_usage_crawl_job_id", table_name="crawl_job_usage")
    op.drop_index("ix_crawl_job_usage_user_id", table_name="crawl_job_usage")
    op.drop_table("crawl_job_usage")
