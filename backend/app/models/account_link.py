"""OAuth account-link table stub for Google / Microsoft One Tap identities."""

from datetime import datetime
from enum import Enum
from uuid import uuid4

from sqlalchemy import Column, DateTime, UniqueConstraint, func
from sqlmodel import Field, SQLModel


class IdentityProvider(str, Enum):
    GOOGLE = "google"
    MICROSOFT = "microsoft"
    PASSWORD = "password"


class AccountLink(SQLModel, table=True):
    __tablename__ = "account_links"
    __table_args__ = (
        UniqueConstraint("provider", "provider_subject", name="uq_provider_subject"),
    )

    id: str = Field(default_factory=lambda: str(uuid4()), primary_key=True)
    user_id: str = Field(foreign_key="users.id", index=True)
    provider: IdentityProvider = Field(index=True)
    provider_subject: str = Field(max_length=255, index=True)
    # Do not store email/PHI in this stub beyond what OAuth subject requires for linking.
    created_at: datetime | None = Field(
        default=None,
        sa_column=Column(DateTime(timezone=True), server_default=func.now(), nullable=False),
    )
