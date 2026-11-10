"""Signed short-lived media URL stub."""

from __future__ import annotations

import time

from fastapi.testclient import TestClient

from app.media.signed_urls import create_signed_url, verify_signed_url
from tests.helpers_auth import register_user


def test_signed_url_issue_and_verify(client: TestClient) -> None:
    user = register_user(client, email="media-user@example.com")
    headers = {"Authorization": f"Bearer {user['access_token']}"}
    response = client.post(
        "/api/v1/media/signed-url",
        headers=headers,
        json={
            "object_key": "org/demo/uuid-object.bin",
            "method": "GET",
            "ttl_seconds": 120,
        },
    )
    assert response.status_code == 200, response.text
    body = response.json()
    assert body["method"] == "GET"
    assert "Signature=" in body["url"]
    assert body["ttl_seconds"] == 120

    assert verify_signed_url(
        object_key=body["object_key"],
        method=body["method"],
        expires_at=body["expires_at"],
        signature=body["url"].split("Signature=")[1].split("&")[0],
    )


def test_signed_url_rejects_path_traversal(client: TestClient) -> None:
    user = register_user(client, email="media-bad@example.com")
    response = client.post(
        "/api/v1/media/signed-url",
        headers={"Authorization": f"Bearer {user['access_token']}"},
        json={"object_key": "../secret", "method": "GET"},
    )
    assert response.status_code == 400


def test_signed_url_expires() -> None:
    signed = create_signed_url(object_key="a/b.bin", method="GET", ttl_seconds=30)
    assert (
        verify_signed_url(
            object_key=signed.object_key,
            method=signed.method,
            expires_at=signed.expires_at,
            signature=signed.url.split("Signature=")[1].split("&")[0],
            now=int(time.time()) + 10_000,
        )
        is False
    )
