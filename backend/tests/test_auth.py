"""Auth deny/allow, password flows, federated exchange (mocked verifiers)."""

from __future__ import annotations

from unittest.mock import patch

import pytest
from fastapi.testclient import TestClient

from app.services.google_id_token import VerifiedIdentity as GoogleIdentity
from app.services.microsoft_id_token import VerifiedIdentity as MicrosoftIdentity


def test_providers_status(client: TestClient) -> None:
    response = client.get("/api/v1/auth/providers")
    assert response.status_code == 200
    body = response.json()
    assert body["password"] is True
    assert body["google"] is True
    assert body["microsoft"] is True


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


def test_register_login_refresh_and_me(client: TestClient) -> None:
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
    assert body["email"] == "user@example.com"
    assert "user" in body["roles"]
    user_id = body["subject"]

    login = client.post(
        "/api/v1/auth/login",
        json={"email": "user@example.com", "password": "securepass1"},
    )
    assert login.status_code == 200

    refresh = client.post(
        "/api/v1/auth/refresh",
        json={"refresh_token": tokens["refresh_token"]},
    )
    assert refresh.status_code == 200
    new_tokens = refresh.json()

    # Old refresh should be revoked after rotation.
    reused = client.post(
        "/api/v1/auth/refresh",
        json={"refresh_token": tokens["refresh_token"]},
    )
    assert reused.status_code == 401

    me2 = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {new_tokens['access_token']}"},
    )
    assert me2.status_code == 200
    assert me2.json()["subject"] == user_id


def test_login_rejects_bad_password(client: TestClient) -> None:
    client.post(
        "/api/v1/auth/register",
        json={"email": "other@example.com", "password": "securepass1"},
    )
    bad = client.post(
        "/api/v1/auth/login",
        json={"email": "other@example.com", "password": "wrong-password"},
    )
    assert bad.status_code == 401


def test_duplicate_register_conflicts(client: TestClient) -> None:
    payload = {"email": "dupe@example.com", "password": "securepass1"}
    assert client.post("/api/v1/auth/register", json=payload).status_code == 201
    assert client.post("/api/v1/auth/register", json=payload).status_code == 409


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


def test_logout_blacklists_access_token(client: TestClient) -> None:
    register = client.post(
        "/api/v1/auth/register",
        json={"email": "logout@example.com", "password": "securepass1"},
    )
    tokens = register.json()
    access = tokens["access_token"]
    assert (
        client.get("/api/v1/auth/me", headers={"Authorization": f"Bearer {access}"}).status_code
        == 200
    )

    logout = client.post(
        "/api/v1/auth/logout",
        json={"access_token": access, "refresh_token": tokens["refresh_token"]},
    )
    assert logout.status_code == 204
    assert (
        client.get("/api/v1/auth/me", headers={"Authorization": f"Bearer {access}"}).status_code
        == 401
    )


def test_google_onetap_exchange_mocked(client: TestClient) -> None:
    identity = GoogleIdentity(
        subject="google-sub-1",
        email="google.user@example.com",
        email_verified=True,
        full_name="Google User",
        provider="google",
    )
    with patch(
        "app.api.routes.auth.verify_google_id_token",
        return_value=identity,
    ) as mocked:
        response = client.post(
            "/api/v1/auth/google/onetap",
            json={"id_token": "fake-google-id-token-value", "provider": "google"},
        )
        mocked.assert_called_once()
    assert response.status_code == 200
    tokens = response.json()
    me = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert me.status_code == 200
    assert me.json()["email"] == "google.user@example.com"


def test_google_onetap_rejects_invalid_token(client: TestClient) -> None:
    with patch(
        "app.api.routes.auth.verify_google_id_token",
        side_effect=ValueError("Invalid Google ID token"),
    ):
        response = client.post(
            "/api/v1/auth/google/onetap",
            json={"id_token": "bad-token-value-xx", "provider": "google"},
        )
    assert response.status_code == 401


def test_microsoft_onetap_exchange_mocked(client: TestClient) -> None:
    identity = MicrosoftIdentity(
        subject="ms-sub-1",
        email="ms.user@example.com",
        email_verified=True,
        full_name="MS User",
        provider="microsoft",
    )
    with patch(
        "app.api.routes.auth.verify_microsoft_id_token",
        return_value=identity,
    ) as mocked:
        response = client.post(
            "/api/v1/auth/microsoft/onetap",
            json={"id_token": "fake-ms-id-token-value", "provider": "microsoft"},
        )
        mocked.assert_called_once()
    assert response.status_code == 200
    tokens = response.json()
    me = client.get(
        "/api/v1/auth/me",
        headers={"Authorization": f"Bearer {tokens['access_token']}"},
    )
    assert me.status_code == 200
    assert me.json()["email"] == "ms.user@example.com"


def test_microsoft_verifier_uses_jwks(monkeypatch: pytest.MonkeyPatch) -> None:
    """Unit: microsoft verifier selects JWKS key and validates claims (mocked httpx/jwt)."""
    from app.services import microsoft_id_token as ms

    ms.clear_jwks_cache()

    fake_jwks = {"keys": [{"kid": "kid-1", "kty": "RSA", "n": "x", "e": "AQAB"}]}

    class FakeResponse:
        def raise_for_status(self) -> None:
            return None

        def json(self) -> dict:
            return fake_jwks

    class FakeClient:
        def __init__(self, *args, **kwargs) -> None:
            pass

        def __enter__(self):
            return self

        def __exit__(self, *args):
            return False

        def get(self, url: str):
            assert "discovery/v2.0/keys" in url
            return FakeResponse()

    monkeypatch.setattr(ms.httpx, "Client", FakeClient)
    monkeypatch.setattr(
        ms.jwt,
        "get_unverified_header",
        lambda _token: {"kid": "kid-1", "alg": "RS256"},
    )
    monkeypatch.setattr(
        ms.jwt,
        "decode",
        lambda *args, **kwargs: {
            "iss": "https://login.microsoftonline.com/common/v2.0",
            "sub": "oid-subject",
            "email": "jwks@example.com",
            "name": "JWKS User",
        },
    )

    identity = ms.verify_microsoft_id_token("header.payload.sig")
    assert identity.subject == "oid-subject"
    assert identity.email == "jwks@example.com"
    ms.clear_jwks_cache()
