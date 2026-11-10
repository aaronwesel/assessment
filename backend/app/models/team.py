"""Team + membership domain stubs with parceling roles.

Roles (owner / admin / developer / viewer) describe *who can steer a work
stream*, not application-wide RBAC (see `app.core.rbac.Role`). Keep them
orthogonal so a platform admin can still be a viewer on a feature team.
"""

from datetime import datetime
from enum import Enum
from typing import Optional
from uuid import uuid4

from sqlalchemy import Column, DateTime, UniqueConstraint, func
from sqlmodel import Field, SQLModel


class TeamRole(str, Enum):
    """Membership roles for parceling assessment work to developers.

    - owner: full team lifecycle (create/archive, invite, role changes)
    - admin: invite + assign streams; cannot delete the team
    - developer: contribute to assigned streams; open/own PRs
    - viewer: read-only (docs, dashboards, PR review without merge)
    """

    OWNER = "owner"
    ADMIN = "admin"
    DEVELOPER = "developer"
    VIEWER = "viewer"


class Team(SQLModel, table=True):
    """Named developer group under an Organization (e.g. API, UI, infra)."""

    __tablename__ = "teams"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    organization_id: str = Field(foreign_key="organizations.id", index=True, max_length=36)
    name: str = Field(max_length=255, index=True)
    # Workstream label: api | ui | infra | mobile | ai — free string in stub
    # so assessment-day prompts can invent streams without a migration.
    workstream: str = Field(default="general", max_length=64, index=True)
    description: Optional[str] = Field(default=None, max_length=512)
    created_by_user_id: str = Field(max_length=36, index=True)
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
    updated_at: datetime | None = Field(
        default=None,
        sa_column=Column(
            DateTime(timezone=True),
            server_default=func.now(),
            onupdate=func.now(),
            nullable=False,
        ),
    )


class TeamMembership(SQLModel, table=True):
    """Links a user (or invite email) to a Team with a TeamRole.

    Invite stub: when `user_id` is None and `invite_email` is set, the row is
    a pending invite. Email is invite routing only — never log it as PHI, and
    never forward it into Slack message bodies (see Slack client guards).
    """

    __tablename__ = "team_memberships"
    __table_args__ = (
        UniqueConstraint("team_id", "user_id", name="uq_team_user"),
        UniqueConstraint("team_id", "invite_email", name="uq_team_invite_email"),
    )

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    team_id: str = Field(foreign_key="teams.id", index=True, max_length=36)
    # Nullable until the invitee accepts / registers.
    user_id: Optional[str] = Field(default=None, foreign_key="users.id", index=True, max_length=36)
    invite_email: Optional[str] = Field(default=None, max_length=320, index=True)
    role: TeamRole = Field(default=TeamRole.DEVELOPER)
    invited_by_user_id: Optional[str] = Field(default=None, max_length=36)
    accepted_at: Optional[datetime] = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), nullable=True),
    )
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
