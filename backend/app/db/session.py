"""SQLAlchemy / SQLModel engine and session helpers."""

from collections.abc import Generator

from sqlmodel import Session, create_engine

from app.core.config import get_settings

_settings = get_settings()

# SQLite fallback for unit tests without Postgres.
_connect_args = {"check_same_thread": False} if _settings.database_url.startswith("sqlite") else {}

engine = create_engine(
    _settings.database_url,
    echo=False,
    connect_args=_connect_args,
    pool_pre_ping=True,
)


def get_session() -> Generator[Session, None, None]:
    with Session(engine) as session:
        yield session
