"""Team management stubs: organizations, teams, team_memberships.

Revision ID: 0002_teams_stub
Revises: 0001_initial_stub
Create Date: 2026-11-09

Detailed notes:
- Invite emails live on team_memberships for routing only; treat as PII,
  never as clinical identifiers, and never emit them to Slack/logs.
- Roles are stored as strings matching TeamRole enum values.
"""

from typing import Sequence, Union

import sqlalchemy as sa
from alembic import op

revision: str = "0002_teams_stub"
down_revision: Union[str, None] = "0001_initial_stub"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "organizations",
        sa.Column("id", sa.String(), nullable=False),
        sa.Column("name", sa.String(length=255), nullable=False),
        sa.Column("slug", sa.String(length=64), nullable=False),
        sa.Column("created_by_user_id", sa.String(length=36), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("slug"),
    )
    op.create_index("ix_organizations_name", "organizations", ["name"])
    op.create_index("ix_organizations_slug", "organizations", ["slug"])
    op.create_index("ix_organizations_created_by_user_id", "organizations", ["created_by_user_id"])

    op.create_table(
        "teams",
        sa.Column("id", sa.String(), nullable=False),
        sa.Column("organization_id", sa.String(length=36), nullable=False),
        sa.Column("name", sa.String(length=255), nullable=False),
        sa.Column("workstream", sa.String(length=64), nullable=False),
        sa.Column("description", sa.String(length=512), nullable=True),
        sa.Column("created_by_user_id", sa.String(length=36), nullable=False),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.Column("updated_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["organization_id"], ["organizations.id"]),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_teams_organization_id", "teams", ["organization_id"])
    op.create_index("ix_teams_name", "teams", ["name"])
    op.create_index("ix_teams_workstream", "teams", ["workstream"])
    op.create_index("ix_teams_created_by_user_id", "teams", ["created_by_user_id"])

    op.create_table(
        "team_memberships",
        sa.Column("id", sa.String(), nullable=False),
        sa.Column("team_id", sa.String(length=36), nullable=False),
        sa.Column("user_id", sa.String(length=36), nullable=True),
        sa.Column("invite_email", sa.String(length=320), nullable=True),
        sa.Column("role", sa.String(), nullable=False),
        sa.Column("invited_by_user_id", sa.String(length=36), nullable=True),
        sa.Column("accepted_at", sa.DateTime(timezone=True), nullable=True),
        sa.Column("created_at", sa.DateTime(timezone=True), server_default=sa.func.now(), nullable=False),
        sa.ForeignKeyConstraint(["team_id"], ["teams.id"]),
        sa.ForeignKeyConstraint(["user_id"], ["users.id"]),
        sa.PrimaryKeyConstraint("id"),
        sa.UniqueConstraint("team_id", "user_id", name="uq_team_user"),
        sa.UniqueConstraint("team_id", "invite_email", name="uq_team_invite_email"),
    )
    op.create_index("ix_team_memberships_team_id", "team_memberships", ["team_id"])
    op.create_index("ix_team_memberships_user_id", "team_memberships", ["user_id"])
    op.create_index("ix_team_memberships_invite_email", "team_memberships", ["invite_email"])


def downgrade() -> None:
    op.drop_index("ix_team_memberships_invite_email", table_name="team_memberships")
    op.drop_index("ix_team_memberships_user_id", table_name="team_memberships")
    op.drop_index("ix_team_memberships_team_id", table_name="team_memberships")
    op.drop_table("team_memberships")
    op.drop_index("ix_teams_created_by_user_id", table_name="teams")
    op.drop_index("ix_teams_workstream", table_name="teams")
    op.drop_index("ix_teams_name", table_name="teams")
    op.drop_index("ix_teams_organization_id", table_name="teams")
    op.drop_table("teams")
    op.drop_index("ix_organizations_created_by_user_id", table_name="organizations")
    op.drop_index("ix_organizations_slug", table_name="organizations")
    op.drop_index("ix_organizations_name", table_name="organizations")
    op.drop_table("organizations")
