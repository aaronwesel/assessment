"""Consent flags + AI gating for DeepSeek / Rekognition."""

from __future__ import annotations

import pytest
from fastapi.testclient import TestClient
from sqlmodel import Session

from app.clients.deepseek import DeepSeekError, complete_for_subject
from app.consent.gate import ConsentRequired
from app.consent.models import PURPOSE_AI_DEEPSEEK
from app.db.session import engine
from tests.helpers_auth import register_user


def test_consent_self_service(client: TestClient) -> None:
    user = register_user(client, email="consent-user@example.com")
    headers = {"Authorization": f"Bearer {user['access_token']}"}

    purposes = client.get("/api/v1/consent/purposes", headers=headers)
    assert purposes.status_code == 200
    assert PURPOSE_AI_DEEPSEEK in purposes.json()["purposes"]

    updated = client.put(
        "/api/v1/consent/me",
        headers=headers,
        json={"purpose": PURPOSE_AI_DEEPSEEK, "granted": True},
    )
    assert updated.status_code == 200
    assert updated.json()["granted"] is True

    listed = client.get("/api/v1/consent/me", headers=headers)
    assert listed.status_code == 200
    assert any(item["purpose"] == PURPOSE_AI_DEEPSEEK for item in listed.json())


def test_deepseek_requires_consent(client: TestClient) -> None:
    user = register_user(client, email="ai-consent@example.com")
    with Session(engine) as session:
        with pytest.raises(ConsentRequired):
            complete_for_subject(
                session,
                subject_user_id=user["user_id"],
                prompt="summarize opaque token only",
            )

    headers = {"Authorization": f"Bearer {user['access_token']}"}
    assert (
        client.put(
            "/api/v1/consent/me",
            headers=headers,
            json={"purpose": PURPOSE_AI_DEEPSEEK, "granted": True},
        ).status_code
        == 200
    )

    # Consent present but DeepSeek still disabled by env → DeepSeekError, not ConsentRequired.
    with Session(engine) as session:
        with pytest.raises(DeepSeekError):
            complete_for_subject(
                session,
                subject_user_id=user["user_id"],
                prompt="summarize opaque token only",
            )
