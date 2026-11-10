"""Add mfa_policy_required for healthcare account signup.

Revision ID: 0006_healthcare_mfa_policy
Revises: 0005_platform_ops
Create Date: 2026-11-10
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0006_healthcare_mfa_policy"
down_revision: Union[str, None] = "0005_platform_ops"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column(
        "users",
        sa.Column(
            "mfa_policy_required",
            sa.Boolean(),
            nullable=False,
            server_default=sa.false(),
        ),
    )


def downgrade() -> None:
    op.drop_column("users", "mfa_policy_required")
