"""Shared auth helpers for platform-ops admin tests."""

from __future__ import annotations

from fastapi.testclient import TestClient
from sqlmodel import Session

from app.core.security import create_access_token
from app.db.session import engine
from app.models.user import User, UserRole


def register_user(
    client: TestClient,
    *,
    email: str,
    password: str = "SecurePass1!x",
) -> dict[str, str]:
    response = client.post(
        "/api/v1/auth/register",
        json={"email": email, "password": password},
    )
    assert response.status_code == 201, response.text
    tokens = response.json()
    me = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert me.status_code == 200
    return {
        "access_token": tokens["access_token"],
        "refresh_token": tokens["refresh_token"],
        "user_id": me.json()["subject"],
        "email": email,
    }


def promote_admin(user_id: str) -> None:
    with Session(engine) as session:
        user = session.get(User, user_id)
        assert user is not None
        user.role = UserRole.ADMIN
        session.add(user)
        session.commit()


def admin_auth_header(client: TestClient, *, email: str = "admin@example.com") -> dict[str, str]:
    """Register + promote + mint an admin access token."""
    info = register_user(client, email=email)
    promote_admin(info["user_id"])
    token = create_access_token(info["user_id"], roles=["admin"])
    return {"Authorization": f"Bearer {token}", "X-User-Id": info["user_id"]}
