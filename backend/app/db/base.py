"""Import models for Alembic metadata discovery."""

from sqlmodel import SQLModel

# Import model modules so metadata is populated.
from app.models import account_link as account_link  # noqa: F401
from app.models import user as user  # noqa: F401

__all__ = ["SQLModel"]
