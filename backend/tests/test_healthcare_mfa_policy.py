"""Healthcare signup sets mfa_policy_required; standard signup does not."""

from __future__ import annotations

from fastapi.testclient import TestClient

STRONG_PASSWORD = "SecurePass1!x"


def test_register_healthcare_sets_policy_flag(client: TestClient) -> None:
    response = client.post(
        "/api/v1/auth/register",
        json={
            "email": "clinic@example.com",
            "password": STRONG_PASSWORD,
            "healthcare_account": True,
        },
    )
    assert response.status_code == 201, response.text
    tokens = response.json()
    me = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert me.status_code == 200
    body = me.json()
    assert body["mfa_policy_required"] is True
    assert body["mfa_enabled"] is False

    status = client.get(
        "/api/v1/auth/mfa/status",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert status.json()["mfa_policy_required"] is True


def test_register_standard_user_no_policy_flag(client: TestClient) -> None:
    response = client.post(
        "/api/v1/auth/register",
        json={"email": "standard-policy@example.com", "password": STRONG_PASSWORD},
    )
    assert response.status_code == 201
    tokens = response.json()
    me = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert me.json()["mfa_policy_required"] is False
