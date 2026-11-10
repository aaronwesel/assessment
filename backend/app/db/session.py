"""
SQLAlchemy / SQLModel engine and session helpers.

`get_session` is the FastAPI dependency that yields a request-scoped Session.
SQLite StaticPool support exists for pytest in-memory DBs; production uses
Postgres (`postgresql+psycopg://...`) with `pool_pre_ping`.

`init_db` is for local/test convenience only — production schema changes go
through Alembic as a deploy step (see `alembic/env.py`).
"""

from collections.abc import Generator

from sqlalchemy.pool import StaticPool
from sqlmodel import Session, SQLModel, create_engine

from app.core.config import get_settings

_settings = get_settings()

_is_sqlite = _settings.database_url.startswith("sqlite")
_connect_args = {"check_same_thread": False} if _is_sqlite else {}
_engine_kwargs: dict = {
    "echo": False,
    "connect_args": _connect_args,
    "pool_pre_ping": not _is_sqlite,
}
# Shared in-memory DB across connections for pytest.
if _is_sqlite and ":memory:" in _settings.database_url:
    _engine_kwargs["poolclass"] = StaticPool

engine = create_engine(_settings.database_url, **_engine_kwargs)


def init_db() -> None:
    """Create tables for local/test when migrations have not run yet."""
    # Import models so metadata is populated.
    from app.billing import models as _billing_models  # noqa: F401
    from app.models import account_link as _account_link  # noqa: F401
    from app.models import organization as _organization  # noqa: F401
    from app.models import team as _team  # noqa: F401
    from app.models import user as _user  # noqa: F401

    SQLModel.metadata.create_all(engine)


def get_session() -> Generator[Session, None, None]:
    with Session(engine) as session:
        yield session
