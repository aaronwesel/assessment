"""Team management APIs: JWT gate, create, invite stub, list."""

from __future__ import annotations

from fastapi.testclient import TestClient


def _register_and_token(client: TestClient, email: str = "team-owner@example.com") -> str:
    response = client.post(
        "/api/v1/auth/register",
        json={"email": email, "password": "SecurePass1!x", "full_name": "Team Owner"},
    )
    assert response.status_code == 201
    return response.json()["access_token"]


def test_create_team_requires_jwt(client: TestClient) -> None:
    response = client.post(
        "/api/v1/teams",
        json={
            "organization": {"name": "Acme", "slug": "acme"},
            "name": "API stream",
            "workstream": "api",
        },
    )
    assert response.status_code == 401


def test_create_list_and_invite_team(client: TestClient) -> None:
    token = _register_and_token(client)
    headers = {"Authorization": f"Bearer {token}"}

    created = client.post(
        "/api/v1/teams",
        headers=headers,
        json={
            "organization": {"name": "Acme Prep", "slug": "acme-prep"},
            "name": "API stream",
            "workstream": "api",
            "description": "FastAPI ownership",
        },
    )
    assert created.status_code == 201, created.text
    team = created.json()
    assert team["workstream"] == "api"
    assert team["organization_id"]
    team_id = team["id"]

    listed = client.get("/api/v1/teams", headers=headers)
    assert listed.status_code == 200
    teams = listed.json()["teams"]
    assert any(t["id"] == team_id for t in teams)

    invite = client.post(
        f"/api/v1/teams/{team_id}/invites",
        headers=headers,
        json={"email": "dev@example.com", "role": "developer"},
    )
    assert invite.status_code == 201, invite.text
    body = invite.json()
    assert body["team_id"] == team_id
    assert body["role"] == "developer"
    assert body["status"] == "pending"
    # Prep: invite email intentionally omitted from response.
    assert "email" not in body


def test_invite_unknown_team_404(client: TestClient) -> None:
    token = _register_and_token(client, email="team-invite-404@example.com")
    response = client.post(
        "/api/v1/teams/00000000-0000-0000-0000-000000000099/invites",
        headers={"Authorization": f"Bearer {token}"},
        json={"email": "x@example.com", "role": "viewer"},
    )
    assert response.status_code == 404
