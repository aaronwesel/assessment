"""Break-glass grants — time-boxed, mandatory reason, alert hooks."""

from __future__ import annotations

from unittest.mock import patch

from fastapi.testclient import TestClient

from tests.helpers_auth import admin_auth_header, register_user


def test_break_glass_requires_reason_and_admin(client: TestClient) -> None:
    user = register_user(client, email="bg-user@example.com")
    denied = client.post(
        "/api/v1/break-glass/grants",
        headers={"Authorization": f"Bearer {user['access_token']}"},
        json={
            "target_user_id": user["user_id"],
            "reason_code": "mfa_recovery",
        },
    )
    assert denied.status_code == 403


def test_break_glass_grant_revoke_and_alerts(client: TestClient) -> None:
    target = register_user(client, email="bg-target@example.com")
    headers = admin_auth_header(client, email="bg-admin@example.com")

    with patch(
        "app.break_glass.service.notify_break_glass_granted",
        return_value={"ses": False, "slack": False},
    ) as notify:
        created = client.post(
            "/api/v1/break-glass/grants",
            headers=headers,
            json={
                "target_user_id": target["user_id"],
                "reason_code": "mfa_recovery",
                "ticket_ref": "INC-1001",
                "ttl_minutes": 30,
            },
        )
        notify.assert_called_once()

    assert created.status_code == 201, created.text
    grant = created.json()
    assert grant["active"] is True
    assert grant["reason_code"] == "mfa_recovery"
    assert grant["target_user_id"] == target["user_id"]

    listed = client.get("/api/v1/break-glass/grants?active_only=true", headers=headers)
    assert listed.status_code == 200
    assert any(item["id"] == grant["id"] for item in listed.json())

    revoked = client.post(
        f"/api/v1/break-glass/grants/{grant['id']}/revoke",
        headers=headers,
    )
    assert revoked.status_code == 200
    assert revoked.json()["active"] is False

    # Audit trail should include grant + revoke actions.
    audit = client.get("/api/v1/audit/events", headers=headers)
    actions = {item["action"] for item in audit.json()["items"]}
    assert "break_glass_grant" in actions
    assert "break_glass_revoke" in actions


def test_break_glass_rejects_phi_detail(client: TestClient) -> None:
    target = register_user(client, email="bg-phi-target@example.com")
    headers = admin_auth_header(client, email="bg-phi-admin@example.com")
    response = client.post(
        "/api/v1/break-glass/grants",
        headers=headers,
        json={
            "target_user_id": target["user_id"],
            "reason_code": "suspected_compromise",
            "reason_detail": "user@example.com called in",
        },
    )
    assert response.status_code == 400
