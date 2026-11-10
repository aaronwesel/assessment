"""
Import models for Alembic metadata discovery.

`alembic/env.py` imports this module so `SQLModel.metadata` includes all tables
before generating or applying migrations.
"""

from sqlmodel import SQLModel

# Import model modules so metadata is populated.
from app.models import account_link as account_link  # noqa: F401
from app.models import organization as organization  # noqa: F401
from app.models import team as team  # noqa: F401
from app.models import user as user  # noqa: F401

__all__ = ["SQLModel"]
