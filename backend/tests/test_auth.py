"""Smoke tests: auth deny without token; allow with issued JWT."""

from fastapi.testclient import TestClient


def test_protected_me_denies_without_token(client: TestClient) -> None:
    response = client.get("/api/v1/auth/me")
    assert response.status_code == 401


def test_protected_me_denies_invalid_token(client: TestClient) -> None:
    response = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": "Bearer not-a-real-token"},
    )
    assert response.status_code == 401


def test_admin_ping_denies_without_token(client: TestClient) -> None:
    response = client.get("/api/v1/auth/admin/ping")
    assert response.status_code == 401


def test_register_and_access_me(client: TestClient) -> None:
    register = client.post(
        "/api/v1/auth/register",
        json={
            "email": "user@example.com",
            "password": "securepass1",
            "full_name": "Test User",
        },
    )
    assert register.status_code == 201
    tokens = register.json()
    assert "access_token" in tokens
    assert "refresh_token" in tokens

    me = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert me.status_code == 200
    body = me.json()
    assert body["subject"] == "user@example.com"
    assert "user" in body["roles"]


def test_user_denied_admin_route(client: TestClient) -> None:
    register = client.post(
        "/api/v1/auth/register",
        json={"email": "plain@example.com", "password": "securepass1"},
    )
    assert register.status_code == 201
    token = register.json()["access_token"]

    response = client.get(
        "/api/v1/auth/admin/ping",
        headers={"Authorization": f"Bearer {token}"},
    )
    assert response.status_code == 403


def test_google_onetap_stub_issues_tokens(client: TestClient) -> None:
    response = client.post(
        "/api/v1/auth/google/onetap",
        json={"id_token": "stub-google-id-token-value", "provider": "google"},
    )
    assert response.status_code == 200
    assert "access_token" in response.json()


def test_microsoft_onetap_stub_issues_tokens(client: TestClient) -> None:
    response = client.post(
        "/api/v1/auth/microsoft/onetap",
        json={"id_token": "stub-ms-id-token-value", "provider": "microsoft"},
    )
    assert response.status_code == 200
    assert "access_token" in response.json()
