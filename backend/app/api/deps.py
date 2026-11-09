"""
Shared FastAPI dependency aliases used by route handlers.

Keeps `Annotated[..., Depends(...)]` types in one place so handlers stay thin
and RBAC/session/Redis wiring is consistent across the API surface.
"""

from typing import Annotated

from fastapi import Depends
from sqlmodel import Session

from app.core.rbac import AuthContext, Role, get_current_auth, require_role
from app.core.redis import RedisClientStub, get_redis
from app.db.session import get_session

SessionDep = Annotated[Session, Depends(get_session)]
RedisDep = Annotated[RedisClientStub, Depends(get_redis)]
CurrentAuth = Annotated[AuthContext, Depends(get_current_auth)]
RequireAdmin = Annotated[AuthContext, Depends(require_role(Role.ADMIN))]
RequireUser = Annotated[AuthContext, Depends(require_role(Role.USER, Role.ADMIN, Role.MODERATOR))]

__all__ = [
    "SessionDep",
    "RedisDep",
    "CurrentAuth",
    "RequireAdmin",
    "RequireUser",
    "Role",
    "require_role",
]
