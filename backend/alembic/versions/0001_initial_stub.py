"""
Initial stub tables: users + account_links.

Creates the auth schema only — no clinical/PHI tables. Password hashes live on
`users`; federated subjects live on `account_links` with a unique
(provider, provider_subject) constraint.

Revision ID: 0001_initial_stub
Revises:
Create Date: 2026-11-09
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0001_initial_stub"
down_revision: Union[str, None] = None
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "users",
        sa.Column("id", sa.String(), nullable=False),
        sa.Column("email", sa.String(length=320), nullable=False),
        sa.Column("hashed_password", sa.String(length=255), nullable=True),
        sa.Column("full_name", sa.String(length=255), nullable=True),
        sa.Column("role", sa.String(), nullable=False),
        sa.Column("is_active", sa.Boolean(), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("email"),
    )
    op.create_index("ix_users_email", "users", ["email"])

    op.create_table(
        "account_links",
        sa.Column("id", sa.String(), nullable=False),
        sa.Column("user_id", sa.String(), nullable=False),
        sa.Column("provider", sa.String(), nullable=False),
        sa.Column("provider_subject", sa.String(length=255), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["user_id"], ["users.id"]),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("provider", "provider_subject", name="uq_provider_subject"),
    )
    op.create_index("ix_account_links_user_id", "account_links", ["user_id"])
    op.create_index("ix_account_links_provider", "account_links", ["provider"])
    op.create_index("ix_account_links_provider_subject", "account_links", ["provider_subject"])


def downgrade() -> None:
    op.drop_index("ix_account_links_provider_subject", table_name="account_links")
    op.drop_index("ix_account_links_provider", table_name="account_links")
    op.drop_index("ix_account_links_user_id", table_name="account_links")
    op.drop_table("account_links")
    op.drop_index("ix_users_email", table_name="users")
    op.drop_table("users")
