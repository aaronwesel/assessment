"""Add MFA columns to users.

Revision ID: 0003_mfa_fields
Revises: 0002_teams_stub
Create Date: 2026-11-09

Chains after team stubs so alembic has a single head. TOTP secrets are stored
Fernet-encrypted; backup codes are bcrypt hashes in a JSON text column.
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0003_mfa_fields"
down_revision: Union[str, None] = "0002_teams_stub"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column(
        "users",
        sa.Column("mfa_enabled", sa.Boolean(), nullable=False, server_default=sa.false()),
    )
    op.add_column(
        "users",
        sa.Column("mfa_secret_encrypted", sa.String(length=512), nullable=True),
    )
    op.add_column(
        "users",
        sa.Column("mfa_backup_codes_hashed", sa.Text(), nullable=True),
    )


def downgrade() -> None:
    op.drop_column("users", "mfa_backup_codes_hashed")
    op.drop_column("users", "mfa_secret_encrypted")
    op.drop_column("users", "mfa_enabled")
