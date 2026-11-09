"""Organization domain stub — top-level tenant for team parceling.

An Organization owns zero-or-more Teams. Prep scaffold only: no billing,
SSO federation, or PHI fields. Emails on related memberships are invite
routing metadata, never clinical identifiers.
"""

from datetime import datetime
from uuid import uuid4

from sqlalchemy import Column, DateTime, func
from sqlmodel import Field, SQLModel


class Organization(SQLModel, table=True):
    """Tenant container so workstreams can be assigned across developer teams.

    Detailed comments (assessment requirement):
    - `slug` is a stable URL/key-friendly label for ops tooling; keep it
      non-PHI (no patient/org clinical names).
    - `created_by_user_id` is the JWT subject that created the org; used for
      audit trails, not for soft authorization alone (membership roles win).
    """

    __tablename__ = "organizations"

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    name: str = Field(max_length=255, index=True)
    slug: str = Field(max_length=64, unique=True, index=True)
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
