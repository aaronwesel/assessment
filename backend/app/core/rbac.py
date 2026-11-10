"""RBAC role enum and FastAPI dependency stubs."""

from __future__ import annotations

from enum import StrEnum
from typing import Annotated

from fastapi import Depends, HTTPException, status
from fastapi.security import HTTPAuthorizationCredentials, HTTPBearer

from app.core.security import decode_token

bearer_scheme = HTTPBearer(auto_error=False)


class Role(StrEnum):
    USER = "user"
    ADMIN = "admin"
    MODERATOR = "moderator"


class AuthContext:
    """Authenticated principal extracted from JWT (no PHI)."""

    def __init__(self, subject: str, roles: list[str]) -> None:
        self.subject = subject
        self.roles = frozenset(roles)

    def has_role(self, role: Role | str) -> bool:
        return str(role) in self.roles


async def get_current_auth(
    credentials: Annotated[
        HTTPAuthorizationCredentials | None,
        Depends(bearer_scheme),
    ],
) -> AuthContext:
    if credentials is None or credentials.scheme.lower() != "bearer":
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Not authenticated",
            headers={"WWW-Authenticate": "Bearer"},
        )
    try:
        payload = decode_token(credentials.credentials, expected_type="access")
    except ValueError as exc:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid or expired token",
            headers={"WWW-Authenticate": "Bearer"},
        ) from exc
    subject = payload.get("sub")
    if not subject:
        raise HTTPException(
            status_code=status.HTTP_401_UNAUTHORIZED,
            detail="Invalid token subject",
        )
    roles = payload.get("roles") or []
    if not isinstance(roles, list):
        roles = []
    return AuthContext(subject=str(subject), roles=[str(r) for r in roles])


def require_role(*allowed: Role):
    """Dependency factory: require at least one of the given roles."""

    allowed_set = {str(role) for role in allowed}

    async def _dependency(
        auth: Annotated[AuthContext, Depends(get_current_auth)],
    ) -> AuthContext:
        if not auth.roles.intersection(allowed_set):
            raise HTTPException(
                status_code=status.HTTP_403_FORBIDDEN,
                detail="Insufficient permissions",
            )
        return auth

    return _dependency
